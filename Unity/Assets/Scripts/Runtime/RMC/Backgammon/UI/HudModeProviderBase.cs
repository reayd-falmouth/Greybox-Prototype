using Runtime.RMC.Backgammon.Core;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Abstract MonoBehaviour base for game-mode HUD providers. Subclass this so Unity
/// can serialise the reference in BackgammonHudController's inspector list.
/// Keep lifecycle methods (Awake/OnEnable) out of this class — all setup happens
/// in BindToHud so providers can be wired before the game starts.
/// </summary>
public abstract class HudModeProviderBase : MonoBehaviour, IHudModeProvider
{
    protected BackgammonGameController GameController;
    protected BackgammonHudController  Hud;
    protected VisualElement            Root;

    public abstract GameModeType SupportedMode { get; }
    public abstract string ScoreDisplay   { get; }
    public abstract string GamesDisplay   { get; }
    public abstract string StakeDisplay   { get; }
    public abstract string HeadingDisplay { get; }

    public virtual void RefreshModeHud(VisualElement root, BackgammonGameController ctrl) { }

    public abstract void StartGame(string seedString, string startingPositionId);

    public virtual void BindToHud(VisualElement root, BackgammonHudController hud)
    {
        Root           = root;
        Hud            = hud;
        GameController = hud.GameControllerRef;
    }

    public virtual void UnbindFromHud()
    {
        Root           = null;
        Hud            = null;
        GameController = null;
    }
}
