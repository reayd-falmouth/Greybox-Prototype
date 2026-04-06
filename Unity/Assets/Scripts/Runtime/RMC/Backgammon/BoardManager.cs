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

    [Header("Movable highlight (human turn)")]
    [Tooltip("Multiplies HDR emission when a checker can start a legal turn.")]
    [SerializeField] private float movableEmissionMultiplier = 1.35f;

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

    private void ApplyCheckerVisuals(GameObject obj, PlayerColor color, bool movableHighlight = false)
    {
        MeshRenderer mr = obj.GetComponentInChildren<MeshRenderer>();
        if (mr == null) return;

        MaterialPropertyBlock props = new MaterialPropertyBlock();
        Color baseCol = (color == PlayerColor.White) ? whiteBaseColor : blackBaseColor;
        Color emissCol = (color == PlayerColor.White) ? whiteEmissionColor : blackEmissionColor;
        float intensity = (color == PlayerColor.White) ? whiteEmissionIntensity : blackEmissionIntensity;

        Color emission = emissCol * Mathf.Pow(2f, intensity);
        if (movableHighlight)
            emission *= movableEmissionMultiplier;

        props.SetColor("_BaseColor", baseCol);
        props.SetColor("_EmissionColor", emission);
        mr.SetPropertyBlock(props);
    }

    /// <summary>Reset all checker materials to baseline (no movable boost).</summary>
    public void ClearMovableCheckerHighlights()
    {
        for (int i = 0; i < allPoints.Length; i++)
        {
            if (allPoints[i] == null) continue;
            foreach (GameObject go in allPoints[i].checkers)
            {
                if (go == null) continue;
                Checker c = go.GetComponent<Checker>();
                if (c != null) ApplyCheckerVisuals(go, c.color, false);
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
            if (ch != null) ApplyCheckerVisuals(go, ch.color, false);
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
                if (barWhiteAnchor == null) continue;
                for (int c = 0; c < barWhiteAnchor.childCount; c++)
                {
                    GameObject go = barWhiteAnchor.GetChild(c).gameObject;
                    Checker ch = go.GetComponent<Checker>();
                    if (ch != null) ApplyCheckerVisuals(go, ch.color, true);
                }
                continue;
            }

            if (from < 0 || from > 23) continue;

            int boardIdx = BackgammonBoardLayout.EnginePointToBoardIndex(from);
            if (boardIdx < 0 || boardIdx >= allPoints.Length || allPoints[boardIdx] == null) continue;

            BoardPoint bp = allPoints[boardIdx];
            foreach (GameObject go in bp.checkers)
            {
                if (go == null) continue;
                Checker checker = go.GetComponent<Checker>();
                if (checker != null && checker.color == PlayerColor.White)
                    ApplyCheckerVisuals(go, checker.color, true);
            }
        }
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