using System;
using System.Collections.Generic;
using UnityEngine;
using SlotMachine.Config;
using SlotMachine.Core;

namespace SlotMachine.Reel
{
    /// <summary>
    /// Coordinates multiple SlotReel instances, orchestrating synchronous spin launches,
    /// staggered anticipation stop sequences, quick-stop overrides, and payline evaluation queries.
    /// Strictly zero-allocation during gameplay; Update loop is disabled when idle.
    /// </summary>
    public class ReelGroup : MonoBehaviour
    {
        [Header("Reels Assembly")]
        [Tooltip("The ordered array of reels (e.g. Left, Middle, Right). Auto-discovered if empty.")]
        [SerializeField] private SlotReel[] _reels;

        [Header("Timing Parameters")]
        [Tooltip("Base duration in seconds before the first reel starts its deceleration.")]
        [SerializeField] private float _spinDuration = 1.2f;

        [Tooltip("Staggered delay in seconds between consecutive reel stops.")]
        [SerializeField] private float _staggerDelay = 0.35f;

        [Header("Data References")]
        [Tooltip("Default payout table used for symbol catalog resolution.")]
        [SerializeField] private PayoutTable _payoutTable;

        // Runtime states
        private bool _isSpinning;
        private bool _quickStopRequested;
        private float _spinTimer;
        private int _nextStopIndex;
        private int _stoppedReelsCount;
        private SymbolType[] _targetStops;
        private SymbolData[] _catalog;
        private IRandomProvider _randomProvider;

        public event Action OnAllReelsSpinStarted;
        public event Action<int, SlotReel> OnReelStopped;
        public event Action OnAllReelsStopped;

        public bool IsSpinning => _isSpinning;
        public int ReelCount => _reels != null ? _reels.Length : 0;
        public IReadOnlyList<SlotReel> Reels => _reels;

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
                    _reels[i].OnSpinStopped += (reel) => HandleReelStopped(index, reel);
                }
            }

            ResolveCatalog();
            enabled = false; // Disable update when idle
        }

        private void OnDestroy()
        {
            if (_reels != null)
            {
                for (int i = 0; i < _reels.Length; i++)
                {
                    if (_reels[i] != null)
                    {
                        // Clean unsubscription is handled per reel lifecycle
                    }
                }
            }
        }

        private void ResolveCatalog()
        {
            if (_catalog == null && _payoutTable != null && _payoutTable.Symbols != null)
            {
                var tableSymbols = _payoutTable.Symbols;
                _catalog = new SymbolData[tableSymbols.Count];
                for (int i = 0; i < tableSymbols.Count; i++)
                {
                    _catalog[i] = tableSymbols[i];
                }
            }
        }

        /// <summary>
        /// Initiates a synchronized spin across all reels toward designated target stopping symbols.
        /// </summary>
        public void Spin(SymbolType[] targetStops, IRandomProvider rng = null)
        {
            if (_isSpinning) return;
            if (_reels == null || _reels.Length == 0) return;

            if (targetStops == null || targetStops.Length < _reels.Length)
            {
                throw new ArgumentException($"Target stops count must match reel count ({_reels.Length})", nameof(targetStops));
            }

            ResolveCatalog();

            if (rng != null)
            {
                _randomProvider = rng;
            }
            else if (_randomProvider == null)
            {
                _randomProvider = new UnityRngProvider();
            }

            for (int i = 0; i < _reels.Length; i++)
            {
                _targetStops[i] = targetStops[i];
                _reels[i].Spin(_catalog, _randomProvider);
            }

            _isSpinning = true;
            _quickStopRequested = false;
            _spinTimer = 0f;
            _nextStopIndex = 0;
            _stoppedReelsCount = 0;

            enabled = true; // Activate Update loop

            OnAllReelsSpinStarted?.Invoke();
        }

        /// <summary>
        /// Requests an immediate stop for any spinning reels, skipping anticipation delays.
        /// </summary>
        public void QuickStop()
        {
            if (!_isSpinning || _quickStopRequested) return;

            _quickStopRequested = true;

            // Immediately command any pending unstopped reels
            while (_nextStopIndex < _reels.Length)
            {
                _reels[_nextStopIndex].Stop(_targetStops[_nextStopIndex]);
                _nextStopIndex++;
            }
        }

        /// <summary>
        /// Instantly snaps all reels to display the specified symbols without spinning.
        /// </summary>
        public void SnapAllToSymbols(SymbolType[] symbols)
        {
            if (_reels == null || symbols == null) return;

            ResolveCatalog();
            int count = Mathf.Min(_reels.Length, symbols.Length);

            for (int i = 0; i < count; i++)
            {
                _reels[i].SnapToSymbol(symbols[i], _catalog);
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

            _spinTimer += Time.deltaTime;

            // Check if next reel in stagger sequence is ready to stop
            while (_nextStopIndex < _reels.Length)
            {
                float scheduledStopTime = _spinDuration + (_nextStopIndex * _staggerDelay);

                if (_quickStopRequested || _spinTimer >= scheduledStopTime)
                {
                    _reels[_nextStopIndex].Stop(_targetStops[_nextStopIndex]);
                    _nextStopIndex++;

                    if (!_quickStopRequested)
                    {
                        break; // Await next frame for remaining stagger
                    }
                }
                else
                {
                    break;
                }
            }
        }

        private void HandleReelStopped(int reelIndex, SlotReel reel)
        {
            _stoppedReelsCount++;
            OnReelStopped?.Invoke(reelIndex, reel);

            if (_stoppedReelsCount >= _reels.Length)
            {
                _isSpinning = false;
                enabled = false; // Turn off Update loop when all reels are idle

                OnAllReelsStopped?.Invoke();
            }
        }

        /// <summary>
        /// Reads the symbols currently centered on the payline across all reels.
        /// Fills caller-provided buffer to prevent heap allocations.
        /// </summary>
        public void GetPaylineSymbols(SymbolType[] destinationBuffer)
        {
            if (destinationBuffer == null || _reels == null) return;

            int count = Mathf.Min(destinationBuffer.Length, _reels.Length);
            for (int i = 0; i < count; i++)
            {
                destinationBuffer[i] = _reels[i].GetPaylineSymbolType();
            }
        }

        /// <summary>
        /// Retrieves the payline symbol item for a specific reel.
        /// </summary>
        public SlotSymbolItem GetPaylineItem(int reelIndex)
        {
            if (_reels == null || reelIndex < 0 || reelIndex >= _reels.Length) return null;
            return _reels[reelIndex].GetPaylineItem();
        }
    }
}
