using NUnit.Framework;
using Runtime.RMC._MyProject_.Dice;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;

public class DiceAudioSafetyEditModeTests
{
    [Test]
    public void PlayHitSound_DisabledAudioSource_LogsGuardWarningWithoutError()
    {
        var go = new GameObject("Dice_AudioSafety_Test");
        var dice = go.AddComponent<Dice>();
        var source = go.AddComponent<AudioSource>();
        source.enabled = false;

        var clip = AudioClip.Create("impact-test", 128, 1, 44100, false);
        dice.SetAudioProfile(new List<AudioClip> { clip }, 1f);
        SetPrivateField(dice, "audioSource", source);

        var playHit = typeof(Dice).GetMethod("PlayHitSound", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(playHit, Is.Not.Null, "Expected PlayHitSound private method.");

        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[Backgammon\]\[Audio\] Skipping dice hit sound.*"));
        playHit.Invoke(dice, new object[] { 3f });

        Object.DestroyImmediate(clip);
        Object.DestroyImmediate(go);
    }

    private static void SetPrivateField<T>(Dice dice, string fieldName, T value)
    {
        var field = typeof(Dice).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}' to exist.");
        field.SetValue(dice, value);
    }
}
