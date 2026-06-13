using System.Collections;
using Runtime.RMC.Backgammon.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace Runtime.RMC.Backgammon.UI
{
    /// <summary>
    /// Coroutine-based popup animator. All popup controllers call Show/Hide here
    /// rather than toggling DisplayStyle directly, so animation behaviour is centralised
    /// and controlled by BackgammonSettings.PopupAnimation.
    /// 0=None, 1=Fade, 2=Scale In/Out, 3=Slide Down/Up, 4=Bounce/Shrink
    /// </summary>
    public static class PopupAnimator
    {
        private const float OpenDuration  = 0.22f;
        private const float CloseDuration = 0.16f;

        // ── Public API ────────────────────────────────────────────────────────

        public static void Show(VisualElement layer, MonoBehaviour host,
                                System.Action onComplete = null)
        {
            if (layer == null) return;
            int style = BackgammonSettings.PopupAnimation;
            if (style == 0)
            {
                layer.style.display = DisplayStyle.Flex;
                ResetTransform(layer);
                onComplete?.Invoke();
                return;
            }
            host.StartCoroutine(AnimateOpen(layer, style, onComplete));
        }

        public static void Hide(VisualElement layer, MonoBehaviour host,
                                System.Action onComplete = null)
        {
            if (layer == null) return;
            int style = BackgammonSettings.PopupAnimation;
            if (style == 0)
            {
                layer.style.display = DisplayStyle.None;
                ResetTransform(layer);
                onComplete?.Invoke();
                return;
            }
            host.StartCoroutine(AnimateClose(layer, style, onComplete));
        }

        // ── Coroutines ────────────────────────────────────────────────────────

        private static IEnumerator AnimateOpen(VisualElement layer, int style,
                                               System.Action onComplete)
        {
            layer.style.display = DisplayStyle.Flex;
            float t = 0f;
            while (t < OpenDuration)
            {
                t += Time.unscaledDeltaTime;
                float p  = Mathf.Clamp01(t / OpenDuration);
                float ep = EaseOut(p);
                ApplyOpenFrame(layer, style, ep);
                yield return null;
            }
            ResetTransform(layer);
            onComplete?.Invoke();
            Debug.Log($"[Event][PopupAnimator] Open complete (style={style})");
        }

        private static IEnumerator AnimateClose(VisualElement layer, int style,
                                                System.Action onComplete)
        {
            float t = 0f;
            while (t < CloseDuration)
            {
                t += Time.unscaledDeltaTime;
                float p  = Mathf.Clamp01(t / CloseDuration);
                float ep = EaseIn(p);
                ApplyCloseFrame(layer, style, ep);
                yield return null;
            }
            layer.style.display = DisplayStyle.None;
            ResetTransform(layer);
            onComplete?.Invoke();
            Debug.Log($"[Event][PopupAnimator] Close complete (style={style})");
        }

        // ── Per-frame style application ───────────────────────────────────────

        private static void ApplyOpenFrame(VisualElement el, int style, float p)
        {
            switch (style)
            {
                case 1: // Fade
                    el.style.opacity = p;
                    break;
                case 2: // Scale In
                    float s2 = Mathf.Lerp(0.85f, 1f, p);
                    el.style.opacity = p;
                    el.style.scale = new Scale(new Vector2(s2, s2));
                    break;
                case 3: // Slide Down
                    float y3 = Mathf.Lerp(-30f, 0f, p);
                    el.style.opacity = p;
                    el.style.translate = new Translate(0, y3, 0);
                    break;
                case 4: // Bounce
                    float s4 = p < 0.7f
                        ? Mathf.Lerp(0.7f, 1.05f, p / 0.7f)
                        : Mathf.Lerp(1.05f, 1f, (p - 0.7f) / 0.3f);
                    el.style.opacity = Mathf.Min(p * 2f, 1f);
                    el.style.scale = new Scale(new Vector2(s4, s4));
                    break;
            }
        }

        private static void ApplyCloseFrame(VisualElement el, int style, float p)
        {
            switch (style)
            {
                case 1: // Fade out
                    el.style.opacity = 1f - p;
                    break;
                case 2: // Scale Out
                    float s2 = Mathf.Lerp(1f, 0.85f, p);
                    el.style.opacity = 1f - p;
                    el.style.scale = new Scale(new Vector2(s2, s2));
                    break;
                case 3: // Slide Up
                    float y3 = Mathf.Lerp(0f, -30f, p);
                    el.style.opacity = 1f - p;
                    el.style.translate = new Translate(0, y3, 0);
                    break;
                case 4: // Shrink
                    float s4 = Mathf.Lerp(1f, 0.7f, p);
                    el.style.opacity = 1f - p;
                    el.style.scale = new Scale(new Vector2(s4, s4));
                    break;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void ResetTransform(VisualElement el)
        {
            el.style.opacity   = 1f;
            el.style.scale     = new Scale(Vector2.one);
            el.style.translate = new Translate(0, 0, 0);
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseIn(float t)  => t * t;
    }
}
