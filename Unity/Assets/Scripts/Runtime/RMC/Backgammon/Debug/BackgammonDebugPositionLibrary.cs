using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BackgammonDebugPositionLibrary",
    menuName = "RMC/Backgammon/Debug Position Library",
    order = 2000)]
public sealed class BackgammonDebugPositionLibrary : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public string label;
        public string positionId;
        public string source;
        public string move;
        public int cubeValue;
    }

    [SerializeField] private List<Entry> entries = new();

    public IReadOnlyList<Entry> Entries => entries;

    public void AddUnique(string positionId, string label = null, string source = null, string move = null, int cubeValue = 0)
    {
        if (string.IsNullOrWhiteSpace(positionId))
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            Entry existing = entries[i];
            if (existing != null && string.Equals(existing.positionId, positionId, StringComparison.Ordinal))
                return;
        }

        entries.Add(new Entry
        {
            label = string.IsNullOrWhiteSpace(label) ? $"PID {entries.Count + 1}" : label.Trim(),
            positionId = positionId.Trim(),
            source = string.IsNullOrWhiteSpace(source) ? "unknown" : source.Trim(),
            move = string.IsNullOrWhiteSpace(move) ? string.Empty : move.Trim(),
            cubeValue = cubeValue
        });
    }
}
