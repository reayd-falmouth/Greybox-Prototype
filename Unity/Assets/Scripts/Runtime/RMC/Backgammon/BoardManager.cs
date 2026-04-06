using System;
using System.Collections.Generic;
using EngineCore;
using Runtime.RMC._MyProject_.Core;
using Runtime.RMC.Backgammon.Core;
using UnityEngine;

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

    [Header("Movable highlight (neon pulse)")]
    [SerializeField] private Color movableNeonBaseColor = new Color(0.2f, 0.85f, 1f, 1f);
    [SerializeField] private Color movableNeonEmissionColor = new Color(0.3f, 0.85f, 1f, 1f);
    [Tooltip("HDR emission exponent, same convention as whiteEmissionIntensity.")]
    [SerializeField] private float movableNeonEmissionIntensity = 2.5f;
    [SerializeField] private float movablePulseFrequency = 1.15f;
    [SerializeField] private float movablePulseEmissionMinScale = 0.55f;
    [SerializeField] private float movablePulseEmissionMaxScale = 1.45f;
    [SerializeField] private float movableHoverEmissionBoost = 1.45f;

    [Header("Move preview (hover lines)")]
    [SerializeField] private bool enableMovePreviewLines = true;
    [SerializeField] private BackgammonGameController gameController;
    [SerializeField] private Camera rayCamera;
    [Tooltip("Raycast hits are sorted by distance; the first hit with a Checker wins. If the table blocks, set this mask to the Checker layer only.")]
    [SerializeField] private LayerMask movePreviewRaycastLayers = ~0;
    [SerializeField] private float movePreviewRayDistance = 80f;
    [SerializeField] private int movePreviewMaxLines = 8;
    [SerializeField] private float movePreviewLineWidth = 0.08f;
    [SerializeField] private Color movePreviewLineColor = new Color(0f, 1f, 1f, 1f);
    [SerializeField] private float movePreviewHeightOffset = 0.05f;
    [Tooltip("Optional world position for bear-off (-1) line ends. If unset, a point near P1 home edge is used.")]
    [SerializeField] private Transform bearOffLineEnd;

    private MeshRenderer _movableHoverRenderer;

    private LineRenderer[] _movePreviewLines;
    private Transform _movePreviewRoot;
    private readonly HashSet<int> _moveDestScratch = new();

    private readonly List<MovablePulseTarget> _movablePulseTargets = new();

    private struct MovablePulseTarget
    {
        public MeshRenderer Renderer;
    }

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
        _movePreviewRoot = new GameObject("MovableMovePreviewLines").transform;
        _movePreviewRoot.SetParent(transform, false);
        _movePreviewLines = new LineRenderer[nLines];
        Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (lineShader == null) lineShader = Shader.Find("Unlit/Color");
        if (lineShader == null) lineShader = Shader.Find("Sprites/Default");

        for (int i = 0; i < nLines; i++)
        {
            GameObject go = new GameObject($"MovePreviewLine_{i}");
            go.transform.SetParent(_movePreviewRoot, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = movePreviewLineWidth;
            lr.endWidth = movePreviewLineWidth;
            lr.numCapVertices = 6;
            lr.numCornerVertices = 3;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            if (lineShader != null)
            {
                Material mat = new Material(lineShader);
                ApplyMovePreviewLineMaterialColors(mat, movePreviewLineColor);
                lr.material = mat;
            }

            lr.startColor = movePreviewLineColor;
            lr.endColor = movePreviewLineColor;
            lr.enabled = false;
            _movePreviewLines[i] = lr;
        }
    }

    private static void ApplyMovePreviewLineMaterialColors(Material mat, Color c)
    {
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
    }

    private void Update()
    {
        if (enableMovePreviewLines)
            UpdateMovePreviewLines();
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
            SetMovableHoverRenderer(null);
            return;
        }

        if (!gameController.CanShowMovableCheckerInteraction())
        {
            HideMovePreviewLines();
            SetMovableHoverRenderer(null);
            return;
        }

        Ray ray = rayCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, movePreviewRayDistance, movePreviewRaycastLayers, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            HideMovePreviewLines();
            SetMovableHoverRenderer(null);
            return;
        }

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Checker ch = null;
        RaycastHit checkerHit = default;
        bool found = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Checker c = hits[i].collider.GetComponentInParent<Checker>();
            if (c == null) continue;
            ch = c;
            checkerHit = hits[i];
            found = true;
            break;
        }

        if (!found || ch == null)
        {
            HideMovePreviewLines();
            SetMovableHoverRenderer(null);
            return;
        }

        if (!IsTopLogicalP1Checker(ch))
        {
            HideMovePreviewLines();
            SetMovableHoverRenderer(null);
            return;
        }

        if (!TryGetEngineFromForChecker(ch, out int engineFrom))
        {
            HideMovePreviewLines();
            SetMovableHoverRenderer(null);
            return;
        }

        BackgammonMovableDestinations.CollectDistinctFirstMoveTos(engineFrom, gameController.CurrentLegalTurns, _moveDestScratch);
        if (_moveDestScratch.Count == 0)
        {
            HideMovePreviewLines();
            SetMovableHoverRenderer(null);
            return;
        }

        MeshRenderer mr = ch.GetComponentInChildren<MeshRenderer>();
        SetMovableHoverRenderer(mr);

        Vector3 up = Vector3.up * movePreviewHeightOffset;
        Vector3 start = checkerHit.collider.bounds.center + up;
        int lineIdx = 0;
        foreach (int engineTo in _moveDestScratch)
        {
            if (lineIdx >= _movePreviewLines.Length) break;
            if (!TryGetWorldPositionForMoveDestination(engineTo, out Vector3 end))
                continue;
            end += up;

            LineRenderer lr = _movePreviewLines[lineIdx++];
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            lr.enabled = true;
        }

        for (int i = lineIdx; i < _movePreviewLines.Length; i++)
            _movePreviewLines[i].enabled = false;
    }

    private void HideMovePreviewLines()
    {
        if (_movePreviewLines == null) return;
        for (int i = 0; i < _movePreviewLines.Length; i++)
        {
            if (_movePreviewLines[i] != null)
                _movePreviewLines[i].enabled = false;
        }
    }

    private void OnDisable()
    {
        HideMovePreviewLines();
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

    /// <summary>URP/Built-in Lit often mirror albedo in <c>_BaseColor</c> and <c>_Color</c>; set both so MPB fully overrides tint.</summary>
    private static void SetAlbedoAndEmission(MaterialPropertyBlock props, Color baseCol, Color emission)
    {
        props.SetColor("_BaseColor", baseCol);
        props.SetColor("_Color", baseCol);
        props.SetColor("_EmissionColor", emission);
    }

    private static void ApplyPropertyBlock(MeshRenderer mr, MaterialPropertyBlock props)
    {
        if (mr == null) return;
        var mats = mr.sharedMaterials;
        int n = mats != null ? mats.Length : 0;
        if (n <= 1)
            mr.SetPropertyBlock(props);
        else
        {
            for (int mi = 0; mi < n; mi++)
                mr.SetPropertyBlock(props, mi);
        }
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

        SetAlbedoAndEmission(props, baseCol, emission);
        ApplyPropertyBlock(mr, props);
    }

    private static float ComputePulseScale(float time, float frequency, float minS, float maxS)
    {
        float w = Mathf.Sin(time * (Mathf.PI * 2f) * frequency);
        return Mathf.Lerp(minS, maxS, 0.5f + 0.5f * w);
    }

    private void RefreshMovablePulseVisuals()
    {
        if (_movablePulseTargets.Count == 0) return;
        float pulse = ComputePulseScale(Time.time, movablePulseFrequency, movablePulseEmissionMinScale, movablePulseEmissionMaxScale);
        Color baseEmission = movableNeonEmissionColor * (Mathf.Pow(2f, movableNeonEmissionIntensity) * pulse);

        for (int i = 0; i < _movablePulseTargets.Count; i++)
        {
            MeshRenderer mr = _movablePulseTargets[i].Renderer;
            if (mr == null) continue;
            Color emission = baseEmission;
            if (mr == _movableHoverRenderer)
                emission *= movableHoverEmissionBoost;
            var props = new MaterialPropertyBlock();
            SetAlbedoAndEmission(props, movableNeonBaseColor, emission);
            ApplyPropertyBlock(mr, props);
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

    /// <summary>Reset all checker materials to baseline (no movable neon).</summary>
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

    public void ClearAllPointHighlights()
    {
        for (int i = 0; i < allPoints.Length; i++)
            allPoints[i]?.SetHighlighted(false);
    }

    /// <summary>HUD "Change view" — rotates an optional pivot (assign in Inspector).</summary>
    public void SetBoardViewHorizontal(bool horizontal)
    {
        if (boardViewPivot == null) return;
        boardViewPivot.localRotation = Quaternion.Euler(horizontal ? horizontalBoardEuler : verticalBoardEuler);
    }
}