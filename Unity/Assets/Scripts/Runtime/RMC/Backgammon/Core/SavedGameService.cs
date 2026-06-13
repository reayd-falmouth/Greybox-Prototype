using System.IO;
using UnityEngine;

namespace Runtime.RMC.Backgammon.Core
{
    public static class SavedGameService
    {
        private const int SupportedSchemaVersion = 1;

        public static string SavePath => Path.Combine(Application.persistentDataPath, "backgammon_saved_game.json");

        public static bool HasSavedGame => File.Exists(SavePath);

        public static void Save(SavedGameData data)
        {
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(data, prettyPrint: true));
                Debug.Log($"[SavedGame] Saved to {SavePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SavedGame] Save failed: {ex.Message}");
            }
        }

        public static SavedGameData Load()
        {
            if (!HasSavedGame) return null;
            try
            {
                var data = JsonUtility.FromJson<SavedGameData>(File.ReadAllText(SavePath));
                if (data == null || data.schemaVersion != SupportedSchemaVersion)
                {
                    Debug.LogWarning($"[SavedGame] Incompatible or null save data — discarding.");
                    Delete();
                    return null;
                }
                Debug.Log($"[SavedGame] Loaded from {SavePath}");
                return data;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SavedGame] Load failed: {ex.Message}");
                Delete();
                return null;
            }
        }

        public static void Delete()
        {
            if (!HasSavedGame) return;
            try
            {
                File.Delete(SavePath);
                Debug.Log("[SavedGame] Save file deleted.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SavedGame] Delete failed: {ex.Message}");
            }
        }
    }
}
