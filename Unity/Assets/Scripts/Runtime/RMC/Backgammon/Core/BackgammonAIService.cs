using System.Collections.Generic;
using System.IO;
using EngineCore;
using UnityEngine;

namespace Runtime.RMC.Backgammon.Core
{
    /// <summary>Lazily loads GNUBG weights and exposes <see cref="SearchEngine"/>.</summary>
    public static class BackgammonAIService
    {
        static SearchEngine _engine;
        static bool _loadFailed;

        public static bool TryGetSearchEngine(out SearchEngine engine)
        {
            engine = null;
            if (_loadFailed) return false;
            if (_engine != null)
            {
                engine = _engine;
                return true;
            }

            try
            {
                string dir = BackgammonEnginePaths.GetDataDirectoryOrFallback(EnginePathResolver.GetDataDirectory);
                string weights = Path.Combine(dir, "gnubg.weights");
                List<NeuralNet> nets = WeightParser.Load(weights);
                NeuralNet contact = null;
                for (int i = 0; i < nets.Count; i++)
                {
                    if (nets[i].InputCount == 250)
                    {
                        contact = nets[i];
                        break;
                    }
                }

                if (contact == null)
                {
                    Debug.LogError("BackgammonAIService: no 250-input contact net in gnubg.weights.");
                    _loadFailed = true;
                    return false;
                }

                var bearoff = new BearoffEvaluator(dir);
                var cube = new CubeEvaluator();
                _engine = new SearchEngine(contact, null, bearoff, cube);
                engine = _engine;
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("BackgammonAIService: failed to load AI — " + ex.Message);
                _loadFailed = true;
                return false;
            }
        }

        public static void ClearSearchEngineCache()
        {
            _engine?.ClearCache();
        }
    }
}
