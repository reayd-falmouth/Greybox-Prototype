using UnityEngine;

namespace Runtime.RMC.Backgammon
{
    /// <summary>
    /// Shared URP/Built-in Lit MPB helpers for checker albedo and emission.
    /// </summary>
    public static class CheckerMaterialPropertyBlockUtility
    {
        /// <summary>URP/Built-in Lit often mirror albedo in <c>_BaseColor</c> and <c>_Color</c>; set both so MPB fully overrides tint.</summary>
        /// <param name="mrForPropertyCheck">When set, only writes properties that exist on the first non-null shared material.</param>
        public static void SetAlbedoAndEmission(MaterialPropertyBlock props, Color baseCol, Color emission, MeshRenderer mrForPropertyCheck = null)
        {
            Material mat = ResolveMaterialForPropertyCheck(mrForPropertyCheck);
            if (mat == null)
            {
                props.SetColor("_BaseColor", baseCol);
                props.SetColor("_Color", baseCol);
                props.SetColor("_EmissionColor", emission);
                return;
            }

            if (mat.HasProperty("_BaseColor")) props.SetColor("_BaseColor", baseCol);
            if (mat.HasProperty("_Color")) props.SetColor("_Color", baseCol);
            if (mat.HasProperty("_EmissionColor")) props.SetColor("_EmissionColor", emission);
        }

        public static void ApplyPropertyBlock(MeshRenderer mr, MaterialPropertyBlock props)
        {
            if (mr == null) return;
            var mats = mr.sharedMaterials;
            int n = mats != null ? mats.Length : 0;
            if (n <= 1)
                mr.SetPropertyBlock(props);
            else
            {
                for (int mi = 0; mi < n; mi++)
                    mr.SetPropertyBlock(props, mi);
            }
        }

        private static Material ResolveMaterialForPropertyCheck(MeshRenderer mr)
        {
            if (mr == null) return null;
            var mats = mr.sharedMaterials;
            if (mats == null) return null;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null)
                    return mats[i];
            }

            return null;
        }
    }
}
