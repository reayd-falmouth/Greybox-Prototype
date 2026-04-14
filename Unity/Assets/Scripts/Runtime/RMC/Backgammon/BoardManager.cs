using System;
using System.Collections.Generic;
using EngineCore;
using Runtime.RMC._MyProject_.Core;
using Runtime.RMC.Backgammon;
using Runtime.RMC.Backgammon.Core;
using UnityEngine;
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

    [Header("Layout Tuning")]
    public float edgePaddingX = 0.1f;
    public Color darkColor = new Color(0.1f, 0.1f, 0.1f);
    public Color lightColor = new Color(0.9f, 0.9f, 0.9f);

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

    [Header("Move preview (hover lines)")]
    [Tooltip("Optional: clone this material for lines (same idea as MoveVisualizer lineMaterial). When set, dashed shader is skipped; use Unlit/Color or Sprites/Default.")]
    [SerializeField] private Material movePreviewLineTemplateMaterial;
    [SerializeField] private bool enableMovePreviewLines = true;
    [SerializeField] private BackgammonGameController gameController;
    [SerializeField] private Camera rayCamera;
    [SerializeField] private bool enableMoveSelectionDebugLogs = true;
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
        ClearPoints();
        // 0-5 Right Bottom, 6-11 Left Bottom, 12-17 Left Top, 18-23 Right Top
        GenerateSet(rightHalfFloor, 0, true);
        GenerateSet(leftHalfFloor, 6, true);
        GenerateSet(leftHalfFloor, 12, false);
        GenerateSet(rightHalfFloor, 18, false);
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

    public void SpawnInitialCheckers()
    {
        var setup = new Dictionary<int, int>() { { 0, 2 }, { 11, 5 }, { 16, 3 }, { 18, 5 }, { 5, -5 }, { 7, -3 }, { 12, -5 }, { 23, -2 } };

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

    /// <summary>Logical P1 top-of-stack checker on a point or bar (matches movable highlight).</summary>
    public bool IsTopLogicalP1Checker(Checker checker)
    {
        if (checker == null || checker.color != PlayerColor.White) return false;
        BoardPoint bp = checker.GetComponentInParent<BoardPoint>();
        if (bp != null)
        {
            int n = bp.checkers.Count;
            return n > 0 && bp.checkers[n - 1] == checker.gameObject;
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
        BoardPoint bp = checker.GetComponentInParent<BoardPoint>();
        if (bp != null)
        {
            engineFrom = BackgammonBoardLayout.BoardIndexToEnginePoint(bp.pointIndex);
            return true;
        }

        if (barWhiteAnchor != null && checker.transform.IsChildOf(barWhiteAnchor))
        {
            engineFrom = BackgammonBoardLayout.BarEngineIndex;
            return true;
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

        ReapplyBarCheckerBaselines(barWhiteAnchor);
        ReapplyBarCheckerBaselines(barBlackAnchor);
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

    /// <summary>Highlight logical P1 checkers that can start a legal turn (engine <paramref name="engineFromPoints"/>).</summary>
    public void ApplyMovableCheckerHighlights(IReadOnlyCollection<int> engineFromPoints)
    {
        ClearMovableCheckerHighlights();
        if (engineFromPoints == null || engineFromPoints.Count == 0) return;

        foreach (int from in engineFromPoints)
        {
            if (from == BackgammonBoardLayout.BarEngineIndex)
            {
                if (barWhiteAnchor == null || barWhiteAnchor.childCount == 0) continue;
                GameObject go = barWhiteAnchor.GetChild(barWhiteAnchor.childCount - 1).gameObject;
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

        ClearChildCheckers(barWhiteAnchor);
        ClearChildCheckers(barBlackAnchor);
    }

    private static void ClearChildCheckers(Transform anchor)
    {
        if (anchor == null) return;
        for (int c = anchor.childCount - 1; c >= 0; c--)
        {
            GameObject ch = anchor.GetChild(c).gameObject;
            if (Application.isPlaying)
                Destroy(ch);
            else
                DestroyImmediate(ch);
        }
    }

    /// <summary>Rebuild checker stacks from the current logical engine state (P1 = white, P2 = black).</summary>
    public void SyncCheckersFromGameState(GameState state)
    {
        if (state == null) return;
        _movablePulseTargets.Clear();
        ClearAllPointHighlights();
        ClearAllCheckersFromBoard();

        for (int enginePoint = 0; enginePoint < 24; enginePoint++)
        {
            int boardIdx = BackgammonBoardLayout.EnginePointToBoardIndex(enginePoint);
            if (boardIdx < 0 || boardIdx >= allPoints.Length || allPoints[boardIdx] == null) continue;

            int p1 = state.Player1Checkers[enginePoint];
            // EngineCore: same physical point as P1[enginePoint] uses P2[23 - enginePoint] (see MoveGenerator / GameStateExtensions).
            int p2 = state.Player2Checkers[23 - enginePoint];
            if (p1 > 0 && p2 > 0)
                Debug.LogWarning($"BoardManager: both players on physical point (P1 idx {enginePoint}, P2 idx {23 - enginePoint}, board {boardIdx}) — invalid position.");

            BoardPoint bp = allPoints[boardIdx];
            for (int i = 0; i < p1; i++)
                SpawnCheckerOnPoint(bp, PlayerColor.White);
            for (int i = 0; i < p2; i++)
                SpawnCheckerOnPoint(bp, PlayerColor.Black);
        }

        StackBarCheckers(state.Player1Checkers[24], barWhiteAnchor, PlayerColor.White);
        StackBarCheckers(state.Player2Checkers[24], barBlackAnchor, PlayerColor.Black);
    }

    private void StackBarCheckers(int count, Transform anchor, PlayerColor color)
    {
        if (anchor == null || count <= 0) return;
        float yStep = GetPrefabHeight(whiteCheckerPrefab) * 1.02f;
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = color == PlayerColor.White ? whiteCheckerPrefab : blackCheckerPrefab;
            GameObject checkerObj = Instantiate(prefab, anchor);
            ApplyCheckerVisuals(checkerObj, color);
            checkerObj.GetComponent<Checker>().color = color;
            checkerObj.transform.localPosition = new Vector3(0f, i * yStep, 0f);
            checkerObj.transform.localRotation = Quaternion.identity;
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
    public bool TryApplySingleVisualMove(Move move)
    {
        if (allPoints == null || allPoints.Length != 24) return false;
        if (move.From < 0 || move.From > BackgammonBoardLayout.BarEngineIndex) return false;
        if (move.To < 0 || move.To > 23) return false;

        GameObject movingChecker = PeekSourceCheckerForMove(move.From);
        if (movingChecker == null) return false;

        int toBoardIdx = BackgammonBoardLayout.EnginePointToBoardIndex(move.To);
        if (toBoardIdx < 0 || toBoardIdx >= allPoints.Length) return false;
        BoardPoint toPoint = allPoints[toBoardIdx];
        if (toPoint == null) return false;

        GameObject hitChecker = TryPeekSingleOpposingBlot(toPoint);

        if (hitChecker != null)
        {
            GameObject removedHit = toPoint.RemoveTopChecker();
            if (removedHit != hitChecker) return false;
            if (!TryStackCheckerOnBar(removedHit, barBlackAnchor)) return false;
        }

        if (!TryDetachCheckerFromSource(move.From, movingChecker)) return false;
        toPoint.AddChecker(movingChecker, animated: true);
        return true;
    }

    /// <summary>
    /// Best-effort visual reverse of a previously applied single move for undo animations.
    /// Falls back to full sync in caller when this returns false.
    /// </summary>
    public bool TryApplySingleVisualUndoMove(Move appliedMove)
    {
        if (appliedMove.To < 0 || appliedMove.To > 23) return false;
        if (appliedMove.From < 0 || appliedMove.From > BackgammonBoardLayout.BarEngineIndex) return false;

        Move reverse = new Move { From = appliedMove.To, To = appliedMove.From };
        GameObject movingChecker = PeekSourceCheckerForMove(reverse.From);
        if (movingChecker == null) return false;

        // Undo path only animates simple checker return. Hit/blot restoration still falls back to sync.
        if (!TryDetachCheckerFromSource(reverse.From, movingChecker)) return false;
        if (reverse.To == BackgammonBoardLayout.BarEngineIndex)
            return TryStackCheckerOnBar(movingChecker, barWhiteAnchor);

        int toBoardIdx = BackgammonBoardLayout.EnginePointToBoardIndex(reverse.To);
        if (toBoardIdx < 0 || toBoardIdx >= allPoints.Length) return false;
        BoardPoint toPoint = allPoints[toBoardIdx];
        if (toPoint == null) return false;
        toPoint.AddChecker(movingChecker, animated: true);
        return true;
    }

    private GameObject PeekSourceCheckerForMove(int engineFrom)
    {
        if (engineFrom == BackgammonBoardLayout.BarEngineIndex)
        {
            if (barWhiteAnchor == null || barWhiteAnchor.childCount == 0) return null;
            return barWhiteAnchor.GetChild(barWhiteAnchor.childCount - 1).gameObject;
        }

        int fromBoardIdx = BackgammonBoardLayout.EnginePointToBoardIndex(engineFrom);
        if (fromBoardIdx < 0 || fromBoardIdx >= allPoints.Length) return null;
        BoardPoint fromPoint = allPoints[fromBoardIdx];
        if (fromPoint == null || fromPoint.checkers.Count == 0) return null;
        return fromPoint.checkers[fromPoint.checkers.Count - 1];
    }

    private bool TryDetachCheckerFromSource(int engineFrom, GameObject expectedChecker)
    {
        if (expectedChecker == null) return false;
        if (engineFrom == BackgammonBoardLayout.BarEngineIndex)
        {
            if (barWhiteAnchor == null || barWhiteAnchor.childCount == 0) return false;
            Transform top = barWhiteAnchor.GetChild(barWhiteAnchor.childCount - 1);
            if (top == null || top.gameObject != expectedChecker) return false;
            top.SetParent(null, true);
            return true;
        }

        int fromBoardIdx = BackgammonBoardLayout.EnginePointToBoardIndex(engineFrom);
        if (fromBoardIdx < 0 || fromBoardIdx >= allPoints.Length) return false;
        BoardPoint fromPoint = allPoints[fromBoardIdx];
        if (fromPoint == null) return false;
        GameObject removed = fromPoint.RemoveTopChecker();
        return removed == expectedChecker;
    }

    private static GameObject TryPeekSingleOpposingBlot(BoardPoint point)
    {
        if (point == null || point.checkers.Count != 1) return null;
        GameObject top = point.checkers[0];
        if (top == null) return null;
        Checker checker = top.GetComponent<Checker>();
        if (checker == null || checker.color != PlayerColor.Black) return null;
        return top;
    }

    private bool TryStackCheckerOnBar(GameObject checkerObj, Transform anchor)
    {
        if (checkerObj == null || anchor == null) return false;
        int stackIndex = anchor.childCount;
        float yStep = Mathf.Max(0.001f, GetCheckerHeightForStack(checkerObj) * 1.02f);
        checkerObj.transform.SetParent(anchor, false);
        checkerObj.transform.localRotation = Quaternion.identity;
        checkerObj.transform.localPosition = new Vector3(0f, stackIndex * yStep, 0f);
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

    /// <summary>HUD "Change view" — rotates an optional pivot (assign in Inspector).</summary>
    public void SetBoardViewHorizontal(bool horizontal)
    {
        BackgammonBoardLayout.SetHorizontal(horizontal);
        if (boardViewPivot == null) return;
        boardViewPivot.localRotation = Quaternion.Euler(horizontal ? horizontalBoardEuler : verticalBoardEuler);
    }
}