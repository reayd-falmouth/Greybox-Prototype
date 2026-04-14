using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Runtime.RMC._MyProject_.Core
{
    public class DeterministicRNG : MonoBehaviour
    {
        public static DeterministicRNG Instance;

        [Header("UI Settings")]
        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private string _labelName = "SeedLabel";
        private Label _seedLabel;

        [Header("Seed State")]
        [SerializeField] private string _currentSeedString = "DEFAULT1";
        
        public string CurrentSeedString => _currentSeedString;
        private double _masterHashedSeed;
        private Dictionary<string, double> _rngStates = new Dictionary<string, double>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        
            if (_uiDocument != null)
                _seedLabel = _uiDocument.rootVisualElement.Q<Label>(_labelName);

            SetMasterSeed(_currentSeedString);
        }

        public void SetMasterSeed(string seedKey)
        {
            _currentSeedString = seedKey.ToUpper();
            _rngStates.Clear();
            _masterHashedSeed = PseudoHash(_currentSeedString);
            
            if (_seedLabel != null) 
                _seedLabel.text = $"Seed: {_currentSeedString}";

            Debug.Log($"Seed set to: {_currentSeedString} (Hash: {_masterHashedSeed})");
        }

        // --- Balatro Logic ---

        private double PseudoHash(string str)
        {
            double num = 1.0;
            for (int i = str.Length - 1; i >= 0; i--)
            {
                int byteVal = (int)str[i];
                num = ((1.1239285023 / num) * byteVal * Math.PI + Math.PI * (i + 1)) % 1.0;
            }
            return num;
        }

        private double PseudoSeed(string key)
        {
            if (!_rngStates.ContainsKey(key))
                _rngStates[key] = PseudoHash(key + _currentSeedString);

            double currentVal = _rngStates[key];
            double nextVal = Math.Abs((2.134453429141 + currentVal * 1.72431234) % 1.0);
            nextVal = Math.Round(nextVal, 13);
            _rngStates[key] = nextVal;

            return (nextVal + _masterHashedSeed) / 2.0;
        }

        public int RandomRange(string contextKey, int min, int max)
        {
            double seedFloat = PseudoSeed(contextKey);
            int integerSeed = (int)(seedFloat * 2147483647);
            System.Random tempRng = new System.Random(integerSeed);
            return tempRng.Next(min, max + 1);
        }

        public string GenerateRandomSeedString(int length = 8)
        {
            string chars = "123456789ABCDEFGHIJKLMNPQRSTUVWXYZ"; 
            string result = "";
            for (int i = 0; i < length; i++)
            {
                result += chars[UnityEngine.Random.Range(0, chars.Length)];
            }
            return result;
        }

        #region Clipboard Helpers
        public void CopyToClipboard()
        {
            GUIUtility.systemCopyBuffer = _currentSeedString;
            Debug.Log($"Copied {_currentSeedString} to clipboard.");
        }

        public void PasteFromClipboard()
        {
            string pasted = GUIUtility.systemCopyBuffer.Trim().ToUpper();
            if (pasted.Length == 8) SetMasterSeed(pasted);
            else Debug.LogWarning("Invalid seed in clipboard (must be 8 characters).");
        }
        #endregion
    }

    // --- Custom Editor to Draw Buttons ---
    #if UNITY_EDITOR
    [CustomEditor(typeof(DeterministicRNG))]
    public class DeterministicRNGEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default fields (UI Document, Seed String, etc.)
            DrawDefaultInspector();

            DeterministicRNG script = (DeterministicRNG)target;

            GUILayout.Space(10);
            GUILayout.Label("Seed Tools", EditorStyles.boldLabel);

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy Seed"))
                {
                    script.CopyToClipboard();
                }

                if (GUILayout.Button("Paste Seed"))
                {
                    script.PasteFromClipboard();
                }
            }

            if (GUILayout.Button("Generate Random Seed"))
            {
                // This works in both Edit mode and Play mode
                string newSeed = script.GenerateRandomSeedString();
                script.SetMasterSeed(newSeed);
                
                // Ensure the Inspector field updates visually if we are in Edit mode
                EditorUtility.SetDirty(target);
            }
        }
    }
    #endif
}