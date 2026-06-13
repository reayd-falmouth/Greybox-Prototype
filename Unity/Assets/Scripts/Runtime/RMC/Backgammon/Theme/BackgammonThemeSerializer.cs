using UnityEngine;

namespace Runtime.RMC.Backgammon.Theme
{
    [System.Serializable]
    public struct BackgammonThemeData
    {
        // Flat r/g/b floats — JsonUtility does not serialize Color inside plain structs
        public float c1r, c1g, c1b;
        public float c1er, c1eg, c1eb;
        public float c1intensity;

        public float c2r, c2g, c2b;
        public float c2er, c2eg, c2eb;
        public float c2intensity;

        public float hlr, hlg, hlb;

        public float bpDr, bpDg, bpDb;
        public float bpLr, bpLg, bpLb;

        public float cubeR, cubeG, cubeB;
        public float cubeEr, cubeEg, cubeEb;
        public float cubeIntensity;

        public float diceR, diceG, diceB;
        public float dicePipR, dicePipG, dicePipB;
        public float diceLuminosity;

        public float surfR, surfG, surfB;

        public float uiAccR, uiAccG, uiAccB;
        public float uiSecR, uiSecG, uiSecB;
    }

    public static class BackgammonThemeSerializer
    {
        private const string CustomThemeKey = "bg_custom_theme_json";

        public static BackgammonThemeData ToData(BackgammonThemeSo so)
        {
            return new BackgammonThemeData
            {
                c1r = so.checker1BaseColor.r,
                c1g = so.checker1BaseColor.g,
                c1b = so.checker1BaseColor.b,
                c1er = so.checker1EmissionColor.r,
                c1eg = so.checker1EmissionColor.g,
                c1eb = so.checker1EmissionColor.b,
                c1intensity = so.checker1EmissionIntensity,

                c2r = so.checker2BaseColor.r,
                c2g = so.checker2BaseColor.g,
                c2b = so.checker2BaseColor.b,
                c2er = so.checker2EmissionColor.r,
                c2eg = so.checker2EmissionColor.g,
                c2eb = so.checker2EmissionColor.b,
                c2intensity = so.checker2EmissionIntensity,

                hlr = so.movableHighlightColor.r,
                hlg = so.movableHighlightColor.g,
                hlb = so.movableHighlightColor.b,

                bpDr = so.boardPointDarkColor.r,
                bpDg = so.boardPointDarkColor.g,
                bpDb = so.boardPointDarkColor.b,
                bpLr = so.boardPointLightColor.r,
                bpLg = so.boardPointLightColor.g,
                bpLb = so.boardPointLightColor.b,

                cubeR = so.doublingCubeColor.r,
                cubeG = so.doublingCubeColor.g,
                cubeB = so.doublingCubeColor.b,
                cubeEr = so.doublingCubeEmission.r,
                cubeEg = so.doublingCubeEmission.g,
                cubeEb = so.doublingCubeEmission.b,
                cubeIntensity = so.doublingCubeEmissionIntensity,

                diceR = so.diceBodyColor.r,
                diceG = so.diceBodyColor.g,
                diceB = so.diceBodyColor.b,
                dicePipR = so.dicePipColor.r,
                dicePipG = so.dicePipColor.g,
                dicePipB = so.dicePipColor.b,
                diceLuminosity = so.diceLuminosity,

                surfR = so.boardSurfaceColor.r,
                surfG = so.boardSurfaceColor.g,
                surfB = so.boardSurfaceColor.b,

                uiAccR = so.uiAccentColor.r,
                uiAccG = so.uiAccentColor.g,
                uiAccB = so.uiAccentColor.b,
                uiSecR = so.uiSecondaryColor.r,
                uiSecG = so.uiSecondaryColor.g,
                uiSecB = so.uiSecondaryColor.b,
            };
        }

        public static void ApplyData(BackgammonThemeData d, BackgammonThemeSo target)
        {
            target.checker1BaseColor     = new Color(d.c1r, d.c1g, d.c1b);
            target.checker1EmissionColor = new Color(d.c1er, d.c1eg, d.c1eb);
            target.checker1EmissionIntensity = d.c1intensity;

            target.checker2BaseColor     = new Color(d.c2r, d.c2g, d.c2b);
            target.checker2EmissionColor = new Color(d.c2er, d.c2eg, d.c2eb);
            target.checker2EmissionIntensity = d.c2intensity;

            target.movableHighlightColor = new Color(d.hlr, d.hlg, d.hlb, 1f);

            target.boardPointDarkColor  = new Color(d.bpDr, d.bpDg, d.bpDb);
            target.boardPointLightColor = new Color(d.bpLr, d.bpLg, d.bpLb);

            target.doublingCubeColor    = new Color(d.cubeR, d.cubeG, d.cubeB);
            target.doublingCubeEmission = new Color(d.cubeEr, d.cubeEg, d.cubeEb);
            target.doublingCubeEmissionIntensity = d.cubeIntensity;

            target.diceBodyColor = new Color(d.diceR, d.diceG, d.diceB);
            target.dicePipColor  = new Color(d.dicePipR, d.dicePipG, d.dicePipB);
            target.diceLuminosity = d.diceLuminosity;

            target.boardSurfaceColor = new Color(d.surfR, d.surfG, d.surfB);

            target.uiAccentColor    = new Color(d.uiAccR, d.uiAccG, d.uiAccB);
            target.uiSecondaryColor = new Color(d.uiSecR, d.uiSecG, d.uiSecB);
        }

        public static void SaveCustom(BackgammonThemeData data)
        {
            PlayerPrefs.SetString(CustomThemeKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static BackgammonThemeData LoadCustom(BackgammonThemeSo fallback)
        {
            string json = PlayerPrefs.GetString(CustomThemeKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
                return JsonUtility.FromJson<BackgammonThemeData>(json);

            return fallback != null ? ToData(fallback) : default;
        }
    }
}
