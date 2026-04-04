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
        
        // --- PHYSICS & FEEL ---
        [Header("Physics Tuning")]
        [Tooltip("The base impulse force applied to the throw.")]
        [Range(12.5f, 45.0f)]
        public float initialForce = 35.0f;

        [Tooltip("Adds a random variance to the force so every throw feels unique.")]
        [SerializeField] private bool addRandomVariability = true;

        [Tooltip("The maximum extra random force added if variability is enabled.")]
        [Range(0.0f, 5.0f)]
        [SerializeField] private float variabilityRange = 2.5f;

        [Header("Simulation Playback")]
        [Tooltip("Total physics steps to record for pre-calculation (higher = longer roll).")]
        public int simulationFrameLength = 100; // Fixed typo from 'Lenght'
        
        // --- INTERNAL DATA (Hidden or Grouped at bottom) ---
        [Header("Scene References & Data")]
        [Tooltip("Populated automatically at Runtime.")]
        public List<Transform> Dices = new();
        
        [HideInInspector] 
        public List<Vector3> initialDicePositions = new();

        /// <summary>Fired after playback completes with the two dice values (die1, die2).</summary>
        public event System.Action<int, int> OnDiceRollFinished;

        private Button _rollButton;
        private readonly Dictionary<int, List<TransformData>> diceAnimationData = new();
        private bool diceHasThrown;
        private Vector3 force;
        private Quaternion rotation;
        private Vector3 torque;
        private List<Transform> spawnedDice = new();

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
            SpawnDice();
        }
        
        private void SpawnDice()
        {
            // 1. Cleanup
            foreach (var d in spawnedDice) if (d != null) Destroy(d.gameObject);
            spawnedDice.Clear();
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
        
                DisablePhysics(go.transform);
            }

            Dices = spawnedDice; 
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
            // Generate random values for rotation on each axis
            var x = Random.Range(0f, 360f);
            var y = Random.Range(0f, 360f);
            var z = Random.Range(0f, 360f);

            // Create a Quaternion representing the rotation
            rotation = Quaternion.Euler(x, y, z);

            // Apply the rotation to the dice's transform
            dice.rotation = rotation;
        }


        /// <summary>
        ///     Sets the initial force applied to the dice.
        /// </summary>
        /// <param name="dice">The transform of the dice.</param>
        private void SetInitialForce(Transform dice)
        {
            // 1. Randomize the slight horizontal deviation
            var x = Random.Range(0f, 0.1f);
            var y = Random.Range(0f, 0.1f);

            // 2. Calculate the magnitude based on your slider and variability
            float z = initialForce;
            if (addRandomVariability)
            {
                z += Random.Range(0f, variabilityRange);
            }

            // 3. Apply the calculated finalForce to the Z axis
            force = new Vector3(x, y, z);
    
            // 4. Set the velocity
            dice.GetComponent<Rigidbody>().linearVelocity = force;
        }

        /// <summary>
        ///     Sets the initial torque applied to the dice.
        /// </summary>
        /// <param name="dice">The transform of the dice.</param>
        private void SetInitialTorque(Transform dice)
        {
            var x = Random.Range(0f, 25f);
            var y = Random.Range(0f, 25f);
            var z = Random.Range(0f, 25f);

            torque = new Vector3(x, y, z);
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
                    rb.isKinematic = true; // Locks the physics
                    rb.linearVelocity = Vector3.zero; // Clears momentum
                    rb.angularVelocity = Vector3.zero; // Clears spin
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