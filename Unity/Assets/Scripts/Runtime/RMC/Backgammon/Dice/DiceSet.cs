using System;
using System.Reflection;
using UnityEngine;

namespace Runtime.RMC._MyProject_.Dice
{
    [Serializable]
    public class DiceSet
    {
        public Sprite dice1;
        public Sprite dice2;
        public Sprite dice3;
        public Sprite dice4;
        public Sprite dice5;
        public Sprite dice6;

        /// <summary>
        /// Initializes the DiceSet by loading Sprites from a specified folder within the Resources directory.
        /// </summary>
        /// <param name="folderName">The name of the folder containing the dice images.</param>
        public void InitializeFromFolder(string folderName)
        {
            Sprite[] loadedImages = Resources.LoadAll<Sprite>(folderName);

            if (loadedImages.Length != 6)
            {
                Debug.LogError($"Failed to load dice images from folder: {folderName}");
                return;
            }

            dice1 = loadedImages[0];
            dice2 = loadedImages[1];
            dice3 = loadedImages[2];
            dice4 = loadedImages[3];
            dice5 = loadedImages[4];
            dice6 = loadedImages[5];
        }

        /// <summary>
        /// Retrieves a Sprite based on a given number.
        /// </summary>
        /// <param name="number">The number corresponding to the desired Sprite.</param>
        /// <returns>The Sprite corresponding to the given number.</returns>
        public Sprite GetSpriteByNumber(int number)
        {
            if (number < 1 || number > 6)
            {
                throw new ArgumentException($"Number must be between 1 and 6. Number: {number}", nameof(number));
            }

            FieldInfo field = GetType().GetField($"dice{number}");
            return (Sprite)field.GetValue(this);
        }
    }
}