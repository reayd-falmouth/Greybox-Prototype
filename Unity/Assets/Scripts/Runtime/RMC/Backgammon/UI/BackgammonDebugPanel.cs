using Runtime.RMC.Backgammon.Settings;
using UnityEngine;

/// <summary>In-play debug controls (stripped from non-editor / non-dev builds).</summary>
public class BackgammonDebugPanel : MonoBehaviour
{
    [SerializeField] private BackgammonGameController gameController;
    [SerializeField] private bool showPanel = true;

    private string _d1Str = "6";
    private string _d2Str = "4";

    private void OnValidate()
    {
        if (gameController == null)
            gameController = FindFirstObjectByType<BackgammonGameController>();
    }

    private void OnGUI()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#else
        if (!showPanel || gameController == null) return;

        GUILayout.BeginArea(new Rect(12, Screen.height * 0.35f, 280, 320), GUI.skin.box);
        GUILayout.Label("Backgammon debug");
        _d1Str = GUILayout.TextField(_d1Str, 1);
        _d2Str = GUILayout.TextField(_d2Str, 1);
        if (GUILayout.Button("Set dice + refresh"))
        {
            if (int.TryParse(_d1Str, out int a) && int.TryParse(_d2Str, out int b))
                gameController.DebugSetDiceAndRefresh(a, b);
        }

        if (GUILayout.Button("Force pass (no moves)"))
            gameController.DebugForcePassTurn();

        if (GUILayout.Button("Print board (console)"))
            gameController.DebugPrintBoardConsole();

        GUILayout.Label($"AI depth: {BackgammonSettings.AiSearchDepth}");
        GUILayout.Label($"Opponent AI: {BackgammonSettings.OpponentIsAi}");
        GUILayout.EndArea();
#endif
    }
}
