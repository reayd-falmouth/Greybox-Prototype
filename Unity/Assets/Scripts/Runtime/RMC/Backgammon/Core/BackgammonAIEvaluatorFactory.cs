using Runtime.RMC.Backgammon.Settings;
using UnityEngine;

namespace Runtime.RMC.Backgammon.Core
{
    public static class BackgammonAIEvaluatorFactory
    {
        private static IBackgammonAIEvaluator _currentEvaluator;
        private static string _lastEngineType;

        public static IBackgammonAIEvaluator GetEvaluator()
        {
            string engineType = BackgammonSettings.AiEngineType;

            if (_currentEvaluator == null || _lastEngineType != engineType)
            {
                _lastEngineType = engineType;

                switch (engineType)
                {
                    case "GnubgPython":
                        Debug.Log("[BackgammonAI] Using GNUBG Python engine");
                        _currentEvaluator = new GnubgPythonEvaluator();
                        break;

                    case "LocalNeuralNet":
                    default:
                        Debug.Log("[BackgammonAI] Using Local Neural Net engine");
                        _currentEvaluator = new LocalNeuralNetEvaluator();
                        break;
                }
            }

            return _currentEvaluator;
        }

        public static void ClearCache()
        {
            _currentEvaluator?.ClearCache();
        }
    }
}
