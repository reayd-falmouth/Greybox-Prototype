using UnityEngine;

namespace Runtime.RMC.Backgammon.Scenes
{
    /// <summary>Scene hook: assign <see cref="BackgammonGameController"/>, board, dice, and UIDocument source <c>BackgammonHUD</c> in the Inspector.</summary>
    public class Scene01_Intro : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log($"{nameof(Scene01_Intro)}: backgammon scene — ensure UIDocument uses Resources/Layouts/BackgammonHUD.");
        }
    }
}
