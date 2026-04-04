using System.Collections.Generic;
using UnityEngine;

namespace Runtime.RMC._MyProject_.Dice
{
    [CreateAssetMenu(fileName = "NewDiceAudioProfile", menuName = "Dice/Audio Profile")]
    public class DiceAudioProfileSo : ScriptableObject
    {
        [Header("Collision Sounds")]
        [Tooltip("Drag all your .wav files for this surface type here.")]
        public List<AudioClip> impactClips = new List<AudioClip>();

        [Header("Volume Settings")]
        [Range(0f, 1f)]
        public float baseVolume = 1f;
    }
}