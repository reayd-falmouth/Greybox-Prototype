using NUnit.Framework;
using Runtime.RMC.Backgammon;
using UnityEditor;
using UnityEngine;

public class CheckerMaterialPropertyBlockUtilityEditModeTests
{
    private const string CheckerWhiteMatPath = "Assets/Art/Materials/CheckerWhite_Mat.mat";

    [Test]
    public void SetAlbedoAndEmission_WithUrpLitCheckerMaterial_WritesBaseColorAndEmissionToPropertyBlock()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(CheckerWhiteMatPath);
        Assert.IsNotNull(mat, "Missing " + CheckerWhiteMatPath);

        var go = new GameObject("CheckerMaterialPropertyBlockUtilityEditModeTests_temp");
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;

        var baseCol = new Color(0.2f, 0.85f, 1f, 1f);
        var emission = new Color(0.5f, 0.25f, 0.1f, 1f);
        var props = new MaterialPropertyBlock();
        CheckerMaterialPropertyBlockUtility.SetAlbedoAndEmission(props, baseCol, emission, mr);

        Assert.IsTrue(mat.HasProperty("_BaseColor"));
        Assert.AreEqual(baseCol, props.GetColor("_BaseColor"));

        Assert.IsTrue(mat.HasProperty("_Color"));
        Assert.AreEqual(baseCol, props.GetColor("_Color"));

        Assert.IsTrue(mat.HasProperty("_EmissionColor"));
        Assert.AreEqual(emission, props.GetColor("_EmissionColor"));

        Object.DestroyImmediate(go);
    }
}
