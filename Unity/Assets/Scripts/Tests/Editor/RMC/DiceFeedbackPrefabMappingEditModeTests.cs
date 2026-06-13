using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class DiceFeedbackPrefabMappingEditModeTests
{
    [Test]
    public void PrecDiePhysics_HasGeneralResetPickupFeedbackSlot()
    {
        const string prefabPath = "Assets/Prefabs/RMC/MoneySession/PrecDiePhysics.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.IsNotNull(prefab, $"Expected prefab at '{prefabPath}'.");

        MonoBehaviour host = null;
        System.Reflection.FieldInfo slotsField = null;
        var behaviours = prefab.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            var behaviour = behaviours[i];
            if (behaviour == null) continue;
            var candidate = behaviour.GetType().GetField("feedbackSlots");
            if (candidate == null) continue;
            host = behaviour;
            slotsField = candidate;
            break;
        }

        Assert.IsNotNull(host, "Expected DiceFeedbackHost-like MonoBehaviour on prefab root.");
        Assert.IsNotNull(slotsField, "Expected feedbackSlots field on feedback host.");

        var slots = slotsField.GetValue(host) as System.Collections.IEnumerable;
        Assert.IsNotNull(slots, "Expected feedbackSlots enumerable.");

        bool hasGeneralReset = false;
        foreach (object slot in slots)
        {
            if (slot == null) continue;
            var eventTypeField = slot.GetType().GetField("eventType");
            if (eventTypeField == null) continue;
            int eventTypeValue = (int)eventTypeField.GetValue(slot);
            if (eventTypeValue == (int)DiceFeedbackEventType.GeneralDiceResetPickup)
            {
                hasGeneralReset = true;
                break;
            }
        }

        Assert.IsTrue(hasGeneralReset, "Expected feedback slot for GeneralDiceResetPickup (eventType=3).");
    }
}
