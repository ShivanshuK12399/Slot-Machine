using UnityEngine;

namespace SlotMachine.Config
{
    /// <summary>
    /// Configuration data asset for a single symbol type on the slot reels.
    /// </summary>
    [CreateAssetMenu(fileName = "SymbolData_", menuName = "SlotMachine/Symbol Data", order = 0)]
    public class SymbolData : ScriptableObject
    {
        [Header("Symbol Identity")]
        [Tooltip("The unique enum identifier for this symbol.")]
        [SerializeField] private SymbolType _symbolType;

        [Tooltip("Human-readable display name for this symbol.")]
        [SerializeField] private string _displayName = "Symbol";

        [Header("Visuals")]
        [Tooltip("The sprite rendered for this symbol on reels.")]
        [SerializeField] private Sprite _symbolSprite;

        [Header("Payout & Behavior")]
        [Tooltip("Base payout multiplier when this symbol forms a full match (e.g., 3 of a kind).")]
        [SerializeField] private int _baseMultiplier = 5;

        [Tooltip("Indicates whether this symbol acts as a special bonus/scatter trigger.")]
        [SerializeField] private bool _isBonusSymbol = false;

        public SymbolType SymbolType => _symbolType;
        public string DisplayName => _displayName;
        public Sprite SymbolSprite => _symbolSprite;
        public int BaseMultiplier => _baseMultiplier;
        public bool IsBonusSymbol => _isBonusSymbol;

#if UNITY_EDITOR
        /// <summary>
        /// Editor helper method to initialize symbol data programmatically.
        /// </summary>
        public void SetInitialData(SymbolType type, string displayName, Sprite sprite, int baseMultiplier, bool isBonus)
        {
            _symbolType = type;
            _displayName = displayName;
            _symbolSprite = sprite;
            _baseMultiplier = baseMultiplier;
            _isBonusSymbol = isBonus;
        }
#endif
    }
}

