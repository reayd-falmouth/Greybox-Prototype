/*
 * Dice.cs
 * 
 * This script is responsible for managing the behavior of a dice object in a game. It handles the rotation of the dice and determines the value of the face that is currently facing up.
 * 
 * The script requires a DiceRotationSo (ScriptableObject) asset to define the rotations for each face of the dice.
 * 
 * The script uses a list of Transform objects to represent the faces of the dice. The diceMesh Transform represents the mesh of the dice.
 * 
 * The totalFaceValue variable is set to 7, as the sum of the opposite face values on a dice is always 7.
 * 
 * The Start() method is called when the script is initialized and sets up the face objects by adding them to the diceFaces list.
 * 
 * The SetFaceObjects() method iterates through the child objects of the dice and adds the face objects to the diceFaces list. It also assigns the diceMesh object.
 * 
 * The GetDiceValue() method determines the value of the face that is currently facing up. It finds the bottom face by comparing the y positions of the faces and subtracts its value from the totalFaceValue to get the face value.
 * 
 * The ResetDiceRotation() method resets the rotation of the dice mesh to zero.
 * 
 * The RotateDice() method rotates the dice mesh to the desired value by using the rotations defined in the diceRotationData asset.
 */

using System.Collections.Generic;
using UnityEngine;

namespace Runtime.RMC._MyProject_.Dice
{
    /// <summary>
    ///     Script responsible for managing the behavior of a dice object in a game.
    /// </summary>
    public class Dice : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private Color bodyColor = Color.white;
        [SerializeField] private Color pipColor = Color.black; // Now you can have white pips!
        [Range(0f, 10f)]
        [SerializeField] private float luminosity = 1f;
        [Range(0f, 1f)] // Add this slider
        [SerializeField] private float alpha = 1f;
        private MeshRenderer _diceRenderer;
        private MaterialPropertyBlock _propBlock;
        
        [Header("Audio Dynamics")]
        [SerializeField] private AudioSource audioSource;
        
        [Tooltip("Defines how Velocity (X-axis) maps to Volume (Y-axis).")]
        [SerializeField] private AnimationCurve impactCurve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.2f, 0.5f), new Keyframe(1f, 1f));

        [Tooltip("The velocity at which the sound plays at maximum volume.")]
        [SerializeField] private float maxVelocityCap = 15f;

        [Header("Pitch Settings")]
        [Tooltip("If true, harder hits sound deeper, lighter hits sound higher.")]
        [SerializeField] private bool scalePitchWithForce = true;
        [SerializeField] private float minPitch = 0.9f;
        [SerializeField] private float maxPitch = 1.1f;

        [Header("Throttling")]
        [SerializeField] private float collisionThreshold = 0.5f;
        [SerializeField] private float soundCooldown = 0.1f;
        
        private List<AudioClip> _currentImpactSounds = new();
        private float _baseProfileVolume = 1f; // Renamed for clarity
        private float _lastSoundTime;
        
        /// <summary>
        ///     A ScriptableObject asset that defines the rotations for each face of the dice.
        /// </summary>
        [SerializeField] public DiceRotationSo diceRotationData;

        /// <summary>
        ///     List of Transform objects representing the faces of the dice.
        /// </summary>
        public List<Transform> diceFaces = new();

        /// <summary>
        ///     Transform representing the mesh of the dice.
        /// </summary>
        public Transform diceMesh;

        /// <summary>
        ///     The sum of the opposite face values on a dice, which is always 7.
        /// </summary>
        private readonly int totalFaceValue = 7; // Every sum of the opposite face values is 7.
        
        /// <summary>
        ///     Called when the script is initialized. Sets up the face objects.
        /// </summary>
        private void Start()
        {
            SetFaceObjects();
            InitializeVisuals();
        }

        private void InitializeVisuals()
        {
            if (diceMesh != null && diceMesh.TryGetComponent(out _diceRenderer))
            {
                _propBlock = new MaterialPropertyBlock();
                ApplyVisuals();
            }
        }
        
        // This allows you to see the color changes in the Inspector in real-time
        private void OnValidate()
        {
            if (_diceRenderer != null) 
                ApplyVisuals();
        }

        public void ApplyVisuals()
        {
            if (_diceRenderer == null) return;
            _diceRenderer.GetPropertyBlock(_propBlock);
    
            _propBlock.SetColor("_BodyColor", bodyColor);
            _propBlock.SetColor("_PipColor", pipColor);
            _propBlock.SetFloat("_Luminosity", luminosity);
    
            // Use the variable from the slider instead of a fixed number
            _propBlock.SetFloat("_Alpha", alpha); 
    
            _diceRenderer.SetPropertyBlock(_propBlock);
        }
        
        public void SetVisuals(Color body, Color pip, float lum, float alp)
        {
            bodyColor = body;
            pipColor = pip;
            luminosity = lum;
            alpha = alp;
    
            // Ensure the renderer is ready and apply immediately
            if (_diceRenderer == null) diceMesh.TryGetComponent(out _diceRenderer);
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
    
            ApplyVisuals();
        }
        
        public void SetAudioProfile(List<AudioClip> clips, float volume)
        {
            _currentImpactSounds = clips;
            _baseProfileVolume = volume;
        }
        
        /// <summary>
        ///     Iterates through the child objects of the dice and adds the face objects to the diceFaces list. Assigns the
        ///     diceMesh object.
        /// </summary>
        private void SetFaceObjects()
        {
            foreach (var face in GetComponentsInChildren<Transform>())
                // Check if the face is not a Rigidbody or a MeshRenderer (excluding diceFaces and diceMesh)
                if (face.GetComponent<Rigidbody>() == null && face.GetComponent<MeshRenderer>() == null)
                    diceFaces.Add(face);
                // Check if the face has a MeshRenderer component (assuming it represents the dice mesh)
                else if (face.TryGetComponent(out MeshRenderer meshRenderer))
                    diceMesh = face;
        }

        /// <summary>
        ///     Determines the value of the face that is currently facing up.
        /// </summary>
        /// <returns>The value of the face that is currently facing up.</returns>
        public int GetDiceValue()
        {
            var bottomFace = diceFaces[0];

            // Find the bottom face by comparing the y positions of the faces
            foreach (var face in diceFaces)
                if (bottomFace.position.y > face.position.y)
                    bottomFace = face;

            var bottomFaceInt = int.Parse(bottomFace.name);
            var faceValue = totalFaceValue - bottomFaceInt; // Calculate the face value
            return faceValue;
        }

        /// <summary>
        ///     Resets the rotation of the dice mesh to zero.
        /// </summary>
        public void ResetDiceRotation()
        {
            diceMesh.rotation = Quaternion.Euler(Vector3.zero);
        }

        /// <summary>
        ///     Rotates the dice mesh to the desired value by using the rotations defined in the diceRotationData asset.
        /// </summary>
        /// <param name="desiredValue">The desired value to rotate the dice to.</param>
        public void RotateDice(int desiredValue)
        {
            ResetDiceRotation();
            diceMesh.rotation = Quaternion.Euler(diceRotationData.rotationsForIndexFaces[desiredValue]);
        }
        
        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time - _lastSoundTime < soundCooldown) return;

            float hitMagnitude = collision.relativeVelocity.magnitude;
            
            // Only play if above threshold
            if (hitMagnitude > collisionThreshold)
            {
                PlayHitSound(hitMagnitude);
                _lastSoundTime = Time.time;
            }
        }

        private void PlayHitSound(float velocity)
        {
            if (audioSource == null || _currentImpactSounds.Count == 0) return;

            AudioClip clipToPlay = _currentImpactSounds[Random.Range(0, _currentImpactSounds.Count)];

            // --- 1. NORMALIZE THE IMPACT ---
            // Convert velocity (e.g., 0 to 20) into a 0.0 to 1.0 range
            float normalizedStrength = Mathf.Clamp01(velocity / maxVelocityCap);

            // --- 2. EVALUATE THE CURVE ---
            // Look up the volume on your graph based on how hard it hit
            float curveVolume = impactCurve.Evaluate(normalizedStrength);

            // --- 3. DYNAMIC PITCH (Optional Realism) ---
            // Hard hits = Deeper (minPitch). Light bounces = Higher (maxPitch).
            if (scalePitchWithForce)
            {
                // Lerp inversely: 1.0 strength = minPitch, 0.0 strength = maxPitch
                audioSource.pitch = Mathf.Lerp(maxPitch, minPitch, normalizedStrength);
            }
            else
            {
                // Classic random pitch
                audioSource.pitch = Random.Range(minPitch, maxPitch);
            }

            // --- 4. FINAL VOLUME CALCULATION ---
            // Combine Curve * Profile Volume
            float finalVolume = curveVolume * _baseProfileVolume;

            audioSource.PlayOneShot(clipToPlay, finalVolume);
        }
    }
}