using UnityEngine;

namespace Runtime.RMC.Backgammon.Theme
{
    [CreateAssetMenu(fileName = "Theme_Default", menuName = "RMC/Backgammon/Theme")]
    public class BackgammonThemeSo : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Default";

        [Header("Checker – Player 1")]
        public Material checker1BaseMaterial;
        public Color checker1BaseColor = new Color(1f, 0.42f, 0f);
        public Color checker1EmissionColor = Color.yellow;
        [Range(0f, 6f)] public float checker1EmissionIntensity = 3.41f;

        [Header("Checker – Player 2")]
        public Material checker2BaseMaterial;
        public Color checker2BaseColor = new Color(0.1f, 0.1f, 0.1f);
        public Color checker2EmissionColor = Color.red;
        [Range(0f, 6f)] public float checker2EmissionIntensity = 2.0f;

        [Header("Movable Highlight")]
        public Color movableHighlightColor = new Color(0.2f, 0.85f, 1f, 1f);

        [Header("Board Points")]
        public Color boardPointDarkColor = new Color(0.1f, 0.1f, 0.1f);
        public Color boardPointLightColor = new Color(0.9f, 0.9f, 0.9f);

        [Header("Doubling Cube")]
        public Material doublingCubeMaterial;
        public Color doublingCubeColor = Color.white;
        public Color doublingCubeEmission = Color.black;
        [Range(0f, 5f)] public float doublingCubeEmissionIntensity = 0f;

        [Header("Dice")]
        public Material diceMaterial;
        public Color diceBodyColor = Color.red;
        public Color dicePipColor = Color.white;
        [Range(0f, 5f)] public float diceLuminosity = 1f;

        [Header("Board Surface")]
        public Material boardSurfaceMaterial;
        public Color boardSurfaceColor = new Color(0.18f, 0.22f, 0.2f);

        [Header("UI Colours")]
        public Color uiAccentColor = new Color(0.012f, 0.4f, 0.655f);
        public Color uiSecondaryColor = new Color(0.34f, 0.34f, 0.34f);
    }
}
