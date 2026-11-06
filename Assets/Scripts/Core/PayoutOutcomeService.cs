using System;
using SlotMachine.Config;

namespace SlotMachine.Core
{
    /// <summary>
    /// Represents the evaluated result of a slot machine spin.
    /// Struct is zero-allocation and passed by value or ref.
    /// </summary>
    public readonly struct SpinOutcome
    {
        public readonly bool IsWin;
        public readonly bool IsBonus;
        public readonly int PayoutAmount;
        public readonly int Multiplier;
        public readonly SymbolType WinningSymbol;
        public readonly int MatchCount;
        public readonly string Message;

        public SpinOutcome(
            bool isWin,
            bool isBonus,
            int payoutAmount,
            int multiplier,
            SymbolType winningSymbol,
            int matchCount,
            string message)
        {
            IsWin = isWin;
            IsBonus = isBonus;
            PayoutAmount = payoutAmount;
            Multiplier = multiplier;
            WinningSymbol = winningSymbol;
            MatchCount = matchCount;
            Message = message ?? string.Empty;
        }

        public static SpinOutcome NoWin() =>
            new(false, false, 0, 0, SymbolType.Symbol1, 0, "Try Again!");
    }

    /// <summary>
    /// Pure logic service responsible for generating spin outcomes and calculating payouts based on rules.
    /// Adheres to zero-GC standards: no LINQ, no repeated allocations, cached lookups.
    /// </summary>
    public sealed class PayoutOutcomeService
    {
        private readonly PayoutTable _payoutTable;
        private readonly IRandomProvider _randomProvider;

        public PayoutOutcomeService(PayoutTable payoutTable, IRandomProvider randomProvider)
        {
            _payoutTable = payoutTable ?? throw new ArgumentNullException(nameof(payoutTable));
            _randomProvider = randomProvider ?? throw new ArgumentNullException(nameof(randomProvider));
        }

        /// <summary>
        /// Selects random stopping symbols for the given number of reels using the configured symbol pool.
        /// Fills the caller-provided array to ensure zero allocation.
        /// </summary>
        public void GenerateRandomStops(SymbolType[] targetStops)
        {
            if (targetStops == null || targetStops.Length == 0) return;

            var symbols = _payoutTable.Symbols;
            int count = symbols.Count;
            if (count == 0) return;

            for (int i = 0; i < targetStops.Length; i++)
            {
                int randomIndex = _randomProvider.Range(0, count);
                targetStops[i] = symbols[randomIndex].SymbolType;
            }
        }

        /// <summary>
        /// Evaluates a spin's outcome given the final stopping symbols and current bet amount.
        /// Zero allocations in hot path.
        /// </summary>
        public SpinOutcome EvaluateOutcome(SymbolType[] stops, int currentBet)
        {
            if (stops == null || stops.Length == 0 || currentBet <= 0)
            {
                return SpinOutcome.NoWin();
            }

            // Check if all symbols match
            bool allMatch = true;
            SymbolType firstSymbol = stops[0];
            int matchCount = 1;

            for (int i = 1; i < stops.Length; i++)
            {
                if (stops[i] == firstSymbol)
                {
                    matchCount++;
                }
                else
                {
                    allMatch = false;
                }
            }

            // Check for explicit matching rule in PayoutTable
            var rules = _payoutTable.Rules;
            int rulesCount = rules.Count;

            // 1. Highest priority: All slots match (docs: "Player wins when all slots have the same symbol")
            if (allMatch)
            {
                var symbolData = _payoutTable.GetSymbolData(firstSymbol);

                // Look for explicit full-match rule
                for (int i = 0; i < rulesCount; i++)
                {
                    var rule = rules[i];
                    if (rule.Symbol == firstSymbol && rule.MatchCount == stops.Length)
                    {
                        int payout = currentBet * rule.PayoutMultiplier;
                        return new SpinOutcome(
                            isWin: true,
                            isBonus: false,
                            payoutAmount: payout,
                            multiplier: rule.PayoutMultiplier,
                            winningSymbol: firstSymbol,
                            matchCount: stops.Length,
                            message: rule.WinDescription
                        );
                    }
                }

                // Fallback to base multiplier on SymbolData if no explicit rule is configured
                int baseMultiplier = symbolData != null ? symbolData.BaseMultiplier : 10;
                int totalPayout = currentBet * baseMultiplier;
                string winMsg = $"JACKPOT! 3x {firstSymbol}";

                return new SpinOutcome(
                    isWin: true,
                    isBonus: false,
                    payoutAmount: totalPayout,
                    multiplier: baseMultiplier,
                    winningSymbol: firstSymbol,
                    matchCount: stops.Length,
                    message: winMsg
                );
            }

            // If not all slots match, it's a loss according to docs.txt (Player wins when all slots have the same symbol)
            return SpinOutcome.NoWin();
        }
    }
}

