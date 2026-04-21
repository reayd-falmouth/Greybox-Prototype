using NUnit.Framework;
using Runtime.RMC._MyProject_.Dice;
using System.Reflection;
using UnityEngine;

public class DiceManagerResetEditModeTests
{
    [Test]
    public void SetDiceCount_SpawnedDiceRenderersAreHiddenByDefault()
    {
        var managerGo = new GameObject("DiceManager_Test");
        var manager = managerGo.AddComponent<DiceManager>();
        var floor = new GameObject("Floor");
        floor.transform.localScale = new Vector3(1f, 1f, 1f);
        var diePrefab = CreateDiePrefabForTests();
        ConfigureSpawnFields(manager, diePrefab, floor.transform, 1);

        manager.SetDiceCount(1);

        Assert.That(manager.Dices, Is.Not.Null);
        Assert.That(manager.Dices.Count, Is.EqualTo(1));
        var renderer = manager.Dices[0].GetComponentInChildren<Renderer>();
        Assert.That(renderer, Is.Not.Null);
        Assert.IsFalse(renderer.enabled);

        Object.DestroyImmediate(managerGo);
        Object.DestroyImmediate(floor);
        Object.DestroyImmediate(diePrefab);
    }

    [Test]
    public void ResetDiceForOpeningReroll_RepositionsAndFreezesRigidbodies()
    {
        var managerGo = new GameObject("DiceManager_Test");
        var manager = managerGo.AddComponent<DiceManager>();
        var floor = new GameObject("Floor");
        floor.transform.localScale = new Vector3(1f, 1f, 1f);
        var diePrefab = CreateDiePrefabForTests();
        ConfigureSpawnFields(manager, diePrefab, floor.transform, 1);
        manager.SetDiceCount(1);

        var dieGo = manager.Dices[0].gameObject;
        dieGo.transform.position = new Vector3(5f, 5f, 5f);
        dieGo.transform.rotation = Quaternion.Euler(30f, 40f, 50f);
        var rb = dieGo.GetComponent<Rigidbody>();
        Assert.That(rb, Is.Not.Null);
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = new Vector3(1f, 2f, 3f);
        rb.angularVelocity = new Vector3(4f, 5f, 6f);

        manager.ResetDiceForOpeningReroll();

        Assert.AreEqual(Vector3.zero, dieGo.transform.position);
        Assert.AreEqual(Quaternion.identity, dieGo.transform.rotation);
        Assert.AreEqual(Vector3.zero, rb.linearVelocity);
        Assert.AreEqual(Vector3.zero, rb.angularVelocity);
        Assert.IsTrue(rb.isKinematic);
        Assert.IsFalse(rb.useGravity);
        var renderer = dieGo.GetComponentInChildren<Renderer>();
        Assert.That(renderer, Is.Not.Null);
        Assert.IsFalse(renderer.enabled);

        Object.DestroyImmediate(managerGo);
        Object.DestroyImmediate(floor);
        Object.DestroyImmediate(diePrefab);
    }

    [Test]
    public void ResetDiceToIdleBetweenTurns_RepositionsAndFreezesRigidbodies()
    {
        var managerGo = new GameObject("DiceManager_Test");
        var manager = managerGo.AddComponent<DiceManager>();
        var floor = new GameObject("Floor");
        floor.transform.localScale = new Vector3(1f, 1f, 1f);
        var diePrefab = CreateDiePrefabForTests();
        ConfigureSpawnFields(manager, diePrefab, floor.transform, 1);
        manager.SetDiceCount(1);

        var dieGo = manager.Dices[0].gameObject;
        dieGo.transform.position = new Vector3(5f, 5f, 5f);
        dieGo.transform.rotation = Quaternion.Euler(30f, 40f, 50f);
        var rb = dieGo.GetComponent<Rigidbody>();
        Assert.That(rb, Is.Not.Null);
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = new Vector3(1f, 2f, 3f);
        rb.angularVelocity = new Vector3(4f, 5f, 6f);

        manager.ResetDiceToIdleBetweenTurns();

        Assert.AreEqual(Vector3.zero, dieGo.transform.position);
        Assert.AreEqual(Quaternion.identity, dieGo.transform.rotation);
        Assert.AreEqual(Vector3.zero, rb.linearVelocity);
        Assert.AreEqual(Vector3.zero, rb.angularVelocity);
        Assert.IsTrue(rb.isKinematic);
        Assert.IsFalse(rb.useGravity);
        var renderer = dieGo.GetComponentInChildren<Renderer>();
        Assert.That(renderer, Is.Not.Null);
        Assert.IsFalse(renderer.enabled);

        Object.DestroyImmediate(managerGo);
        Object.DestroyImmediate(floor);
        Object.DestroyImmediate(diePrefab);
    }

    private static GameObject CreateDiePrefabForTests()
    {
        var diePrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        diePrefab.name = "DiePrefab_Test";
        diePrefab.AddComponent<Rigidbody>();
        return diePrefab;
    }

    private static void ConfigureSpawnFields(DiceManager manager, GameObject diePrefab, Transform floorTransform, int diceCount)
    {
        SetPrivateField(manager, "dicePrefab", diePrefab);
        SetPrivateField(manager, "floorTransform", floorTransform);
        SetPrivateField(manager, "diceCount", diceCount);
        SetPrivateField(manager, "boardFillAmount", 0.8f);
        SetPrivateField(manager, "baseLocalPosition", Vector3.zero);
    }

    private static void SetPrivateField<T>(DiceManager manager, string fieldName, T value)
    {
        var field = typeof(DiceManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}' to exist.");
        field.SetValue(manager, value);
    }
}
