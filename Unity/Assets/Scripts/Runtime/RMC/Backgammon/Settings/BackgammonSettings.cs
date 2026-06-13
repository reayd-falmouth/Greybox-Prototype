using UnityEngine;

namespace Runtime.RMC.Backgammon.Settings
{
    public static class BackgammonSettings
    {
        const string MoveAnim = "bg_move_anim_duration";
        const string AiDepth = "bg_ai_search_depth";
        const string OpponentAi = "bg_opponent_is_ai";
        const string BoardViewHorizontal = "bg_board_view_horizontal";
        const string MasterVol = "bg_master_volume";
        const string SfxVol = "bg_sfx_volume";
        const string GameSpeed = "bg_game_speed_seconds_per_step";
        const string EventQueueBaseGap = "bg_event_queue_base_gap";
        const string EventQueueAudioLeadIn = "bg_event_queue_audio_lead_in";
        const string EventQueueCubeRotateHold = "bg_event_queue_cube_rotate_hold";
        const string AiEngine = "bg_ai_engine_type";

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

        public static float GameSpeedSecondsPerStep
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(GameSpeed, 0.35f), 0.05f, 2f);
            set { PlayerPrefs.SetFloat(GameSpeed, Mathf.Clamp(value, 0.05f, 2f)); PlayerPrefs.Save(); }
        }

        public static float EventQueueBaseGapSeconds
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(EventQueueBaseGap, 0.10f), 0f, 2f);
            set { PlayerPrefs.SetFloat(EventQueueBaseGap, Mathf.Clamp(value, 0f, 2f)); PlayerPrefs.Save(); }
        }

        public static float EventQueueAudioLeadInSeconds
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(EventQueueAudioLeadIn, 0.03f), 0f, 2f);
            set { PlayerPrefs.SetFloat(EventQueueAudioLeadIn, Mathf.Clamp(value, 0f, 2f)); PlayerPrefs.Save(); }
        }

        public static float EventQueueCubeRotateHoldSeconds
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(EventQueueCubeRotateHold, 0.15f), 0f, 2f);
            set { PlayerPrefs.SetFloat(EventQueueCubeRotateHold, Mathf.Clamp(value, 0f, 2f)); PlayerPrefs.Save(); }
        }

        public static bool BoardViewIsHorizontal
        {
            get => PlayerPrefs.GetInt(BoardViewHorizontal, 1) != 0;
            set { PlayerPrefs.SetInt(BoardViewHorizontal, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static string AiEngineType
        {
            get => PlayerPrefs.GetString(AiEngine, "LocalNeuralNet");
            set { PlayerPrefs.SetString(AiEngine, value); PlayerPrefs.Save(); }
        }

        const string MusicVol = "bg_music_volume";

        public static float MusicVolumeLinear
        {
            get => PlayerPrefs.GetFloat(MusicVol, 0.5f);
            set { PlayerPrefs.SetFloat(MusicVol, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        public static event System.Action<float> OnMusicVolumeChanged;
        public static void RaiseMusicVolumeChanged(float v) => OnMusicVolumeChanged?.Invoke(v);

        // ── Graphics Settings ─────────────────────────────────────────────────

        const string CameraAngleKey = "bg_camera_angle";
        const string ScanLinesKey   = "bg_scan_lines";
        const string CrtBloomKey    = "bg_crt_bloom";
        const string BrightnessKey  = "bg_brightness";
        const string ContrastKey    = "bg_contrast";
        const string ThemeIndexKey  = "bg_theme_index";

        // 0 = Top Down, 1 = Angled
        public static int CameraAngle
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(CameraAngleKey, 0), 0, 1);
            set { PlayerPrefs.SetInt(CameraAngleKey, Mathf.Clamp(value, 0, 1)); PlayerPrefs.Save(); }
        }

        public static bool ScanLines
        {
            get => PlayerPrefs.GetInt(ScanLinesKey, 0) != 0;
            set { PlayerPrefs.SetInt(ScanLinesKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool CrtBloom
        {
            get => PlayerPrefs.GetInt(CrtBloomKey, 0) != 0;
            set { PlayerPrefs.SetInt(CrtBloomKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static float Brightness
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(BrightnessKey, 1f), 0.5f, 1.5f);
            set { PlayerPrefs.SetFloat(BrightnessKey, Mathf.Clamp(value, 0.5f, 1.5f)); PlayerPrefs.Save(); }
        }

        public static float Contrast
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(ContrastKey, 1f), 0.5f, 1.5f);
            set { PlayerPrefs.SetFloat(ContrastKey, Mathf.Clamp(value, 0.5f, 1.5f)); PlayerPrefs.Save(); }
        }

        public static int ThemeIndex
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(ThemeIndexKey, 0), 0, 3);
            set { PlayerPrefs.SetInt(ThemeIndexKey, Mathf.Clamp(value, 0, 3)); PlayerPrefs.Save(); }
        }

        const string PopupAnimKey = "bg_popup_animation";

        public static int PopupAnimation
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(PopupAnimKey, 1), 0, 4);
            set { PlayerPrefs.SetInt(PopupAnimKey, Mathf.Clamp(value, 0, 4)); PlayerPrefs.Save(); }
        }

        const string TransitionShapeKey = "bg_transition_shape";

        // 0=Diamond, 1=Circle, 2=Square, 3=None
        public static int TransitionShape
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(TransitionShapeKey, 0), 0, 3);
            set { PlayerPrefs.SetInt(TransitionShapeKey, Mathf.Clamp(value, 0, 3)); PlayerPrefs.Save(); }
        }

        public static event System.Action OnGraphicsSettingsChanged;
        public static void RaiseGraphicsSettingsChanged() => OnGraphicsSettingsChanged?.Invoke();

        // ── Layout Selection ──────────────────────────────────────────────────

        const string LayoutTypeKey = "bg_layout_type";

        /// <summary>
        /// The HUD layout variant chosen on the startup selector
        /// (<see cref="UI.BackgammonLayoutType"/>). Persisted so the choice can be
        /// reflected in the version/layout debug string and pre-selected later.
        /// </summary>
        public static UI.BackgammonLayoutType LayoutType
        {
            get => (UI.BackgammonLayoutType)Mathf.Clamp(PlayerPrefs.GetInt(LayoutTypeKey, 0), 0, 1);
            set
            {
                PlayerPrefs.SetInt(LayoutTypeKey, Mathf.Clamp((int)value, 0, 1));
                PlayerPrefs.Save();
                RaiseLayoutChanged();
            }
        }

        public static event System.Action OnLayoutChanged;
        public static void RaiseLayoutChanged() => OnLayoutChanged?.Invoke();

    }
}
