using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class BackgammonAiTimingLogsEditModeTests
{
    // BuildAiTimingLogLine and LogAiTiming moved to BackgammonAiTurnManager (internal class).
    private static Type GetAiTurnManagerType()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType("BackgammonAiTurnManager", throwOnError: false);
            if (t != null) return t;
        }
        return null;
    }

    [Test]
    public void BuildAiTimingLogLine_IncludesPhaseMsAndExtra()
    {
        Type managerType = GetAiTurnManagerType();
        Assert.IsNotNull(managerType, "BackgammonAiTurnManager type not found.");
        MethodInfo method = managerType.GetMethod(
            "BuildAiTimingLogLine",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, "Expected static BuildAiTimingLogLine on BackgammonAiTurnManager.");

        string line = method.Invoke(null, new object[] { "search", 123.4d, "depth=2 legal=12" }) as string;
        Assert.IsNotNull(line);
        StringAssert.Contains("[Backgammon][AI][Timing]", line);
        StringAssert.Contains("phase=search", line);
        StringAssert.Contains("ms=123.4", line);
        StringAssert.Contains("depth=2 legal=12", line);
    }

    [Test]
    public void LogAiTiming_WhenDisabled_EmitsNoLog()
    {
        Type managerType = GetAiTurnManagerType();
        Assert.IsNotNull(managerType, "BackgammonAiTurnManager type not found.");
        object manager = Activator.CreateInstance(managerType, new object[] { false });
        MethodInfo method = managerType.GetMethod(
            "LogAiTiming",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, "Expected instance LogAiTiming on BackgammonAiTurnManager.");

        LogAssert.NoUnexpectedReceived();
        method.Invoke(manager, new object[] { "search", 12.3d, "depth=1" });
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void LogAiTiming_WhenEnabled_EmitsStructuredTimingLog()
    {
        Type managerType = GetAiTurnManagerType();
        Assert.IsNotNull(managerType, "BackgammonAiTurnManager type not found.");
        object manager = Activator.CreateInstance(managerType, new object[] { true });
        MethodInfo method = managerType.GetMethod(
            "LogAiTiming",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, "Expected instance LogAiTiming on BackgammonAiTurnManager.");

        LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"\[Backgammon\]\[AI\]\[Timing\] phase=search ms=12\.3 depth=1"));
        method.Invoke(manager, new object[] { "search", 12.3d, "depth=1" });
    }
}
