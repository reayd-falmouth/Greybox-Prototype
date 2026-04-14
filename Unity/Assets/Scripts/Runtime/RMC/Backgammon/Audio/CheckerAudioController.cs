using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class CheckerAudioController : MonoBehaviour
{
    private static CheckerAudioController _activeListener;

    [Header("References")]
    [SerializeField] private BackgammonGameController controller;

    [Header("MMF Players (Feel)")]
    [Tooltip("Assign the MMF_Player component for a regular checker move.")]
    [SerializeField] private Component movePlayer;
    [Tooltip("Assign the MMF_Player component for a hit/blot sent to bar.")]
    [SerializeField] private Component hitToBarPlayer;
    [Tooltip("Assign the MMF_Player component for entering from bar.")]
    [SerializeField] private Component enterFromBarPlayer;
    [Tooltip("Assign the MMF_Player component for bearing off.")]
    [SerializeField] private Component bearOffPlayer;
    [Tooltip("Assign the MMF_Player component for undo.")]
    [SerializeField] private Component undoPlayer;

    [Header("Playback Guards")]
    [Tooltip("Ignore repeated identical event types in this short window.")]
    [SerializeField] private float sameEventCooldownSeconds = 0.03f;
    [Tooltip("After a high-priority event (hit/bear off/undo), suppress lower-priority sounds briefly.")]
    [SerializeField] private float lowerPrioritySuppressSeconds = 0.05f;
    [SerializeField] private bool enableAudioDebugLogs;

    private readonly Dictionary<CheckerSoundEventType, float> _lastPlayedAtByType = new();
    private float _lastHighPriorityAt = -999f;

    private void Start()
    {
        if (controller == null)
            controller = FindFirstObjectByType<BackgammonGameController>();
        Subscribe();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (!TryBecomeActiveListener())
            return;

        if (controller == null) return;
        controller.OnCheckerSoundEvent -= HandleCheckerSoundEvent;
        controller.OnCheckerSoundEvent += HandleCheckerSoundEvent;
    }

    private void Unsubscribe()
    {
        if (_activeListener == this)
            _activeListener = null;

        if (controller == null) return;
        controller.OnCheckerSoundEvent -= HandleCheckerSoundEvent;
    }

    private bool TryBecomeActiveListener()
    {
        if (_activeListener == null || _activeListener == this)
        {
            _activeListener = this;
            return true;
        }

        if (enableAudioDebugLogs)
        {
            Debug.LogWarning(
                $"[Backgammon][Audio] Duplicate CheckerAudioController ignored on '{name}'. Active listener is '{_activeListener.name}'.");
        }

        return false;
    }

    private void HandleCheckerSoundEvent(CheckerSoundEventData data)
    {
        float now = Time.unscaledTime;
        if (IsSameTypeWithinCooldown(data.EventType, now))
        {
            if (enableAudioDebugLogs)
                Debug.Log($"[Backgammon][Audio] Suppressed by same-event cooldown: {data.EventType}");
            return;
        }

        bool isHighPriority = IsHighPriority(data.EventType);
        if (!isHighPriority && now - _lastHighPriorityAt < lowerPrioritySuppressSeconds)
        {
            if (enableAudioDebugLogs)
                Debug.Log($"[Backgammon][Audio] Suppressed by priority window: {data.EventType}");
            return;
        }

        bool played = TryPlayMappedPlayer(data.EventType);
        if (played)
        {
            _lastPlayedAtByType[data.EventType] = now;
            if (isHighPriority)
                _lastHighPriorityAt = now;
        }
        else if (enableAudioDebugLogs)
        {
            Debug.LogWarning($"[Backgammon][Audio] No playable MMF player for event: {data.EventType}");
        }
    }

    private bool IsSameTypeWithinCooldown(CheckerSoundEventType eventType, float now)
    {
        if (!_lastPlayedAtByType.TryGetValue(eventType, out float last))
            return false;
        return now - last < sameEventCooldownSeconds;
    }

    private static bool IsHighPriority(CheckerSoundEventType eventType)
    {
        return eventType == CheckerSoundEventType.HitToBar
               || eventType == CheckerSoundEventType.BearOff
               || eventType == CheckerSoundEventType.Undo;
    }

    private bool TryPlayMappedPlayer(CheckerSoundEventType eventType)
    {
        Component configured = ResolveMappedPlayer(eventType);
        Component playableTarget = ResolvePlayableTarget(configured);
        if (enableAudioDebugLogs && playableTarget != null)
        {
            Debug.Log(
                $"[Backgammon][Audio] event={eventType} configured={DescribeComponent(configured)} resolved={DescribeComponent(playableTarget)}");
        }

        bool played = TryPlayFeelPlayer(playableTarget, eventType, enableAudioDebugLogs);
        if (!played && enableAudioDebugLogs)
        {
            if (configured != null && playableTarget == null)
            {
                Debug.LogWarning(
                    $"[Backgammon][Audio] event={eventType} configured={DescribeComponent(configured)} has no playable component exposing PlayFeedbacks.");
            }
            else if (playableTarget != null)
            {
                Debug.LogWarning(
                    $"[Backgammon][Audio] event={eventType} failed to invoke PlayFeedbacks on {DescribeComponent(playableTarget)}.");
            }
            else
            {
                Debug.LogWarning($"[Backgammon][Audio] event={eventType} has no configured MMF player reference.");
            }
        }

        return played;
    }

    private Component ResolveMappedPlayer(CheckerSoundEventType eventType)
    {
        switch (eventType)
        {
            case CheckerSoundEventType.HitToBar: return hitToBarPlayer != null ? hitToBarPlayer : movePlayer;
            case CheckerSoundEventType.EnterFromBar: return enterFromBarPlayer != null ? enterFromBarPlayer : movePlayer;
            case CheckerSoundEventType.BearOff: return bearOffPlayer != null ? bearOffPlayer : movePlayer;
            case CheckerSoundEventType.Undo: return undoPlayer != null ? undoPlayer : movePlayer;
            default: return movePlayer;
        }
    }

    private static Component ResolvePlayableTarget(Component candidate)
    {
        if (candidate == null) return null;
        if (HasPlayFeedbacksMethod(candidate)) return candidate;

        if (candidate is Transform tr)
            return FindPlayableComponentOnOrUnder(tr);

        Component onSelf = FindPlayableComponentOnOrUnder(candidate.transform);
        if (onSelf != null) return onSelf;

        return null;
    }

    private static Component FindPlayableComponentOnOrUnder(Transform root)
    {
        if (root == null) return null;
        Component[] components = root.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            if (HasPlayFeedbacksMethod(c))
                return c;
        }

        return null;
    }

    private static bool HasPlayFeedbacksMethod(Component candidate)
    {
        if (candidate == null) return false;
        return candidate.GetType().GetMethod(
                   "PlayFeedbacks",
                   BindingFlags.Instance | BindingFlags.Public,
                   binder: null,
                   types: System.Type.EmptyTypes,
                   modifiers: null) != null
               || candidate.GetType().GetMethod(
                   "PlayFeedbacks",
                   BindingFlags.Instance | BindingFlags.Public,
                   binder: null,
                   types: new[] { typeof(Vector3) },
                   modifiers: null) != null
               || candidate.GetType().GetMethod(
                   "PlayFeedbacks",
                   BindingFlags.Instance | BindingFlags.Public,
                   binder: null,
                   types: new[] { typeof(Vector3), typeof(float) },
                   modifiers: null) != null;
    }

    private static string DescribeComponent(Component component)
    {
        if (component == null) return "<null>";
        return $"{component.name} ({component.GetType().FullName})";
    }

    private static bool TryPlayFeelPlayer(Component candidate, CheckerSoundEventType eventType, bool enableLogs)
    {
        if (candidate == null) return false;
        System.Type t = candidate.GetType();

        // Feel versions can expose different PlayFeedbacks signatures.
        MethodInfo playNoArgs = t.GetMethod(
            "PlayFeedbacks",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: System.Type.EmptyTypes,
            modifiers: null);
        if (playNoArgs != null)
        {
            if (!TryInvoke(playNoArgs, candidate, null, eventType, enableLogs))
                return false;
            return true;
        }

        MethodInfo playVector3 = t.GetMethod(
            "PlayFeedbacks",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(Vector3) },
            modifiers: null);
        if (playVector3 != null)
        {
            if (!TryInvoke(playVector3, candidate, new object[] { Vector3.zero }, eventType, enableLogs))
                return false;
            return true;
        }

        MethodInfo playVector3AndIntensity = t.GetMethod(
            "PlayFeedbacks",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(Vector3), typeof(float) },
            modifiers: null);
        if (playVector3AndIntensity != null)
        {
            if (!TryInvoke(playVector3AndIntensity, candidate, new object[] { Vector3.zero, 1f }, eventType, enableLogs))
                return false;
            return true;
        }

        return false;
    }

    private static bool TryInvoke(MethodInfo method, Component target, object[] args, CheckerSoundEventType eventType, bool enableLogs)
    {
        try
        {
            method.Invoke(target, args);
            return true;
        }
        catch (System.Exception ex)
        {
            if (enableLogs)
            {
                Debug.LogError(
                    $"[Backgammon][Audio] event={eventType} PlayFeedbacks threw on {DescribeComponent(target)}. {ex.GetType().Name}: {ex.Message}");
            }

            return false;
        }
    }
}
