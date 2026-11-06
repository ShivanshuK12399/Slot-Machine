using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlotMachine.Config
{
    /// <summary>
    /// Definition of a winning rule or payout line condition.
    /// </summary>
    [Serializable]
    public struct PayoutRule
    {
        [Tooltip("The symbol required for this win rule.")]
        public SymbolType Symbol;

        [Tooltip("Number of matching consecutive symbols required (e.g., 2 or 3).")]
        public int MatchCount;

        [Tooltip("Multiplier applied to the bet amount when this condition is met.")]
        public int PayoutMultiplier;

        [Tooltip("Optional description for celebration UI banner.")]
        public string WinDescription;
    }

    /// <summary>
    /// Master payout and rules table configuring game economy, symbol definitions, and win conditions.
    /// </summary>
    [CreateAssetMenu(fileName = "PayoutTable", menuName = "SlotMachine/Payout Table", order = 1)]
    public class PayoutTable : ScriptableObject
    {
        [Header("Symbol Catalog")]
        [Tooltip("All registered symbols available in the slot machine.")]
        [SerializeField] private List<SymbolData> _symbols = new(8);

        [Header("Payout Rules")]
        [Tooltip("Explicit payout rules evaluated in priority order.")]
        [SerializeField] private List<PayoutRule> _rules = new(16);

        public IReadOnlyList<SymbolData> Symbols => _symbols;
        public IReadOnlyList<PayoutRule> Rules => _rules;

        /// <summary>
        /// Retrieves the SymbolData asset for a given SymbolType.
        /// </summary>
        public SymbolData GetSymbolData(SymbolType symbolType)
        {
            for (int i = 0; i < _symbols.Count; i++)
            {
                if (_symbols[i] != null && _symbols[i].SymbolType == symbolType)
                {
                    return _symbols[i];
                }
            }

            return null;
        }

#if UNITY_EDITOR
        public void Configure(List<SymbolData> symbolList, List<PayoutRule> ruleList)
        {
            _symbols = symbolList;
            _rules = ruleList;
        }
#endif
    }
}

