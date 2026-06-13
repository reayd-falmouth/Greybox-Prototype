using System;
using System.Collections;
using Runtime.RMC.Backgammon.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace Runtime.RMC.Backgammon.UI
{
    public class ScreenWipeController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;

        [Header("Wipe Settings")]
        [SerializeField] private float wipeDuration   = 0.35f;
        [SerializeField] private float holdDuration   = 0f;
        [SerializeField] private Color wipeColor      = new Color(0.039f, 0.075f, 0.098f, 1f);
        [SerializeField] private Sprite wipeIcon;
        [SerializeField] private AudioClip wipeSound;

        private VisualElement _wipeLayer;
        private VisualElement _wipeShape;
        private VisualElement _wipeIcon;
        private AudioSource   _audioSource;

        private void Awake()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        private void ResolveElements()
        {
            if (_wipeLayer != null) return;
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) { Debug.LogWarning("[ScreenWipe] uiDocument is not assigned."); return; }
            var root = uiDocument.rootVisualElement;
            if (root == null) { Debug.LogWarning("[ScreenWipe] rootVisualElement is null."); return; }
            _wipeLayer = root.Q<VisualElement>("WipeLayer");
            _wipeShape = root.Q<VisualElement>("WipeShape");
            _wipeIcon  = root.Q<VisualElement>("WipeIcon");
            if (_wipeLayer == null) Debug.LogWarning("[ScreenWipe] WipeLayer not found in UXML.");
        }

        public void PlayWipe(Color color, Sprite icon, Action onMidpoint)
        {
            Color prevColor = wipeColor;
            Sprite prevIcon = wipeIcon;
            wipeColor = color;
            wipeIcon  = icon;
            PlayWipe(() =>
            {
                wipeColor = prevColor;
                wipeIcon  = prevIcon;
                onMidpoint?.Invoke();
            });
        }

        public void PlayWipe(Action onMidpoint)
        {
            ResolveElements();
            int shape = BackgammonSettings.TransitionShape;
            if (shape == 3 || _wipeLayer == null)
            {
                onMidpoint?.Invoke();
                return;
            }
            StartCoroutine(WipeRoutine(shape, onMidpoint));
        }

        private IEnumerator WipeRoutine(int shape, Action onMidpoint)
        {
            ApplyShapeStyle(shape);
            _wipeLayer.style.display = DisplayStyle.Flex;

            if (wipeSound != null)
                _audioSource.PlayOneShot(wipeSound);

            // Phase 1 — expand to cover
            yield return Animate(0f, 1f, EaseOut);

            onMidpoint?.Invoke();

            if (holdDuration > 0f)
                yield return new WaitForSecondsRealtime(holdDuration);

            // Phase 2 — shrink to reveal
            yield return Animate(1f, 0f, EaseIn);

            _wipeLayer.style.display = DisplayStyle.None;
        }

        private IEnumerator Animate(float from, float to, Func<float, float> ease)
        {
            float t = 0f;
            while (t < wipeDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = ease(Mathf.Clamp01(t / wipeDuration));
                SetScale(Mathf.Lerp(from, to, p));
                yield return null;
            }
            SetScale(to);
        }

        private void ApplyShapeStyle(int shape)
        {
            if (_wipeShape == null) return;

            _wipeShape.style.backgroundColor = new StyleColor(wipeColor);

            if (_wipeIcon != null)
            {
                if (wipeIcon != null)
                {
                    _wipeIcon.style.backgroundImage = new StyleBackground(wipeIcon);
                    _wipeIcon.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _wipeIcon.style.display = DisplayStyle.None;
                }
                _wipeIcon.style.rotate = new StyleRotate(new Rotate(0f));
            }

            // Reset shape style
            _wipeShape.style.borderTopLeftRadius     = 0;
            _wipeShape.style.borderTopRightRadius    = 0;
            _wipeShape.style.borderBottomLeftRadius  = 0;
            _wipeShape.style.borderBottomRightRadius = 0;
            _wipeShape.style.rotate = new StyleRotate(new Rotate(0f));

            switch (shape)
            {
                case 0: // Diamond — rotate square 45deg, counter-rotate icon so it stays upright
                    _wipeShape.style.rotate = new StyleRotate(new Rotate(45f));
                    if (_wipeIcon != null)
                        _wipeIcon.style.rotate = new StyleRotate(new Rotate(-45f));
                    break;
                case 1: // Circle
                    Length radius = new Length(50f, LengthUnit.Percent);
                    _wipeShape.style.borderTopLeftRadius     = radius;
                    _wipeShape.style.borderTopRightRadius    = radius;
                    _wipeShape.style.borderBottomLeftRadius  = radius;
                    _wipeShape.style.borderBottomRightRadius = radius;
                    break;
                case 2: // Square — no extra styling needed
                    break;
            }
        }

        private void SetScale(float s)
        {
            if (_wipeShape == null) return;
            _wipeShape.style.scale = new StyleScale(new Scale(new Vector2(s, s)));

            // Counter-scale the icon so it remains at a fixed screen size regardless of shape scale
            if (_wipeIcon != null && s > 0.001f)
                _wipeIcon.style.scale = new StyleScale(new Scale(new Vector2(1f / s, 1f / s)));
        }

        private static float EaseOut(float p) => 1f - (1f - p) * (1f - p);
        private static float EaseIn(float p)  => p * p;
    }
}
