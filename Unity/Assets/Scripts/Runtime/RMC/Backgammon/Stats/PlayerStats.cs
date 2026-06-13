using UnityEngine;

namespace Runtime.RMC.Backgammon.Stats
{
    public static class PlayerStats
    {
        const string KeyRating           = "bg_stats_rating";
        const string KeyHighestScore     = "bg_stats_highest_score";
        const string KeyTotalGames       = "bg_stats_total_games";
        const string KeyTotalSessions    = "bg_stats_total_sessions";
        const string KeyHighestMoney     = "bg_stats_highest_money";
        const string KeyTotalWins        = "bg_stats_total_wins";
        const string KeyTotalGammons     = "bg_stats_total_gammons";
        const string KeyTotalBackgammons = "bg_stats_total_backgammons";
        const string KeyCurrentStreak    = "bg_stats_current_streak";
        const string KeyLongestStreak    = "bg_stats_longest_streak";
        const string KeyTotalMoneyEarned = "bg_stats_money_earned";
        const string KeyTotalMoneyLost   = "bg_stats_money_lost";

        public static float Rating
        {
            get => PlayerPrefs.GetFloat(KeyRating, 1500f);
            private set => PlayerPrefs.SetFloat(KeyRating, Mathf.Clamp(value, 0f, 3000f));
        }

        public static int HighestScore
        {
            get => PlayerPrefs.GetInt(KeyHighestScore, 0);
            private set => PlayerPrefs.SetInt(KeyHighestScore, value);
        }

        public static int TotalGamesPlayed
        {
            get => PlayerPrefs.GetInt(KeyTotalGames, 0);
            private set => PlayerPrefs.SetInt(KeyTotalGames, value);
        }

        public static int TotalSessionsPlayed
        {
            get => PlayerPrefs.GetInt(KeyTotalSessions, 0);
            private set => PlayerPrefs.SetInt(KeyTotalSessions, value);
        }

        public static int HighestMoneyEarned
        {
            get => PlayerPrefs.GetInt(KeyHighestMoney, 0);
            private set => PlayerPrefs.SetInt(KeyHighestMoney, value);
        }

        public static int TotalWins
        {
            get => PlayerPrefs.GetInt(KeyTotalWins, 0);
            private set => PlayerPrefs.SetInt(KeyTotalWins, value);
        }

        public static int TotalGammons
        {
            get => PlayerPrefs.GetInt(KeyTotalGammons, 0);
            private set => PlayerPrefs.SetInt(KeyTotalGammons, value);
        }

        public static int TotalBackgammons
        {
            get => PlayerPrefs.GetInt(KeyTotalBackgammons, 0);
            private set => PlayerPrefs.SetInt(KeyTotalBackgammons, value);
        }

        public static int CurrentWinStreak
        {
            get => PlayerPrefs.GetInt(KeyCurrentStreak, 0);
            private set => PlayerPrefs.SetInt(KeyCurrentStreak, value);
        }

        public static int LongestWinStreak
        {
            get => PlayerPrefs.GetInt(KeyLongestStreak, 0);
            private set => PlayerPrefs.SetInt(KeyLongestStreak, value);
        }

        public static int TotalMoneyEarned
        {
            get => PlayerPrefs.GetInt(KeyTotalMoneyEarned, 0);
            private set => PlayerPrefs.SetInt(KeyTotalMoneyEarned, value);
        }

        public static int TotalMoneyLost
        {
            get => PlayerPrefs.GetInt(KeyTotalMoneyLost, 0);
            private set => PlayerPrefs.SetInt(KeyTotalMoneyLost, value);
        }

        public static void RecordGameEnd(bool playerWon, int playerScore, int bankBalance, bool isGammon = false, bool isBackgammon = false)
        {
            TotalGamesPlayed++;
            if (playerWon)
            {
                TotalWins++;
                CurrentWinStreak++;
                if (CurrentWinStreak > LongestWinStreak) LongestWinStreak = CurrentWinStreak;
            }
            else
            {
                CurrentWinStreak = 0;
            }

            if (isGammon)     TotalGammons++;
            if (isBackgammon) TotalBackgammons++;

            if (playerScore > HighestScore) HighestScore = playerScore;
            if (bankBalance > HighestMoneyEarned) HighestMoneyEarned = bankBalance;
            Rating = ComputeNewRating(Rating, playerWon);
            PlayerPrefs.Save();
            Debug.Log($"[Stats] GameEnd: won={playerWon} gammon={isGammon} backgammon={isBackgammon} streak={CurrentWinStreak} rating={Rating:F0} totalGames={TotalGamesPlayed}");
        }

        public static void RecordMoneyResult(bool playerWon, int amount)
        {
            if (playerWon) TotalMoneyEarned += amount;
            else           TotalMoneyLost   += amount;
            PlayerPrefs.Save();
        }

        public static void RecordSessionStart()
        {
            TotalSessionsPlayed++;
            PlayerPrefs.Save();
            Debug.Log($"[Stats] SessionStart: totalSessions={TotalSessionsPlayed}");
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(KeyRating);
            PlayerPrefs.DeleteKey(KeyHighestScore);
            PlayerPrefs.DeleteKey(KeyTotalGames);
            PlayerPrefs.DeleteKey(KeyTotalSessions);
            PlayerPrefs.DeleteKey(KeyHighestMoney);
            PlayerPrefs.DeleteKey(KeyTotalWins);
            PlayerPrefs.DeleteKey(KeyTotalGammons);
            PlayerPrefs.DeleteKey(KeyTotalBackgammons);
            PlayerPrefs.DeleteKey(KeyCurrentStreak);
            PlayerPrefs.DeleteKey(KeyLongestStreak);
            PlayerPrefs.DeleteKey(KeyTotalMoneyEarned);
            PlayerPrefs.DeleteKey(KeyTotalMoneyLost);
            PlayerPrefs.Save();
        }

        private static float ComputeNewRating(float current, bool won)
        {
            const float opponentRating = 1400f;
            const float k = 32f;
            float expected = 1f / (1f + Mathf.Pow(10f, (opponentRating - current) / 400f));
            float outcome = won ? 1f : 0f;
            return Mathf.Clamp(current + k * (outcome - expected), 0f, 3000f);
        }
    }
}
