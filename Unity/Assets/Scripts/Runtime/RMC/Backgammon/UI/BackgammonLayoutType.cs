namespace Runtime.RMC.Backgammon.UI
{
    /// <summary>
    /// Identifies which HUD layout variant is active. Both layouts are functionally
    /// identical for now and differ only by their UXML source asset (and the
    /// version/layout debug string shown in the corner).
    /// </summary>
    public enum BackgammonLayoutType
    {
        Desktop = 0,
        Mobile = 1,
    }
}
