namespace SlotMachine.Core
{
    /// <summary>
    /// Contract for random number generation to allow fair gameplay, testability, and deterministic seeding.
    /// </summary>
    public interface IRandomProvider
    {
        /// <summary>
        /// Returns a random integer within [minInclusive, maxExclusive).
        /// </summary>
        int Range(int minInclusive, int maxExclusive);

        /// <summary>
        /// Returns a random float within [0.0, 1.0).
        /// </summary>
        float NextFloat();
    }
}

