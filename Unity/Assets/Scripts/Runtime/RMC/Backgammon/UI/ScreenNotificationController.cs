using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        [Tooltip("Optional audio clip override. If null, uses Default Notification Sound.")]
        public AudioClip audioClipOverride;
    }

    public readonly struct NotificationPresetResolved
    {
        public readonly string Message;
        public readonly float DisplayDurationSeconds;
        public readonly int ResolvedFontSize;
        public readonly Vector2 ResolvedLabelOffsetPixels;
        public readonly AudioClip ResolvedAudioClip;

        public NotificationPresetResolved(
            string message,
            float displayDurationSeconds,
            int resolvedFontSize,
            Vector2 resolvedLabelOffsetPixels,
            AudioClip resolvedAudioClip)
        {
            Message = message;
            DisplayDurationSeconds = displayDurationSeconds;
            ResolvedFontSize = resolvedFontSize;
            ResolvedLabelOffsetPixels = resolvedLabelOffsetPixels;
            ResolvedAudioClip = resolvedAudioClip;
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

    /// <summary>Total duration for event queue blocking (fadeIn + visible + fadeOut)</summary>
    public float TotalNotificationDuration => fadeInSeconds + visibleDuration + fadeOutSeconds;

    [Header("Sound Effects")]
    [Tooltip("Default sound effect played when a preset has no audioClipOverride.")]
    [SerializeField] private AudioClip defaultNotificationSound;

    [Header("Optional Feel")]
    [Tooltip("Optional MMF_Player; invoked on show via reflection. Should contain an MMF_AudioSource feedback for sound playback.")]
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
    [Tooltip("Optional audio clip override for preset authoring.")]
    [SerializeField] private AudioClip debugAudioClip;

    [Header("Opening roll winner messages")]
    [SerializeField] private string openingRollPlayerWinsMessage = "Your go";
    [SerializeField] private string openingRollAiWinsMessage = "AI goes first";
    [Header("Game end messages")]
    [SerializeField] private string gameEndWinMessage = "You win!";
    [SerializeField] private string gameEndLoseMessage = "Game over";

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
            gameController.OnScreenNotificationEvent -= HandleQueuedScreenNotification;
            gameController.OnScreenNotificationEvent += HandleQueuedScreenNotification;
        }
    }

    private void OnDisable()
    {
        if (gameController != null)
        {
            gameController.OnDiceFeedbackEvent -= HandleDiceFeedback;
            gameController.OnScreenNotificationEvent -= HandleQueuedScreenNotification;
        }
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
        if (IsQueueOwnedNotificationEvent(data.EventType))
            return;

        if (data.EventType == DiceFeedbackEventType.OpeningRollWinnerResolved)
        {
            string winnerMessage = ResolveOpeningRollWinnerMessage(
                data.OpeningRollWinnerPlayerIndex,
                openingRollPlayerWinsMessage,
                openingRollAiWinsMessage);
            var winnerResolved = new NotificationPresetResolved(
                winnerMessage,
                visibleDuration,
                defaultFontSize,
                defaultLabelOffsetPixels,
                defaultNotificationSound);
            if (enableVerboseLogs)
                Debug.Log(
                    $"[Backgammon][Notify] opening-winner winnerIndex={data.OpeningRollWinnerPlayerIndex} message=\"{winnerMessage}\"");
            ShowNotificationInternal(winnerResolved, visibleDuration);
            return;
        }

        if (!TryResolvePreset(
                presets,
                data.EventType,
                defaultFontSize,
                defaultLabelOffsetPixels,
                defaultNotificationSound,
                out NotificationPresetResolved resolved))
        {
            if (data.EventType == DiceFeedbackEventType.CubeOffered)
            {
                resolved = new NotificationPresetResolved(
                    "Double!",
                    visibleDuration,
                    defaultFontSize,
                    defaultLabelOffsetPixels,
                    defaultNotificationSound);
            }
            else
            {
                if (enableVerboseLogs)
                    Debug.LogWarning($"[Backgammon][Notify] No preset for event={data.EventType}");
                return;
            }
        }

        float hold = resolved.DisplayDurationSeconds > 0f ? resolved.DisplayDurationSeconds : visibleDuration;
        if (enableVerboseLogs)
            Debug.Log(
                $"[Backgammon][Notify] event={data.EventType} cubeAfter={data.CubeValueAfter} message=\"{resolved.Message}\" hold={hold:F2}s font={resolved.ResolvedFontSize} offset={resolved.ResolvedLabelOffsetPixels}");
        ShowNotificationInternal(resolved, hold);
    }

    private void HandleQueuedScreenNotification(DiceFeedbackEventData data)
    {
        if (!TryResolvePreset(
                presets,
                data.EventType,
                defaultFontSize,
                defaultLabelOffsetPixels,
                defaultNotificationSound,
                out NotificationPresetResolved resolved))
        {
            if (data.EventType == DiceFeedbackEventType.GameEnded)
            {
                bool localWon = data.GameWinnerPlayerIndex == Runtime.RMC.Backgammon.Core.BackgammonPlayerRoles.LocalPlayerIndex;
                string defaultMessage = localWon ? gameEndWinMessage : gameEndLoseMessage;
                resolved = new NotificationPresetResolved(
                    defaultMessage,
                    visibleDuration,
                    defaultFontSize,
                    defaultLabelOffsetPixels,
                    defaultNotificationSound);
            }
            else
            if (data.EventType == DiceFeedbackEventType.CubeValueChanged)
            {
                resolved = new NotificationPresetResolved(
                    $"Double to {Mathf.Max(2, data.CubeValueAfter)}x",
                    visibleDuration,
                    defaultFontSize,
                    defaultLabelOffsetPixels,
                    defaultNotificationSound);
            }
            else
            if (data.EventType == DiceFeedbackEventType.NoLegalMoves)
            {
                resolved = new NotificationPresetResolved(
                    "No legal moves available",
                    visibleDuration,
                    defaultFontSize,
                    defaultLabelOffsetPixels,
                    defaultNotificationSound);
            }
            else
            if (data.EventType == DiceFeedbackEventType.BeaverOffered)
            {
                resolved = new NotificationPresetResolved(
                    $"Beaver! Double to {Mathf.Max(2, data.CubeValueAfter)}x",
                    visibleDuration,
                    defaultFontSize,
                    defaultLabelOffsetPixels,
                    defaultNotificationSound);
            }
            else
            {
                if (enableVerboseLogs)
                    Debug.LogWarning($"[Backgammon][Notify] No preset for queued event={data.EventType}");
                return;
            }
        }

        float hold = resolved.DisplayDurationSeconds > 0f ? resolved.DisplayDurationSeconds : visibleDuration;
        if (enableVerboseLogs)
        {
            Debug.Log(
                $"[Backgammon][Notify] queued event={data.EventType} cubeAfter={data.CubeValueAfter} message=\"{resolved.Message}\" hold={hold:F2}s");
        }
        ShowNotificationInternal(resolved, hold);
    }

    public static bool IsQueueOwnedNotificationEvent(DiceFeedbackEventType eventType)
    {
        return eventType == DiceFeedbackEventType.OpeningRollTieAutodouble
               || eventType == DiceFeedbackEventType.CubeValueChanged
               || eventType == DiceFeedbackEventType.GameEnded
               || eventType == DiceFeedbackEventType.NoLegalMoves
               || eventType == DiceFeedbackEventType.BeaverOffered;
    }

    /// <summary>Show a custom text toast (used by trophy / stake-unlock observers).</summary>
    public void ShowCustomNotification(string message, float holdDuration = 1.5f)
    {
        var resolved = new NotificationPresetResolved(message, holdDuration, defaultFontSize, Vector2.zero, defaultNotificationSound);
        ShowNotificationInternal(resolved, holdDuration);
    }

    /// <summary>Inspector / editor: same path as live events (fades + optional MMF).</summary>
    public void PlayDebugPreview()
    {
        if (enableVerboseLogs)
            Debug.Log($"[Backgammon][Notify] Preview authoring event={debugPresetEventType}");
        string msg = string.IsNullOrEmpty(debugPreviewText) ? "Preview" : debugPreviewText;
        int font = debugFontSize > 0 ? debugFontSize : defaultFontSize;
        Vector2 offset = debugLabelOffsetPixels;
        var resolved = new NotificationPresetResolved(msg, visibleDuration, font, offset, defaultNotificationSound);
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
        TryPlayNotificationSound(style.ResolvedAudioClip);
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

    private void TryPlayNotificationSound(AudioClip clip)
    {
        Component playable = ResolvePlayableTarget(notificationFeedbackPlayer);
        if (playable == null)
        {
            if (enableVerboseLogs && notificationFeedbackPlayer != null)
                Debug.LogWarning("[Backgammon][Notify] Cannot play notification sound: MMF_Player not found.");
            return;
        }

        if (clip == null)
        {
            if (enableVerboseLogs)
                Debug.Log("[Backgammon][Notify] No audio clip resolved for notification.");
            return;
        }

        if (TryConfigureAudioSourceFeedback(playable, clip))
        {
            if (enableVerboseLogs)
                Debug.Log($"[Backgammon][Notify] Configured MMF_AudioSource with clip: {clip.name}");
        }
        else
        {
            if (enableVerboseLogs)
                Debug.LogWarning("[Backgammon][Notify] Failed to configure MMF_AudioSource feedback.");
        }
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

    private static bool TryConfigureAudioSourceFeedback(Component mmfPlayer, AudioClip clip)
    {
        Debug.Log($"[Backgammon][Notify] TryConfigureAudioSourceFeedback called. mmfPlayer={mmfPlayer?.GetType().Name}, clip={clip?.name}");

        if (mmfPlayer == null || clip == null)
        {
            Debug.LogWarning($"[Backgammon][Notify] Null check failed. mmfPlayer={mmfPlayer}, clip={clip}");
            return false;
        }

        Type playerType = mmfPlayer.GetType();

        // Try to get FeedbacksList as a field first (MMF_Player uses a field)
        FieldInfo feedbacksField = playerType.GetField("FeedbacksList", BindingFlags.Instance | BindingFlags.Public);
        object feedbacksList = null;

        if (feedbacksField != null)
        {
            feedbacksList = feedbacksField.GetValue(mmfPlayer);
        }
        else
        {
            // Fall back to property-based approach (old MMFeedbacks system)
            PropertyInfo feedbacksProp = playerType.GetProperty("FeedbacksList", BindingFlags.Instance | BindingFlags.Public);
            if (feedbacksProp == null)
            {
                feedbacksProp = playerType.GetProperty("Feedbacks", BindingFlags.Instance | BindingFlags.Public);
            }

            if (feedbacksProp == null)
            {
                Debug.LogWarning("[Backgammon][Notify] Could not find FeedbacksList field or property");
                return false;
            }

            feedbacksList = feedbacksProp.GetValue(mmfPlayer);
        }
        if (feedbacksList == null)
        {
            Debug.LogWarning("[Backgammon][Notify] FeedbacksList is null");
            return false;
        }

        Type listType = feedbacksList.GetType();
        if (!listType.IsGenericType)
        {
            Debug.LogWarning($"[Backgammon][Notify] FeedbacksList is not generic. Type: {listType}");
            return false;
        }

        PropertyInfo countProp = listType.GetProperty("Count");
        MethodInfo getItem = listType.GetMethod("get_Item");
        if (countProp == null || getItem == null)
        {
            Debug.LogWarning("[Backgammon][Notify] Could not get Count or get_Item from list");
            return false;
        }

        int count = (int)countProp.GetValue(feedbacksList);
        Debug.Log($"[Backgammon][Notify] Found {count} feedbacks in list");

        for (int i = 0; i < count; i++)
        {
            object feedback = getItem.Invoke(feedbacksList, new object[] { i });
            if (feedback == null)
            {
                Debug.LogWarning($"[Backgammon][Notify] Feedback at index {i} is null");
                continue;
            }

            Type feedbackType = feedback.GetType();
            Debug.Log($"[Backgammon][Notify] Checking feedback {i}: Type={feedbackType.Name}");

            // Try MMF_AudioSource first (newer type)
            if (feedbackType.Name == "MMF_AudioSource")
            {
                FieldInfo randomSfxField = feedbackType.GetField("RandomSfx", BindingFlags.Instance | BindingFlags.Public);
                if (randomSfxField != null && randomSfxField.FieldType == typeof(AudioClip[]))
                {
                    randomSfxField.SetValue(feedback, new AudioClip[] { clip });
                    Debug.Log($"[Backgammon][Notify] Configured MMF_AudioSource RandomSfx with clip: {clip.name}");
                    return true;
                }
            }

            // Try MMF_Sound (legacy type)
            if (feedbackType.Name == "MMF_Sound")
            {
                Debug.Log($"[Backgammon][Notify] Found MMF_Sound feedback, attempting to configure...");

                FieldInfo randomSfxField = feedbackType.GetField("RandomSfx", BindingFlags.Instance | BindingFlags.Public);

                if (randomSfxField == null)
                {
                    Debug.LogWarning("[Backgammon][Notify] RandomSfx field not found on MMF_Sound");
                    continue;
                }

                Debug.Log($"[Backgammon][Notify] RandomSfx field found. Type: {randomSfxField.FieldType}, Expected: {typeof(AudioClip[])}");

                if (randomSfxField.FieldType != typeof(AudioClip[]))
                {
                    Debug.LogWarning($"[Backgammon][Notify] RandomSfx field type mismatch. Expected AudioClip[], got {randomSfxField.FieldType}");
                    continue;
                }

                randomSfxField.SetValue(feedback, new AudioClip[] { clip });
                Debug.Log($"[Backgammon][Notify] Configured MMF_Sound RandomSfx with clip: {clip.name}");
                return true;
            }
        }

        // No audio feedback found - attempt to create one
        return TryCreateAndAddAudioSourceFeedback(mmfPlayer, feedbacksList, listType, clip);
    }

    private static bool TryCreateAndAddAudioSourceFeedback(Component mmfPlayer, object feedbacksList, Type listType, AudioClip clip)
    {
        // Find the MMF_AudioSource type in the Feel assembly
        Type audioSourceFeedbackType = FindTypeByName("MMF_AudioSource");
        if (audioSourceFeedbackType == null)
        {
            Debug.LogWarning("[Backgammon][Notify] MMF_AudioSource type not found. Feel plugin may not be installed.");
            return false;
        }

        // Create a new MMF_AudioSource feedback instance
        object newFeedback;
        try
        {
            newFeedback = Activator.CreateInstance(audioSourceFeedbackType);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Backgammon][Notify] Failed to create MMF_AudioSource feedback: {e.Message}");
            return false;
        }

        // Configure the feedback fields
        if (!ConfigureAudioSourceFeedbackFields(newFeedback, audioSourceFeedbackType, clip))
        {
            return false;
        }

        // Add the feedback to the list
        MethodInfo addMethod = listType.GetMethod("Add");
        if (addMethod == null)
        {
            Debug.LogWarning("[Backgammon][Notify] Could not find Add method on Feedbacks list.");
            return false;
        }

        try
        {
            addMethod.Invoke(feedbacksList, new object[] { newFeedback });
            Debug.Log($"[Backgammon][Notify] Auto-created MMF_AudioSource feedback with clip: {clip.name}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Backgammon][Notify] Failed to add MMF_AudioSource to Feedbacks list: {e.Message}");
            return false;
        }
    }

    private static bool ConfigureAudioSourceFeedbackFields(object feedback, Type feedbackType, AudioClip clip)
    {
        // Set Active = true
        FieldInfo activeField = feedbackType.GetField("Active", BindingFlags.Instance | BindingFlags.Public);
        if (activeField != null && activeField.FieldType == typeof(bool))
        {
            activeField.SetValue(feedback, true);
        }

        // Set RandomSfx array with the audio clip
        FieldInfo randomSfxField = feedbackType.GetField("RandomSfx", BindingFlags.Instance | BindingFlags.Public);
        if (randomSfxField != null && randomSfxField.FieldType == typeof(AudioClip[]))
        {
            randomSfxField.SetValue(feedback, new AudioClip[] { clip });
        }
        else
        {
            Debug.LogWarning("[Backgammon][Notify] Could not set RandomSfx field on MMF_AudioSource.");
            return false;
        }

        // Set Label for debugging
        FieldInfo labelField = feedbackType.GetField("Label", BindingFlags.Instance | BindingFlags.Public);
        if (labelField != null && labelField.FieldType == typeof(string))
        {
            labelField.SetValue(feedback, "Auto-created Notification Audio");
        }

        return true;
    }

    private static Type FindTypeByName(string typeName)
    {
        // Search all loaded assemblies for the type
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
            if (type != null)
                return type;
        }
        return null;
    }

    public static string ResolveOpeningRollWinnerMessage(int winnerPlayerIndex, string playerWinsMessage, string aiWinsMessage)
    {
        // In this project, player index 1 is the local player and index 0 is the AI/opponent.
        return winnerPlayerIndex == 1
            ? (string.IsNullOrEmpty(playerWinsMessage) ? "Your go" : playerWinsMessage)
            : (string.IsNullOrEmpty(aiWinsMessage) ? "AI goes first" : aiWinsMessage);
    }

    /// <summary>Resolves preset including font, offset, and audio clip (uses defaults when row uses 0 / zero vector / null).</summary>
    public static bool TryResolvePreset(
        IReadOnlyList<DiceFeedbackNotificationEntry> entries,
        DiceFeedbackEventType eventType,
        int defaultFontSize,
        Vector2 defaultLabelOffsetPixels,
        AudioClip defaultAudioClip,
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
            AudioClip audio = e.audioClipOverride != null ? e.audioClipOverride : defaultAudioClip;
            resolved = new NotificationPresetResolved(e.message, e.displayDurationSeconds, font, offset, audio);
            return true;
        }

        return false;
    }

    /// <summary>Resolves preset message and per-row duration (0 = caller should use global). Uses 72 / zero / null for default font/offset/audio in resolution.</summary>
    public static bool TryResolveMessage(
        IReadOnlyList<DiceFeedbackNotificationEntry> entries,
        DiceFeedbackEventType eventType,
        out string message,
        out float displayDurationSeconds)
    {
        message = null;
        displayDurationSeconds = 0f;
        if (!TryResolvePreset(entries, eventType, 72, Vector2.zero, null, out NotificationPresetResolved r))
            return false;
        message = r.Message;
        displayDurationSeconds = r.DisplayDurationSeconds;
        return true;
    }
}
