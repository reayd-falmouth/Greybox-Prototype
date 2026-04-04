using UnityEngine;

namespace RMC.MyProject
{

    /// <summary>
    /// Represent the Player in the game world.
    /// </summary>
    public class Player : MonoBehaviour
    {
        //  Properties ------------------------------------
        public Rigidbody Rigidbody { get { return _rigidBody; }}
        public PlayerData PlayerData { get { return _playerData; }}

        
        //  Fields ----------------------------------------
        [Header("Player")]
        [SerializeField]
        private Rigidbody _rigidBody;

        [Header("Player Data")]
        [SerializeField]
        private PlayerData _playerData;

        
        //  Unity Methods ---------------------------------
        protected void Start()
        {
            Debug.Log($"{GetType().Name}.Start()");
        }

        
        //  Methods ---------------------------------------
        

        //  Event Handlers --------------------------------
    }
}