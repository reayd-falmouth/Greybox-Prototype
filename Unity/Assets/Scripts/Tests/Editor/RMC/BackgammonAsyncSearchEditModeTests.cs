using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using EngineCore;
using NUnit.Framework;
using Runtime.RMC.Backgammon.Settings;
using UnityEngine;

public class BackgammonAsyncSearchEditModeTests
{
    [Test]
    public void OracleParseHelper_IsRemovedFromRuntimeController()
    {
        MethodInfo parseMethod = typeof(BackgammonGameController).GetMethod(
            "TryExtractOracleField",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNull(parseMethod, "Temporary oracle JSON parsing helper should not remain in runtime controller.");
    }

    [Test]
    public void AiTurn_WaitsOnBackgroundSearchTask()
    {
        bool previousAiMode = BackgammonSettings.OpponentIsAi;
        float previousGameSpeed = BackgammonSettings.GameSpeedSecondsPerStep;
        var go = new GameObject("BackgammonGameController_AsyncSearchWait");
        TaskCompletionSource<Turn> tcs = new TaskCompletionSource<Turn>();
        FieldInfo taskFactoryField = typeof(BackgammonGameController).GetField(
            "AiSearchTaskFactory",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.IsNotNull(taskFactoryField, "Expected test hook for AI search task factory.");
        object previousFactory = taskFactoryField.GetValue(null);

        try
        {
            BackgammonSettings.OpponentIsAi = true;
            BackgammonSettings.GameSpeedSecondsPerStep = 0.05f;
            taskFactoryField.SetValue(null, (Func<SearchEngine, GameState, MatchState, int, Task<Turn>>)((_, _, _, _) => tcs.Task));

            var ctrl = go.AddComponent<BackgammonGameController>();
            SetPrivateField(ctrl, "<Match>k__BackingField", new MatchState());
            ctrl.NewGame();
            ctrl.DebugSetDiceAndRefresh(6, 1);
            ctrl.State.PlayerOnRoll = 0;

            IEnumerator aiTurn = InvokeNonPublicEnumerator(ctrl, "CoAiTurn");
            bool observedWaitingYield = false;
            for (int i = 0; i < 256 && aiTurn.MoveNext(); i++)
            {
                if (aiTurn.Current == null)
                {
                    observedWaitingYield = true;
                    break;
                }
            }

            Assert.IsTrue(observedWaitingYield, "Expected coroutine to yield while awaiting background search task.");
            Assert.IsTrue(ctrl.IsBusy, "AI turn should remain busy while waiting for search completion.");
        }
        finally
        {
            taskFactoryField.SetValue(null, previousFactory);
            BackgammonSettings.OpponentIsAi = previousAiMode;
            BackgammonSettings.GameSpeedSecondsPerStep = previousGameSpeed;
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static IEnumerator InvokeNonPublicEnumerator(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Expected method '{methodName}' to exist.");
        var enumerator = method.Invoke(target, null) as IEnumerator;
        Assert.IsNotNull(enumerator, $"Expected '{methodName}' to return IEnumerator.");
        return enumerator;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected field '{fieldName}' to exist.");
        field.SetValue(target, value);
    }
}
