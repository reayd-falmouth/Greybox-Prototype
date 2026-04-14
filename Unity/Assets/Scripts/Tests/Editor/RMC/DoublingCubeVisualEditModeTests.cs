using NUnit.Framework;
using System.Reflection;
using UnityEngine;

public class DoublingCubeVisualEditModeTests
{
    [Test]
    public void GetLocalRotationForCubeValue_Show64OnTop_ValueOne_MatchesValue64()
    {
        Quaternion forOne = DoublingCubeVisual.GetLocalRotationForCubeValue(
            1,
            DoublingCubeVisual.CubeOneDisplayMode.Show64OnTop);
        Quaternion forSixtyFour = DoublingCubeVisual.GetLocalRotationForCubeValue(
            64,
            DoublingCubeVisual.CubeOneDisplayMode.Show64OnTop);

        Assert.That(Quaternion.Dot(forOne, forSixtyFour), Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void GetLocalRotationForCubeValue_Show64OnTop_Value64_HasExpectedRotation()
    {
        Quaternion rotation = DoublingCubeVisual.GetLocalRotationForCubeValue(
            64,
            DoublingCubeVisual.CubeOneDisplayMode.Show64OnTop);

        Quaternion expected = Quaternion.Euler(90f, 0f, 0f);
        Assert.That(Quaternion.Dot(rotation, expected), Is.EqualTo(1f).Within(0.0001f));
    }

    [TestCase(2)]
    [TestCase(4)]
    [TestCase(8)]
    [TestCase(16)]
    [TestCase(32)]
    [TestCase(64)]
    public void GetLocalRotationForCubeValue_SupportedValues_ReturnFiniteQuaternion(int cubeValue)
    {
        Quaternion rotation = DoublingCubeVisual.GetLocalRotationForCubeValue(
            cubeValue,
            DoublingCubeVisual.CubeOneDisplayMode.Show64OnTop);

        Assert.That(float.IsFinite(rotation.x), Is.True);
        Assert.That(float.IsFinite(rotation.y), Is.True);
        Assert.That(float.IsFinite(rotation.z), Is.True);
        Assert.That(float.IsFinite(rotation.w), Is.True);
    }

    [Test]
    public void ShouldAnimateCubeValueTransition_ChangedValue_ReturnsTrue()
    {
        bool shouldAnimate = DoublingCubeVisual.ShouldAnimateCubeValueTransition(2, 4);
        Assert.That(shouldAnimate, Is.True);
    }

    [Test]
    public void ShouldAnimateCubeValueTransition_UnchangedValue_ReturnsFalse()
    {
        bool shouldAnimate = DoublingCubeVisual.ShouldAnimateCubeValueTransition(8, 8);
        Assert.That(shouldAnimate, Is.False);
    }

    [Test]
    public void CanAnimateCubeValueTransition_FirstApply_ReturnsFalse()
    {
        bool shouldAnimate = DoublingCubeVisual.CanAnimateCubeValueTransition(
            preferAnimation: true,
            hasAppliedCubeValue: false,
            previousCubeValue: 2,
            nextCubeValue: 4,
            durationSeconds: 0.2f);
        Assert.That(shouldAnimate, Is.False);
    }

    [Test]
    public void CanAnimateCubeValueTransition_ZeroDuration_ReturnsFalse()
    {
        bool shouldAnimate = DoublingCubeVisual.CanAnimateCubeValueTransition(
            preferAnimation: true,
            hasAppliedCubeValue: true,
            previousCubeValue: 2,
            nextCubeValue: 4,
            durationSeconds: 0f);
        Assert.That(shouldAnimate, Is.False);
    }

    [Test]
    public void CanAnimateCubeValueTransition_ChangedValueAndPositiveDuration_ReturnsTrue()
    {
        bool shouldAnimate = DoublingCubeVisual.CanAnimateCubeValueTransition(
            preferAnimation: true,
            hasAppliedCubeValue: true,
            previousCubeValue: 2,
            nextCubeValue: 4,
            durationSeconds: 0.2f);
        Assert.That(shouldAnimate, Is.True);
    }

    [Test]
    public void ComputeAutoLabelScale_ReturnsFiniteWithinBounds()
    {
        float scale = DoublingCubeVisual.ComputeAutoLabelScale(
            0.5f,
            "64",
            0.72f,
            0.09f,
            0.22f);

        Assert.That(float.IsFinite(scale), Is.True);
        Assert.That(scale, Is.GreaterThanOrEqualTo(0.09f));
        Assert.That(scale, Is.LessThanOrEqualTo(0.22f));
    }

    [Test]
    public void ComputeAutoLabelScale_MultiDigit_IsSmallerThanSingleDigit()
    {
        float single = DoublingCubeVisual.ComputeAutoLabelScale(
            0.5f,
            "8",
            0.72f,
            0.01f,
            1f);

        float multi = DoublingCubeVisual.ComputeAutoLabelScale(
            0.5f,
            "64",
            0.72f,
            0.01f,
            1f);

        Assert.That(multi, Is.LessThan(single));
    }

    [Test]
    public void ComputeAutoLabelScale_LargerCube_ProducesLargerScale()
    {
        float smallCube = DoublingCubeVisual.ComputeAutoLabelScale(
            0.4f,
            "16",
            0.72f,
            0.01f,
            1f);
        float largeCube = DoublingCubeVisual.ComputeAutoLabelScale(
            0.8f,
            "16",
            0.72f,
            0.01f,
            1f);

        Assert.That(largeCube, Is.GreaterThan(smallCube));
    }

    [Test]
    public void GetCameraUprightRotation_Value2_PlacesFaceOnTop()
    {
        Quaternion rotation = DoublingCubeVisual.GetCameraUprightRotation(2, Vector3.up, Vector3.forward);
        Vector3 top = rotation * Vector3.forward;
        Assert.That(Vector3.Dot(top.normalized, Vector3.up), Is.GreaterThan(0.999f));
    }

    [Test]
    public void GetCameraUprightRotation_IsDeterministicAndFinite()
    {
        Quaternion a = DoublingCubeVisual.GetCameraUprightRotation(16, new Vector3(0.1f, 0.99f, 0.1f), Vector3.forward);
        Quaternion b = DoublingCubeVisual.GetCameraUprightRotation(16, new Vector3(0.1f, 0.99f, 0.1f), Vector3.forward);
        Assert.That(float.IsFinite(a.x) && float.IsFinite(a.y) && float.IsFinite(a.z) && float.IsFinite(a.w), Is.True);
        Assert.That(Quaternion.Dot(a, b), Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void GetLocalRotationFromAuthoredFace_IdentityFace_PlacesFaceForwardOnTop()
    {
        var root = new GameObject("Root");
        var face = new GameObject("64");
        try
        {
            face.transform.SetParent(root.transform, false);
            face.transform.localRotation = Quaternion.identity;

            Quaternion rotation = DoublingCubeVisual.GetLocalRotationFromAuthoredFace(face.transform, Vector3.forward);
            Vector3 topNormal = rotation * (face.transform.localRotation * Vector3.forward);
            Assert.That(Vector3.Dot(topNormal.normalized, Vector3.up), Is.GreaterThan(0.999f));
        }
        finally
        {
            Object.DestroyImmediate(face);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void GetLocalRotationFromAuthoredFace_NullFace_ReturnsIdentity()
    {
        Quaternion rotation = DoublingCubeVisual.GetLocalRotationFromAuthoredFace(null, Vector3.forward);
        Assert.That(Quaternion.Dot(rotation, Quaternion.identity), Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void GetAuthoredFaceBasis_UsesPositionForNormal_WhenPositionAvailable()
    {
        var go = new GameObject("2");
        try
        {
            go.transform.localPosition = new Vector3(1f, 0f, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            DoublingCubeVisual.AuthoredFaceBasis basis = DoublingCubeVisual.GetAuthoredFaceBasis(go.transform);
            Assert.That(basis.normalSource, Is.EqualTo("position"));
            Assert.That(Vector3.Dot(basis.localFaceNormal, Vector3.right), Is.GreaterThan(0.999f));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void GetAuthoredFaceBasis_UsesRotationFallback_WhenPositionIsZero()
    {
        var go = new GameObject("4");
        try
        {
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            DoublingCubeVisual.AuthoredFaceBasis basis = DoublingCubeVisual.GetAuthoredFaceBasis(go.transform);
            Assert.That(basis.normalSource, Is.EqualTo("rotation-fallback"));
            Assert.That(Vector3.Dot(basis.localFaceNormal, Vector3.right), Is.GreaterThan(0.999f));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void GetAuthoredFaceBasis_RectTransform_UsesAnchoredPositionWhenLocalPositionIsZero()
    {
        var root = new GameObject("Root", typeof(RectTransform));
        var go = new GameObject("2", typeof(RectTransform));
        try
        {
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(root.transform, false);
            rect.localPosition = Vector3.zero;
            rect.anchoredPosition3D = new Vector3(1f, 0f, 0f);
            rect.localRotation = Quaternion.Euler(0f, 90f, 0f);

            DoublingCubeVisual.AuthoredFaceBasis basis = DoublingCubeVisual.GetAuthoredFaceBasis(rect);
            Assert.That(basis.normalSource, Is.EqualTo("position(rect-aware)"));
            Assert.That(Vector3.Dot(basis.localFaceNormal, Vector3.right), Is.GreaterThan(0.999f));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void GetLocalRotationFromAuthoredFace_SameRotationOppositePosition_ProducesDifferentRotations()
    {
        var root = new GameObject("Root");
        var faceA = new GameObject("FaceA");
        var faceB = new GameObject("FaceB");
        try
        {
            faceA.transform.SetParent(root.transform, false);
            faceB.transform.SetParent(root.transform, false);
            faceA.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            faceB.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            faceA.transform.localPosition = new Vector3(1f, 0f, 0f);
            faceB.transform.localPosition = new Vector3(-1f, 0f, 0f);

            Quaternion a = DoublingCubeVisual.GetLocalRotationFromAuthoredFace(faceA.transform, Vector3.forward);
            Quaternion b = DoublingCubeVisual.GetLocalRotationFromAuthoredFace(faceB.transform, Vector3.forward);

            Assert.That(Quaternion.Dot(a, b), Is.LessThan(0.999f));
        }
        finally
        {
            Object.DestroyImmediate(faceA);
            Object.DestroyImmediate(faceB);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void EnsureLabels_CreatesSixUniqueFaceLabels()
    {
        var go = new GameObject("DoublingCubeVisualTest");
        try
        {
            var visual = go.AddComponent<DoublingCubeVisual>();
            typeof(DoublingCubeVisual)
                .GetField("visualRoot", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(visual, go.transform);

            typeof(DoublingCubeVisual)
                .GetMethod("EnsureLabels", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(visual, null);

            string[] expected = { "FaceLabel_2", "FaceLabel_4", "FaceLabel_8", "FaceLabel_16", "FaceLabel_32", "FaceLabel_64" };
            Assert.That(go.transform.childCount, Is.EqualTo(expected.Length));
            foreach (string name in expected)
                Assert.That(go.transform.Find(name), Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void DebugApplySpecificValue_ValueOne_NormalizesToSixtyFour()
    {
        var go = new GameObject("DoublingCubeVisualDebugTest");
        try
        {
            var visual = go.AddComponent<DoublingCubeVisual>();
            typeof(DoublingCubeVisual)
                .GetField("visualRoot", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(visual, go.transform);

            visual.DebugApplySpecificValue(1, false);

            var normalizedField = typeof(DoublingCubeVisual).GetField("lastNormalizedCubeValue", BindingFlags.Instance | BindingFlags.NonPublic);
            int normalized = normalizedField != null ? (int)normalizedField.GetValue(visual) : -1;
            Assert.That(normalized, Is.EqualTo(64));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void DebugApplySpecificValue_UpdatesDebugReadoutFields()
    {
        var go = new GameObject("DoublingCubeVisualDebugReadoutTest");
        try
        {
            var visual = go.AddComponent<DoublingCubeVisual>();
            typeof(DoublingCubeVisual)
                .GetField("visualRoot", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(visual, go.transform);

            visual.DebugApplySpecificValue(8, false);

            var requestedField = typeof(DoublingCubeVisual).GetField("lastRequestedCubeValue", BindingFlags.Instance | BindingFlags.NonPublic);
            var sourceField = typeof(DoublingCubeVisual).GetField("lastRotationSource", BindingFlags.Instance | BindingFlags.NonPublic);
            int requested = requestedField != null ? (int)requestedField.GetValue(visual) : -1;
            string source = sourceField != null ? (string)sourceField.GetValue(visual) : string.Empty;

            Assert.That(requested, Is.EqualTo(8));
            Assert.That(string.IsNullOrWhiteSpace(source), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
