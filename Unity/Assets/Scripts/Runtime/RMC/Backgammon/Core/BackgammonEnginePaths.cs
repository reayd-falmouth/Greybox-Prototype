using System;
using System.IO;
using EngineCore;
using UnityEngine;

namespace Runtime.RMC.Backgammon.Core
{
    /// <summary>Resolves BackgammonAI Runtime/Data when the package lives under Assets/3rdParty (Editor-safe via <see cref="Application.dataPath"/>).</summary>
    public static class BackgammonEnginePaths
    {
        public static string GetDataDirectoryOrFallback(Func<string> fallback)
        {
            string embedded = Path.Combine(
                Application.dataPath,
                "3rdParty/BackgammonAI/com.stonesandice.backgammonai/Runtime/Data");
            if (Directory.Exists(embedded))
                return embedded;
            return fallback != null ? fallback() : EnginePathResolver.GetDataDirectory();
        }
    }
}
