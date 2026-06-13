using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements; // 1. Added namespace for UI Toolkit
using Runtime.RMC._MyProject_.Core;

namespace Runtime.RMC._MyProject_.Dice
{
    /// <summary>
    ///     Manages the throwing and animation of dice in a dice rolling simulation.
    ///     Physics record/playback is conceptually similar to MoneySession; roll results are exposed via <see cref="OnDiceRollFinished"/> for EngineCore.
    /// </summary>
    public class DiceManager : MonoBehaviour
    {
        // --- UI CONFIGURATION ---
        [Header("UI Connectivity")]
        [Tooltip("The UI Document containing the visual tree for the game HUD.")]
        [SerializeField] private UIDocument uiDocument;

        [Tooltip("The name (ID) of the button element in the UI Builder.")]
        [SerializeField] private string rollButtonName = "RollButton";

        /// <summary>When true, <see cref="OnEnable"/> does not subscribe (e.g. backgammon HUD rolls via <see cref="RequestRoll"/>).</summary>
        public bool SuppressRollButtonBinding;

        // --- SPAWNING SETTINGS ---
        [Header("Dice Spawning")]
        [Tooltip("The dice prefab to instantiate.")]
        [SerializeField] private GameObject dicePrefab;
        [SerializeField] private Transform floorTransform;
        
        [Tooltip("How many dice should be in the game?")]
        [Range(1, 5)]
        [SerializeField] private int diceCount = 2;

        [Tooltip("What percentage of the floor width should the dice occupy? (0.1 to 1.0)")]
        [Range(0.1f, 1.0f)]
        [SerializeField] private float boardFillAmount = 0.8f;

        [Tooltip("The base local position for the first die.")]
        [SerializeField] private Vector3 baseLocalPosition = new Vector3(0f, 0.126f, -0.25f);
        
        // --- CORE LOGIC SETTINGS ---
        [Header("Simulation Logic")]
        [Tooltip("Checked: Generates random dice results on click.\nUnchecked: Uses the manual values set below.")]
        [SerializeField] private bool useRandomValues = true;

        [Space(10)]
        [Header("Manual Dice Values (If Random is Off)")]
        [Tooltip("This list will automatically resize to match 'Dice Count'.")]
        [SerializeField] private List<int> manualRollValues = new List<int>();

        [Header("Global Dice Visuals")]
        [SerializeField] private Color diceBodyColor = Color.red;
        [SerializeField] private Color dicePipColor = Color.white;
        [Range(0f, 5f)]
        [SerializeField] private float diceLuminosity = 1f;
        [Range(0f, 1f)]
        [SerializeField] private float diceAlpha = 1f;
        
        [Header("Global Audio Settings")]
        [Tooltip("The audio profile containing the list of sounds (Wood, Felt, etc).")]
        [SerializeField] private DiceAudioProfileSo audioProfile;
        
        // This ensures the list length always matches your dice count in the editor
        private void OnValidate()
        {
            if (manualRollValues == null) manualRollValues = new List<int>();
    
            while (manualRollValues.Count < diceCount) manualRollValues.Add(1);
            while (manualRollValues.Count > diceCount) manualRollValues.RemoveAt(manualRollValues.Count - 1);
            UpdateAllDiceSettings();
        }

        private void UpdateAllDiceSettings()
        {
            if (Dices == null || Dices.Count == 0) return;

            foreach (var diceTransform in Dices)
            {
                if (diceTransform != null && diceTransform.TryGetComponent(out Dice diceScript))
                {
                    diceScript.SetVisuals(diceBodyColor, dicePipColor, diceLuminosity, diceAlpha);

                    // Update Audio too
                    if (audioProfile != null)
                    {
                        diceScript.SetAudioProfile(audioProfile.impactClips, audioProfile.baseVolume);
                    }
                }
            }
        }

        public void SetDiceTheme(Color bodyColor, Color pipColor, float luminosity)
        {
            diceBodyColor = bodyColor;
            dicePipColor = pipColor;
            diceLuminosity = luminosity;
            UpdateAllDiceSettings();
        }

        // --- PHYSICS & FEEL ---
        [Header("Physics Tuning")]
        [Tooltip("The base impulse force applied to the throw.")]
        [Range(12.5f, 45.0f)]
        public float initialForce = 35.0f;

        [Header("Force Variation")]
        [Tooltip("Adds a random variance to the force so every throw feels unique.")]
        [SerializeField] private bool addRandomVariability = true;

        [Tooltip("The maximum extra random force added to the forward throw (Z-axis) if variability is enabled.")]
        [Range(0.0f, 5.0f)]
        [SerializeField] private float forceVariabilityRange = 2.5f;

        [Tooltip("Random horizontal deviation (X-axis, left/right) applied to each throw.")]
        [Range(0.0f, 1.0f)]
        [SerializeField] private float lateralForceVariance = 0.1f;

        [Tooltip("Random vertical deviation (Y-axis, up/down) applied to each throw.")]
        [Range(0.0f, 1.0f)]
        [SerializeField] private float verticalForceVariance = 0.1f;

        [Header("Rotation Variation")]
        [Tooltip("Randomize the starting rotation of dice before each throw.")]
        [SerializeField] private bool randomizeStartRotation = true;

        [Header("Torque Variation")]
        [Tooltip("Randomize the spin/torque applied to dice during throw.")]
        [SerializeField] private bool randomizeTorque = true;

        [Tooltip("Maximum random torque per axis (X, Y, Z) if torque randomization is enabled.")]
        [Range(0.0f, 50.0f)]
        [SerializeField] private float maxTorquePerAxis = 25.0f;

        [Header("Simulation Playback")]
        [Tooltip("Total physics steps to record for pre-calculation (higher = longer roll).")]
        public int simulationFrameLength = 100; // Fixed typo from 'Lenght'
        
        // --- INTERNAL DATA (Hidden or Grouped at bottom) ---
        [Header("Scene References & Data")]
        [Tooltip("Populated automatically at Runtime.")]
        public List<Transform> Dices = new();
        
        [HideInInspector] 
        public List<Vector3> initialDicePositions = new();

        /// <summary>Fired after playback completes. With one die, die2 equals die1.</summary>
        public event System.Action<int, int> OnDiceRollFinished;

        private Button _rollButton;
        private readonly Dictionary<int, List<TransformData>> diceAnimationData = new();
        private bool diceHasThrown;
        private Vector3 force;
        private Quaternion rotation;
        private Vector3 torque;
        private List<Transform> spawnedDice = new();
        private readonly List<Renderer> _diceRenderers = new();
        private bool _diceVisualsVisible = true;

        // 2. Setup the UI connection when the script enables
        private void OnEnable()
        {
            if (SuppressRollButtonBinding) return;

            if (uiDocument == null)
            {
                Debug.LogError("DiceManager: UI Document is not assigned in the Inspector!");
                return;
            }

            var root = uiDocument.rootVisualElement;
            _rollButton = root.Q<Button>(rollButtonName);

            if (_rollButton != null)
            {
                _rollButton.clicked += OnRollButtonClicked;
            }
            else
            {
                Debug.LogError($"DiceManager: Could not find button named '{rollButtonName}'");
            }
        }

        // 3. Clean up the connection when the script disables
        private void OnDisable()
        {
            if (SuppressRollButtonBinding) return;

            if (_rollButton != null)
            {
                _rollButton.clicked -= OnRollButtonClicked;
            }
        }

        private void Start()
        {
            Random.InitState((int)System.DateTime.Now.Ticks);
            SpawnDice();
        }
        
        private void SpawnDice()
        {
            // 1. Cleanup
            foreach (var d in spawnedDice) if (d != null) Destroy(d.gameObject);
            spawnedDice.Clear();
            _diceRenderers.Clear();
            initialDicePositions.Clear();
            diceAnimationData.Clear();

            // 2. Calculate Bounds based on Floor Scale
            // We use localScale.x to determine how wide the floor is
            float floorWidth = floorTransform != null ? floorTransform.localScale.x : 1f;
            float playableWidth = floorWidth * boardFillAmount;

            // 3. Spacing Logic
            // If 1 die: position is 0. If more, space them across playableWidth.
            float startX = 0f;
            float currentSpacing = 0f;

            if (diceCount > 1)
            {
                startX = -playableWidth / 2f;
                currentSpacing = playableWidth / (diceCount - 1);
            }

            // 4. Instantiate
            for (int i = 0; i < diceCount; i++)
            {
                Vector3 spawnPos = baseLocalPosition;
                spawnPos.x = startX + (i * currentSpacing);

                GameObject go = Instantiate(dicePrefab, transform);
                
                // Set Physics Layer immediately to prevent physical "shoving"
                go.layer = LayerMask.NameToLayer("Dice");
                
                // PASS THE SETTINGS TO THE DICE SCRIPT
                if (go.TryGetComponent(out Dice diceScript))
                {
                    diceScript.SetVisuals(diceBodyColor, dicePipColor, diceLuminosity, diceAlpha);
                    
                    if (audioProfile != null)
                    {
                        diceScript.SetAudioProfile(audioProfile.impactClips, audioProfile.baseVolume);
                    }
                    
                    // FORCE RENDER QUEUE: Use the mesh reference from the script
                    if (diceScript.diceMesh != null && diceScript.diceMesh.TryGetComponent(out MeshRenderer mr))
                    {
                        // By setting this to 3500, we put it ahead of standard transparency (3000)
                        // and the checkers' emission/HDR materials.
                        mr.material.renderQueue = 3500; 
                    }
                }
                go.transform.localPosition = spawnPos;
        
                spawnedDice.Add(go.transform);
                initialDicePositions.Add(go.transform.position);
                diceAnimationData.Add(i, new List<TransformData>());
                CacheDiceRenderers(go.transform);
        
                DisablePhysics(go.transform);
            }

            Dices = spawnedDice; 
            HideDiceVisuals();
        }
        
        // 4. Handle the button click
        private void OnRollButtonClicked()
        {
            if (useRandomValues)
            {
                // Check if the DeterministicRNG is present in the scene
                if (DeterministicRNG.Instance != null)
                {
                    for (int i = 0; i < manualRollValues.Count; i++)
                    {
                        // DETERMINISTIC: Use your seed to pick the numbers 1-6
                        manualRollValues[i] = DeterministicRNG.Instance.RandomRange("DiceValue", 1, 6);
                    }
                }
                else
                {
                    // Fallback to standard random if the script isn't in the scene
                    for (int i = 0; i < manualRollValues.Count; i++)
                    {
                        manualRollValues[i] = Random.Range(1, 7);
                    }
                }
            }
    
            // This will now use the deterministic values to set the final face rotation
            SimulateThrow(); 
        }   

        // --- METHOD B: NATURAL ---
        private void RollNaturally()
        {
            ResetDiceState();
            // In natural mode, we don't call RotateDice() because we want the physics 
            // to determine the outcome, not the script.
        }
        
        private void ResetDiceState()
        {
            for (var i = 0; i < Dices.Count; i++)
            {
                Rigidbody rb = Dices[i].GetComponent<Rigidbody>();
        
                // 1. Reset Position
                Dices[i].position = initialDicePositions[i];
        
                // 2. IMPORTANT: Disable Kinematic BEFORE applying force
                rb.isKinematic = false; 
                rb.useGravity = true;

                // 3. Force the Rigidbody to wake up
                rb.WakeUp(); 

                // 4. Clear any leftover velocity from previous rolls
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                // 5. Apply the "Kick"
                Dices[i].rotation = Random.rotation;
        
                // Ensure initialForce is high enough (try 5.0f or more)
                rb.AddForce(new Vector3(Random.Range(-0.5f, 0.5f), 0, initialForce), ForceMode.Impulse);
                rb.AddTorque(new Vector3(Random.Range(10, 50), Random.Range(10, 50), Random.Range(10, 50)), ForceMode.VelocityChange);
            }
        }
        
        /// <summary>
        ///     Simulates the dice throw and plays the recorded animation.
        /// </summary>
        public void SimulateThrow()
        {
            ShowDiceVisuals();
            Physics.autoSimulation = false;
            SetInitialState();
            diceHasThrown = true;
            ClearAnimationData();
            RecordAnimation();
            RotateDices();
            Physics.autoSimulation = true;
            StartCoroutine(PlayAnimation());
        }

        /// <summary>
        ///     Enables physics simulation for the given dice.
        /// </summary>
        /// <param name="dice">The transform of the dice.</param>
        private void EnablePhysics(Transform dice)
        {
            dice.GetComponent<Rigidbody>().isKinematic = false;
            dice.GetComponent<Rigidbody>().useGravity = true;
        }

        /// <summary>
        ///     Disables physics simulation for the given dice.
        /// </summary>
        /// <param name="dice">The transform of the dice.</param>
        private void DisablePhysics(Transform dice)
        {
            dice.GetComponent<Rigidbody>().isKinematic = true;
            dice.GetComponent<Rigidbody>().useGravity = false;
        }

        /// <summary>
        ///     Sets the initial state of the dice, including position, rotation, force, and torque.
        /// </summary>
        public void SetInitialState()
        {
            for (var i = 0; i < Dices.Count; i++)
            {
                Dices[i].position = initialDicePositions[i];
                EnablePhysics(Dices[i]);
                SetInitialRotation(Dices[i]);
                SetInitialForce(Dices[i]);
                SetInitialTorque(Dices[i]);
            }
        }

        /// <summary>
        ///     Sets the initial rotation of the dice based on random values.
        /// </summary>
        /// <param name="dice">The transform of the dice.</param>
        private void SetInitialRotation(Transform dice)
        {
            if (randomizeStartRotation)
            {
                // Generate random values for rotation on each axis
                var x = Random.Range(0f, 360f);
                var y = Random.Range(0f, 360f);
                var z = Random.Range(0f, 360f);

                // Create a Quaternion representing the rotation
                rotation = Quaternion.Euler(x, y, z);
            }
            else
            {
                // Use identity rotation for consistent starting pose
                rotation = Quaternion.identity;
            }

            // Apply the rotation to the dice's transform
            dice.rotation = rotation;
        }


        /// <summary>
        ///     Sets the initial force applied to the dice.
        /// </summary>
        /// <param name="dice">The transform of the dice.</param>
        private void SetInitialForce(Transform dice)
        {
            // 1. Calculate lateral variance (X and Y) from inspector settings
            float x = addRandomVariability ? Random.Range(0f, lateralForceVariance) : 0f;
            float y = addRandomVariability ? Random.Range(0f, verticalForceVariance) : 0f;

            // 2. Calculate forward force magnitude with optional variability
            float z = initialForce;
            if (addRandomVariability)
            {
                z += Random.Range(0f, forceVariabilityRange);
            }

            // 3. Calculate force relative to dice container's orientation
            // Use transform.forward for the main throw direction instead of world Z-axis
            Vector3 forwardForce = transform.forward * z;
            Vector3 lateralVariance = transform.right * x + transform.up * y;
            force = forwardForce + lateralVariance;

            // 4. Set the velocity
            dice.GetComponent<Rigidbody>().linearVelocity = force;
        }

        /// <summary>
        ///     Sets the initial torque applied to the dice.
        /// </summary>
        /// <param name="dice">The transform of the dice.</param>
        private void SetInitialTorque(Transform dice)
        {
            if (randomizeTorque)
            {
                var x = Random.Range(0f, maxTorquePerAxis);
                var y = Random.Range(0f, maxTorquePerAxis);
                var z = Random.Range(0f, maxTorquePerAxis);
                torque = new Vector3(x, y, z);
            }
            else
            {
                // No torque for deterministic rolls
                torque = Vector3.zero;
            }

            dice.GetComponent<Rigidbody>().AddTorque(torque, ForceMode.VelocityChange);
        }

        /// <summary>
        ///     Records the animation data for each dice during the simulation.
        /// </summary>
        private void RecordAnimation()
        {
            for (var i = 0; i <= simulationFrameLength; i++)
            {
                for (var j = 0; j < Dices.Count; j++)
                    diceAnimationData[j].Add(new TransformData(Dices[j].position, Dices[j].rotation));
                Physics.Simulate(Time.fixedDeltaTime);
            }
        }

        /// <summary>
        ///     Plays the recorded animation by updating the dice transforms over time.
        /// </summary>
        /// <returns>An enumerator to control the animation playback.</returns>
        private IEnumerator PlayAnimation()
        {
            for (var i = 0; i <= simulationFrameLength; i++)
            {
                for (var j = 0; j < Dices.Count; j++)
                {
                    Dices[j].transform.position = diceAnimationData[j][i].position;
                    Dices[j].transform.rotation = diceAnimationData[j][i].rotation;
                }

                yield return new WaitForFixedUpdate();
            }
            
            // --- THE FIX ---
            // 2. Once animation is done, FREEZE the physics so they don't drift or spin.
            foreach (var dice in Dices)
            {
                var rb = dice.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // Clear momentum before locking; Unity logs errors if velocity is set while kinematic.
                    if (rb.isKinematic)
                        rb.isKinematic = false;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }
            }

            int d1 = ReadDieValue(0);
            int d2 = Dices.Count > 1 ? ReadDieValue(1) : d1;
            OnDiceRollFinished?.Invoke(d1, d2);
        }

        private int ReadDieValue(int index)
        {
            if (index < 0 || index >= Dices.Count) return 1;
            if (index < manualRollValues.Count)
                return Mathf.Clamp(manualRollValues[index], 1, 6);
            return Dices[index].TryGetComponent(out Dice d) ? Mathf.Clamp(d.GetDiceValue(), 1, 6) : 1;
        }

        /// <summary>Same as clicking the roll button (respects random vs manual).</summary>
        public void RequestRoll()
        {
            OnRollButtonClicked();
        }

        /// <summary>Runtime dice count (clamped 1–5). Resizes manual values and respawns dice.</summary>
        public void SetDiceCount(int count)
        {
            diceCount = Mathf.Clamp(count, 1, 5);
            if (manualRollValues == null) manualRollValues = new List<int>();
            while (manualRollValues.Count < diceCount) manualRollValues.Add(1);
            while (manualRollValues.Count > diceCount) manualRollValues.RemoveAt(manualRollValues.Count - 1);
            SpawnDice();
        }

        /// <summary>Resets spawned dice transforms/rigidbodies to idle state for opening reroll.</summary>
        public void ResetDiceForOpeningReroll()
        {
            ResetAllDiceTransformsToIdleHidden("opening-reroll");
        }

        /// <summary>Hides dice and returns them to cached spawn poses between turns (same physics reset as opening reroll).</summary>
        public void ResetDiceToIdleBetweenTurns()
        {
            ResetAllDiceTransformsToIdleHidden("between-turns");
        }

        /// <summary>
        /// Shows one die at its idle pose with the given face (no roll animation, no <see cref="OnDiceRollFinished"/>).
        /// Used for AI roll display parity with human dice.
        /// </summary>
        public void ApplySettledDisplayValue(int value)
        {
            int v = Mathf.Clamp(value, 1, 6);
            if (manualRollValues != null && manualRollValues.Count > 0)
                manualRollValues[0] = v;
            if (Dices == null || Dices.Count == 0 || initialDicePositions == null || initialDicePositions.Count == 0)
                return;

            Transform die = Dices[0];
            if (die == null) return;

            die.position = initialDicePositions[0];
            die.rotation = Quaternion.identity;

            Rigidbody rb = die.GetComponent<Rigidbody>();
            if (rb != null)
            {
                if (rb.isKinematic)
                    rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            if (die.TryGetComponent(out Dice diceScript)
                && diceScript.diceRotationData != null
                && diceScript.diceRotationData.rotationsForIndexFaces != null
                && v >= 0
                && v < diceScript.diceRotationData.rotationsForIndexFaces.Count)
                diceScript.RotateDice(v);

            ShowDiceVisuals();
            Debug.Log($"[Backgammon][Dice] Apply settled display value={v} (no roll event). manager={name}");
        }

        private void ResetAllDiceTransformsToIdleHidden(string logContext)
        {
            if (Dices == null || initialDicePositions == null) return;
            int count = Mathf.Min(Dices.Count, initialDicePositions.Count);
            for (int i = 0; i < count; i++)
            {
                Transform die = Dices[i];
                if (die == null) continue;
                die.position = initialDicePositions[i];
                die.rotation = Quaternion.identity;

                Rigidbody rb = die.GetComponent<Rigidbody>();
                if (rb == null) continue;
                if (rb.isKinematic)
                    rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            HideDiceVisuals();
            Debug.Log($"[Backgammon][Dice] Dice reset to idle ({logContext}). manager={name} count={count}");
        }

        private void CacheDiceRenderers(Transform dieRoot)
        {
            if (dieRoot == null) return;
            Renderer[] renderers = dieRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || _diceRenderers.Contains(renderer)) continue;
                _diceRenderers.Add(renderer);
            }
        }

        public void ShowDiceVisuals()
        {
            SetDiceVisualsVisible(true);
        }

        public void HideDiceVisuals()
        {
            SetDiceVisualsVisible(false);
        }

        private void SetDiceVisualsVisible(bool visible)
        {
            _diceVisualsVisible = visible;
            for (int i = _diceRenderers.Count - 1; i >= 0; i--)
            {
                Renderer renderer = _diceRenderers[i];
                if (renderer == null)
                {
                    _diceRenderers.RemoveAt(i);
                    continue;
                }

                renderer.enabled = visible;
            }

            Debug.Log($"[Backgammon][Dice] Visuals {(visible ? "shown" : "hidden")}. manager={name} renderers={_diceRenderers.Count}");
        }

        /// <summary>
        ///     Clears the recorded animation data for all dice.
        /// </summary>
        private void ClearAnimationData()
        {
            for (var i = 0; i < Dices.Count; i++)
                if (diceAnimationData[i] != null)
                    diceAnimationData[i].Clear();
        }

        /// <summary>
        ///     Rotates the dices based on the roll values.
        /// </summary>
        private void RotateDices()
        {
            for (int i = 0; i < Dices.Count; i++)
            {
                // Safety check to ensure we don't go out of bounds of the list
                int rollValue = (i < manualRollValues.Count) ? manualRollValues[i] : 1;
        
                Dices[i].GetComponent<Dice>().RotateDice(rollValue);
        
                // Debug to verify the result in the console
                Debug.Log($"Die {i} assigned value: {rollValue}");
            }
        }
    }

    /// <summary>
    ///     Represents the position and rotation of a transform.
    /// </summary>
    internal class TransformData
    {
        public Vector3 position;
        public Quaternion rotation;

        public TransformData(Vector3 position, Quaternion rotation)
        {
            this.position = position;
            this.rotation = rotation;
        }
    }
}