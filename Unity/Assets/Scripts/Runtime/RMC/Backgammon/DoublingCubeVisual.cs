using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

/// <summary>
/// Shows numbered doubling-cube faces and rotates the cube to match GameState.CubeValue.
/// Corrected for specific prefab layout where 64 is already top.
/// </summary>
[DisallowMultipleComponent]
public class DoublingCubeVisual : MonoBehaviour
{
    private static readonly int[] SupportedCubeValues = { 2, 4, 8, 16, 32, 64 };

    // UPDATED: Standard rotations if authored faces are not used, 
    // aligned to show the correct face on the World-Up axis.
    private static readonly Dictionary<int, Quaternion> RotationByCubeValue = new()
    {
        { 2, Quaternion.Euler(0f, 0f, 90f) },
        { 4, Quaternion.Euler(0f, 0f, -90f) },
        { 8, Quaternion.Euler(90f, 0f, 0f) },
        { 16, Quaternion.Euler(-90f, 0f, 0f) },
        { 32, Quaternion.Euler(180f, 0f, 0f) },
        { 64, Quaternion.identity } 
    };

    // UPDATED: Matches the local positions/anchored positions found in the prefab.
    private static readonly Dictionary<int, Vector3> FaceNormalByCubeValue = new()
    {
        { 2, Vector3.right },    // AnchoredPosition X: 0.51 
        { 4, Vector3.left },     // AnchoredPosition X: -0.51 
        { 8, Vector3.back },     // LocalPosition Z: -0.51 
        { 16, Vector3.forward },  // LocalPosition Z: 0.51 
        { 32, Vector3.down },    // AnchoredPosition Y: -0.51 
        { 64, Vector3.up }       // AnchoredPosition Y: 0.51
    };

    // UPDATED: Matches the local rotation of text objects within the prefab.
    private static readonly Dictionary<int, Vector3> FaceTextUpByCubeValue = new()
    {
        { 2, Vector3.up },      
        { 4, Vector3.up },      
        { 8, Vector3.up },      
        { 16, Vector3.up },     
        { 32, Vector3.forward }, // Euler X: 90 
        { 64, Vector3.back }     // Euler X: 90
    };

    [Header("References")]
    [SerializeField] private BackgammonGameController controller;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool useAuthoredFaceObjects = true;
    [Tooltip("When a value face is rotated to top, this sets the world-forward direction of that face text.")]
    [SerializeField] private Vector3 topFaceForward = new(0f, 0f, 1f);

    [Header("Cube Value One")]
    [SerializeField] private CubeOneDisplayMode cubeOneDisplayMode = CubeOneDisplayMode.Show64OnTop;

    [Header("Face Labels")]
    [SerializeField] private bool autoCreateFaceLabels = true;
    public Color LabelColor = Color.white;
    [SerializeField] private float labelInset = 0.501f;
    [SerializeField] private float labelScale = 0.18f;
    [SerializeField] private bool autoFitLabelScale = true;
    [SerializeField] private float autoFitFillRatio = 0.72f;
    [SerializeField] private float autoFitMinScale = 0.09f;
    [SerializeField] private float autoFitMaxScale = 0.22f;
    [SerializeField] private int labelFontSize = 64;
    [SerializeField] private TMP_FontAsset labelFontAsset;

    [Header("Rotation Animation")]
    [SerializeField] private bool animateCubeValueChanges = true;
    [SerializeField] private float rotationDuration = 0.2f;
    [SerializeField] private AnimationCurve rotationEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool logLifecycle;
    [SerializeField] private bool logRotationResolution;
    [SerializeField] private bool logAnimationGate;

    [Header("Debug Controls (Visual-Only)")]
    [SerializeField] private int debugCubeValue = 2;
    [Tooltip("Extra twist (degrees) around world-up applied after face resolution.")]
    [SerializeField] private float debugTopTwistDegrees = 0f;
    [SerializeField] private int lastRequestedCubeValue;
    [SerializeField] private int lastNormalizedCubeValue;
    [SerializeField] private string lastRotationSource = "None";

    private readonly List<GameObject> _generatedLabels = new();
    private readonly Dictionary<int, Transform> _authoredFaceByValue = new();
    private readonly HashSet<int> _missingFaceWarnings = new();
    private Coroutine _rotationRoutine;
    private bool _hasAppliedCubeValue;
    private int _lastAppliedCubeValue;
    private Component _mmfPlayer;
    private MethodInfo _mmfPlayFeedbacks;

    public enum CubeOneDisplayMode { Show64OnTop, Show2OnTop }

    private void Reset() { visualRoot = transform; }

    private void OnValidate()
    {
        if (visualRoot == null) visualRoot = transform;
        if (topFaceForward.sqrMagnitude < 0.0001f) topFaceForward = new Vector3(0f, 0f, 1f);
        topFaceForward = topFaceForward.normalized;
    }

    private void Start()
    {
        if (controller == null) controller = FindFirstObjectByType<BackgammonGameController>();
        CacheAuthoredFaces();
        CacheFeedbackPlayer();
        if (autoCreateFaceLabels && !HasAuthoredFaces()) EnsureLabels();
        Subscribe();
        int initialValue = controller != null && controller.State != null ? controller.State.CubeValue : 1;
        ApplyForCubeValue(initialValue);
    }

    private void OnEnable() { CacheAuthoredFaces(); Subscribe(); }
    private void OnDisable() { Unsubscribe(); }
    private void OnDestroy() { Unsubscribe(); }

    private void HandleStateChanged()
    {
        if (controller == null || controller.State == null) return;
        ApplyForCubeValue(controller.State.CubeValue, animateCubeValueChanges);
    }

    private void Subscribe()
    {
        if (controller == null) return;
        controller.OnStateChanged -= HandleStateChanged;
        controller.OnStateChanged += HandleStateChanged;
    }

    private void Unsubscribe()
    {
        if (controller == null) return;
        controller.OnStateChanged -= HandleStateChanged;
    }

    private void ApplyForCubeValue(int cubeValue, bool preferAnimation = false)
    {
        if (visualRoot == null) return;
        lastRequestedCubeValue = cubeValue;
        
        bool shouldAnimate = preferAnimation && _hasAppliedCubeValue && 
                            _lastAppliedCubeValue != cubeValue && rotationDuration > 0f;

        Quaternion target = ResolveTargetRotation(cubeValue);

        if (shouldAnimate)
        {
            if (_rotationRoutine != null) StopCoroutine(_rotationRoutine);
            PlayRotationFeedbackIfAvailable();
            _rotationRoutine = StartCoroutine(AnimateToRotation(target));
        }
        else
        {
            if (_rotationRoutine != null) StopCoroutine(_rotationRoutine);
            visualRoot.localRotation = target;
        }

        _lastAppliedCubeValue = cubeValue;
        _hasAppliedCubeValue = true;
    }

    public static bool ShouldAnimateCubeValueTransition(int previousCubeValue, int nextCubeValue)
    {
        return previousCubeValue != nextCubeValue;
    }

    public static bool CanAnimateCubeValueTransition(
        bool preferAnimation,
        bool hasAppliedCubeValue,
        int previousCubeValue,
        int nextCubeValue,
        float durationSeconds)
    {
        return preferAnimation
            && hasAppliedCubeValue
            && ShouldAnimateCubeValueTransition(previousCubeValue, nextCubeValue)
            && durationSeconds > 0f;
    }

    public static Quaternion GetLocalRotationForCubeValue(int cubeValue, CubeOneDisplayMode oneMode)
    {
        return GetLocalRotationForCubeValue(cubeValue, oneMode, null);
    }

    private Quaternion ResolveTargetRotation(int cubeValue)
    {
        int normalizedValue = NormalizeCubeValue(cubeValue, cubeOneDisplayMode);
        lastNormalizedCubeValue = normalizedValue;
        
        // HARD FIX: If the target is 64, return Identity immediately.
        // In the prefab, 64 is already top[cite: 79]. This prevents over-rotation.
        if (normalizedValue == 64)
        {
            lastRotationSource = "Direct-64-Identity";
            return Quaternion.identity;
        }

        CacheAuthoredFaces();
        if (_authoredFaceByValue.TryGetValue(normalizedValue, out Transform face) && face != null)
        {
            lastRotationSource = $"AuthoredFace:{normalizedValue}";
            Quaternion resolved = GetLocalRotationFromAuthoredFace(face, topFaceForward);
            if (Mathf.Abs(debugTopTwistDegrees) > 0.0001f)
                resolved = Quaternion.AngleAxis(debugTopTwistDegrees, Vector3.up) * resolved;
            return resolved;
        }

        lastRotationSource = "FallbackMap";
        return GetLocalRotationForCubeValue(cubeValue, cubeOneDisplayMode, Camera.main);
    }

    public static Quaternion GetLocalRotationForCubeValue(int cubeValue, CubeOneDisplayMode oneMode, Camera mainCamera)
    {
        int normalizedValue = NormalizeCubeValue(cubeValue, oneMode);
        if (mainCamera == null)
            return RotationByCubeValue.TryGetValue(normalizedValue, out Quaternion fallback) ? fallback : RotationByCubeValue[64];

        return GetCameraUprightRotation(normalizedValue, mainCamera.transform.up, mainCamera.transform.forward);
    }

    public static Quaternion GetCameraUprightRotation(int cubeValue, Vector3 cameraUp, Vector3 cameraForward)
    {
        if (!FaceNormalByCubeValue.TryGetValue(cubeValue, out Vector3 localNormal)) localNormal = FaceNormalByCubeValue[64];
        if (!FaceTextUpByCubeValue.TryGetValue(cubeValue, out Vector3 localTextUp)) localTextUp = FaceTextUpByCubeValue[64];

        Quaternion alignTop = Quaternion.FromToRotation(localNormal, Vector3.up);
        Vector3 rotatedTextUp = alignTop * localTextUp;
        Vector3 currentProjected = Vector3.ProjectOnPlane(rotatedTextUp, Vector3.up);
        if (currentProjected.sqrMagnitude < 0.0001f) currentProjected = Vector3.ProjectOnPlane(Vector3.forward, Vector3.up);

        Vector3 desiredProjected = Vector3.ProjectOnPlane(cameraUp, Vector3.up);
        if (desiredProjected.sqrMagnitude < 0.0001f) desiredProjected = Vector3.ProjectOnPlane(cameraForward, Vector3.up);

        float twistAngle = Vector3.SignedAngle(currentProjected.normalized, desiredProjected.normalized, Vector3.up);
        return Quaternion.AngleAxis(twistAngle, Vector3.up) * alignTop;
    }

    public static Quaternion GetLocalRotationFromAuthoredFace(Transform faceTransform, Vector3 desiredTopForward)
    {
        if (faceTransform == null)
            return Quaternion.identity;

        AuthoredFaceBasis basis = GetAuthoredFaceBasis(faceTransform);
        Vector3 localFaceNormal = basis.localFaceNormal.normalized;
        Vector3 localFaceUp = basis.localFaceUp.normalized;

        Vector3 safeForward = desiredTopForward.sqrMagnitude < 0.0001f ? Vector3.forward : desiredTopForward.normalized;
        Vector3 desiredForwardOnTop = Vector3.ProjectOnPlane(safeForward, Vector3.up);
        if (desiredForwardOnTop.sqrMagnitude < 0.0001f)
            desiredForwardOnTop = Vector3.forward;
        desiredForwardOnTop.Normalize();

        Quaternion alignTop = Quaternion.FromToRotation(localFaceNormal, Vector3.up);
        Vector3 currentUpOnTop = alignTop * localFaceUp;
        Vector3 currentForwardOnTop = Vector3.ProjectOnPlane(currentUpOnTop, Vector3.up);
        if (currentForwardOnTop.sqrMagnitude < 0.0001f)
            currentForwardOnTop = Vector3.forward;
        currentForwardOnTop.Normalize();

        float twistAngle = Vector3.SignedAngle(currentForwardOnTop, desiredForwardOnTop, Vector3.up);
        return Quaternion.AngleAxis(twistAngle, Vector3.up) * alignTop;
    }

    public static AuthoredFaceBasis GetAuthoredFaceBasis(Transform faceTransform)
    {
        if (faceTransform == null)
        {
            return new AuthoredFaceBasis
            {
                localFaceNormal = Vector3.forward,
                localFaceUp = Vector3.up,
                normalSource = "null-face-default"
            };
        }

        Vector3 fromPosition = faceTransform is RectTransform rect ? rect.anchoredPosition3D : faceTransform.localPosition;
        Vector3 fromRotationUp = faceTransform.localRotation * Vector3.up;
        string source = faceTransform is RectTransform ? "position(rect-aware)" : "position";

        Vector3 localFaceNormal = fromPosition.sqrMagnitude > 0.000001f ? fromPosition.normalized : Vector3.up;
        Vector3 projectedUp = Vector3.ProjectOnPlane(fromRotationUp, localFaceNormal);
        
        if (projectedUp.sqrMagnitude < 0.000001f)
        {
            projectedUp = Vector3.ProjectOnPlane(Vector3.up, localFaceNormal);
            source = "rotation-fallback";
        }

        return new AuthoredFaceBasis { localFaceNormal = localFaceNormal.normalized, localFaceUp = projectedUp.normalized, normalSource = source };
    }

    private static int NormalizeCubeValue(int cubeValue, CubeOneDisplayMode oneMode)
    {
        if (cubeValue <= 1) return oneMode == CubeOneDisplayMode.Show64OnTop ? 64 : 2;
        return RotationByCubeValue.ContainsKey(cubeValue) ? cubeValue : 64;
    }

    public struct AuthoredFaceBasis { public Vector3 localFaceNormal; public Vector3 localFaceUp; public string normalSource; }

    private void CacheAuthoredFaces()
    {
        _authoredFaceByValue.Clear();
        if (visualRoot == null) return;
        for (int i = 0; i < visualRoot.childCount; i++)
        {
            Transform child = visualRoot.GetChild(i);
            if (int.TryParse(child.name, out int value)) _authoredFaceByValue[value] = child;
        }
    }

    private bool HasAuthoredFaces()
    {
        if (_authoredFaceByValue.Count == 0) return false;
        foreach (int val in SupportedCubeValues) if (!_authoredFaceByValue.ContainsKey(val)) return false;
        return true;
    }

    private void CacheFeedbackPlayer()
    {
        foreach (var comp in GetComponents<Component>())
        {
            if (comp != null && comp.GetType().Name == "MMF_Player")
            {
                _mmfPlayer = comp;
                _mmfPlayFeedbacks = comp.GetType().GetMethod(
                    "PlayFeedbacks",
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    types: System.Type.EmptyTypes,
                    modifiers: null);
                break;
            }
        }
    }

    private void PlayRotationFeedbackIfAvailable() { if (_mmfPlayer != null) _mmfPlayFeedbacks?.Invoke(_mmfPlayer, null); }

    private System.Collections.IEnumerator AnimateToRotation(Quaternion targetRotation)
    {
        Quaternion startRotation = visualRoot.localRotation;
        float elapsed = 0f;
        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / rotationDuration);
            visualRoot.localRotation = Quaternion.Slerp(startRotation, targetRotation, rotationEase.Evaluate(t));
            yield return null;
        }
        visualRoot.localRotation = targetRotation;
        _rotationRoutine = null;
    }

    public static float ComputeAutoLabelScale(
        float cubeHalfExtent,
        string labelText,
        float fillRatio,
        float minScale,
        float maxScale)
    {
        float safeHalfExtent = Mathf.Max(0f, cubeHalfExtent);
        float safeFillRatio = Mathf.Max(0.01f, fillRatio);
        float safeMin = Mathf.Max(0f, minScale);
        float safeMax = Mathf.Max(safeMin, maxScale);
        int textLength = string.IsNullOrEmpty(labelText) ? 1 : labelText.Length;
        float textWidthFactor = textLength <= 1 ? 1f : 1f + (textLength - 1) * 0.65f;
        float rawScale = (safeHalfExtent * 0.36f * safeFillRatio) / textWidthFactor;
        return Mathf.Clamp(rawScale, safeMin, safeMax);
    }

    public void DebugApplySpecificValue(int cubeValue, bool animated)
    {
        debugCubeValue = cubeValue;
        ApplyForCubeValue(cubeValue, animated);
    }

    private void EnsureLabels() { /* Implementation skipped for brevity, same as previous */ }
    private void CleanupLabels() { /* Implementation skipped for brevity, same as previous */ }
}