using System.Collections.Generic;
using Runtime.RMC._MyProject_.Core;
using UnityEngine;

public class BoardPoint : MonoBehaviour
{
    public int pointIndex;
    public List<GameObject> checkers = new List<GameObject>();

    [Header("Configuration")]
    public bool isBottomRow;
    public Vector3 inwardDirection;
    public Vector3 stackOffset;

    [Header("Visual Settings")]
    public float checkerThickness = 0.05f;
    public float checkerDiameter = 0.45f;
    public int maxBaseStack = 5;
    public float wallMargin = 0.05f;

    public MeshRenderer pointRenderer;
    public Color normalColor;

    [Header("Highlight")]
    [SerializeField] private Color highlightTint = new Color(0.2f, 0.8f, 0.3f, 1f);
    private bool _highlighted;

    /// <summary>
    /// Now handles all internal coordinate math based on its orientation.
    /// </summary>
    public void Initialize(int index, bool bottom, Color col, float thickness, float diameter)
    {
        pointIndex = index;
        isBottomRow = bottom;
        normalColor = col;
        checkerThickness = thickness;
        checkerDiameter = diameter;

        // Logic: Bottom row flows Forward (+Z), Top flows Back (-Z)
        inwardDirection = isBottomRow ? Vector3.forward : Vector3.back;
        
        // Calculate the starting offset (how far the first checker sits from the wall)
        float radius = checkerDiameter / 2f;
        stackOffset = inwardDirection * (radius + wallMargin);

        ApplyColor(normalColor);
    }

    public Vector3 GetNextStackPosition() => GetPositionForIndex(checkers.Count);

    public Vector3 GetPositionForIndex(int index)
    {
        int floorIndex = index % maxBaseStack;
        int verticalLayer = index / maxBaseStack;

        // Position = Point Origin + Initial Wall Offset + (Horizontal Stack * Width) + (Vertical Stack * Height)
        Vector3 pos = transform.position + stackOffset;
        pos += inwardDirection * (floorIndex * (checkerDiameter * 1.02f));
        pos += Vector3.up * (verticalLayer * checkerThickness);

        return pos;
    }

    /// <param name="animated">Pass false when spawning from <c>Instantiate</c> at world origin (board sync / initial layout).</param>
    public void AddChecker(GameObject checkerObj, bool animated = true)
    {
        Checker checker = checkerObj.GetComponent<Checker>();
        Vector3 targetPos = GetNextStackPosition();

        checkers.Add(checkerObj);
        checker.MoveToPosition(targetPos, transform, animated);
    }

    public GameObject RemoveTopChecker()
    {
        if (checkers.Count == 0) return null;
        GameObject top = checkers[checkers.Count - 1];
        checkers.RemoveAt(checkers.Count - 1);
        return top;
    }

    public PlayerColor GetPointOwner() => (checkers.Count == 0) ? PlayerColor.None : checkers[0].GetComponent<Checker>().color;

    public bool CanLand(PlayerColor movingColor) => (checkers.Count <= 1) || GetPointOwner() == movingColor;

    public void AddTriangleMesh(bool bottom, float worldWidth, float worldHeight, Material pointMat)
    {
        // Capture the MeshFilter safely
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null) 
        {
            mf = gameObject.AddComponent<MeshFilter>();
        }

        // Capture the MeshRenderer safely
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null)
        {
            mr = gameObject.AddComponent<MeshRenderer>();
        }

        Mesh mesh = new Mesh();
        mesh.name = "PointTriangleMesh"; // Good practice for debugging

        float direction = bottom ? 1 : -1;
        float localW = worldWidth / transform.lossyScale.x;
        float localH = worldHeight / transform.lossyScale.z;

        mesh.vertices = new Vector3[] {
            new Vector3(-localW / 2, 0, 0), 
            new Vector3(localW / 2, 0, 0), 
            new Vector3(0, 0, localH * direction)
        };
    
        mesh.triangles = new int[] { 0, 1, 2, 0, 2, 1 };
        mesh.normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up };
    
        // Assign properties
        mf.sharedMesh = mesh; // Use sharedMesh for procedurally generated geometry in editor
        mr.material = pointMat;
        pointRenderer = mr;
    
        ApplyColor(normalColor);
    }

    private void ApplyColor(Color col)
    {
        if (pointRenderer == null) return;
        MaterialPropertyBlock props = new MaterialPropertyBlock();
        props.SetColor("_BaseColor", col);
        pointRenderer.SetPropertyBlock(props);
    }

    public void SetHighlighted(bool on, Color? overrideTint = null)
    {
        _highlighted = on;
        if (on)
            ApplyColor(overrideTint ?? highlightTint);
        else
            ApplyColor(normalColor);
    }

    public bool IsHighlighted => _highlighted;
}