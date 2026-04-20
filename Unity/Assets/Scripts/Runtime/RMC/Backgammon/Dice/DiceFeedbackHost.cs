using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Per-die mapping from <see cref="DiceFeedbackEventType"/> to Feel MMF_Player (or compatible) components.
/// Uses reflection for <c>PlayFeedbacks</c> so runtime asmdef does not reference Feel assemblies.
/// </summary>
[DisallowMultipleComponent]
public class DiceFeedbackHost : MonoBehaviour
{
    [Serializable]
    public class FeedbackSlot
    {
        public DiceFeedbackEventType eventType;
        [Tooltip("Usually MMF_Player; may be a parent with MMF under it.")]
        public Component feedbackPlayer;
    }

    [SerializeField] private List<FeedbackSlot> feedbackSlots = new();
    [SerializeField] private bool enableDebugLogs;

    public bool TryPlay(DiceFeedbackEventType eventType)
    {
        Component configured = ResolvePlayer(eventType);
        Component playable = ResolvePlayableTarget(configured);
        if (playable == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning(
                    $"[Backgammon][DiceFeedback] '{name}' no MMF mapping for {eventType} (configured={(configured != null ? configured.name : "null")}).");
            return false;
        }

        bool ok = TryPlayFeelPlayer(playable, eventType);
        if (ok && enableDebugLogs)
            Debug.Log($"[Backgammon][DiceFeedback] '{name}' played {eventType} via {playable.GetType().Name}");
        return ok;
    }

    private Component ResolvePlayer(DiceFeedbackEventType eventType)
    {
        if (feedbackSlots == null) return null;
        for (int i = 0; i < feedbackSlots.Count; i++)
        {
            FeedbackSlot s = feedbackSlots[i];
            if (s != null && s.eventType == eventType && s.feedbackPlayer != null)
                return s.feedbackPlayer;
        }

        return null;
    }

    private static Component ResolvePlayableTarget(Component candidate)
    {
        if (candidate == null) return null;
        if (HasPlayFeedbacksMethod(candidate)) return candidate;
        if (candidate is Transform tr)
            return FindPlayableComponentOnOrUnder(tr);
        return FindPlayableComponentOnOrUnder(candidate.transform);
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
        Type t = candidate.GetType();
        return t.GetMethod(
                   "PlayFeedbacks",
                   BindingFlags.Instance | BindingFlags.Public,
                   binder: null,
                   types: Type.EmptyTypes,
                   modifiers: null) != null
               || t.GetMethod(
                   "PlayFeedbacks",
                   BindingFlags.Instance | BindingFlags.Public,
                   binder: null,
                   types: new[] { typeof(Vector3) },
                   modifiers: null) != null
               || t.GetMethod(
                   "PlayFeedbacks",
                   BindingFlags.Instance | BindingFlags.Public,
                   binder: null,
                   types: new[] { typeof(Vector3), typeof(float) },
                   modifiers: null) != null;
    }

    private bool TryPlayFeelPlayer(Component candidate, DiceFeedbackEventType eventType)
    {
        if (candidate == null) return false;
        Type t = candidate.GetType();

        MethodInfo playNoArgs = t.GetMethod(
            "PlayFeedbacks",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);
        if (playNoArgs != null)
            return TryInvoke(playNoArgs, candidate, null, eventType);

        MethodInfo playVector3 = t.GetMethod(
            "PlayFeedbacks",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(Vector3) },
            modifiers: null);
        if (playVector3 != null)
            return TryInvoke(playVector3, candidate, new object[] { Vector3.zero }, eventType);

        MethodInfo playVector3AndIntensity = t.GetMethod(
            "PlayFeedbacks",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(Vector3), typeof(float) },
            modifiers: null);
        if (playVector3AndIntensity != null)
            return TryInvoke(playVector3AndIntensity, candidate, new object[] { Vector3.zero, 1f }, eventType);

        return false;
    }

    private bool TryInvoke(MethodInfo method, Component target, object[] args, DiceFeedbackEventType eventType)
    {
        try
        {
            method.Invoke(target, args);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[Backgammon][DiceFeedback] event={eventType} PlayFeedbacks threw on {target.name} ({target.GetType().Name}). {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}
