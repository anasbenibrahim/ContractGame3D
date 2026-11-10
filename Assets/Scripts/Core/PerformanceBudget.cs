namespace Contract.Core
{
    /// <summary>Per-device budgets set by the quality preset and read by the systems that spend them.</summary>
    public static class PerformanceBudget
    {
        /// <summary>Civilians at once (0 = no limit beyond the scene's own maximum).</summary>
        public static int MaxCivilians { get; set; }
    }
}
