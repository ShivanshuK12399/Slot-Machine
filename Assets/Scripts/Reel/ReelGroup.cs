using System;
using System.Collections.Generic;
using UnityEngine;
using SlotMachine.Config;
using SlotMachine.Core;

namespace SlotMachine.Reel
{
    /// <summary>
    /// Selectable stopping mechanism physics for the slot reels.
    /// </summary>
    public enum ReelStopMode
    {
        [Tooltip("Authentic vintage one-armed bandit: High speed spin, instant pawl drop into star wheel with damped spring recoil")]
        MechanicalPawl = 0,

        [Tooltip("Modern physical stepper slot: Smooth ramp-down to crisp lock with zero bounce")]
        StepperMotor = 1
    }

    /// <summary>
    /// Manages multiple slot reels, coordinating spin starts, staggered mechanical stops,
    /// and payline evaluations.
    /// </summary>
    public class ReelGroup : MonoBehaviour
    {
        [Header("Stopping Mechanism")]
        [Tooltip("Select the physical stopping simulation mode.")]
        [SerializeField] private ReelStopMode _stopMode = ReelStopMode.MechanicalPawl;

        [Header("Reels Assembly")]
        [Tooltip("Reel columns in order from left to right. Auto-discovered if empty.")]
        [SerializeField] private SlotReel[] _reels;

        [Header("Reel Settings")]
        [Tooltip("Vertical pixel spacing between symbol centers.")]
        [SerializeField] private float _itemSpacing = 70f;

        [Tooltip("Spin-up acceleration time in seconds.")]
        [SerializeField] private float _spinUpDuration = 0.2f;

        [Tooltip("Spin duration in seconds for each reel before stopping (Reel 0, Reel 1, Reel 2...).")]
        [SerializeField] private float[] _reelSpinDurations = new float[] { 1.0f, 1.8f, 2.6f };

        [Header("Data References")]
        [Tooltip("Master payout table providing symbol catalog data.")]
        [SerializeField] private PayoutTable _payoutTable;

        [Tooltip("Prefab template for reel symbol items.")]
        [SerializeField] private SlotSymbolItem _symbolItemPrefab;

        // Runtime states
        private bool _isSpinning;
        private bool _quickStop;
        private float _timer;
        private int _nextStop;
        private int _stoppedCount;
        private SymbolType[] _targetStops;
        private SymbolData[] _catalog;
        private IRandomProvider _rng;

        // Events
        public event Action OnAllReelsSpinStarted;
        public event Action<int, SlotReel> OnReelStopped;
        public event Action OnAllReelsStopped;

        // Properties
        public ReelStopMode StopMode => _stopMode;
        public bool IsSpinning => _isSpinning;
        public int ReelCount => _reels != null ? _reels.Length : 0;
        public IReadOnlyList<SlotReel> Reels => _reels;
        public PayoutTable PayoutTable => _payoutTable;
        public float ItemSpacing => _itemSpacing;
        public float SpinUpDuration => _spinUpDuration;
        public SlotSymbolItem SymbolItemPrefab => _symbolItemPrefab;

        public float GetSpinDuration(int index)
        {
            if (_reelSpinDurations != null && index >= 0 && index < _reelSpinDurations.Length)
            {
                return _reelSpinDurations[index];
            }
            return 1.0f + (index * 0.8f);
        }

        public void SetPayoutTable(PayoutTable table)
        {
            _payoutTable = table;
            _catalog = null;
            ResolveCatalog();
        }

        private void Awake()
        {
            if (_reels == null || _reels.Length == 0)
            {
                _reels = GetComponentsInChildren<SlotReel>(true);
            }

            if (_reels != null)
            {
                _targetStops = new SymbolType[_reels.Length];
                for (int i = 0; i < _reels.Length; i++)
                {
                    int index = i;
                    if (_reels[i] != null)
                    {
                        _reels[i].SetReelIndex(index);
                        _reels[i].SetReelGroup(this);
                        _reels[i].OnSpinStopped += (reel) => HandleReelStopped(index, reel);
                    }
                }
            }

            ResolveCatalog();
            enabled = false;
        }

        private void ResolveCatalog()
        {
            if (_payoutTable == null)
            {
                var controller = FindFirstObjectByType<SlotMachineController>();
                if (controller != null)
                {
                    _payoutTable = controller.PayoutTable;
                }
            }

            if (_catalog == null && _payoutTable != null && _payoutTable.Symbols != null)
            {
                var list = _payoutTable.Symbols;
                _catalog = new SymbolData[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    _catalog[i] = list[i];
                }
            }
        }

        /// <summary>
        /// Starts synchronized spinning across all reels toward target symbols.
        /// </summary>
        public void Spin(SymbolType[] targetStops, IRandomProvider rng = null)
        {
            if (_isSpinning) return;

            if (_reels == null || _reels.Length == 0)
            {
                _reels = GetComponentsInChildren<SlotReel>(true);
            }

            if (_reels == null || _reels.Length == 0) return;

            if (targetStops == null || targetStops.Length < _reels.Length)
            {
                throw new ArgumentException($"Target count must match reel count ({_reels.Length})", nameof(targetStops));
            }

            ResolveCatalog();
            _rng = rng ?? new UnityRngProvider();

            for (int i = 0; i < _reels.Length; i++)
            {
                _targetStops[i] = targetStops[i];
                if (_reels[i] != null)
                {
                    _reels[i].SetReelIndex(i);
                    _reels[i].SetReelGroup(this);
                    _reels[i].Spin(_catalog, _rng);
                }
            }

            _isSpinning = true;
            _quickStop = false;
            _timer = 0f;
            _nextStop = 0;
            _stoppedCount = 0;

            enabled = true;
            OnAllReelsSpinStarted?.Invoke();
        }

        /// <summary>
        /// Commands all remaining spinning reels to stop immediately.
        /// </summary>
        public void QuickStop()
        {
            if (!_isSpinning || _quickStop) return;

            _quickStop = true;
            while (_nextStop < _reels.Length)
            {
                if (_reels[_nextStop] != null)
                {
                    _reels[_nextStop].Stop(_targetStops[_nextStop]);
                }
                _nextStop++;
            }
        }

        /// <summary>
        /// Instantly snaps all reels to target symbols without animation.
        /// </summary>
        public void SnapAllToSymbols(SymbolType[] symbols)
        {
            if (_reels == null || symbols == null) return;

            ResolveCatalog();
            int count = Mathf.Min(_reels.Length, symbols.Length);

            for (int i = 0; i < count; i++)
            {
                if (_reels[i] != null)
                {
                    _reels[i].SetReelIndex(i);
                    _reels[i].SetReelGroup(this);
                    _reels[i].SnapToSymbol(symbols[i], _catalog);
                }
            }

            _isSpinning = false;
            enabled = false;
        }

        private void Update()
        {
            if (!_isSpinning)
            {
                enabled = false;
                return;
            }

            _timer += Time.deltaTime;

            // Trigger each reel in sequence when its spin time arrives
            while (_nextStop < _reels.Length)
            {
                float stopTime = GetSpinDuration(_nextStop);

                if (_quickStop || _timer >= stopTime)
                {
                    if (_reels[_nextStop] != null)
                    {
                        _reels[_nextStop].Stop(_targetStops[_nextStop]);
                    }
                    _nextStop++;

                    if (!_quickStop) break;
                }
                else
                {
                    break;
                }
            }
        }

        private void HandleReelStopped(int index, SlotReel reel)
        {
            _stoppedCount++;
            SlotMachine.Audio.AudioManager.Instance?.PlayReelStop(index);
            OnReelStopped?.Invoke(index, reel);

            if (_stoppedCount >= _reels.Length)
            {
                _isSpinning = false;
                enabled = false;
                OnAllReelsStopped?.Invoke();
            }
        }

        public void GetPaylineSymbols(SymbolType[] buffer)
        {
            if (buffer == null || _reels == null) return;
            int count = Mathf.Min(buffer.Length, _reels.Length);
            for (int i = 0; i < count; i++)
            {
                buffer[i] = _reels[i] != null ? _reels[i].GetPaylineSymbolType() : SymbolType.Symbol1;
            }
        }

        public SlotSymbolItem GetPaylineItem(int reelIndex)
        {
            if (_reels == null || reelIndex < 0 || reelIndex >= _reels.Length) return null;
            return _reels[reelIndex].GetPaylineItem();
        }
    }
}
