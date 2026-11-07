using System;
using UnityEngine;
using SlotMachine.Config;
using SlotMachine.Reel;

namespace SlotMachine.Core
{
    /// <summary>
    /// Master coordinator for the slot machine game.
    /// Manages player credit balance, bet adjustments, spin lifecycle pipeline,
    /// RNG outcome generation, and win evaluation dispatching.
    /// Strictly event-driven and zero-allocation in hot paths.
    /// </summary>
    public class SlotMachineController : MonoBehaviour
    {
        [Header("Hardware / View References")]
        [Tooltip("The reel group coordinating visual symbol strips.")]
        [SerializeField] private ReelGroup _reelGroup;

        [Tooltip("The master payout and rules table.")]
        [SerializeField] private PayoutTable _payoutTable;

        [Header("Economy Ledger")]
        [Tooltip("Initial player credits granted on startup.")]
        [SerializeField] private int _startingBalance = 1000;

        [Tooltip("Default bet amount on initial load.")]
        [SerializeField] private int _defaultBet = 10;

        [Tooltip("Minimum allowable wager per spin.")]
        [SerializeField] private int _minBet = 5;

        [Tooltip("Maximum allowable wager per spin.")]
        [SerializeField] private int _maxBet = 100;

        [Tooltip("Credit increment/decrement step when modifying bet.")]
        [SerializeField] private int _betStep = 5;

        [Header("Celebration Timings")]
        [Tooltip("Duration in seconds to display win line highlight before returning to Idle.")]
        [SerializeField] private float _winCelebrationDuration = 1.5f;

        // Runtime state
        private SlotMachineState _state = SlotMachineState.Idle;
        private int _currentBalance;
        private int _currentBet;
        private float _celebrationTimer;
        private bool _isTimingCelebration;

        // Pure calculation service & buffers
        private PayoutOutcomeService _outcomeService;
        private IRandomProvider _randomProvider;
        private SymbolType[] _targetStops;
        private SpinOutcome _lastOutcome;

        // Public lifecycle & economy events
        public event Action<SlotMachineState> OnStateChanged;
        public event Action<int> OnBalanceChanged;
        public event Action<int> OnBetChanged;
        public event Action<SpinOutcome> OnSpinStarted;
        public event Action<SpinOutcome> OnSpinCompleted;
        public event Action<SpinOutcome> OnWinCelebrationRequested;

        // Read-only state queries
        public SlotMachineState State => _state;
        public int Balance => _currentBalance;
        public int Bet => _currentBet;
        public int MinBet => _minBet;
        public int MaxBet => _maxBet;
        public int BetStep => _betStep;
        public SpinOutcome LastOutcome => _lastOutcome;

        public bool CanSpin => _state == SlotMachineState.Idle &&
                               _currentBalance >= _currentBet &&
                               _currentBet > 0;

        public bool CanChangeBet => _state == SlotMachineState.Idle;

        private void Awake()
        {
            InitializeDependencies();
            _currentBalance = _startingBalance;
            _currentBet = Mathf.Clamp(_defaultBet, _minBet, _maxBet);

            enabled = false; // Disable update when idle
        }

        private void Start()
        {
            // Initial event broadcast for listeners (UI HUD)
            OnBalanceChanged?.Invoke(_currentBalance);
            OnBetChanged?.Invoke(_currentBet);
            OnStateChanged?.Invoke(_state);
        }

        private void OnEnable()
        {
            if (_reelGroup != null)
            {
                _reelGroup.OnAllReelsStopped += HandleAllReelsStopped;
            }
        }

        private void OnDisable()
        {
            if (_reelGroup != null)
            {
                _reelGroup.OnAllReelsStopped -= HandleAllReelsStopped;
            }
        }

        private void InitializeDependencies()
        {
            if (_reelGroup == null)
            {
                _reelGroup = GetComponentInChildren<ReelGroup>();
            }

            int reelCount = _reelGroup != null ? _reelGroup.ReelCount : 3;
            if (_targetStops == null || _targetStops.Length != reelCount)
            {
                _targetStops = new SymbolType[reelCount];
            }

            if (_randomProvider == null)
            {
                _randomProvider = new UnityRngProvider();
            }

            if (_payoutTable != null && _outcomeService == null)
            {
                _outcomeService = new PayoutOutcomeService(_payoutTable, _randomProvider);
            }
        }

        /// <summary>
        /// Explicit dependency injection for test harnesses or dynamic initialization.
        /// </summary>
        public void Initialize(PayoutTable payoutTable, ReelGroup reelGroup, IRandomProvider rng = null)
        {
            _payoutTable = payoutTable;
            _reelGroup = reelGroup;
            _randomProvider = rng ?? new UnityRngProvider();

            if (_payoutTable != null)
            {
                _outcomeService = new PayoutOutcomeService(_payoutTable, _randomProvider);
            }

            int reelCount = _reelGroup != null ? _reelGroup.ReelCount : 3;
            _targetStops = new SymbolType[reelCount];

            if (isActiveAndEnabled && _reelGroup != null)
            {
                _reelGroup.OnAllReelsStopped -= HandleAllReelsStopped;
                _reelGroup.OnAllReelsStopped += HandleAllReelsStopped;
            }
        }

        /// <summary>
        /// Increases the current bet by one step up to the maximum allowable limit.
        /// </summary>
        public bool IncreaseBet()
        {
            if (!CanChangeBet) return false;

            int targetBet = _currentBet + _betStep;
            int limit = Mathf.Min(_maxBet, _currentBalance > 0 ? _currentBalance : _maxBet);

            if (targetBet > limit)
            {
                targetBet = limit;
            }

            if (targetBet != _currentBet)
            {
                _currentBet = targetBet;
                OnBetChanged?.Invoke(_currentBet);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Decreases the current bet by one step down to the minimum bet limit.
        /// </summary>
        public bool DecreaseBet()
        {
            if (!CanChangeBet) return false;

            int targetBet = _currentBet - _betStep;
            if (targetBet < _minBet)
            {
                targetBet = _minBet;
            }

            if (targetBet != _currentBet)
            {
                _currentBet = targetBet;
                OnBetChanged?.Invoke(_currentBet);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sets the current bet directly to the maximum allowable by balance and max bet setting.
        /// </summary>
        public bool SetMaxBet()
        {
            if (!CanChangeBet) return false;

            int maxAffordable = Mathf.Min(_maxBet, _currentBalance);
            int targetBet = Mathf.Max(_minBet, maxAffordable);

            if (targetBet != _currentBet)
            {
                _currentBet = targetBet;
                OnBetChanged?.Invoke(_currentBet);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sets the bet amount directly with clamping.
        /// </summary>
        public bool SetBet(int newBet)
        {
            if (!CanChangeBet) return false;

            int clamped = Mathf.Clamp(newBet, _minBet, Mathf.Min(_maxBet, _currentBalance));
            if (clamped != _currentBet)
            {
                _currentBet = clamped;
                OnBetChanged?.Invoke(_currentBet);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Credits additional coins/credits to the player's balance (e.g., reward, bonus, reload).
        /// </summary>
        public void AddBalance(int amount)
        {
            if (amount <= 0) return;

            _currentBalance += amount;
            OnBalanceChanged?.Invoke(_currentBalance);
        }

        /// <summary>
        /// Executes the core spin pipeline: deducts wager, calculates RNG outcome,
        /// commands reels to spin, and transitions state machine.
        /// </summary>
        public bool Spin()
        {
            if (!CanSpin) return false;

            InitializeDependencies();

            // 1. Deduct wager
            _currentBalance -= _currentBet;
            OnBalanceChanged?.Invoke(_currentBalance);

            // 2. Transition state to Spinning
            SetState(SlotMachineState.Spinning);

            // 3. Pre-calculate deterministic outcome via pure logic service
            _outcomeService.GenerateRandomStops(_targetStops);
            _lastOutcome = _outcomeService.EvaluateOutcome(_targetStops, _currentBet);

            // 4. Command physical/visual reels
            if (_reelGroup != null)
            {
                _reelGroup.Spin(_targetStops, _randomProvider);
            }

            OnSpinStarted?.Invoke(_lastOutcome);
            return true;
        }

        /// <summary>
        /// Requests an immediate stop for spinning reels if permitted.
        /// </summary>
        public void QuickStop()
        {
            if (_state == SlotMachineState.Spinning && _reelGroup != null)
            {
                _reelGroup.QuickStop();
            }
        }

        private void HandleAllReelsStopped()
        {
            SetState(SlotMachineState.EvaluatingWin);

            if (_lastOutcome.IsWin)
            {
                // Award winnings to balance
                _currentBalance += _lastOutcome.PayoutAmount;
                OnBalanceChanged?.Invoke(_currentBalance);

                // Highlight payline symbols
                HighlightPaylineSymbols(true);

                // Transition to celebration state
                SetState(SlotMachineState.WinCelebration);
                OnWinCelebrationRequested?.Invoke(_lastOutcome);

                // Start celebration timer if duration > 0
                if (_winCelebrationDuration > 0f)
                {
                    _celebrationTimer = 0f;
                    _isTimingCelebration = true;
                    enabled = true; // Enable Update for timer
                }
            }
            else
            {
                // No win, return directly to Idle
                SetState(SlotMachineState.Idle);
                OnSpinCompleted?.Invoke(_lastOutcome);
            }
        }

        /// <summary>
        /// Dismisses an active win celebration immediately, clearing highlights and returning to Idle.
        /// </summary>
        public void DismissWinCelebration()
        {
            if (_state != SlotMachineState.WinCelebration) return;

            _isTimingCelebration = false;
            enabled = false;

            HighlightPaylineSymbols(false);
            SetState(SlotMachineState.Idle);
            OnSpinCompleted?.Invoke(_lastOutcome);
        }

        private void Update()
        {
            if (!_isTimingCelebration)
            {
                enabled = false;
                return;
            }

            _celebrationTimer += Time.deltaTime;
            if (_celebrationTimer >= _winCelebrationDuration)
            {
                DismissWinCelebration();
            }
        }

        private void HighlightPaylineSymbols(bool highlight)
        {
            if (_reelGroup == null) return;

            int count = _reelGroup.ReelCount;
            for (int i = 0; i < count; i++)
            {
                var paylineItem = _reelGroup.GetPaylineItem(i);
                if (paylineItem != null)
                {
                    if (highlight)
                    {
                        paylineItem.SetHighlight(true);
                    }
                    else
                    {
                        paylineItem.ResetVisualState();
                    }
                }
            }
        }

        private void SetState(SlotMachineState newState)
        {
            if (_state != newState)
            {
                _state = newState;
                OnStateChanged?.Invoke(_state);
            }
        }
    }
}

