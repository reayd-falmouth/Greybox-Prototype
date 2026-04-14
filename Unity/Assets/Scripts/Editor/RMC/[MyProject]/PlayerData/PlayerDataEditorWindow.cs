// using UnityEditor;
// using UnityEditor.UIElements;
// using UnityEngine;
// using UnityEngine.UIElements;
//
// namespace RMC.MyProject.Data
// {
//     /// <summary>
//     /// Renders the <see cref="PlayerData"/> in the Unity Custom Editor Window.
//     /// </summary>
//     public class PlayerDataEditorWindow : EditorWindow 
//     {
//         //  Properties ------------------------------------
//
//
//         //  Const -----------------------------------------
//         private const string PathMenuItemWindowCompanyProject = "Window/" + CompanyName + "/" + ProjectName;
//         private const string CompanyName = "RMC";
//         private const string ProjectName = "[MyProject]";
//         private const int PriorityMenuItem = -100;
//
//         //  Fields ----------------------------------------
//         [SerializeField]
//         private PlayerData _playerData;
//         
//         private SerializedObject serializedPlayerData;
//         private TemplateContainer _playerDataLayout;
//
//         //  Methods ---------------------------------------
//         [MenuItem( PathMenuItemWindowCompanyProject + "/" + "Open SampleEditorWindow", 
//             false,
//             PriorityMenuItem)]
//         public static void ShowWindow()
//         {
//             var window = GetWindow<PlayerDataEditorWindow>();
//             window.titleContent = new GUIContent("Player Data Editor");
//         }
//         
//         
//         //  Unity Methods ---------------------------------
//         private void OnEnable()
//         {
//             // Load and clone the UXML layout
//             var visualTree = Resources.Load<VisualTreeAsset>("Layouts/PlayerDataLayout");
//             _playerDataLayout = visualTree.CloneTree();
//             rootVisualElement.Add(_playerDataLayout);
//             
//             // Bind PlayerData
//             BindPlayerData();
//
//         }
//
//         
//         private void BindPlayerData()
//         {
//             // Create a dummy player data object to display in the editor
//             _playerData = new PlayerData { MoveSpeed = 5.0f, JumpSpeed = 3.0f };
//
//             Player player = GameObject.FindAnyObjectByType<Player>();
//
//             if (player == null)
//             {
//                 Debug.LogError($"BindPlayerData() failed. Add '{nameof(Player)}' Component to Scene.");
//                 return;
//             }
//             serializedPlayerData = new SerializedObject(player);
//             serializedPlayerData.Update();
//
//             // Bind the serialized object to the UI fields
//             _playerDataLayout.Bind(serializedPlayerData);
//
//             // Bind individual fields if needed
//             _playerDataLayout.Q<FloatField>("MoveSpeed").BindProperty(serializedPlayerData.FindProperty("_playerData.MoveSpeed"));
//             _playerDataLayout.Q<FloatField>("JumpSpeed").BindProperty(serializedPlayerData.FindProperty("_playerData.JumpSpeed"));
//         }
//
//
//         //  Event Handlers --------------------------------
//     }
// }