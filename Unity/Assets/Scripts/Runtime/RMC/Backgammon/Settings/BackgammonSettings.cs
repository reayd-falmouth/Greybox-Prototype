using UnityEngine;

namespace Runtime.RMC.Backgammon.Settings
{
    public static class BackgammonSettings
    {
        const string MoveAnim = "bg_move_anim_duration";
        const string AiDepth = "bg_ai_search_depth";
        const string OpponentAi = "bg_opponent_is_ai";
        const string MasterVol = "bg_master_volume";
        const string SfxVol = "bg_sfx_volume";

        public static float MoveAnimDurationSeconds
        {
            get => PlayerPrefs.GetFloat(MoveAnim, 0.3f);
            set { PlayerPrefs.SetFloat(MoveAnim, value); PlayerPrefs.Save(); }
        }

        public static int AiSearchDepth
        {
            get => PlayerPrefs.GetInt(AiDepth, 1);
            set { PlayerPrefs.SetInt(AiDepth, Mathf.Clamp(value, 1, 3)); PlayerPrefs.Save(); }
        }

        public static bool OpponentIsAi
        {
            get => PlayerPrefs.GetInt(OpponentAi, 1) != 0;
            set { PlayerPrefs.SetInt(OpponentAi, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static float MasterVolumeLinear
        {
            get => PlayerPrefs.GetFloat(MasterVol, 1f);
            set { PlayerPrefs.SetFloat(MasterVol, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        public static float SfxVolumeLinear
        {
            get => PlayerPrefs.GetFloat(SfxVol, 1f);
            set { PlayerPrefs.SetFloat(SfxVol, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }
    }
}
