/*
 *   DiceRotationSo.cs
 *   
 *   Description:
 *   ------------
 *   This script defines a ScriptableObject named 'DiceRotationSo' that is used to store rotation data for each face value of a dice.
 *   
 *   Usage:
 *   ------
 *   1. Attach this script to a GameObject in the Unity Editor.
 *   2. Create an instance of this ScriptableObject by right-clicking in the Project window, selecting 'Create', and choosing 'DiceRotationData'.
 *   3. Customize the rotation data for each dice face value using the 'rotationsForIndexFaces' list.
 *   4. Access the rotation data for a specific dice face value by indexing the 'DiceRotationSo' instance with the desired face value.
 *   
 *   Public Properties:
 *   -----------------
 *   - rotationsForIndexFaces: A List<Vector3> that stores the rotation data for each dice face value. The index numbers represent the dice face values. Index 0 is not used.
 *   
 *   Public Indexer:
 *   ---------------
 *   - this[int diceMgDiceOneRollValue]: Allows accessing the rotation data for a specific dice face value using an integer index.
 *                                        Throws a System.NotImplementedException if accessed (needs implementation).
 *   
 *   Example:
 *   --------
 *   // Create a new instance of DiceRotationSo
 *   DiceRotationSo rotationData = ScriptableObject.CreateInstance<DiceRotationSo>();
 *   
 *   // Customize the rotation data for each dice face value
 *   rotationData.rotationsForIndexFaces[1] = new Vector3(0f, 90f, 0f);   // Face value 1 rotation
 *   rotationData.rotationsForIndexFaces[2] = new Vector3(0f, 0f, 90f);   // Face value 2 rotation
 *   // ...
 *   
 *   // Access the rotation data for a specific dice face value
 *   Vector3 faceOneRotation = rotationData[1];   // Retrieve the rotation data for face value 1
 *   
 *   References:
 *   -----------
 *   - Unity ScriptableObject: https://docs.unity3d.com/ScriptReference/ScriptableObject.html
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.RMC._MyProject_.Dice
{
    /// <summary>
    ///     ScriptableObject that stores rotation data for each dice face value.
    /// </summary>
    [CreateAssetMenu(fileName = "New Dice Rotation Data", menuName = "DiceRotationData")]
    public class DiceRotationSo : ScriptableObject
    {
        /// <summary>
        ///     List of rotations for each dice face value.
        /// </summary>
        public List<Vector3>
            rotationsForIndexFaces =
                new(7); // Index numbers here represent dice face values. Please do not use index 0.

        /// <summary>
        ///     Indexer to access the rotation data for a specific dice face value.
        /// </summary>
        /// <param name="diceMgDiceOneRollValue">The dice face value.</param>
        /// <returns>The rotation data for the specified dice face value.</returns>
        public object this[int diceMgDiceOneRollValue] => throw new NotImplementedException();
    }
}