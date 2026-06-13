namespace Runtime.RMC.Backgammon.Core
{
    /// <summary>
    /// Configuration for Money Session game mode including stakes and doubling rules.
    /// </summary>
    [System.Serializable]
    public class MoneySessionConfig
    {
        public int BaseStake = 1;
        public bool AutoDoublesEnabled = true;
        public bool BeaversAllowed = true;
        public bool RaccoonsAllowed = false;
        public bool ArdvarksAllowed = false;
        public bool JacobyRule = true;
        public string CurrencyCode = "USD";
        public string CurrencySymbol = "$";
    }
}
