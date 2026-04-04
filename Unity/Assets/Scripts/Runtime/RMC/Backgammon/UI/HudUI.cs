using UnityEngine;
using UnityEngine.UIElements;

namespace RMC.MyProject.UI
{
    /// <summary>
    /// Renders the <see cref="PlayerData"/> in the Unity Game Window.
    /// </summary>
    public class HudUI : MonoBehaviour
    {
        //  Properties ------------------------------------
        public Label UpperLeftLabel { get { return _uiDocument?.rootVisualElement.Q<Label>("UpperLeftLabel"); }}
        public Label UpperRightLabel { get { return _uiDocument?.rootVisualElement.Q<Label>("UpperRightLabel"); }}
        public Label LowerLeftLabel { get { return _uiDocument?.rootVisualElement.Q<Label>("LowerLeftLabel"); }}
        public Label LowerRightLabel { get { return _uiDocument?.rootVisualElement.Q<Label>("LowerRightLabel"); }}
        
        public VisualElement LowerRightVisualElement { get { return _uiDocument?.rootVisualElement.Q<VisualElement>("LowerRightVisualElement"); }}
        public TemplateContainer PlayerDataLayout { get { return _uiDocument?.rootVisualElement.Q<TemplateContainer>("PlayerDataLayout"); }}

        //  Fields ----------------------------------------
        [SerializeField]
        private UIDocument _uiDocument;


        //  Unity Methods ---------------------------------
        protected void Awake()
        {
            //This must happen in Awake
            // AddPlayerDataLayout();
        }

        protected void Start()
        {
            Debug.Log($"{GetType().Name}.Start()");
            
        }

        //  Methods ---------------------------------------
        public void SetLives(string message)
        {
            UpperLeftLabel.text = message;
        }
        
        public void SetScore(string message)
        {
            UpperRightLabel.text = message;
        }
        
        public void SetInstructions(string message)
        {
            LowerLeftLabel.text = message;
        }
        
        public void SetTitle(string message)
        {
            LowerRightLabel.text = message;
        }

        // private void AddPlayerDataLayout()
        // {
        //     var visualTree = Resources.Load<VisualTreeAsset>("Layouts/PlayerDataLayout");
        //     VisualElement _playerDataLayout = visualTree.CloneTree();
        //     _playerDataLayout.name = "PlayerDataLayout";
        //     LowerRightVisualElement.Add(_playerDataLayout);
        //     
        //     // Load the shared style sheet
        //     var styleSheet = Resources.Load<StyleSheet>("Styles/TemplateStyles");
        //     if (styleSheet != null)
        //     {
        //         Debug.Log("Added");
        //         _playerDataLayout.styleSheets.Add(styleSheet);
        //     }
        //     
        //     //Setup  Events - Such that WASD/arrows don't accidentally focus on this UI
        //     FloatField _moveSpeedField = _playerDataLayout.Q<FloatField>("MoveSpeed");
        //     FloatField _jumpSpeedField = _playerDataLayout.Q<FloatField>("JumpSpeed");
        //     
        //     _moveSpeedField.focusable = false;
        //     _jumpSpeedField.focusable = false;
        //     
        //     //Clicks = focus
        //     _moveSpeedField.RegisterCallback<MouseCaptureEvent>(evt =>
        //     {
        //         _moveSpeedField.focusable = true;
        //     });
        //
        //     _jumpSpeedField.RegisterCallback<MouseCaptureEvent>(evt =>
        //     {
        //         _jumpSpeedField.focusable = true;
        //     });
        //     
        //     //Keys = no focus
        //     _moveSpeedField.RegisterCallback<KeyDownEvent>(evt =>
        //     {
        //         _moveSpeedField.focusable = false;
        //     });
        //
        //     _jumpSpeedField.RegisterCallback<KeyDownEvent>(evt =>
        //     {
        //         _jumpSpeedField.focusable = false;
        //     });
        // }

        //  Event Handlers --------------------------------
    }
}