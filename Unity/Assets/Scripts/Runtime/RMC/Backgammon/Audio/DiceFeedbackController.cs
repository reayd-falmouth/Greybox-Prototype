using Runtime.RMC._MyProject_.Dice;
using UnityEngine;

/// <summary>
/// Listens for dice-related feedback events and triggers <see cref="DiceFeedbackHost"/> on spawned dice.
/// </summary>
[DisallowMultipleComponent]
public class DiceFeedbackController : MonoBehaviour
{
    [SerializeField] private BackgammonGameController gameController;
    [SerializeField] private bool enableDebugLogs;

    private void Start()
    {
        if (gameController == null)
            gameController = FindFirstObjectByType<BackgammonGameController>();
        Subscribe();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (gameController == null) return;
        gameController.OnDiceFeedbackEvent -= HandleDiceFeedbackEvent;
        gameController.OnDiceFeedbackEvent += HandleDiceFeedbackEvent;
    }

    private void Unsubscribe()
    {
        if (gameController == null) return;
        gameController.OnDiceFeedbackEvent -= HandleDiceFeedbackEvent;
    }

    private void HandleDiceFeedbackEvent(DiceFeedbackEventData data)
    {
        if (gameController == null) return;

        switch (data.EventType)
        {
            case DiceFeedbackEventType.OpeningRollTieAutodouble:
                PlayOnAllOpeningDice(data);
                break;
            default:
                if (enableDebugLogs)
                    Debug.LogWarning($"[Backgammon][DiceFeedback] No routing for {data.EventType}");
                break;
        }
    }

    private void PlayOnAllOpeningDice(DiceFeedbackEventData data)
    {
        DiceManager p0 = gameController.DiceManagerPlayer0;
        DiceManager p1 = gameController.DiceManagerPlayer1;
        if (p0 == null || p1 == null)
        {
            Debug.LogWarning("[Backgammon][DiceFeedback] OpeningRollTieAutodouble: missing DiceManager reference.");
            return;
        }

        int played = 0;
        played += TryPlayOnManagerDice(p0, data.EventType);
        played += TryPlayOnManagerDice(p1, data.EventType);

        if (enableDebugLogs)
            Debug.Log($"[Backgammon][DiceFeedback] OpeningRollTieAutodouble played={played} cubeAfter={data.CubeValueAfter}");
    }

    private static int TryPlayOnManagerDice(DiceManager manager, DiceFeedbackEventType eventType)
    {
        if (manager?.Dices == null) return 0;
        int n = 0;
        for (int i = 0; i < manager.Dices.Count; i++)
        {
            Transform t = manager.Dices[i];
            if (t == null) continue;
            if (!t.TryGetComponent(out DiceFeedbackHost host))
                continue;
            if (host.TryPlay(eventType))
                n++;
        }

        return n;
    }
}
