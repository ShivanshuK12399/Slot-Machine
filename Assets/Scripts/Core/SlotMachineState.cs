namespace SlotMachine.Core
{
    /// <summary>
    /// Defines the operational lifecycle states of the slot machine.
    /// Used by controllers and presentation views for event-driven state tracking.
    /// </summary>
    public enum SlotMachineState
    {
        /// <summary>
        /// Machine is resting, ready for player interaction (bet modification, spin launch).
        /// </summary>
        Idle = 0,

        /// <summary>
        /// Reels are actively spinning. Betting controls and duplicate spins are locked.
        /// </summary>
        Spinning = 1,

        /// <summary>
        /// All reels have stopped; payline results and payouts are being calculated.
        /// </summary>
        EvaluatingWin = 2,

        /// <summary>
        /// A winning combination or jackpot is being celebrated and highlighted on the payline.
        /// </summary>
        WinCelebration = 3,

        /// <summary>
        /// Player ran out of credits. Game is locked and level will restart.
        /// </summary>
        GameOver = 4
    }
}

