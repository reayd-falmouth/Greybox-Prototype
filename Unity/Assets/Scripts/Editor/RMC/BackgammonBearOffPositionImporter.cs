using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class BackgammonBearOffPositionImporter
{
    private const string DefaultEditorLogPath = "C:/Users/david/AppData/Local/Unity/Editor/Editor.log";
    private const string LibraryAssetPath = "Assets/Settings/Backgammon/BackgammonBearOffPositionLibrary.asset";
    private static readonly Regex BearOffRegex = new(
        @"\[Backgammon\]\[BearOffDebug\]\s+source=(?<source>\w+)\s+move=(?<move>[^ ]+)\s+hit=(?<hit>\w+)\s+playerOnRoll=(?<por>-?\d+)\s+dice=(?<dice>[^ ]+)\s+cube=(?<cube>-?\d+)\s+pid=(?<pid>[A-Za-z0-9+/=]+)",
        RegexOptions.Compiled);

    [MenuItem("RMC/Backgammon/Import Bear-off Positions From Editor.log")]
    public static void ImportFromDefaultEditorLog()
    {
        if (!File.Exists(DefaultEditorLogPath))
        {
            Debug.LogWarning($"[Backgammon][BearOffImport] Editor log not found at {DefaultEditorLogPath}");
            return;
        }

        string[] lines = File.ReadAllLines(DefaultEditorLogPath);
        if (!TryGetLibrary(out BackgammonDebugPositionLibrary library))
            return;

        int beforeCount = library.Entries.Count;
        List<BackgammonDebugPositionLibrary.Entry> parsed = ParseEntries(lines);
        for (int i = 0; i < parsed.Count; i++)
        {
            BackgammonDebugPositionLibrary.Entry entry = parsed[i];
            library.AddUnique(entry.positionId, entry.label, entry.source, entry.move, entry.cubeValue);
        }

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        int afterCount = library.Entries.Count;
        Debug.Log($"[Backgammon][BearOffImport] Imported {afterCount - beforeCount} new entries from {DefaultEditorLogPath}. total={afterCount}");
    }

    public static List<BackgammonDebugPositionLibrary.Entry> ParseEntries(IEnumerable<string> lines)
    {
        var entries = new List<BackgammonDebugPositionLibrary.Entry>();
        var seen = new HashSet<string>();
        int index = 1;
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            Match match = BearOffRegex.Match(line);
            if (!match.Success)
                continue;

            string pid = match.Groups["pid"].Value.Trim();
            if (string.IsNullOrWhiteSpace(pid) || !seen.Add(pid))
                continue;

            string source = match.Groups["source"].Value.Trim();
            string move = match.Groups["move"].Value.Trim();
            int cube = 0;
            int.TryParse(match.Groups["cube"].Value, out cube);
            entries.Add(new BackgammonDebugPositionLibrary.Entry
            {
                label = $"BearOff {index:00}",
                positionId = pid,
                source = source,
                move = move,
                cubeValue = cube
            });
            index++;
        }

        return entries;
    }

    private static bool TryGetLibrary(out BackgammonDebugPositionLibrary library)
    {
        library = AssetDatabase.LoadAssetAtPath<BackgammonDebugPositionLibrary>(LibraryAssetPath);
        if (library != null)
            return true;

        Debug.LogError($"[Backgammon][BearOffImport] Could not load library asset at {LibraryAssetPath}");
        return false;
    }
}
