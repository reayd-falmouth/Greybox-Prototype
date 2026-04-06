using System.Collections.Generic;
using Runtime.RMC.Backgammon.Core;
using UnityEngine;

/// <summary>
/// When movable checkers are active, raycasts the mouse and draws preview lines from the hovered top checker
/// to each distinct first-step <see cref="EngineCore.Move.To"/> for that origin.
/// </summary>
[RequireComponent(typeof(BoardManager))]
public class BackgammonMovableHoverInput : MonoBehaviour
{
    [SerializeField] private BackgammonGameController gameController;
    [SerializeField] private Camera rayCamera;
    [SerializeField] private LayerMask checkerLayers = ~0;
    [SerializeField] private float rayMaxDistance = 40f;
    [SerializeField] private int maxPreviewLines = 8;
    [SerializeField] private float lineWidth = 0.018f;
    [SerializeField] private Color lineColor = new Color(0.25f, 1f, 1f, 0.92f);
    private BoardManager _board;
    private readonly HashSet<int> _destScratch = new();
    private LineRenderer[] _lines;
    private Transform _linesRoot;

    private void Awake()
    {
        _board = GetComponent<BoardManager>();
        if (rayCamera == null)
            rayCamera = Camera.main;
        BuildLinePool();
    }

    private void BuildLinePool()
    {
        _linesRoot = new GameObject("MovableMovePreviewLines").transform;
        _linesRoot.SetParent(transform, false);
        _lines = new LineRenderer[maxPreviewLines];
        Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (lineShader == null) lineShader = Shader.Find("Unlit/Color");
        if (lineShader == null) lineShader = Shader.Find("Sprites/Default");

        for (int i = 0; i < maxPreviewLines; i++)
        {
            GameObject go = new GameObject($"MovePreviewLine_{i}");
            go.transform.SetParent(_linesRoot, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 2;
            if (lineShader != null)
            {
                lr.material = new Material(lineShader);
                lr.startColor = lineColor;
                lr.endColor = lineColor;
            }
            lr.enabled = false;
            _lines[i] = lr;
        }
    }

    private void Update()
    {
        if (gameController == null)
            gameController = FindFirstObjectByType<BackgammonGameController>();
        if (_board == null || gameController == null || rayCamera == null)
        {
            HideAllLines();
            _board?.SetMovableHoverRenderer(null);
            return;
        }

        if (!gameController.CanShowMovableCheckerInteraction())
        {
            HideAllLines();
            _board.SetMovableHoverRenderer(null);
            return;
        }

        Ray ray = rayCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, rayMaxDistance, checkerLayers, QueryTriggerInteraction.Ignore))
        {
            HideAllLines();
            _board.SetMovableHoverRenderer(null);
            return;
        }

        Checker ch = hit.collider.GetComponentInParent<Checker>();
        if (ch == null || !_board.IsTopLogicalP1Checker(ch))
        {
            HideAllLines();
            _board.SetMovableHoverRenderer(null);
            return;
        }

        if (!_board.TryGetEngineFromForChecker(ch, out int engineFrom))
        {
            HideAllLines();
            _board.SetMovableHoverRenderer(null);
            return;
        }

        BackgammonMovableDestinations.CollectDistinctFirstMoveTos(engineFrom, gameController.CurrentLegalTurns, _destScratch);
        if (_destScratch.Count == 0)
        {
            HideAllLines();
            _board.SetMovableHoverRenderer(null);
            return;
        }

        MeshRenderer mr = ch.GetComponentInChildren<MeshRenderer>();
        _board.SetMovableHoverRenderer(mr);

        Vector3 start = hit.collider.bounds.center;
        int lineIdx = 0;
        foreach (int engineTo in _destScratch)
        {
            if (lineIdx >= _lines.Length) break;
            if (!_board.TryGetWorldPositionForMoveDestination(engineTo, out Vector3 end))
                continue;

            LineRenderer lr = _lines[lineIdx++];
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            lr.enabled = true;
        }

        for (int i = lineIdx; i < _lines.Length; i++)
            _lines[i].enabled = false;
    }

    private void HideAllLines()
    {
        if (_lines == null) return;
        for (int i = 0; i < _lines.Length; i++)
        {
            if (_lines[i] != null)
                _lines[i].enabled = false;
        }
    }

    private void OnDisable()
    {
        HideAllLines();
        _board?.SetMovableHoverRenderer(null);
    }
}
