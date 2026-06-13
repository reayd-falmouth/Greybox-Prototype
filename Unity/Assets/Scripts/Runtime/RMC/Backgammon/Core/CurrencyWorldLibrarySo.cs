using System.Collections.Generic;
using UnityEngine;

namespace Runtime.RMC.Backgammon.Core
{
    [CreateAssetMenu(menuName = "RMC/Backgammon/Currency World Library", fileName = "CurrencyWorldLibrary")]
    public class CurrencyWorldLibrarySo : ScriptableObject
    {
        [SerializeField] public List<CurrencyWorldSo> worlds = new();

        public int WorldCount => worlds.Count;

        public CurrencyWorldSo GetWorld(int index)
        {
            if (index < 0 || index >= worlds.Count) return null;
            return worlds[index];
        }

        public CurrencyWorldSo GetByCode(string code)
        {
            foreach (var w in worlds)
                if (w.currencyCode == code) return w;
            return null;
        }
    }
}
