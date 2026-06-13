using System;
using System.Collections.Generic;
using EngineCore;
using Runtime.RMC._MyProject_.Core;
using Runtime.RMC._MyProject_.Dice;
using Runtime.RMC.Backgammon;
using Runtime.RMC.Backgammon.Core;
using Runtime.RMC.Backgammon.Theme;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

public enum MovePreviewLineShape
{
    Straight,
    Curved
}

public class BoardManager : MonoBehaviour 
{
    [Header("Generation Settings")]
    public GameObject pointPrefab;
    public Transform leftHalfFloor;
    public Transform rightHalfFloor;
    public Material pointMaterial;

    [Header("Checker Settings")]
    public GameObject whiteCheckerPrefab;
    public GameObject blackCheckerPrefab;
    public float forcedCheckerHeight = 0.12f; // Default height if prefab auto-detect fails
    public float checkerWallMargin = 0.05f;

    [Header("Board State")]
    public BoardPoint[] allPoints = new BoardPoint[24];

    [Header("Bar / off (optional)")]
    [Tooltip("Stack logical P1 bar checkers (engine index 24) here when assigned.")]
    public Transform barWhiteAnchor;
    [Tooltip("Stack logical P2 bar checkers here when assigned.")]
    public Transform barBlackAnchor;
    [Tooltip("Programmatically generated center bar point for White checkers.")]
    public BoardPoint barWhitePoint;
    [Tooltip("Programmatically generated center bar point for Black checkers.")]
    public BoardPoint barBlackPoint;
    [Tooltip("Stack logical P1 borne-off checkers here when assigned.")]
    public Transform bearOffWhiteAnchor;
    [Tooltip("Stack logical P2 borne-off checkers here when assigned.")]
    public Transform bearOffBlackAnchor;
    [Tooltip("Programmatically generated off-board point for White borne-off checkers.")]
    public BoardPoint bearOffWhitePoint;
    [Tooltip("Programmatically generated off-board point for Black borne-off checkers.")]
    public BoardPoint bearOffBlackPoint;

    [Header("Layout Tuning")]
    public float edgePaddingX = 0.1f;
    public Color darkColor = new Color(0.1f, 0.1f, 0.1f);
    public Color lightColor = new Color(0.9f, 0.9f, 0.9f);
    [SerializeField] private float barCenterOffsetX = 0f;
    [SerializeField] private float barCenterOffsetZ = 0f;
    [SerializeField] private float barPointLiftY = 0.03f;
    [SerializeField] private float barPointSpacingX = 0.3f;
    [SerializeField] private float barPointWidthScale = 0.6f;
    [SerializeField] private float barPointHeightScale = 0.22f;
    [SerializeField] private bool hideBarPointMesh = false;
    [SerializeField] private float bearOffCenterOffsetX = 0f;
    [SerializeField] private float bearOffCenterOffsetZ = -1.25f;
    [SerializeField] private float bearOffPointLiftY = 0.045f;
    [SerializeField] private float bearOffPointSpacingX = 0.3f;
    [SerializeField] private float bearOffPointWidthScale = 0.6f;
    [SerializeField] private float bearOffPointHeightScale = 0.22f;
    [SerializeField] private bool hideBearOffPointMesh = false;
    [SerializeField] private Transform boardTrayRoot;
    [SerializeField] private Transform trayBearOffBottomAnchor;
    [SerializeField] private Transform trayBearOffTopAnchor;
    [SerializeField] private float trayInnerWallClearance = 0.008f;
    [SerializeField] private bool enableTrayDebugLogs = true;

    private const float TrayBearOffStackStepY = 0.02272728f;

    [Header("Board Points Container")]
    [Tooltip("Parent transform for generated bar and bear-off BoardPoint objects. Assign the Board GameObject. Defaults to this transform if left empty.")]
    [SerializeField] private Transform boardPointContainer;

    [Header("Board view (HUD Horiz / Vert)")]
    [Tooltip("Rotated for horizontal vs vertical table view. Assign e.g. parent of both floor halves.")]
    [SerializeField] private Transform boardViewPivot;
    [SerializeField] private Vector3 horizontalBoardEuler = Vector3.zero;
    [SerializeField] private Vector3 verticalBoardEuler = new Vector3(0f, 0f, 90f);

    [Header("Checker Visuals (HDR)")]
    public Color whiteBaseColor = new Color(1f, 0.42f, 0f);
    public Color whiteEmissionColor = Color.yellow;
    public float whiteEmissionIntensity = 3.41f;

    public Color blackBaseColor = new Color(0.1f, 0.1f, 0.1f);
    public Color blackEmissionColor = Color.red;
    public float blackEmissionIntensity = 2.0f;

    [Header("Movable highlight")]
    [Tooltip("Tint for P1 checkers that can start a legal turn. HDR emission uses the same intensity as white checkers but is tinted with this color so the highlight reads in URP Lit.")]
    [SerializeField, FormerlySerializedAs("movableNeonBaseColor")]
    private Color movableHighlightBaseColor = new Color(0.2f, 0.85f, 1f, 1f);

    [Header("Theme")]
    [SerializeField] private DiceManager diceManager;
    [SerializeField] private DoublingCubeVisual doublingCubeVisual;

    [Header("Move preview (hover lines)")]
    [Tooltip("Optional: clone this material for lines (same idea as MoveVisualizer lineMaterial). When set, dashed shader is skipped; use Unlit/Color or Sprites/Default.")]
    [SerializeField] private Material movePreviewLineTemplateMaterial;
    [SerializeField] private bool enableMovePreviewLines = true;
    [SerializeField] private BackgammonGameController gameController;
    [SerializeField] private Camera rayCamera;
    [SerializeField] private bool enableMoveSelectionDebugLogs = true;
    [SerializeField] private bool enableMoveMappingDebugLogs = true;
    [SerializeField] private bool enableUndoPerformanceLogs;
    [Tooltip("Raycast hits are sorted by distance; the first hit with a Checker wins. If the table blocks, set this mask to the Checker layer only.")]
    [SerializeField] private LayerMask movePreviewRaycastLayers = ~0;
    [SerializeField] private float movePreviewRayDistance = 80f;
    [SerializeField] private int movePreviewMaxLines = 8;
    [FormerlySerializedAs("movePreviewLineWidth")]
    [SerializeField] private float movePreviewLineWidthStart = 0.08f;
    [SerializeField] private float movePreviewLineWidthEnd = 0.08f;
    [Tooltip("MoveVisualizer BuildArc-style: bulge scales as chordLength × heightFactor; heightFactor = base + lineIndex × perLine (matches MoneySession dynamicHeight).")]
    [SerializeField] private float movePreviewArcFactorBase = 0.2f;
    [SerializeField] private float movePreviewArcFactorPerLine = 0.15f;
    [Tooltip("Straight = chord from checker to destination. Curved = MoveVisualizer-style Bezier arcs.")]
    [SerializeField] private MovePreviewLineShape movePreviewLineShape = MovePreviewLineShape.Curved;
    [Tooltip("Polyline segments per curve when Curved (more = smoother arc). Ignored for Straight.")]
    [SerializeField] private int movePreviewCurveSegments = 24;
    [SerializeField] private Color movePreviewLineColor = new Color(0f, 1f, 1f, 1f);
    [Tooltip("When true, hovering a movable checker highlights destination points (triangles) instead of drawing world-space hover lines.")]
    [SerializeField] private bool preferPointHighlightMovePreview = true;
    [SerializeField] private bool movePreviewDashedLine = true;
    [Tooltip("How many dash on/off cycles along the full line (UV 0–1). Higher = more dashes.")]
    [SerializeField] private float movePreviewDashRepeat = 10f;
    [Tooltip("Fraction of each cycle that is solid; the rest is gap.")]
    [SerializeField] [Range(0.05f, 0.95f)] private float movePreviewDashFill = 0.45f;
    [SerializeField] private float movePreviewHeightOffset = 0.05f;
    [Tooltip("Lifts the Bezier control along the board normal (chord length × factor). 0 = lateral arc only.")]
    [SerializeField] [Range(0f, 0.5f)] private float movePreviewVerticalBulgeFactor = 0.15f;
    [Header("Move preview (arrowheads)")]
    [SerializeField] private bool enableMovePreviewArrowheads = true;
    [SerializeField] [Range(5f, 75f)] private float movePreviewArrowHeadAngle = 20f;
    [SerializeField] [Min(0.01f)] private float movePreviewArrowHeadLength = 0.25f;
    [SerializeField] [Min(0.1f)] private float movePreviewArrowWidthMultiplier = 0.9f;
    [Tooltip("Optional world position for bear-off (-1) line ends. If unset, a point near P1 home edge is used.")]
    [SerializeField] private Transform bearOffLineEnd;

    private MeshRenderer _movableHoverRenderer;

    private LineRenderer[] _movePreviewLines;
    private LineRenderer[] _movePreviewArrowWingA;
    private LineRenderer[] _movePreviewArrowWingB;
    private Transform _movePreviewRoot;
    private Vector3[] _movePreviewCurveBuffer;
    private readonly HashSet<int> _moveDestScratch = new();

    private readonly List<MovablePulseTarget> _movablePulseTargets = new();
    private readonly HashSet<int> _movePreviewHighlightedBoardIndices = new();
    private static readonly ProfilerMarker VisualUndoMarker = new("Backgammon.Board.VisualUndo");
    private static readonly ProfilerMarker FullSyncMarker = new("Backgammon.Board.SyncCheckersFromGameState");

    private struct MovablePulseTarget
    {
        public MeshRenderer Renderer;
    }

    /// <summary>Same render queue idea as <c>MoveVisualizer</c> (draw on top of most world geometry).</summary>
    private const int MovePreviewOverlayRenderQueue = 4000;

    private void Awake()
    {
        BuildMovePreviewLinePool();
        if (rayCamera == null)
            rayCamera = ResolveGameplayCamera();
    }

    private static Camera ResolveGameplayCamera()
    {
        if (Camera.main != null) return Camera.main;
        Camera[] cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] != null && cams[i].enabled)
                return cams[i];
        }

        return null;
    }

    private void BuildMovePreviewLinePool()
    {
        if (_movePreviewRoot != null) return;
        int nLines = Mathf.Max(1, movePreviewMaxLines);
        int maxSeg = Mathf.Clamp(movePreviewCurveSegments, 2, 128);
        _movePreviewCurveBuffer = new Vector3[maxSeg + 1];
        _movePreviewRoot = new GameObject("MovableMovePreviewLines").transform;
        _movePreviewRoot.SetParent(transform, false);
        _movePreviewLines = new LineRenderer[nLines];
        _movePreviewArrowWingA = new LineRenderer[nLines];
        _movePreviewArrowWingB = new LineRenderer[nLines];
        Shader lineShader = movePreviewLineTemplateMaterial != null ? null : ResolveMovePreviewLineShader();

        for (int i = 0; i < nLines; i++)
        {
            _movePreviewLines[i] = CreateMovePreviewRenderer(
                $"MovePreviewLine_{i}",
                maxSeg + 1,
                movePreviewLineWidthStart,
                movePreviewLineWidthEnd,
                lineShader,
                useDashedMaterial: movePreviewDashedLine);
            _movePreviewArrowWingA[i] = CreateMovePreviewRenderer(
                $"MovePreviewArrowA_{i}",
                2,
                movePreviewLineWidthEnd * movePreviewArrowWidthMultiplier,
                movePreviewLineWidthEnd * movePreviewArrowWidthMultiplier,
                lineShader,
                useDashedMaterial: false);
            _movePreviewArrowWingB[i] = CreateMovePreviewRenderer(
                $"MovePreviewArrowB_{i}",
                2,
                movePreviewLineWidthEnd * movePreviewArrowWidthMultiplier,
                movePreviewLineWidthEnd * movePreviewArrowWidthMultiplier,
                lineShader,
                useDashedMaterial: false);
        }
    }

    private LineRenderer CreateMovePreviewRenderer(
        string objectName,
        int positionCount,
        float startWidth,
        float endWidth,
        Shader lineShader,
        bool useDashedMaterial)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(_movePreviewRoot, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.positionCount = Mathf.Max(2, positionCount);
        lr.useWorldSpace = true;
        lr.textureMode = LineTextureMode.Stretch;
        lr.generateLightingData = true;
        lr.startWidth = startWidth;
        lr.endWidth = endWidth;
        lr.numCapVertices = 6;
        lr.numCornerVertices = 3;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        if (movePreviewLineTemplateMaterial != null)
        {
            Material mat = new Material(movePreviewLineTemplateMaterial);
            ApplyMovePreviewLineMaterialColors(mat, movePreviewLineColor);
            mat.color = movePreviewLineColor;
            ApplyMovePreviewOverlayQueue(mat);
            lr.material = mat;
        }
        else if (lineShader != null)
        {
            Material mat = new Material(lineShader);
            ApplyMovePreviewLineMaterialColors(mat, movePreviewLineColor);
            if (useDashedMaterial)
                ApplyMovePreviewDashMaterialProps(mat);
            ApplyMovePreviewOverlayQueue(mat);
            lr.material = mat;
        }

        lr.startColor = movePreviewLineColor;
        lr.endColor = movePreviewLineColor;
        lr.enabled = false;
        return lr;
    }

    private static void ApplyMovePreviewOverlayQueue(Material mat)
    {
        if (mat != null)
            mat.renderQueue = MovePreviewOverlayRenderQueue;
    }

    /// <summary>Plane normal for MoveVisualizer-style arcs: matches table orientation when <see cref="boardViewPivot"/> is set.</summary>
    private Vector3 GetMovePreviewArcPlaneNormal()
    {
        if (boardViewPivot != null)
            return boardViewPivot.up;
        return Vector3.up;
    }

    /// <summary>World center of checker mesh (or collider fallback) plus clearance along board normal.</summary>
    private static Vector3 GetMovePreviewLineStartWorld(MeshRenderer mr, Collider collider, Vector3 clearance)
    {
        Bounds b = mr != null ? mr.bounds : collider.bounds;
        return b.center + clearance;
    }

    private Shader ResolveMovePreviewLineShader()
    {
        if (movePreviewDashedLine)
        {
            Shader dashed = Shader.Find("RMC/Backgammon/MovePreviewDashedLine");
            if (dashed != null)
                return dashed;
        }

        Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (lineShader == null) lineShader = Shader.Find("Unlit/Color");
        if (lineShader == null) lineShader = Shader.Find("Sprites/Default");
        return lineShader;
    }

    private static void ApplyMovePreviewLineMaterialColors(Material mat, Color c)
    {
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
    }

    private void ApplyMovePreviewDashMaterialProps(Material mat)
    {
        if (mat == null || !movePreviewDashedLine) return;
        if (mat.HasProperty("_DashRepeat"))
            mat.SetFloat("_DashRepeat", Mathf.Max(0.5f, movePreviewDashRepeat));
        if (mat.HasProperty("_DashFill"))
            mat.SetFloat("_DashFill", movePreviewDashFill);
    }

    private void Update()
    {
        HandleCheckerClickInput();
        if (enableMovePreviewLines)
            UpdateMovePreviewLines();
        else
            SetMovableHoverRenderer(null);
    }

    private void HandleCheckerClickInput()
    {
        bool leftClick = Input.GetMouseButtonDown(0);
        bool rightClick = Input.GetMouseButtonDown(1);
        if (!leftClick && !rightClick) return;

        if (gameController == null)
            gameController = FindFirstObjectByType<BackgammonGameController>();
        if (rayCamera == null)
            rayCamera = ResolveGameplayCamera();
        if (gameController == null || rayCamera == null) return;
        if (!gameController.CanShowMovableCheckerInteraction()) return;

        if (!TryGetHoveredChecker(out Checker checker, out _)) return;
        if (!IsTopLogicalP1Checker(checker)) return;
        if (!TryGetEngineFromForChecker(checker, out int engineFrom)) return;

        BackgammonMovableDestinations.CollectDistinctFirstMoveTos(engineFrom, gameController.CurrentLegalTurns, _moveDestScratch);
        if (_moveDestScratch.Count == 0) return;

        bool preferHighest = leftClick;
        if (enableMoveSelectionDebugLogs)
            Debug.Log($"[Backgammon][Click] from={engineFrom} button={(leftClick ? "Left" : "Right")} preferHighest={preferHighest} candidateTo={string.Join(",", _moveDestScratch)}");
        gameController.TryApplyPreferredFirstMoveForFrom(engineFrom, preferHighest);
    }

    private bool TryGetHoveredChecker(out Checker checker, out RaycastHit checkerHit)
    {
        checker = null;
        checkerHit = default;
        if (rayCamera == null) return false;
        Ray ray = rayCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, movePreviewRayDistance, movePreviewRaycastLayers, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Checker c = hits[i].collider.GetComponentInParent<Checker>();
            if (c == null) continue;
            checker = c;
            checkerHit = hits[i];
            return true;
        }

        return false;
    }

    private void UpdateMovePreviewLines()
    {
        if (_movePreviewLines == null || _movePreviewLines.Length == 0) return;

        if (gameController == null)
            gameController = FindFirstObjectByType<BackgammonGameController>();
        if (rayCamera == null)
            rayCamera = ResolveGameplayCamera();
        if (gameController == null || rayCamera == null)
        {
            HideMovePreviewLines();
            ClearMovePreviewPointHighlights();
            SetMovableHoverRenderer(null);
            return;
        }

        if (!gameController.CanShowMovableCheckerInteraction())
        {
            HideMovePreviewLines();
            ClearMovePreviewPointHighlights();
            SetMovableHoverRenderer(null);
            return;
        }

        if (!TryGetHoveredChecker(out Checker ch, out RaycastHit checkerHit))
        {
            HideMovePreviewLines();
            ClearMovePreviewPointHighlights();
            SetMovableHoverRenderer(null);
            return;
        }

        if (!IsTopLogicalP1Checker(ch))
        {
            HideMovePreviewLines();
            ClearMovePreviewPointHighlights();
            SetMovableHoverRenderer(null);
            return;
        }

        if (!TryGetEngineFromForChecker(ch, out int engineFrom))
        {
            HideMovePreviewLines();
            ClearMovePreviewPointHighlights();
            SetMovableHoverRenderer(null);
            return;
        }

        BackgammonMovableDestinations.CollectDistinctFirstMoveTos(engineFrom, gameController.CurrentLegalTurns, _moveDestScratch);
        if (_moveDestScratch.Count == 0)
        {
            HideMovePreviewLines();
            ClearMovePreviewPointHighlights();
            SetMovableHoverRenderer(null);
            return;
        }

        MeshRenderer mr = ch.GetComponentInChildren<MeshRenderer>();
        SetMovableHoverRenderer(mr);
        PreviewMoveDestinationPoints(_moveDestScratch);
        if (preferPointHighlightMovePreview)
        {
            HideMovePreviewLines();
            return;
        }

        Vector3 boardUp = GetMovePreviewArcPlaneNormal();
        Vector3 clearance = boardUp * movePreviewHeightOffset;
        Vector3 start = GetMovePreviewLineStartWorld(mr, checkerHit.collider, clearance);
        int lineIdx = 0;
        foreach (int engineTo in _moveDestScratch)
        {
            if (lineIdx >= _movePreviewLines.Length) break;
            if (!TryGetWorldPositionForMoveDestination(engineTo, out Vector3 end))
                continue;
            end += clearance;

            LineRenderer lr = _movePreviewLines[lineIdx++];
            int slot = lineIdx - 1;
            int nPos;
            if (movePreviewLineShape == MovePreviewLineShape.Straight)
            {
                if (_movePreviewCurveBuffer == null || _movePreviewCurveBuffer.Length < 2)
                    _movePreviewCurveBuffer = new Vector3[2];
                nPos = BackgammonMovePreviewCurve.FillChord(start, end, _movePreviewCurveBuffer);
            }
            else
            {
                int seg = Mathf.Clamp(movePreviewCurveSegments, 2, 128);
                if (_movePreviewCurveBuffer == null || _movePreviewCurveBuffer.Length < seg + 1)
                    _movePreviewCurveBuffer = new Vector3[seg + 1];
                float heightFactor = movePreviewArcFactorBase + slot * movePreviewArcFactorPerLine;
                Vector3 planeNormal = GetMovePreviewArcPlaneNormal();
                nPos = BackgammonMovePreviewCurve.FillMoveVisualizerStyleBezier(
                    start,
                    end,
                    heightFactor,
                    seg,
                    slot,
                    planeNormal,
                    _movePreviewCurveBuffer,
                    movePreviewVerticalBulgeFactor);
            }

            if (nPos <= 0)
            {
                lr.enabled = false;
                continue;
            }

            lr.positionCount = nPos;
            lr.startWidth = movePreviewLineWidthStart;
            lr.endWidth = movePreviewLineWidthEnd;
            ApplyMovePreviewDashMaterialProps(lr.material);
            for (int pi = 0; pi < nPos; pi++)
                lr.SetPosition(pi, _movePreviewCurveBuffer[pi]);
            lr.enabled = true;

            LineRenderer wingA = _movePreviewArrowWingA != null && slot < _movePreviewArrowWingA.Length ? _movePreviewArrowWingA[slot] : null;
            LineRenderer wingB = _movePreviewArrowWingB != null && slot < _movePreviewArrowWingB.Length ? _movePreviewArrowWingB[slot] : null;
            if (enableMovePreviewArrowheads &&
                wingA != null &&
                wingB != null &&
                nPos >= 2 &&
                BackgammonMovePreviewCurve.TryBuildArrowWings(
                    _movePreviewCurveBuffer[nPos - 1],
                    _movePreviewCurveBuffer[nPos - 1] - _movePreviewCurveBuffer[nPos - 2],
                    boardUp,
                    movePreviewArrowHeadAngle,
                    movePreviewArrowHeadLength,
                    out Vector3 wingEndA,
                    out Vector3 wingEndB))
            {
                float wingWidth = movePreviewLineWidthEnd * movePreviewArrowWidthMultiplier;
                wingA.startWidth = wingWidth;
                wingA.endWidth = wingWidth;
                wingB.startWidth = wingWidth;
                wingB.endWidth = wingWidth;
                Vector3 tip = _movePreviewCurveBuffer[nPos - 1];
                wingA.positionCount = 2;
                wingA.SetPosition(0, tip);
                wingA.SetPosition(1, wingEndA);
                wingA.enabled = true;
                wingB.positionCount = 2;
                wingB.SetPosition(0, tip);
                wingB.SetPosition(1, wingEndB);
                wingB.enabled = true;
            }
            else
            {
                if (wingA != null) wingA.enabled = false;
                if (wingB != null) wingB.enabled = false;
            }
        }

        for (int i = lineIdx; i < _movePreviewLines.Length; i++)
        {
            _movePreviewLines[i].enabled = false;
            if (_movePreviewArrowWingA != null && i < _movePreviewArrowWingA.Length && _movePreviewArrowWingA[i] != null)
                _movePreviewArrowWingA[i].enabled = false;
            if (_movePreviewArrowWingB != null && i < _movePreviewArrowWingB.Length && _movePreviewArrowWingB[i] != null)
                _movePreviewArrowWingB[i].enabled = false;
        }
    }

    public void PreviewMoveDestinationPoints(IReadOnlyCollection<int> engineDestinationPoints)
    {
        ClearMovePreviewPointHighlights();
        if (engineDestinationPoints == null || allPoints == null) return;

        foreach (int engineTo in engineDestinationPoints)
        {
            if (engineTo < 0 || engineTo > 23) continue;
            int boardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(engineTo);
            if (boardIndex < 0 || boardIndex >= allPoints.Length) continue;
            BoardPoint bp = allPoints[boardIndex];
            if (bp == null) continue;
            bp.SetHighlighted(true);
            _movePreviewHighlightedBoardIndices.Add(boardIndex);
        }
    }

    public void ClearMovePreviewPointHighlights()
    {
        if (_movePreviewHighlightedBoardIndices.Count == 0 || allPoints == null) return;
        foreach (int boardIndex in _movePreviewHighlightedBoardIndices)
        {
            if (boardIndex < 0 || boardIndex >= allPoints.Length) continue;
            allPoints[boardIndex]?.SetHighlighted(false);
        }
        _movePreviewHighlightedBoardIndices.Clear();
    }

    private void HideMovePreviewLines()
    {
        if (_movePreviewLines == null) return;
        for (int i = 0; i < _movePreviewLines.Length; i++)
        {
            if (_movePreviewLines[i] != null)
                _movePreviewLines[i].enabled = false;
            if (_movePreviewArrowWingA != null && i < _movePreviewArrowWingA.Length && _movePreviewArrowWingA[i] != null)
                _movePreviewArrowWingA[i].enabled = false;
            if (_movePreviewArrowWingB != null && i < _movePreviewArrowWingB.Length && _movePreviewArrowWingB[i] != null)
                _movePreviewArrowWingB[i].enabled = false;
        }
    }

    private void OnDisable()
    {
        HideMovePreviewLines();
        ClearMovePreviewPointHighlights();
        SetMovableHoverRenderer(null);
    }

    [ContextMenu("Full Setup")]
    public void FullSetup()
    {
        GenerateBoard();
        SpawnInitialCheckers();
    }

    /// <summary>True if at least one <see cref="BoardPoint"/> exists (runtime equivalent of board geometry from Full Setup).</summary>
    public bool HasBoardPoints()
    {
        if (allPoints == null) return false;
        for (int i = 0; i < allPoints.Length; i++)
            if (allPoints[i] != null) return true;
        return false;
    }

    /// <summary>Creates points if missing. Does not spawn editor test checkers — sync from <see cref="GameState"/> afterward.</summary>
    public void EnsureBoardGenerated()
    {
        if (HasBoardPoints()) return;
        GenerateBoard();
    }

    public void GenerateBoard()
    {
        ResolveBoardTrayAnchorsIfNeeded();
        ClearPoints();
        // 0-5 Right Bottom, 6-11 Left Bottom, 12-17 Left Top, 18-23 Right Top
        GenerateSet(rightHalfFloor, 0, true);
        GenerateSet(leftHalfFloor, 6, true);
        GenerateSet(leftHalfFloor, 12, false);
        GenerateSet(rightHalfFloor, 18, false);
        GenerateBarPoints();
        GenerateBearOffPoints();
    }

    private void GenerateSet(Transform parent, int startIdx, bool isBottomRow)
    {
        MeshRenderer floor = parent.GetComponent<MeshRenderer>();
        Vector3 size = floor.bounds.size;
        Vector3 center = floor.bounds.center;

        float zPos = isBottomRow ? (floor.bounds.min.z) : (floor.bounds.max.z);
        float paddingX = size.x * edgePaddingX;
        float spaceBetween = (size.x - (paddingX * 2)) / 5f;

        // Get checker dimensions once to pass to points
        float height = GetPrefabHeight(whiteCheckerPrefab);
        float diameter = GetPrefabWidth(whiteCheckerPrefab);

        for (int i = 0; i < 6; i++)
        {
            int currentIdx = startIdx + i;
            float xPos = isBottomRow 
                ? (floor.bounds.max.x - paddingX) - (i * spaceBetween)
                : (floor.bounds.min.x + paddingX) + (i * spaceBetween);

            GameObject p = Instantiate(pointPrefab, new Vector3(xPos, floor.bounds.max.y + 0.01f, zPos), Quaternion.identity, parent);
            BoardPoint bp = p.GetComponent<BoardPoint>();
            
            bp.wallMargin = checkerWallMargin;
            bp.Initialize(currentIdx, isBottomRow, (currentIdx % 2 == 0 ? darkColor : lightColor), height, diameter);
            bp.AddTriangleMesh(isBottomRow, spaceBetween * 0.8f, size.z * 0.45f, pointMaterial);
            
            allPoints[currentIdx] = bp;
        }
    }

    private void GenerateBarPoints()
    {
        if (pointPrefab == null || leftHalfFloor == null || rightHalfFloor == null) return;
        MeshRenderer leftFloor = leftHalfFloor.GetComponent<MeshRenderer>();
        MeshRenderer rightFloor = rightHalfFloor.GetComponent<MeshRenderer>();
        if (leftFloor == null || rightFloor == null) return;

        Bounds leftBounds = leftFloor.bounds;
        Bounds rightBounds = rightFloor.bounds;
        Vector3 center = (leftBounds.center + rightBounds.center) * 0.5f;
        float yBaseWorld = Mathf.Max(leftBounds.max.y, rightBounds.max.y) + 0.01f + barPointLiftY;
        // Convert world Y to local Y relative to BoardManager
        float yBase = transform.InverseTransformPoint(new Vector3(0f, yBaseWorld, 0f)).y;

        float checkerHeight = GetPrefabHeight(whiteCheckerPrefab);
        float checkerWidth = GetPrefabWidth(whiteCheckerPrefab);
        float pointWidth = Mathf.Max(0.05f, checkerWidth * barPointWidthScale);
        float pointHeight = Mathf.Max(0.05f, Mathf.Min(leftBounds.size.z, rightBounds.size.z) * barPointHeightScale);

        Vector3 whitePos = new(0f, yBase, 0f);
        Vector3 blackPos = new(0f, yBase, 0f);

        barWhitePoint = CreateSpecialPoint("BarPoint_White", whitePos, true, darkColor, checkerHeight, checkerWidth, pointWidth, pointHeight, hideBarPointMesh);
        barBlackPoint = CreateSpecialPoint("BarPoint_Black", blackPos, false, lightColor, checkerHeight, checkerWidth, pointWidth, pointHeight, hideBarPointMesh);
    }

    private void GenerateBearOffPoints()
    {
        // Always generate BoardPoint objects for bearoff (even if anchors exist)
        // This allows us to use the standard BoardPoint stacking logic
        if (TryGenerateTrayBearOffPoints())
            return;

        if (pointPrefab == null || leftHalfFloor == null || rightHalfFloor == null) return;
        MeshRenderer leftFloor = leftHalfFloor.GetComponent<MeshRenderer>();
        MeshRenderer rightFloor = rightHalfFloor.GetComponent<MeshRenderer>();
        if (leftFloor == null || rightFloor == null) return;

        Bounds leftBounds = leftFloor.bounds;
        Bounds rightBounds = rightFloor.bounds;
        Vector3 center = (leftBounds.center + rightBounds.center) * 0.5f;
        float yBase = Mathf.Max(leftBounds.max.y, rightBounds.max.y) + 0.01f + bearOffPointLiftY;

        float checkerHeight = GetPrefabHeight(whiteCheckerPrefab);
        float checkerWidth = GetPrefabWidth(whiteCheckerPrefab);
        float pointWidth = Mathf.Max(0.05f, checkerWidth * bearOffPointWidthScale);
        float pointHeight = Mathf.Max(0.05f, Mathf.Min(leftBounds.size.z, rightBounds.size.z) * bearOffPointHeightScale);

        Vector3 whitePos = new(center.x + bearOffCenterOffsetX - (bearOffPointSpacingX * 0.5f), yBase, center.z + bearOffCenterOffsetZ);
        Vector3 blackPos = new(center.x + bearOffCenterOffsetX + (bearOffPointSpacingX * 0.5f), yBase, center.z + bearOffCenterOffsetZ);

        bearOffWhitePoint = CreateSpecialPoint("BearOffPoint_White", whitePos, true, darkColor, checkerHeight, checkerWidth, pointWidth, pointHeight, hideBearOffPointMesh);
        bearOffBlackPoint = CreateSpecialPoint("BearOffPoint_Black", blackPos, false, lightColor, checkerHeight, checkerWidth, pointWidth, pointHeight, hideBearOffPointMesh);
    }

    private bool TryGenerateTrayBearOffPoints()
    {
        ResolveBoardTrayAnchorsIfNeeded();
        if (pointPrefab == null || trayBearOffBottomAnchor == null || trayBearOffTopAnchor == null)
            return false;

        float checkerHeight = GetPrefabHeight(whiteCheckerPrefab);
        float checkerWidth = GetPrefabWidth(whiteCheckerPrefab);
        float trayInnerGap = ComputeTrayInnerGap(checkerWidth);
        float pointWidth = Mathf.Max(0.05f, checkerWidth * bearOffPointWidthScale);
        float pointHeight = Mathf.Max(0.05f, trayInnerGap * bearOffPointHeightScale);
        float yLift = Mathf.Max(0f, bearOffPointLiftY);

        // Create bearoff points parented to the anchor transforms
        bearOffWhitePoint = CreateSpecialPointAtAnchor("BearOffPoint_White", trayBearOffBottomAnchor, yLift, true, darkColor, checkerHeight, checkerWidth, pointWidth, pointHeight);
        bearOffBlackPoint = CreateSpecialPointAtAnchor("BearOffPoint_Black", trayBearOffTopAnchor, yLift, false, lightColor, checkerHeight, checkerWidth, pointWidth, pointHeight);

        return bearOffWhitePoint != null && bearOffBlackPoint != null;
    }

    private BoardPoint CreateSpecialPointAtAnchor(
        string pointName,
        Transform anchorParent,
        float yLift,
        bool isBottomRow,
        Color color,
        float checkerHeight,
        float checkerWidth,
        float pointWidth,
        float pointHeight)
    {
        // Instantiate at anchor's position and parent to anchor
        GameObject pointObj = Instantiate(pointPrefab);
        pointObj.name = pointName;
        pointObj.transform.position = anchorParent.position + (Vector3.up * yLift);
        pointObj.transform.rotation = Quaternion.identity;
        pointObj.transform.localScale = Vector3.one;
        pointObj.transform.SetParent(anchorParent);

        BoardPoint bp = pointObj.GetComponent<BoardPoint>();
        if (bp == null) return null;

        bp.wallMargin = checkerWallMargin;
        bp.Initialize(BackgammonBoardLayout.BarEngineIndex, isBottomRow, color, checkerHeight, checkerWidth);
        bp.isBearOffPoint = true;

        // Don't add mesh for bearoff points
        return bp;
    }

    public float ComputeTrayInnerGap(float checkerWidth)
    {
        return Mathf.Max(0.01f, checkerWidth + Mathf.Max(0f, trayInnerWallClearance));
    }

    public float GetTrayBearOffStackStepY() => TrayBearOffStackStepY;

    private void ResolveBoardTrayAnchorsIfNeeded()
    {
        if (boardTrayRoot == null)
        {
            GameObject trayObj = GameObject.Find("BoardTray");
            if (trayObj != null)
                boardTrayRoot = trayObj.transform;
        }

        if (boardTrayRoot == null)
            return;

        if (trayBearOffBottomAnchor == null)
            trayBearOffBottomAnchor = boardTrayRoot.Find("BearOffBottomAnchor");
        if (trayBearOffTopAnchor == null)
            trayBearOffTopAnchor = boardTrayRoot.Find("BearOffTopAnchor");

        if (enableTrayDebugLogs)
        {
            Debug.Log($"[Backgammon][Tray] anchorsResolved={(trayBearOffBottomAnchor != null && trayBearOffTopAnchor != null)} " +
                      $"tray={boardTrayRoot.name} bottom={trayBearOffBottomAnchor?.name ?? "<null>"} top={trayBearOffTopAnchor?.name ?? "<null>"}");
        }
    }

    private BoardPoint CreateSpecialPoint(
        string pointName,
        Vector3 localPosition,
        bool isBottomRow,
        Color color,
        float checkerHeight,
        float checkerWidth,
        float pointWidth,
        float pointHeight,
        bool hideMesh = false)
    {
        Transform container = boardPointContainer != null ? boardPointContainer : transform;
        GameObject pointObj = Instantiate(pointPrefab, container);
        pointObj.name = pointName;
        pointObj.transform.localPosition = localPosition;
        pointObj.transform.localRotation = Quaternion.identity;
        BoardPoint bp = pointObj.GetComponent<BoardPoint>();
        if (bp == null) return null;

        bp.wallMargin = checkerWallMargin;
        bp.Initialize(BackgammonBoardLayout.BarEngineIndex, isBottomRow, color, checkerHeight, checkerWidth);

        // Mark as bearoff point if name indicates it
        if (pointName.Contains("BearOff"))
        {
            bp.isBearOffPoint = true;
        }

        if (!hideMesh)
        {
            bp.AddTriangleMesh(isBottomRow, pointWidth, pointHeight, pointMaterial);
        }
        return bp;
    }

    public void SpawnInitialCheckers()
    {
        // Matches standard opening via identity engine→board (same counts as PositionId + SyncCheckersFromGameState).
        var setup = new Dictionary<int, int>
        {
            { 0, -2 }, { 5, 5 }, { 7, 3 }, { 11, -5 }, { 12, 5 },
            { 16, -3 }, { 18, -5 }, { 23, 2 }
        };

        foreach (var entry in setup)
        {
            BoardPoint targetPoint = allPoints[entry.Key];
            PlayerColor color = entry.Value > 0 ? PlayerColor.White : PlayerColor.Black;
            GameObject prefab = (color == PlayerColor.White) ? whiteCheckerPrefab : blackCheckerPrefab;

            for (int i = 0; i < Mathf.Abs(entry.Value); i++)
            {
                // Notice: Manager doesn't care about positions anymore!
                GameObject checkerObj = Instantiate(prefab);
                ApplyCheckerVisuals(checkerObj, color);
                checkerObj.GetComponent<Checker>().color = color;
                
                targetPoint.AddChecker(checkerObj, animated: false);
            }
        }
    }

    private float GetPrefabHeight(GameObject prefab)
    {
        MeshRenderer mr = prefab.GetComponentInChildren<MeshRenderer>();
        return (mr != null) ? mr.bounds.size.y : 0.1f;
    }

    private float GetPrefabWidth(GameObject prefab)
    {
        MeshRenderer mr = prefab.GetComponentInChildren<MeshRenderer>();
        return (mr != null) ? mr.bounds.size.x : 0.45f;
    }

    private void ApplyCheckerVisuals(GameObject obj, PlayerColor color)
    {
        MeshRenderer mr = obj.GetComponentInChildren<MeshRenderer>();
        if (mr == null) return;

        MaterialPropertyBlock props = new MaterialPropertyBlock();
        Color baseCol = (color == PlayerColor.White) ? whiteBaseColor : blackBaseColor;
        Color emissCol = (color == PlayerColor.White) ? whiteEmissionColor : blackEmissionColor;
        float intensity = (color == PlayerColor.White) ? whiteEmissionIntensity : blackEmissionIntensity;

        Color emission = emissCol * Mathf.Pow(2f, intensity);

        CheckerMaterialPropertyBlockUtility.SetAlbedoAndEmission(props, baseCol, emission, mr);
        CheckerMaterialPropertyBlockUtility.ApplyPropertyBlock(mr, props);
    }

    private void RefreshMovablePulseVisuals()
    {
        Color emission = CheckerMaterialPropertyBlockUtility.ComputeMovableHighlightEmission(
            movableHighlightBaseColor,
            whiteEmissionIntensity);

        for (int i = 0; i < _movablePulseTargets.Count; i++)
        {
            MeshRenderer mr = _movablePulseTargets[i].Renderer;
            if (mr == null) continue;
            var props = new MaterialPropertyBlock();
            CheckerMaterialPropertyBlockUtility.SetAlbedoAndEmission(props, movableHighlightBaseColor, emission, mr);
            CheckerMaterialPropertyBlockUtility.ApplyPropertyBlock(mr, props);
        }

        if (_movableHoverRenderer != null)
        {
            bool hoverInPulseList = false;
            for (int i = 0; i < _movablePulseTargets.Count; i++)
            {
                if (_movablePulseTargets[i].Renderer == _movableHoverRenderer)
                {
                    hoverInPulseList = true;
                    break;
                }
            }

            if (!hoverInPulseList)
            {
                var props = new MaterialPropertyBlock();
                CheckerMaterialPropertyBlockUtility.SetAlbedoAndEmission(props, movableHighlightBaseColor, emission, _movableHoverRenderer);
                CheckerMaterialPropertyBlockUtility.ApplyPropertyBlock(_movableHoverRenderer, props);
            }
        }
    }

    private void LateUpdate()
    {
        RefreshMovablePulseVisuals();
    }

    private void AddMovablePulseTarget(GameObject go)
    {
        MeshRenderer mr = go.GetComponentInChildren<MeshRenderer>();
        if (mr == null) return;
        _movablePulseTargets.Add(new MovablePulseTarget { Renderer = mr });
    }

    public void SetMovableHoverRenderer(MeshRenderer renderer)
    {
        _movableHoverRenderer = renderer;
    }

    /// <summary>Player-side top-of-stack checker on a point or bar (matches movable highlight).</summary>
    public bool IsTopLogicalP1Checker(Checker checker)
    {
        if (checker == null || checker.color != PlayerColor.White) return false;
        BoardPoint bp = checker.GetComponentInParent<BoardPoint>();
        if (bp != null)
        {
            int n = bp.checkers.Count;
            return n > 0 && bp.checkers[n - 1] == checker.gameObject;
        }

        if (barWhitePoint != null && checker.transform.IsChildOf(barWhitePoint.transform))
        {
            int n = barWhitePoint.checkers.Count;
            return n > 0 && barWhitePoint.checkers[n - 1] == checker.gameObject;
        }

        if (barWhiteAnchor != null && checker.transform.IsChildOf(barWhiteAnchor))
        {
            int n = barWhiteAnchor.childCount;
            return n > 0 && barWhiteAnchor.GetChild(n - 1) == checker.transform;
        }

        return false;
    }

    public bool TryGetEngineFromForChecker(Checker checker, out int engineFrom)
    {
        engineFrom = -1;
        if (checker == null) return false;

        if (barWhitePoint != null && checker.transform.IsChildOf(barWhitePoint.transform))
        {
            engineFrom = BackgammonBoardLayout.BarEngineIndex;
            if (enableMoveSelectionDebugLogs)
                Debug.Log($"[Backgammon][Select] Checker source resolved from bar point checker={checker.name} engineFrom={engineFrom}");
            return true;
        }

        if (barWhiteAnchor != null && checker.transform.IsChildOf(barWhiteAnchor))
        {
            engineFrom = BackgammonBoardLayout.BarEngineIndex;
            if (enableMoveSelectionDebugLogs)
                Debug.Log($"[Backgammon][Select] Checker source resolved from bar anchor checker={checker.name} engineFrom={engineFrom}");
            return true;
        }

        BoardPoint bp = checker.GetComponentInParent<BoardPoint>();
        if (bp != null)
        {
            engineFrom = BackgammonBoardLayout.BoardIndexToEnginePoint(bp.pointIndex);
            if (engineFrom >= 0)
            {
                if (enableMoveSelectionDebugLogs)
                    Debug.Log($"[Backgammon][Select] Checker source resolved from board point checker={checker.name} pointIndex={bp.pointIndex} engineFrom={engineFrom}");
                return true;
            }

            if (enableMoveSelectionDebugLogs)
                Debug.LogWarning($"[Backgammon][Select] Invalid board point mapping checker={checker.name} pointIndex={bp.pointIndex} engineFrom={engineFrom}; checking bar fallbacks.");
        }

        return false;
    }

    /// <summary>World hint for a first-move destination (<paramref name="engineTo"/> is -1 bear off, 0–23 board).</summary>
    public bool TryGetWorldPositionForMoveDestination(int engineTo, out Vector3 worldPos)
    {
        worldPos = default;
        if (engineTo >= 0 && engineTo <= 23)
        {
            int bi = BackgammonBoardLayout.EnginePointToBoardIndex(engineTo);
            if (bi < 0 || bi >= allPoints.Length || allPoints[bi] == null) return false;
            BoardPoint bp = allPoints[bi];
            int nextIdx = bp.checkers.Count;
            worldPos = bp.GetPositionForIndex(nextIdx);
            return true;
        }

        if (engineTo == -1)
        {
            BoardPoint bearOffPoint = GetBearOffPointForMover(PlayerColor.White);
            if (bearOffPoint != null)
            {
                worldPos = bearOffPoint.GetNextStackPosition();
                return true;
            }
            if (bearOffWhiteAnchor != null)
            {
                worldPos = bearOffWhiteAnchor.position;
                return true;
            }

            if (bearOffLineEnd != null)
            {
                worldPos = bearOffLineEnd.position;
                return true;
            }

            int bi0 = BackgammonBoardLayout.EnginePointToBoardIndex(0);
            if (bi0 >= 0 && bi0 < allPoints.Length && allPoints[bi0] != null)
            {
                BoardPoint bp = allPoints[bi0];
                float w = GetPrefabWidth(whiteCheckerPrefab);
                worldPos = bp.transform.position + Vector3.up * 0.12f - bp.inwardDirection * (w * 2.2f);
                return true;
            }

            return false;
        }

        return false;
    }

    /// <summary>Reset all checker materials to baseline (no movable highlight tint).</summary>
    public void ClearMovableCheckerHighlights()
    {
        _movablePulseTargets.Clear();
        _movableHoverRenderer = null;

        for (int i = 0; i < allPoints.Length; i++)
        {
            if (allPoints[i] == null) continue;
            foreach (GameObject go in allPoints[i].checkers)
            {
                if (go == null) continue;
                Checker c = go.GetComponent<Checker>();
                if (c != null) ApplyCheckerVisuals(go, c.color);
            }
        }

        ReapplyBarCheckerBaselines(barWhitePoint);
        ReapplyBarCheckerBaselines(barBlackPoint);
        ReapplyBarCheckerBaselines(bearOffWhitePoint);
        ReapplyBarCheckerBaselines(bearOffBlackPoint);
        ReapplyBarCheckerBaselines(barWhiteAnchor);
        ReapplyBarCheckerBaselines(barBlackAnchor);
        ReapplyBarCheckerBaselines(bearOffWhiteAnchor);
        ReapplyBarCheckerBaselines(bearOffBlackAnchor);
    }

    private void ReapplyBarCheckerBaselines(BoardPoint point)
    {
        if (point == null) return;
        for (int i = 0; i < point.checkers.Count; i++)
        {
            GameObject go = point.checkers[i];
            if (go == null) continue;
            Checker ch = go.GetComponent<Checker>();
            if (ch != null) ApplyCheckerVisuals(go, ch.color);
        }
    }

    private void ReapplyBarCheckerBaselines(Transform anchor)
    {
        if (anchor == null) return;
        for (int c = 0; c < anchor.childCount; c++)
        {
            GameObject go = anchor.GetChild(c).gameObject;
            Checker ch = go.GetComponent<Checker>();
            if (ch != null) ApplyCheckerVisuals(go, ch.color);
        }
    }

    public void SetMovableHighlightColor(Color color)
    {
        movableHighlightBaseColor = color;
    }

    public void RefreshAllCheckerVisuals()
    {
        ClearMovableCheckerHighlights();
    }

    public void RefreshAllBoardPointColors()
    {
        for (int i = 0; i < allPoints.Length; i++)
        {
            if (allPoints[i] == null) continue;
            Color col = (i % 2 == 0) ? darkColor : lightColor;
            allPoints[i].normalColor = col;
            allPoints[i].RefreshColor();
        }
        if (barWhitePoint != null) { barWhitePoint.normalColor = darkColor; barWhitePoint.RefreshColor(); }
        if (barBlackPoint != null) { barBlackPoint.normalColor = lightColor; barBlackPoint.RefreshColor(); }
    }

    public void SetBoardSurfaceColor(Color color)
    {
        ApplySurfaceColor(leftHalfFloor, color);
        ApplySurfaceColor(rightHalfFloor, color);
    }

    public void ApplyTheme(BackgammonThemeSo theme)
    {
        if (theme == null) return;

        whiteBaseColor = theme.checker1BaseColor;
        whiteEmissionColor = theme.checker1EmissionColor;
        whiteEmissionIntensity = theme.checker1EmissionIntensity;
        blackBaseColor = theme.checker2BaseColor;
        blackEmissionColor = theme.checker2EmissionColor;
        blackEmissionIntensity = theme.checker2EmissionIntensity;
        SetMovableHighlightColor(theme.movableHighlightColor);
        RefreshAllCheckerVisuals();

        darkColor = theme.boardPointDarkColor;
        lightColor = theme.boardPointLightColor;
        RefreshAllBoardPointColors();

        if (doublingCubeVisual != null)
        {
            doublingCubeVisual.SetBodyColor(theme.doublingCubeColor, theme.doublingCubeEmission, theme.doublingCubeEmissionIntensity);
            doublingCubeVisual.LabelColor = theme.doublingCubeColor;
        }

        diceManager?.SetDiceTheme(theme.diceBodyColor, theme.dicePipColor, theme.diceLuminosity);

        SetBoardSurfaceColor(theme.boardSurfaceColor);
    }

    private static void ApplySurfaceColor(Transform surface, Color color)
    {
        if (surface == null) return;
        MeshRenderer mr = surface.GetComponent<MeshRenderer>();
        if (mr == null) return;
        MaterialPropertyBlock props = new MaterialPropertyBlock();
        mr.GetPropertyBlock(props);
        props.SetColor("_BaseColor", color);
        mr.SetPropertyBlock(props);
    }

    /// <summary>Highlight player-side checkers that can start a legal turn (engine <paramref name="engineFromPoints"/>).</summary>
    public void ApplyMovableCheckerHighlights(IReadOnlyCollection<int> engineFromPoints)
    {
        ClearMovableCheckerHighlights();
        if (engineFromPoints == null || engineFromPoints.Count == 0) return;

        foreach (int from in engineFromPoints)
        {
            if (from == BackgammonBoardLayout.BarEngineIndex)
            {
                GameObject go = TryGetTopBarChecker(PlayerColor.White);
                if (go == null) continue;
                if (go.GetComponent<Checker>() != null)
                    AddMovablePulseTarget(go);
                continue;
            }

            if (from < 0 || from > 23) continue;

            int boardIdx = BackgammonBoardLayout.EnginePointToBoardIndex(from);
            if (boardIdx < 0 || boardIdx >= allPoints.Length || allPoints[boardIdx] == null) continue;

            BoardPoint bp = allPoints[boardIdx];
            if (bp.checkers.Count == 0) continue;
            GameObject topOnPoint = bp.checkers[bp.checkers.Count - 1];
            if (topOnPoint == null) continue;
            Checker checker = topOnPoint.GetComponent<Checker>();
            if (checker != null && checker.color == PlayerColor.White)
                AddMovablePulseTarget(topOnPoint);
        }

        RefreshMovablePulseVisuals();
    }

    public void ClearPoints()
    {
        foreach (var floor in new[] { leftHalfFloor, rightHalfFloor })
        {
            if (floor == null) continue;
            for (int i = floor.childCount - 1; i >= 0; i--)
            {
                Transform ch = floor.GetChild(i);
                if (!ch.GetComponent<BoardPoint>()) continue;
                if (Application.isPlaying)
                    Destroy(ch.gameObject);
                else
                    DestroyImmediate(ch.gameObject);
            }
        }
        System.Array.Clear(allPoints, 0, allPoints.Length);
        barWhitePoint = null;
        barBlackPoint = null;
        bearOffWhitePoint = null;
        bearOffBlackPoint = null;
    }

    /// <summary>Removes checker objects from all points (does not destroy point meshes).</summary>
    public void ClearAllCheckersFromBoard()
    {
        for (int i = 0; i < allPoints.Length; i++)
        {
            if (allPoints[i] == null) continue;
            while (allPoints[i].checkers.Count > 0)
            {
                GameObject top = allPoints[i].RemoveTopChecker();
                if (top != null)
                {
                    if (Application.isPlaying)
                        Destroy(top);
                    else
                        DestroyImmediate(top);
                }
            }
        }

        ClearPointCheckers(barWhitePoint);
        ClearPointCheckers(barBlackPoint);
        ClearPointCheckers(bearOffWhitePoint);
        ClearPointCheckers(bearOffBlackPoint);
        ClearChildCheckers(barWhiteAnchor);
        ClearChildCheckers(barBlackAnchor);
        ClearChildCheckers(bearOffWhiteAnchor);
        ClearChildCheckers(bearOffBlackAnchor);
    }

    private static void ClearPointCheckers(BoardPoint point)
    {
        if (point == null) return;
        for (int i = point.checkers.Count - 1; i >= 0; i--)
        {
            GameObject ch = point.RemoveTopChecker();
            if (ch == null) continue;
            if (Application.isPlaying)
                Destroy(ch);
            else
                DestroyImmediate(ch);
        }
    }

    private static void ClearChildCheckers(Transform anchor)
    {
        if (anchor == null) return;
        for (int c = anchor.childCount - 1; c >= 0; c--)
        {
            GameObject ch = anchor.GetChild(c).gameObject;
            // Skip BoardPoint components - only destroy checker objects
            if (ch.GetComponent<BoardPoint>() != null) continue;

            if (Application.isPlaying)
                Destroy(ch);
            else
                DestroyImmediate(ch);
        }
    }

    /// <summary>Rebuild checker stacks from the current logical engine state.</summary>
    public void SyncCheckersFromGameState(GameState state)
    {
        using var syncScope = FullSyncMarker.Auto();
        if (state == null) return;
        _movablePulseTargets.Clear();
        ClearAllPointHighlights();
        ClearAllCheckersFromBoard();

        for (int enginePoint = 0; enginePoint < 24; enginePoint++)
        {
            int boardIdx = BackgammonBoardLayout.EnginePointToBoardIndex(enginePoint);
            if (boardIdx < 0 || boardIdx >= allPoints.Length || allPoints[boardIdx] == null) continue;

            int moverAt = state.Player1Checkers[enginePoint];
            // EngineCore: same physical point as P1[enginePoint] uses P2[23 - enginePoint] (see MoveGenerator / GameStateExtensions).
            int opponentAt = state.Player2Checkers[23 - enginePoint];
            if (moverAt > 0 && opponentAt > 0)
                Debug.LogWarning($"BoardManager: both players on physical point (P1 idx {enginePoint}, P2 idx {23 - enginePoint}, board {boardIdx}) — invalid position.");

            BoardPoint bp = allPoints[boardIdx];
            for (int i = 0; i < moverAt; i++)
                SpawnCheckerOnPoint(bp, PlayerColor.White);
            for (int i = 0; i < opponentAt; i++)
                SpawnCheckerOnPoint(bp, PlayerColor.Black);
        }

        StackBarCheckers(state.Player1Checkers[24], barWhitePoint, barWhiteAnchor, PlayerColor.White);
        StackBarCheckers(state.Player2Checkers[24], barBlackPoint, barBlackAnchor, PlayerColor.Black);

        // Calculate and rebuild bear-off checkers (15 - total on board/bar)
        int player1BorneOff = CalculateBorneOffCount(state.Player1Checkers);
        int player2BorneOff = CalculateBorneOffCount(state.Player2Checkers);
        StackBearOffCheckers(player1BorneOff, bearOffWhitePoint, bearOffWhiteAnchor, PlayerColor.White);
        StackBearOffCheckers(player2BorneOff, bearOffBlackPoint, bearOffBlackAnchor, PlayerColor.Black);
    }

    private int CalculateBorneOffCount(int[] checkers)
    {
        int totalOnBoardAndBar = 0;
        for (int i = 0; i <= 24; i++)
        {
            totalOnBoardAndBar += checkers[i];
        }
        return 15 - totalOnBoardAndBar;
    }

    private void StackBearOffCheckers(int count, BoardPoint point, Transform anchor, PlayerColor color)
    {
        if (count <= 0) return;
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = color == PlayerColor.White ? whiteCheckerPrefab : blackCheckerPrefab;
            GameObject checkerObj = Instantiate(prefab);
            ApplyCheckerVisuals(checkerObj, color);
            checkerObj.GetComponent<Checker>().color = color;
            if (!TryStackCheckerOnBar(checkerObj, point, anchor, animateOnPoint: false))
            {
                if (Application.isPlaying)
                    Destroy(checkerObj);
                else
                    DestroyImmediate(checkerObj);
            }
        }
    }

    private void StackBarCheckers(int count, BoardPoint point, Transform anchor, PlayerColor color)
    {
        if (count <= 0) return;
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = color == PlayerColor.White ? whiteCheckerPrefab : blackCheckerPrefab;
            GameObject checkerObj = Instantiate(prefab, anchor);
            ApplyCheckerVisuals(checkerObj, color);
            checkerObj.GetComponent<Checker>().color = color;
            if (!TryStackCheckerOnBar(checkerObj, point, anchor))
            {
                if (Application.isPlaying)
                    Destroy(checkerObj);
                else
                    DestroyImmediate(checkerObj);
            }
        }
    }

    private void SpawnCheckerOnPoint(BoardPoint bp, PlayerColor color)
    {
        GameObject prefab = color == PlayerColor.White ? whiteCheckerPrefab : blackCheckerPrefab;
        GameObject checkerObj = Instantiate(prefab);
        ApplyCheckerVisuals(checkerObj, color);
        checkerObj.GetComponent<Checker>().color = color;
        bp.AddChecker(checkerObj, animated: false);
    }

    /// <summary>
    /// Apply one move visually in-place so checker travel can animate.
    /// Returns false when prerequisites are missing, so caller can fall back to full sync.
    /// </summary>
    public bool TryApplySingleVisualMove(Move move, PlayerColor moverColor = PlayerColor.White)
    {
        return TryApplySingleVisualMove(move, out _, moverColor);
    }

    /// <summary>
    /// Apply one move visually in-place and return the moved checker component when successful.
    /// </summary>
    public bool TryApplySingleVisualMove(Move move, out Checker movedChecker, PlayerColor moverColor = PlayerColor.White)
    {
        movedChecker = null;
        if (allPoints == null || allPoints.Length != 24) return false;
        if (move.From < 0 || move.From > BackgammonBoardLayout.BarEngineIndex) return false;
        if (move.To < -1 || move.To > 23) return false;

        int mappedFrom = MapMoveEnginePointForMoverColor(move.From, moverColor);
        int mappedTo = MapMoveEnginePointForMoverColor(move.To, moverColor);
        GameObject movingChecker = PeekSourceCheckerForMove(mappedFrom, moverColor);
        if (movingChecker == null)
        {
            if (enableMoveMappingDebugLogs)
                Debug.LogWarning(
                    $"[Backgammon][MoveMap] No source checker found. mover={moverColor} rawFrom={move.From} rawTo={move.To} mappedFrom={mappedFrom} mappedTo={mappedTo}");
            return false;
        }

        BoardPoint toPoint = null;
        int toBoardIdx = -1;
        if (mappedTo == -1)
        {
            // Bearoff move - use either programmatic point or manual anchor
            toPoint = GetBearOffPointForMover(moverColor);
            Transform bearOffAnchor = GetBearOffAnchorForMover(moverColor);

            // Validate at least one target exists
            if (toPoint == null && bearOffAnchor == null)
                return false;
        }
        else
        {
            toBoardIdx = BackgammonBoardLayout.EnginePointToBoardIndex(mappedTo);
            if (toBoardIdx < 0 || toBoardIdx >= allPoints.Length) return false;
            toPoint = allPoints[toBoardIdx];
            if (toPoint == null) return false;
        }

        if (enableMoveMappingDebugLogs)
        {
            int fromBoardIdx = mappedFrom >= 0 && mappedFrom <= 23 ? BackgammonBoardLayout.EnginePointToBoardIndex(mappedFrom) : -1;
            Checker movingCheckerComponent = movingChecker.GetComponent<Checker>();
            PlayerColor movingCheckerColor = movingCheckerComponent != null ? movingCheckerComponent.color : (PlayerColor)(-1);
            string movingCheckerName = movingChecker != null ? movingChecker.name : "<null>";
            Debug.Log(
                $"[Backgammon][MoveMap] VisualMoveStart mover={moverColor} rawFrom={move.From} rawTo={move.To} mappedFrom={mappedFrom} mappedTo={mappedTo} fromBoard={fromBoardIdx} toBoard={toBoardIdx} selectedChecker={movingCheckerName} selectedColor={movingCheckerColor}");
        }

        GameObject hitChecker = mappedTo == -1 ? null : TryPeekSingleOpposingBlot(toPoint, moverColor);

        if (hitChecker != null)
        {
            GameObject removedHit = toPoint.RemoveTopChecker();
            if (removedHit != hitChecker) return false;
            BoardPoint hitBarPoint = moverColor == PlayerColor.White ? barBlackPoint : barWhitePoint;
            Transform hitBarAnchor = moverColor == PlayerColor.White ? barBlackAnchor : barWhiteAnchor;
            if (!TryStackCheckerOnBar(removedHit, hitBarPoint, hitBarAnchor, animateOnPoint: true)) return false;
            if (enableMoveMappingDebugLogs)
            {
                Checker hitCheckerComponent = removedHit != null ? removedHit.GetComponent<Checker>() : null;
                PlayerColor hitCheckerColor = hitCheckerComponent != null ? hitCheckerComponent.color : (PlayerColor)(-1);
                Debug.Log(
                    $"[Backgammon][MoveMap] VisualHit mover={moverColor} captured={removedHit?.name ?? "<null>"} capturedColor={hitCheckerColor} toBar={(moverColor == PlayerColor.White ? "black" : "white")}");
            }
        }

        if (!TryDetachCheckerFromSource(mappedFrom, movingChecker, moverColor)) return false;
        Vector3 moveStartWorld = movingChecker.transform.position;
        Vector3 moveTargetWorld = Vector3.zero;
        float moveDistance = 0f;

        if (mappedTo == -1)
        {
            // Bearoff: use TryStackCheckerOnBar which handles both point and anchor
            BoardPoint bearOffPoint = GetBearOffPointForMover(moverColor);
            Transform bearOffAnchor = GetBearOffAnchorForMover(moverColor);
            if (!TryStackCheckerOnBar(movingChecker, bearOffPoint, bearOffAnchor, animateOnPoint: true))
                return false;
        }
        else
        {
            // Regular board move: use standard AddChecker
            moveTargetWorld = toPoint.GetNextStackPosition();
            moveDistance = Vector3.Distance(moveStartWorld, moveTargetWorld);
            toPoint.AddChecker(movingChecker, animated: true);
        }

        movedChecker = movingChecker.GetComponent<Checker>();
        if (move.From == BackgammonBoardLayout.BarEngineIndex)
        {
            Debug.Log(
                $"[Backgammon][MoveMap] BarEntryMotion mover={moverColor} checker={movingChecker.name} start={moveStartWorld} target={moveTargetWorld} distance={moveDistance:F3}");
        }
        if (enableMoveMappingDebugLogs)
        {
            Checker movingCheckerComponent = movingChecker.GetComponent<Checker>();
            PlayerColor movingCheckerColor = movingCheckerComponent != null ? movingCheckerComponent.color : (PlayerColor)(-1);
            int toStackCount = toPoint != null ? toPoint.checkers.Count : -1;
            Debug.Log(
                $"[Backgammon][MoveMap] VisualMoveEnd mover={moverColor} moved={movingChecker.name} movedColor={movingCheckerColor} mappedTo={mappedTo} toBoard={toBoardIdx} toStackCount={toStackCount}");
        }
        return true;
    }

    private static int MapMoveEnginePointForMoverColor(int enginePoint, PlayerColor moverColor)
    {
        if (enginePoint == BackgammonBoardLayout.BarEngineIndex)
            return enginePoint;
        if (enginePoint < 0 || enginePoint > 23)
            return enginePoint;
        return moverColor == PlayerColor.Black ? 23 - enginePoint : enginePoint;
    }

    /// <summary>
    /// Best-effort visual reverse of a previously applied single move for undo animations.
    /// Falls back to full sync in caller when this returns false.
    /// </summary>
    public bool TryApplySingleVisualUndoMove(Move appliedMove)
    {
        return TryApplySingleVisualUndoMove(appliedMove, PlayerColor.White, out _);
    }

    public bool TryApplySingleVisualUndoMove(Move appliedMove, PlayerColor moverColor, out string failureReason)
    {
        using var undoScope = VisualUndoMarker.Auto();
        failureReason = "unknown";
        if (appliedMove.To < -1 || appliedMove.To > 23)
        {
            failureReason = "invalid_move_to";
            return false;
        }
        if (appliedMove.From < 0 || appliedMove.From > BackgammonBoardLayout.BarEngineIndex)
        {
            failureReason = "invalid_move_from";
            return false;
        }

        if (enableMoveMappingDebugLogs)
            Debug.Log($"[Backgammon][Undo] VisualUndoStart from={appliedMove.From} to={appliedMove.To} isHit={appliedMove.IsHit} mover={moverColor}");

        int reverseFrom = MapMoveEnginePointForMoverColor(appliedMove.To, moverColor);
        int reverseTo = MapMoveEnginePointForMoverColor(appliedMove.From, moverColor);
        Move reverse = new Move { From = reverseFrom, To = reverseTo };
        GameObject movingChecker = PeekSourceCheckerForMove(reverse.From, moverColor);
        if (movingChecker == null)
        {
            failureReason = "no_moving_checker";
            if (enableMoveMappingDebugLogs)
                Debug.LogWarning($"[Backgammon][Undo] VisualUndoFail reason=no_moving_checker reverseFrom={reverse.From}");
            return false;
        }

        if (!TryDetachCheckerFromSource(reverse.From, movingChecker, moverColor))
        {
            failureReason = "detach_mover";
            if (enableMoveMappingDebugLogs)
                Debug.LogWarning($"[Backgammon][Undo] VisualUndoFail reason=detach_mover reverseFrom={reverse.From}");
            return false;
        }

        if (reverse.To == BackgammonBoardLayout.BarEngineIndex)
        {
            BoardPoint moverBarPoint = moverColor == PlayerColor.White ? barWhitePoint : barBlackPoint;
            Transform moverBarAnchor = moverColor == PlayerColor.White ? barWhiteAnchor : barBlackAnchor;
            bool stackedOnBar = TryStackCheckerOnBar(movingChecker, moverBarPoint, moverBarAnchor, animateOnPoint: true);
            if (!stackedOnBar)
                failureReason = "stack_mover_to_bar";
            if (!stackedOnBar && enableMoveMappingDebugLogs)
                Debug.LogWarning("[Backgammon][Undo] VisualUndoFail reason=stack_mover_to_bar");
            return stackedOnBar;
        }

        if (reverse.To == -1)
        {
            BoardPoint moverBearOffPoint = GetBearOffPointForMover(moverColor);
            Transform moverBearOffAnchor = GetBearOffAnchorForMover(moverColor);
            bool stackedOnBearOff = TryStackCheckerOnBar(movingChecker, moverBearOffPoint, moverBearOffAnchor, animateOnPoint: true);
            if (!stackedOnBearOff)
            {
                failureReason = "stack_mover_to_bear_off";
                if (enableMoveMappingDebugLogs)
                    Debug.LogWarning("[Backgammon][Undo] VisualUndoFail reason=stack_mover_to_bear_off");
                return false;
            }

            failureReason = "ok";
            return true;
        }

        int toBoardIdx = BackgammonBoardLayout.EnginePointToBoardIndex(reverse.To);
        if (toBoardIdx < 0 || toBoardIdx >= allPoints.Length)
        {
            failureReason = "invalid_to_board";
            if (enableMoveMappingDebugLogs)
                Debug.LogWarning($"[Backgammon][Undo] VisualUndoFail reason=invalid_to_board toBoard={toBoardIdx}");
            return false;
        }
        BoardPoint toPoint = allPoints[toBoardIdx];
        if (toPoint == null)
        {
            failureReason = "null_to_point";
            if (enableMoveMappingDebugLogs)
                Debug.LogWarning($"[Backgammon][Undo] VisualUndoFail reason=null_to_point toBoard={toBoardIdx}");
            return false;
        }
        toPoint.AddChecker(movingChecker, animated: true);

        if (!appliedMove.IsHit)
        {
            failureReason = "ok";
            return true;
        }

        int restoreBoardIdx = BackgammonBoardLayout.EnginePointToBoardIndex(reverse.From);
        if (restoreBoardIdx < 0 || restoreBoardIdx >= allPoints.Length || allPoints[restoreBoardIdx] == null)
        {
            failureReason = "invalid_restore_board";
            if (enableMoveMappingDebugLogs)
                Debug.LogWarning($"[Backgammon][Undo] VisualUndoFail reason=invalid_restore_board restoreBoard={restoreBoardIdx}");
            return false;
        }

        PlayerColor capturedColor = moverColor == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;
        GameObject capturedChecker = TryPopTopBarChecker(capturedColor);
        if (capturedChecker == null)
        {
            failureReason = capturedColor == PlayerColor.Black ? "no_captured_checker_on_black_bar" : "no_captured_checker_on_white_bar";
            if (enableMoveMappingDebugLogs)
                Debug.LogWarning($"[Backgammon][Undo] VisualUndoFail reason={failureReason}");
            return false;
        }

        BoardPoint restorePoint = allPoints[restoreBoardIdx];
        restorePoint.AddChecker(capturedChecker, animated: true);
        failureReason = "ok";
        if (enableUndoPerformanceLogs)
            Debug.Log($"[Backgammon][Undo][Perf] Visual undo success move={appliedMove.From}->{appliedMove.To} mover={moverColor} hit={appliedMove.IsHit}");
        if (enableMoveMappingDebugLogs)
            Debug.Log($"[Backgammon][Undo] VisualUndoHitRestoreSuccess mover={movingChecker.name} captured={capturedChecker.name} restoreBoard={restoreBoardIdx}");
        return true;
    }

    private GameObject PeekSourceCheckerForMove(int engineFrom, PlayerColor moverColor)
    {
        if (engineFrom == -1)
        {
            GameObject bearOffTop = TryGetTopBearOffChecker(moverColor);
            if (bearOffTop == null) return null;
            if (!IsCheckerOwnedByMover(bearOffTop, moverColor))
            {
                if (enableMoveMappingDebugLogs)
                    Debug.LogWarning($"[Backgammon][MoveMap] Source bear-off top color mismatch. mover={moverColor} checker={bearOffTop?.name ?? "<null>"}");
                return null;
            }

            return bearOffTop;
        }

        if (engineFrom == BackgammonBoardLayout.BarEngineIndex)
        {
            GameObject barTop = TryGetTopBarChecker(moverColor);
            if (barTop == null) return null;
            if (!IsCheckerOwnedByMover(barTop, moverColor))
            {
                if (enableMoveMappingDebugLogs)
                    Debug.LogWarning($"[Backgammon][MoveMap] Source bar top color mismatch. mover={moverColor} checker={barTop?.name ?? "<null>"}");
                return null;
            }

            return barTop;
        }

        int fromBoardIdx = BackgammonBoardLayout.EnginePointToBoardIndex(engineFrom);
        if (fromBoardIdx < 0 || fromBoardIdx >= allPoints.Length) return null;
        BoardPoint fromPoint = allPoints[fromBoardIdx];
        if (fromPoint == null || fromPoint.checkers.Count == 0) return null;
        GameObject topChecker = fromPoint.checkers[fromPoint.checkers.Count - 1];
        if (!IsCheckerOwnedByMover(topChecker, moverColor))
        {
            if (enableMoveMappingDebugLogs)
                Debug.LogWarning(
                    $"[Backgammon][MoveMap] Source top color mismatch. mover={moverColor} fromEngine={engineFrom} fromBoard={fromBoardIdx} checker={topChecker?.name ?? "<null>"}");
            return null;
        }

        return topChecker;
    }

    private bool TryDetachCheckerFromSource(int engineFrom, GameObject expectedChecker, PlayerColor moverColor)
    {
        if (expectedChecker == null) return false;
        if (engineFrom == -1)
        {
            if (TryDetachCheckerFromBearOffPoint(expectedChecker, moverColor))
                return true;
            if (TryDetachCheckerFromBearOffAnchor(expectedChecker, moverColor))
                return true;
            if (enableMoveMappingDebugLogs)
            {
                Debug.LogWarning(
                    $"[Backgammon][MoveMap] Bear-off detach failed. mover={moverColor} expected={expectedChecker.name} pointCount={(moverColor == PlayerColor.White ? bearOffWhitePoint?.checkers.Count ?? -1 : bearOffBlackPoint?.checkers.Count ?? -1)} anchorCount={(moverColor == PlayerColor.White ? bearOffWhiteAnchor?.childCount ?? -1 : bearOffBlackAnchor?.childCount ?? -1)}");
            }
            return false;
        }

        if (engineFrom == BackgammonBoardLayout.BarEngineIndex)
        {
            if (TryDetachCheckerFromBarPoint(expectedChecker, moverColor))
                return true;
            if (TryDetachCheckerFromBarAnchor(expectedChecker, moverColor))
                return true;
            if (enableMoveMappingDebugLogs)
            {
                Debug.LogWarning(
                    $"[Backgammon][MoveMap] Bar detach failed. mover={moverColor} expected={expectedChecker.name} pointCount={(moverColor == PlayerColor.White ? barWhitePoint?.checkers.Count ?? -1 : barBlackPoint?.checkers.Count ?? -1)} anchorCount={(moverColor == PlayerColor.White ? barWhiteAnchor?.childCount ?? -1 : barBlackAnchor?.childCount ?? -1)}");
            }
            return false;
        }

        int fromBoardIdx = BackgammonBoardLayout.EnginePointToBoardIndex(engineFrom);
        if (fromBoardIdx < 0 || fromBoardIdx >= allPoints.Length) return false;
        BoardPoint fromPoint = allPoints[fromBoardIdx];
        if (fromPoint == null) return false;
        GameObject removed = fromPoint.RemoveTopChecker();
        return removed == expectedChecker;
    }

    private static GameObject TryPeekSingleOpposingBlot(BoardPoint point, PlayerColor moverColor)
    {
        if (point == null || point.checkers.Count != 1) return null;
        GameObject top = point.checkers[0];
        if (top == null) return null;
        Checker checker = top.GetComponent<Checker>();
        if (checker == null) return null;
        PlayerColor opposingColor = moverColor == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;
        if (checker.color != opposingColor) return null;
        return top;
    }

    private static bool IsCheckerOwnedByMover(GameObject checkerObj, PlayerColor moverColor)
    {
        if (checkerObj == null) return false;
        Checker checker = checkerObj.GetComponent<Checker>();
        if (checker == null) return false;
        return checker.color == moverColor;
    }

    private bool TryStackCheckerOnBar(GameObject checkerObj, BoardPoint point, Transform anchor, bool animateOnPoint = false)
    {
        if (checkerObj == null) return false;
        if (point != null)
        {
            // Use BoardPoint.AddChecker for all points (including bearoff)
            // Bearoff points have isBearOffPoint=true which makes them stack with maxBaseStack=1
            Debug.Log($"[BearOff][BoardPoint] Using BoardPoint path for point={point.name} isBearOff={point.isBearOffPoint} checkerCount={point.checkers.Count}");
            point.AddChecker(checkerObj, animated: animateOnPoint);
            return true;
        }

        if (anchor == null) return false;

        Debug.Log($"[BearOff][Anchor] anchor={anchor.name} checker={checkerObj.name} childCount={anchor.childCount} animate={animateOnPoint}");

        int stackIndex = anchor.childCount;

        // Get the world-space thickness from first checker or use constant
        float worldThickness = TrayBearOffStackStepY;
        if (anchor.childCount > 0)
        {
            Transform firstChecker = anchor.GetChild(0);
            MeshRenderer mr = firstChecker.GetComponentInChildren<MeshRenderer>();
            if (mr != null)
            {
                worldThickness = mr.bounds.size.y;
            }
        }

        // Parent the checker - preserves world scale (same as BoardPoint/Checker.MoveToPosition)
        checkerObj.transform.SetParent(anchor);

        // Apply rotation based on anchor (bottom = 90° X, top = -90° X)
        bool isBottomBearOff = (anchor == bearOffWhiteAnchor);
        float xRotation = isBottomBearOff ? 90f : -90f;
        checkerObj.transform.rotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Calculate world position for stacking
        // Since checkers are rotated 90° in X, stack along Z instead of Y
        Vector3 stackDirection = isBottomBearOff ? Vector3.forward : Vector3.back;
        Vector3 worldPosition = anchor.position + (stackDirection * stackIndex * worldThickness);
        checkerObj.transform.position = worldPosition;

        Debug.Log($"[BearOff][AnchorFinal] stackIndex={stackIndex} worldThickness={worldThickness:F6} targetWorldPos={worldPosition} actualWorldPos={checkerObj.transform.position} rotation=X{xRotation} stackDir={stackDirection}");

        return true;
    }

    private GameObject TryPopTopBarChecker(PlayerColor barOwner)
    {
        BoardPoint sourceBarPoint = barOwner == PlayerColor.White ? barWhitePoint : barBlackPoint;
        if (sourceBarPoint != null)
        {
            if (sourceBarPoint.checkers.Count == 0) return null;
            return sourceBarPoint.RemoveTopChecker();
        }

        Transform sourceBarAnchor = barOwner == PlayerColor.White ? barWhiteAnchor : barBlackAnchor;
        if (sourceBarAnchor == null || sourceBarAnchor.childCount == 0) return null;
        Transform top = sourceBarAnchor.GetChild(sourceBarAnchor.childCount - 1);
        if (top == null) return null;
        top.SetParent(null, true);
        return top.gameObject;
    }

    private GameObject TryGetTopBarChecker(PlayerColor moverColor)
    {
        BoardPoint sourceBarPoint = moverColor == PlayerColor.White ? barWhitePoint : barBlackPoint;
        if (sourceBarPoint != null && sourceBarPoint.checkers.Count > 0)
        {
            GameObject fromPoint = sourceBarPoint.checkers[sourceBarPoint.checkers.Count - 1];
            if (IsCheckerOwnedByMover(fromPoint, moverColor))
                return fromPoint;
            if (enableMoveMappingDebugLogs)
                Debug.LogWarning(
                    $"[Backgammon][MoveMap] Bar point top ownership mismatch. mover={moverColor} checker={fromPoint?.name ?? "<null>"}");
        }

        Transform sourceBarAnchor = moverColor == PlayerColor.White ? barWhiteAnchor : barBlackAnchor;
        if (sourceBarAnchor == null || sourceBarAnchor.childCount == 0) return null;
        GameObject fromAnchor = sourceBarAnchor.GetChild(sourceBarAnchor.childCount - 1).gameObject;
        if (IsCheckerOwnedByMover(fromAnchor, moverColor))
            return fromAnchor;
        if (enableMoveMappingDebugLogs)
            Debug.LogWarning(
                $"[Backgammon][MoveMap] Bar anchor top ownership mismatch. mover={moverColor} checker={fromAnchor?.name ?? "<null>"}");
        return null;
    }

    private GameObject TryGetTopBearOffChecker(PlayerColor moverColor)
    {
        BoardPoint sourceBearOffPoint = GetBearOffPointForMover(moverColor);
        if (sourceBearOffPoint != null && sourceBearOffPoint.checkers.Count > 0)
        {
            return sourceBearOffPoint.checkers[sourceBearOffPoint.checkers.Count - 1];
        }

        Transform sourceBearOffAnchor = GetBearOffAnchorForMover(moverColor);
        if (sourceBearOffAnchor == null || sourceBearOffAnchor.childCount == 0) return null;
        return sourceBearOffAnchor.GetChild(sourceBearOffAnchor.childCount - 1).gameObject;
    }

    private BoardPoint GetBearOffPointForMover(PlayerColor moverColor)
    {
        return moverColor == PlayerColor.White ? bearOffWhitePoint : bearOffBlackPoint;
    }

    private Transform GetBearOffAnchorForMover(PlayerColor moverColor)
    {
        return moverColor == PlayerColor.White ? bearOffWhiteAnchor : bearOffBlackAnchor;
    }

    private bool TryDetachCheckerFromBarPoint(GameObject expectedChecker, PlayerColor moverColor)
    {
        BoardPoint sourceBarPoint = moverColor == PlayerColor.White ? barWhitePoint : barBlackPoint;
        if (sourceBarPoint == null || sourceBarPoint.checkers.Count == 0)
            return false;
        GameObject topExpected = sourceBarPoint.checkers[sourceBarPoint.checkers.Count - 1];
        if (topExpected != expectedChecker)
            return false;
        GameObject removed = sourceBarPoint.RemoveTopChecker();
        return removed == expectedChecker;
    }

    private bool TryDetachCheckerFromBarAnchor(GameObject expectedChecker, PlayerColor moverColor)
    {
        Transform sourceBarAnchor = moverColor == PlayerColor.White ? barWhiteAnchor : barBlackAnchor;
        if (sourceBarAnchor == null || sourceBarAnchor.childCount == 0)
            return false;
        Transform topAnchor = sourceBarAnchor.GetChild(sourceBarAnchor.childCount - 1);
        if (topAnchor == null || topAnchor.gameObject != expectedChecker)
            return false;
        topAnchor.SetParent(null, true);
        return true;
    }

    private bool TryDetachCheckerFromBearOffPoint(GameObject expectedChecker, PlayerColor moverColor)
    {
        BoardPoint sourceBearOffPoint = GetBearOffPointForMover(moverColor);
        if (sourceBearOffPoint == null || sourceBearOffPoint.checkers.Count == 0)
            return false;
        GameObject topExpected = sourceBearOffPoint.checkers[sourceBearOffPoint.checkers.Count - 1];
        if (topExpected != expectedChecker)
            return false;
        GameObject removed = sourceBearOffPoint.RemoveTopChecker();
        return removed == expectedChecker;
    }

    private bool TryDetachCheckerFromBearOffAnchor(GameObject expectedChecker, PlayerColor moverColor)
    {
        Transform sourceBearOffAnchor = GetBearOffAnchorForMover(moverColor);
        if (sourceBearOffAnchor == null || sourceBearOffAnchor.childCount == 0)
            return false;
        Transform topAnchor = sourceBearOffAnchor.GetChild(sourceBearOffAnchor.childCount - 1);
        if (topAnchor == null || topAnchor.gameObject != expectedChecker)
            return false;
        topAnchor.SetParent(null, true);
        return true;
    }

    private float GetCheckerHeightForStack(GameObject checkerObj)
    {
        MeshRenderer mr = checkerObj != null ? checkerObj.GetComponentInChildren<MeshRenderer>() : null;
        if (mr != null)
            return Mathf.Max(0.001f, mr.bounds.size.y);
        return Mathf.Max(0.001f, forcedCheckerHeight);
    }

    public void ClearAllPointHighlights()
    {
        for (int i = 0; i < allPoints.Length; i++)
            allPoints[i]?.SetHighlighted(false);
        _movePreviewHighlightedBoardIndices.Clear();
    }

    /// <summary>HUD view: toggles engine→board mapping (identity vs 23−e). Does not rotate the board pivot.</summary>
    public void SetBoardViewHorizontal(bool horizontal)
    {
        BackgammonBoardLayout.SetHorizontal(horizontal);
    }
}