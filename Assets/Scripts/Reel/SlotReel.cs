using System;
using UnityEngine;
using SlotMachine.Config;
using SlotMachine.Core;

namespace SlotMachine.Reel
{
    /// <summary>
    /// Lifecycle states of an individual slot reel.
    /// </summary>
    public enum ReelState
    {
        Idle = 0,
        SpinUp = 1,
        Cruising = 2,
        Decelerating = 3
    }

    /// <summary>
    /// Drives vertical procedural motion, endless symbol recycling, and elastic bounce settling
    /// for a single slot reel column inside a masked aperture.
    /// Completely zero-GC in hot paths; Update loop disabled when idle.
    /// </summary>
    public class SlotReel : MonoBehaviour
    {
        [Header("Reel Configuration")]
        [Tooltip("Vertical spacing in pixels between consecutive symbol centers.")]
        [SerializeField] private float _itemSpacing = 110f;

        [Tooltip("Cruising speed of reel rotation in pixels per second.")]
        [SerializeField] private float _cruisingSpeed = 1600f;

        [Tooltip("Duration in seconds of the initial spin-up acceleration phase.")]
        [SerializeField] private float _spinUpDuration = 0.2f;

        [Tooltip("Minimum symbol steps traveled during anticipation deceleration before stopping.")]
        [SerializeField] private int _minDecelSteps = 8;

        [Tooltip("Array of symbol item views belonging to this reel. If empty, auto-populated from children.")]
        [SerializeField] private SlotSymbolItem[] _symbolItems;

        [Tooltip("Optional PayoutTable used to resolve symbols if catalog is omitted.")]
        [SerializeField] private PayoutTable _payoutTable;

        // Hot-path state caches
        private ReelState _state = ReelState.Idle;
        private float[] _baseY;
        private int[] _lastCycles;
        private float _totalSpan;
        private float _halfSpan;
        private int _centerSlotIndex;

        // Motion variables
        private float _currentDistance;
        private float _currentSpeed;
        private float _spinTimer;
        private float _decelTimer;
        private float _decelDuration;
        private float _decelStartDistance;
        private float _decelTotalDistance;
        private float _targetDistance;

        // Stopping & RNG
        private bool _isStopping;
        private int _targetSlotIndex;
        private SymbolType _targetSymbol;
        private SymbolData[] _catalog;
        private IRandomProvider _randomProvider;

        public event Action<SlotReel> OnSpinStarted;
        public event Action<SlotReel> OnSpinStopped;

        public ReelState State => _state;
        public bool IsSpinning => _state != ReelState.Idle;
        public float ItemSpacing => _itemSpacing;

        private void Awake()
        {
            InitializeItemArray();
            enabled = false; // Disable update polling when idle
        }

        private void InitializeItemArray()
        {
            if (_symbolItems == null || _symbolItems.Length == 0)
            {
                _symbolItems = GetComponentsInChildren<SlotSymbolItem>(true);
            }

            int count = _symbolItems.Length;
            if (count == 0) return;

            _centerSlotIndex = count / 2;
            _baseY = new float[count];
            _lastCycles = new int[count];

            for (int i = 0; i < count; i++)
            {
                _baseY[i] = (i - _centerSlotIndex) * _itemSpacing;
                _symbolItems[i].SetAnchoredY(_baseY[i]);
            }

            _totalSpan = count * _itemSpacing;
            _halfSpan = _totalSpan * 0.5f;

            ResetCycles();
        }

        private void ResetCycles()
        {
            if (_baseY == null) return;

            for (int i = 0; i < _baseY.Length; i++)
            {
                _lastCycles[i] = Mathf.FloorToInt((_baseY[i] - _currentDistance + _halfSpan) / _totalSpan);
            }
        }

        /// <summary>
        /// Initiates continuous procedural spinning on this reel.
        /// </summary>
        public void Spin(SymbolData[] catalog = null, IRandomProvider rng = null)
        {
            if (_symbolItems == null || _symbolItems.Length == 0)
            {
                InitializeItemArray();
            }

            ResolveCatalogAndRng(catalog, rng);

            // Reset visual highlights
            for (int i = 0; i < _symbolItems.Length; i++)
            {
                _symbolItems[i].ResetVisualState();
            }

            _state = ReelState.SpinUp;
            _spinTimer = 0f;
            _currentSpeed = 0f;
            _isStopping = false;

            ResetCycles();
            enabled = true; // Activate Update loop

            OnSpinStarted?.Invoke(this);
        }

        /// <summary>
        /// Commands this reel to begin deceleration and snap cleanly to the given target symbol.
        /// </summary>
        public void Stop(SymbolType targetSymbol)
        {
            if (_state == ReelState.Idle || _state == ReelState.Decelerating) return;

            _targetSymbol = targetSymbol;
            _isStopping = true;

            int count = _symbolItems.Length;
            float currentStep = _currentDistance / _itemSpacing;
            int targetStep = Mathf.CeilToInt(currentStep) + _minDecelSteps;

            _targetDistance = targetStep * _itemSpacing;
            _decelStartDistance = _currentDistance;
            _decelTotalDistance = _targetDistance - _decelStartDistance;

            // Determine which physical item index will land at y = 0
            _targetSlotIndex = ((targetStep + _centerSlotIndex) % count + count) % count;

            // Deceleration duration dynamically tailored to distance and speed
            _decelDuration = Mathf.Max(0.45f, _decelTotalDistance / (_cruisingSpeed * 0.7f));
            _decelTimer = 0f;
            _state = ReelState.Decelerating;
        }

        /// <summary>
        /// Instantly snaps this reel to display the target symbol on the payline without animation.
        /// </summary>
        public void SnapToSymbol(SymbolType symbol, SymbolData[] catalog = null)
        {
            if (_symbolItems == null || _symbolItems.Length == 0)
            {
                InitializeItemArray();
            }

            ResolveCatalogAndRng(catalog, null);

            _currentDistance = 0f;
            _state = ReelState.Idle;
            _isStopping = false;

            int count = _symbolItems.Length;
            SymbolData targetData = GetSymbolData(symbol);

            for (int i = 0; i < count; i++)
            {
                _symbolItems[i].SetAnchoredY(_baseY[i]);
                if (i == _centerSlotIndex)
                {
                    _symbolItems[i].SetSymbol(targetData);
                }
                else
                {
                    _symbolItems[i].SetSymbol(GetRandomSymbol());
                }
                _symbolItems[i].ResetVisualState();
            }

            ResetCycles();
            enabled = false;
        }

        private void Update()
        {
            switch (_state)
            {
                case ReelState.SpinUp:
                    UpdateSpinUp();
                    break;

                case ReelState.Cruising:
                    UpdateCruising();
                    break;

                case ReelState.Decelerating:
                    UpdateDecelerating();
                    break;

                case ReelState.Idle:
                default:
                    enabled = false;
                    break;
            }
        }

        private void UpdateSpinUp()
        {
            _spinTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_spinTimer / _spinUpDuration);

            // Smooth quadratic acceleration
            _currentSpeed = Mathf.Lerp(0f, _cruisingSpeed, t * t);
            _currentDistance += _currentSpeed * Time.deltaTime;

            ApplyItemPositionsAndRecycle();

            if (t >= 1f)
            {
                _currentSpeed = _cruisingSpeed;
                _state = ReelState.Cruising;
            }
        }

        private void UpdateCruising()
        {
            _currentDistance += _cruisingSpeed * Time.deltaTime;
            ApplyItemPositionsAndRecycle();
        }

        private void UpdateDecelerating()
        {
            _decelTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_decelTimer / _decelDuration);
            float ease = EaseOutBack(t);

            _currentDistance = _decelStartDistance + (_decelTotalDistance * ease);

            ApplyItemPositionsAndRecycle();

            if (t >= 1f)
            {
                FinishStop();
            }
        }

        private void ApplyItemPositionsAndRecycle()
        {
            int count = _symbolItems.Length;

            for (int i = 0; i < count; i++)
            {
                float y = Mathf.Repeat(_baseY[i] - _currentDistance + _halfSpan, _totalSpan) - _halfSpan;
                _symbolItems[i].SetAnchoredY(y);

                // Detect when item wraps from bottom to top
                int cycle = Mathf.FloorToInt((_baseY[i] - _currentDistance + _halfSpan) / _totalSpan);
                if (cycle != _lastCycles[i])
                {
                    _lastCycles[i] = cycle;

                    // Assign target symbol if this item is the designated landing slot
                    if (_isStopping && i == _targetSlotIndex)
                    {
                        _symbolItems[i].SetSymbol(GetSymbolData(_targetSymbol));
                    }
                    else
                    {
                        _symbolItems[i].SetSymbol(GetRandomSymbol());
                    }
                }
            }
        }

        private void FinishStop()
        {
            _currentDistance = _targetDistance;

            int count = _symbolItems.Length;
            SymbolData targetData = GetSymbolData(_targetSymbol);

            // Strictly enforce exact grid alignment and center symbol
            for (int i = 0; i < count; i++)
            {
                int relativeOffset = (i - _targetSlotIndex + count) % count;
                if (relativeOffset > count / 2) relativeOffset -= count;

                float finalY = relativeOffset * _itemSpacing;
                _symbolItems[i].SetAnchoredY(finalY);

                if (i == _targetSlotIndex)
                {
                    _symbolItems[i].SetSymbol(targetData);
                }
            }

            _state = ReelState.Idle;
            _isStopping = false;
            ResetCycles();
            enabled = false; // Turn off Update loop

            OnSpinStopped?.Invoke(this);
        }

        /// <summary>
        /// Returns the item currently positioned directly on the center payline.
        /// </summary>
        public SlotSymbolItem GetPaylineItem()
        {
            if (_symbolItems == null || _symbolItems.Length == 0) return null;

            int closestIndex = 0;
            float minAbsY = float.MaxValue;

            for (int i = 0; i < _symbolItems.Length; i++)
            {
                float absY = Mathf.Abs(_symbolItems[i].RectTransform.anchoredPosition.y);
                if (absY < minAbsY)
                {
                    minAbsY = absY;
                    closestIndex = i;
                }
            }

            return _symbolItems[closestIndex];
        }

        /// <summary>
        /// Returns the symbol type currently visible on the center payline.
        /// </summary>
        public SymbolType GetPaylineSymbolType()
        {
            SlotSymbolItem paylineItem = GetPaylineItem();
            return paylineItem != null ? paylineItem.CurrentSymbolType : SymbolType.Symbol1;
        }

        private void ResolveCatalogAndRng(SymbolData[] catalog, IRandomProvider rng)
        {
            if (catalog != null && catalog.Length > 0)
            {
                _catalog = catalog;
            }
            else if (_payoutTable != null && _payoutTable.Symbols != null && _payoutTable.Symbols.Count > 0)
            {
                var tableSymbols = _payoutTable.Symbols;
                if (_catalog == null || _catalog.Length != tableSymbols.Count)
                {
                    _catalog = new SymbolData[tableSymbols.Count];
                }
                for (int i = 0; i < tableSymbols.Count; i++)
                {
                    _catalog[i] = tableSymbols[i];
                }
            }

            if (rng != null)
            {
                _randomProvider = rng;
            }
            else if (_randomProvider == null)
            {
                _randomProvider = new UnityRngProvider();
            }
        }

        private SymbolData GetSymbolData(SymbolType type)
        {
            if (_catalog != null)
            {
                for (int i = 0; i < _catalog.Length; i++)
                {
                    if (_catalog[i] != null && _catalog[i].SymbolType == type)
                    {
                        return _catalog[i];
                    }
                }
            }

            if (_payoutTable != null)
            {
                return _payoutTable.GetSymbolData(type);
            }

            return null;
        }

        private SymbolData GetRandomSymbol()
        {
            if (_catalog == null || _catalog.Length == 0) return null;
            int randomIndex = _randomProvider.Range(0, _catalog.Length);
            return _catalog[randomIndex];
        }

        /// <summary>
        /// Standard EaseOutBack easing function providing natural elastic overshoot and settle.
        /// </summary>
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f; // 2.70158f
            float tMinus1 = t - 1f;
            return 1f + (c3 * tMinus1 * tMinus1 * tMinus1) + (c1 * tMinus1 * tMinus1);
        }
    }
}

