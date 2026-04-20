using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Center-screen HUD toasts driven by <see cref="BackgammonGameController.OnDiceFeedbackEvent"/> presets.
/// </summary>
[DisallowMultipleComponent]
public class ScreenNotificationController : MonoBehaviour
{
    [Serializable]
    public class DiceFeedbackNotificationEntry
    {
        public DiceFeedbackEventType eventType;
        public string message = "";
        [Tooltip("0 uses global Visible Duration.")]
        public float displayDurationSeconds;
        [Tooltip("0 uses Default Font Size. Otherwise pixel size for UI Toolkit label.")]
        public int fontSize;
        [Tooltip("Offset from center in pixels. (0,0) uses Default Label Offset.")]
        public Vector2 labelOffsetPixels;
    }

    public readonly struct NotificationPresetResolved
    {
        public readonly string Message;
        public readonly float DisplayDurationSeconds;
        public readonly int ResolvedFontSize;
        public readonly Vector2 ResolvedLabelOffsetPixels;

        public NotificationPresetResolved(
            string message,
            float displayDurationSeconds,
            int resolvedFontSize,
            Vector2 resolvedLabelOffsetPixels)
        {
            Message = message;
            DisplayDurationSeconds = displayDurationSeconds;
            ResolvedFontSize = resolvedFontSize;
            ResolvedLabelOffsetPixels = resolvedLabelOffsetPixels;
        }
    }

    [SerializeField] private BackgammonGameController gameController;
    [SerializeField] private UIDocument uiDocument;

    [SerializeField]
    private List<DiceFeedbackNotificationEntry> presets = new List<DiceFeedbackNotificationEntry>
    {
        new DiceFeedbackNotificationEntry
        {
            eventType = DiceFeedbackEventType.OpeningRollTieAutodouble,
            message = "AutoDouble!",
            displayDurationSeconds = 0f,
            fontSize = 0,
            labelOffsetPixels = Vector2.zero
        }
    };

    [Header("Default label style")]
    [Tooltip("Used when a preset row has Font Size = 0.")]
    [SerializeField] private int defaultFontSize = 72;
    [Tooltip("Used when a preset row has Label Offset = (0,0).")]
    [SerializeField] private Vector2 defaultLabelOffsetPixels;

    [Header("Timing")]
    [SerializeField] private float visibleDuration = 1.25f;
    [SerializeField] private float fadeInSeconds = 0.2f;
    [SerializeField] private float fadeOutSeconds = 0.35f;

    [Header("Optional Feel")]
    [Tooltip("Optional MMF_Player; invoked on show via reflection.")]
    [SerializeField] private Component notificationFeedbackPlayer;

    [Header("Debug")]
    [SerializeField] private string debugPreviewText = "Preview";
    [SerializeField] private bool enableVerboseLogs;

    [Header("Preset authoring")]
    [Tooltip("Event key used when saving the debug fields into the Presets list.")]
    [SerializeField] private DiceFeedbackEventType debugPresetEventType = DiceFeedbackEventType.OpeningRollTieAutodouble;
    [Tooltip("0 uses Default Font Size.")]
    [SerializeField] private int debugFontSize;
    [SerializeField] private Vector2 debugLabelOffsetPixels;

    private VisualElement _overlay;
    private Label _label;
    private Coroutine _activeRoutine;

    private void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();
        if (gameController == null)
            gameController = FindFirstObjectByType<BackgammonGameController>();
    }

    private void Start()
    {
        TryBindUi();
    }

    private void OnEnable()
    {
        if (gameController == null)
            gameController = FindFirstObjectByType<BackgammonGameController>();
        if (gameController != null)
        {
            gameController.OnDiceFeedbackEvent -= HandleDiceFeedback;
            gameController.OnDiceFeedbackEvent += HandleDiceFeedback;
        }
    }

    private void OnDisable()
    {
        if (gameController != null)
            gameController.OnDiceFeedbackEvent -= HandleDiceFeedback;
        if (_activeRoutine != null)
        {
            StopCoroutine(_activeRoutine);
            _activeRoutine = null;
        }
    }

    private void TryBindUi()
    {
        if (_overlay != null && _label != null) return;
        if (uiDocument == null)
        {
            Debug.LogError("[Backgammon][Notify] UIDocument is not assigned.");
            return;
        }

        VisualElement root = uiDocument.rootVisualElement;
        if (root == null)
        {
            if (enableVerboseLogs)
                Debug.LogWarning("[Backgammon][Notify] rootVisualElement not ready yet.");
            return;
        }

        _overlay = root.Q<VisualElement>("CenterNotificationOverlay");
        _label = root.Q<Label>("CenterNotificationLabel");
        if (_overlay == null || _label == null)
        {
            Debug.LogError(
                "[Backgammon][Notify] Missing CenterNotificationOverlay or CenterNotificationLabel in HUD UXML.");
            return;
        }

        _overlay.style.display = DisplayStyle.None;
        _overlay.style.opacity = 0f;
    }

    private void HandleDiceFeedback(DiceFeedbackEventData data)
    {
        if (!TryResolvePreset(
                presets,
                data.EventType,
                defaultFontSize,
                defaultLabelOffsetPixels,
                out NotificationPresetResolved resolved))
        {
            if (enableVerboseLogs)
                Debug.LogWarning($"[Backgammon][Notify] No preset for event={data.EventType}");
            return;
        }

        float hold = resolved.DisplayDurationSeconds > 0f ? resolved.DisplayDurationSeconds : visibleDuration;
        if (enableVerboseLogs)
            Debug.Log(
                $"[Backgammon][Notify] event={data.EventType} cubeAfter={data.CubeValueAfter} message=\"{resolved.Message}\" hold={hold:F2}s font={resolved.ResolvedFontSize} offset={resolved.ResolvedLabelOffsetPixels}");
        ShowNotificationInternal(resolved, hold);
    }

    /// <summary>Inspector / editor: same path as live events (fades + optional MMF).</summary>
    public void PlayDebugPreview()
    {
        if (enableVerboseLogs)
            Debug.Log($"[Backgammon][Notify] Preview authoring event={debugPresetEventType}");
        string msg = string.IsNullOrEmpty(debugPreviewText) ? "Preview" : debugPreviewText;
        int font = debugFontSize > 0 ? debugFontSize : defaultFontSize;
        Vector2 offset = debugLabelOffsetPixels;
        var resolved = new NotificationPresetResolved(msg, visibleDuration, font, offset);
        ShowNotificationInternal(resolved, visibleDuration);
    }

    private void ShowNotificationInternal(NotificationPresetResolved style, float holdDuration)
    {
        TryBindUi();
        if (_overlay == null || _label == null)
        {
            Debug.LogError("[Backgammon][Notify] Cannot show notification: UI not bound.");
            return;
        }

        if (_activeRoutine != null)
        {
            StopCoroutine(_activeRoutine);
            _activeRoutine = null;
        }

        ResetLabelPresentation();
        _label.text = style.Message;
        ApplyLabelPresentation(style.ResolvedFontSize, style.ResolvedLabelOffsetPixels);
        Debug.Log(
            $"[Backgammon][Notify] Show message=\"{style.Message}\" hold={holdDuration:F2}s fadeIn={fadeInSeconds:F2}s fadeOut={fadeOutSeconds:F2}s font={style.ResolvedFontSize} offset={style.ResolvedLabelOffsetPixels}");
        TryPlayNotificationMmf();
        _activeRoutine = StartCoroutine(NotificationRoutine(holdDuration));
    }

    private void ApplyLabelPresentation(int resolvedFontSize, Vector2 resolvedOffsetPixels)
    {
        _label.style.fontSize = new StyleLength(new Length(resolvedFontSize, LengthUnit.Pixel));
        _label.style.translate = new Translate(resolvedOffsetPixels.x, resolvedOffsetPixels.y, 0f);
    }

    private void ResetLabelPresentation()
    {
        _label.style.fontSize = new StyleLength(StyleKeyword.Null);
        _label.style.translate = new Translate(0f, 0f, 0f);
    }

    private IEnumerator NotificationRoutine(float holdDuration)
    {
        _overlay.style.display = DisplayStyle.Flex;
        float t = 0f;
        while (t < fadeInSeconds)
        {
            t += Time.unscaledDeltaTime;
            float a = fadeInSeconds <= 0.001f ? 1f : Mathf.Clamp01(t / fadeInSeconds);
            _overlay.style.opacity = a;
            yield return null;
        }

        _overlay.style.opacity = 1f;
        yield return new WaitForSecondsRealtime(holdDuration);

        t = 0f;
        while (t < fadeOutSeconds)
        {
            t += Time.unscaledDeltaTime;
            float a = fadeOutSeconds <= 0.001f ? 0f : 1f - Mathf.Clamp01(t / fadeOutSeconds);
            _overlay.style.opacity = a;
            yield return null;
        }

        _overlay.style.opacity = 0f;
        _overlay.style.display = DisplayStyle.None;
        ResetLabelPresentation();
        _activeRoutine = null;
    }

    private void TryPlayNotificationMmf()
    {
        Component playable = ResolvePlayableTarget(notificationFeedbackPlayer);
        if (playable == null)
        {
            if (enableVerboseLogs && notificationFeedbackPlayer != null)
                Debug.LogWarning("[Backgammon][Notify] notificationFeedbackPlayer has no PlayFeedbacks.");
            return;
        }

        if (!TryInvokePlayFeedbacks(playable))
            Debug.LogWarning($"[Backgammon][Notify] Failed to play MMF on {playable.name}.");
    }

    private static Component ResolvePlayableTarget(Component candidate)
    {
        if (candidate == null) return null;
        if (HasPlayFeedbacksMethod(candidate)) return candidate;
        return FindPlayableOnOrUnder(candidate.transform);
    }

    private static Component FindPlayableOnOrUnder(Transform root)
    {
        if (root == null) return null;
        Component[] components = root.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (HasPlayFeedbacksMethod(components[i]))
                return components[i];
        }

        return null;
    }

    private static bool HasPlayFeedbacksMethod(Component candidate)
    {
        if (candidate == null) return false;
        Type t = candidate.GetType();
        return t.GetMethod("PlayFeedbacks", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null) != null
               || t.GetMethod("PlayFeedbacks", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Vector3) }, null) != null
               || t.GetMethod("PlayFeedbacks", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Vector3), typeof(float) }, null) != null;
    }

    private static bool TryInvokePlayFeedbacks(Component target)
    {
        Type t = target.GetType();
        MethodInfo m0 = t.GetMethod("PlayFeedbacks", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
        if (m0 != null)
        {
            try
            {
                m0.Invoke(target, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Backgammon][Notify] PlayFeedbacks threw: {ex.Message}");
                return false;
            }
        }

        MethodInfo m1 = t.GetMethod("PlayFeedbacks", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Vector3) }, null);
        if (m1 != null)
        {
            try
            {
                m1.Invoke(target, new object[] { Vector3.zero });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Backgammon][Notify] PlayFeedbacks(Vector3) threw: {ex.Message}");
                return false;
            }
        }

        MethodInfo m2 = t.GetMethod("PlayFeedbacks", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Vector3), typeof(float) }, null);
        if (m2 != null)
        {
            try
            {
                m2.Invoke(target, new object[] { Vector3.zero, 1f });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Backgammon][Notify] PlayFeedbacks(Vector3,float) threw: {ex.Message}");
                return false;
            }
        }

        return false;
    }

    /// <summary>Resolves preset including font and offset (uses defaults when row uses 0 / zero vector).</summary>
    public static bool TryResolvePreset(
        IReadOnlyList<DiceFeedbackNotificationEntry> entries,
        DiceFeedbackEventType eventType,
        int defaultFontSize,
        Vector2 defaultLabelOffsetPixels,
        out NotificationPresetResolved resolved)
    {
        resolved = default;
        if (entries == null) return false;
        for (int i = 0; i < entries.Count; i++)
        {
            DiceFeedbackNotificationEntry e = entries[i];
            if (e == null || e.eventType != eventType || string.IsNullOrEmpty(e.message))
                continue;

            int font = e.fontSize > 0 ? e.fontSize : defaultFontSize;
            Vector2 offset = e.labelOffsetPixels.sqrMagnitude > 1e-6f ? e.labelOffsetPixels : defaultLabelOffsetPixels;
            resolved = new NotificationPresetResolved(e.message, e.displayDurationSeconds, font, offset);
            return true;
        }

        return false;
    }

    /// <summary>Resolves preset message and per-row duration (0 = caller should use global). Uses 72 / zero for default font/offset in resolution.</summary>
    public static bool TryResolveMessage(
        IReadOnlyList<DiceFeedbackNotificationEntry> entries,
        DiceFeedbackEventType eventType,
        out string message,
        out float displayDurationSeconds)
    {
        message = null;
        displayDurationSeconds = 0f;
        if (!TryResolvePreset(entries, eventType, 72, Vector2.zero, out NotificationPresetResolved r))
            return false;
        message = r.Message;
        displayDurationSeconds = r.DisplayDurationSeconds;
        return true;
    }
}
