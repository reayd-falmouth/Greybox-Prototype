using NUnit.Framework;
using Runtime.RMC._MyProject_.Dice;
using UnityEngine;

public class DiceManagerResetEditModeTests
{
    [Test]
    public void ResetDiceForOpeningReroll_RepositionsAndFreezesRigidbodies()
    {
        var managerGo = new GameObject("DiceManager_Test");
        var manager = managerGo.AddComponent<DiceManager>();

        var dieGo = new GameObject("Die_0");
        dieGo.transform.position = new Vector3(5f, 5f, 5f);
        dieGo.transform.rotation = Quaternion.Euler(30f, 40f, 50f);
        var rb = dieGo.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = new Vector3(1f, 2f, 3f);
        rb.angularVelocity = new Vector3(4f, 5f, 6f);

        manager.Dices = new System.Collections.Generic.List<Transform> { dieGo.transform };
        manager.initialDicePositions = new System.Collections.Generic.List<Vector3> { Vector3.zero };

        manager.ResetDiceForOpeningReroll();

        Assert.AreEqual(Vector3.zero, dieGo.transform.position);
        Assert.AreEqual(Quaternion.identity, dieGo.transform.rotation);
        Assert.AreEqual(Vector3.zero, rb.linearVelocity);
        Assert.AreEqual(Vector3.zero, rb.angularVelocity);
        Assert.IsTrue(rb.isKinematic);
        Assert.IsFalse(rb.useGravity);
    }
}
