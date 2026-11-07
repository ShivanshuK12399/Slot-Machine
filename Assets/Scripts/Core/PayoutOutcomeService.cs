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
        /// Selects stopping symbols based on the target win probability (0.0 = 0% win, 1.0 = 100% win).
        /// When a win occurs, all reels stop on the same randomly chosen symbol.
        /// When a loss occurs, stopping symbols are chosen such that not all reels match.
        /// Fills the caller-provided array to ensure zero allocation.
        /// </summary>
        public void GenerateStops(SymbolType[] targetStops, float winProbability)
        {
            if (targetStops == null || targetStops.Length == 0) return;

            var symbols = _payoutTable.Symbols;
            int count = symbols.Count;
            if (count == 0) return;

            bool isWin = _randomProvider.NextFloat() < winProbability;

            if (isWin)
            {
                // Winning spin: all reels match
                int winIndex = _randomProvider.Range(0, count);
                SymbolType winSymbol = symbols[winIndex].SymbolType;

                for (int i = 0; i < targetStops.Length; i++)
                {
                    targetStops[i] = winSymbol;
                }
            }
            else
            {
                // Losing spin: pick random symbols ensuring at least one reel differs
                for (int i = 0; i < targetStops.Length; i++)
                {
                    int randomIndex = _randomProvider.Range(0, count);
                    targetStops[i] = symbols[randomIndex].SymbolType;
                }

                // If randomly all symbols matched, alter the last reel to guarantee no match
                if (targetStops.Length > 1 && count > 1)
                {
                    bool allMatch = true;
                    for (int i = 1; i < targetStops.Length; i++)
                    {
                        if (targetStops[i] != targetStops[0])
                        {
                            allMatch = false;
                            break;
                        }
                    }

                    if (allMatch)
                    {
                        int firstIndex = 0;
                        for (int i = 0; i < count; i++)
                        {
                            if (symbols[i].SymbolType == targetStops[0])
                            {
                                firstIndex = i;
                                break;
                            }
                        }

                        int offset = _randomProvider.Range(1, count);
                        int diffIndex = (firstIndex + offset) % count;
                        targetStops[targetStops.Length - 1] = symbols[diffIndex].SymbolType;
                    }
                }
            }
        }

        /// <summary>
        /// Selects random stopping symbols for the given number of reels using default 50% win probability.
        /// Fills the caller-provided array to ensure zero allocation.
        /// </summary>
        public void GenerateRandomStops(SymbolType[] targetStops)
        {
            GenerateStops(targetStops, 0.5f);
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
                string winMsg = baseMultiplier >= 50 ? "JACKPOT!" : baseMultiplier >= 20 ? "MEGA WIN!" : "BIG WIN!";

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

