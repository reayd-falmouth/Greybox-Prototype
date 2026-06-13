using System;
using Runtime.RMC.Backgammon.Core;

[Serializable]
public class SavedGameData
{
    public int schemaVersion = 1;
    public string positionId;
    public int gameModeType;
    public MoneySessionConfig moneySessionConfig;
    public int moneySessionPlayer1Score;
    public int moneySessionPlayer2Score;
    public int moneySessionGamesPlayed;
    public int moneySessionBankBalance;
    public int player1MatchScore;
    public int player2MatchScore;
    public int matchTargetScore;
}
