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
        Idle,
        SpinUp,
        Spinning,
        Braking,
        Bouncing
    }

    /// <summary>
    /// Controls vertical scrolling, endless symbol recycling, and mechanical stopping
    /// with authentic star-wheel pawl recoil physics.
    /// </summary>
    public class SlotReel : MonoBehaviour
    {
        [Header("Reel Binding")]
        [Tooltip("Parent ReelGroup coordinating all reels. Auto-discovered if null.")]
        [SerializeField] private ReelGroup _group;

        [Tooltip("Index of this reel column (0 = Left, 1 = Middle, 2 = Right).")]
        [SerializeField] private int _reelIndex;

        [Header("Speed Settings")]
        [Tooltip("Maximum cruising spin speed in pixels per second.")]
        [SerializeField] private float _maxSpeed = 1400f;

        // Auto-discovered symbol items
        private SlotSymbolItem[] _items;

        // Reel geometry
        private float[] _baseY;
        private int[] _lastCycles;
        private float _totalSpan;
        private float _halfSpan;
        private int _centerSlot;

        // Runtime motion
        private ReelState _state = ReelState.Idle;
        private float _speed;
        private float _distance;
        private float _startDistance;
        private float _targetDistance;
        private float _timer;
        private float _brakeDuration;
        private float _bounceDuration;
        private float _bounceHeight;
        private float _lastTickDistance;

        // Target stop data
        private bool _isStopping;
        private int _targetSlot;
        private SymbolType _targetSymbol;
        private SymbolData[] _catalog;
        private IRandomProvider _rng;

        // Events
        public event Action<SlotReel> OnSpinStarted;
        public event Action<SlotReel> OnSpinStopped;

        // Properties
        public ReelState State => _state;
        public bool IsSpinning => _state != ReelState.Idle;
        public int ReelIndex => _reelIndex;
        public float MaxSpeed => _maxSpeed;
        public float ItemSpacing => _group != null ? _group.ItemSpacing : 70f;
        public float SpinUpDuration => _group != null ? _group.SpinUpDuration : 0.2f;
        public PayoutTable PayoutTable => _group != null ? _group.PayoutTable : null;
        public SlotSymbolItem[] SymbolItems => _items;

        public void SetReelGroup(ReelGroup group)
        {
            _group = group;
            _catalog = null;
            ResolveCatalogAndRng(null, null);
        }

        public void SetReelIndex(int index)
        {
            _reelIndex = index;
        }

        private void Awake()
        {
            if (_group == null)
            {
                _group = GetComponentInParent<ReelGroup>();
            }

            InitializeItems();
            enabled = false;
        }

        public void InitializeItemArray() => InitializeItems();

        public void InitializeItems()
        {
            if (_items == null || _items.Length == 0)
            {
                _items = GetComponentsInChildren<SlotSymbolItem>(true);
            }

            int count = _items != null ? _items.Length : 0;
            if (count == 0) return;

            _centerSlot = count / 2;
            if (_baseY == null || _baseY.Length != count)
            {
                _baseY = new float[count];
            }
            if (_lastCycles == null || _lastCycles.Length != count)
            {
                _lastCycles = new int[count];
            }

            float spacing = ItemSpacing;
            for (int i = 0; i < count; i++)
            {
                _baseY[i] = (i - _centerSlot) * spacing;
                if (_items[i] != null)
                {
                    _items[i].SetAnchoredY(_baseY[i]);
                }
            }

            _totalSpan = count * spacing;
            _halfSpan = _totalSpan * 0.5f;

            ResetCycles();
        }

        private void ResetCycles()
        {
            if (_baseY == null) return;
            for (int i = 0; i < _baseY.Length; i++)
            {
                _lastCycles[i] = Mathf.FloorToInt((_baseY[i] - _distance + _halfSpan) / _totalSpan);
            }
        }

        /// <summary>
        /// Starts continuous spinning.
        /// </summary>
        public void Spin(SymbolData[] catalog = null, IRandomProvider rng = null)
        {
            if (_baseY == null || _baseY.Length != (_items != null ? _items.Length : 0))
            {
                InitializeItems();
            }

            ResolveCatalogAndRng(catalog, rng);

            for (int i = 0; i < _items.Length; i++)
            {
                _items[i].ResetVisualState();
            }

            _distance = 0f;
            _speed = 0f;
            _timer = 0f;
            _lastTickDistance = 0f;
            _isStopping = false;
            _state = ReelState.SpinUp;

            ResetCycles();
            enabled = true;

            OnSpinStarted?.Invoke(this);
        }

        /// <summary>
        /// Commands this reel to stop on the target symbol using the active physical stopping mode.
        /// Both modes execute a smooth gradual deceleration ramp so physical movement and audio clicks slow down.
        /// </summary>
        public void Stop(SymbolType targetSymbol)
        {
            if (_state == ReelState.Idle || _state == ReelState.Braking || _state == ReelState.Bouncing) return;

            _targetSymbol = targetSymbol;
            _isStopping = true;

            int count = _items.Length;
            float spacing = Mathf.Max(1f, ItemSpacing);
            ReelStopMode mode = _group != null ? _group.StopMode : ReelStopMode.MechanicalPawl;

            _brakeDuration = 0.50f;
            float estDist = _maxSpeed * 0.5f * _brakeDuration;
            int targetStep = Mathf.CeilToInt((_distance + estDist) / spacing) + 1;
            _targetDistance = targetStep * spacing;
            _targetSlot = ((targetStep + _centerSlot) % count + count) % count;

            float totalDist = _targetDistance - _distance;
            _brakeDuration = Mathf.Max(0.2f, (2f * totalDist) / _maxSpeed);

            _startDistance = _distance;
            _timer = 0f;

            if (mode == ReelStopMode.MechanicalPawl)
            {
                // Mode 1: Authentic Mechanical Pawl - Smooth deceleration ramp + recoil bounce on lock
                _bounceDuration = 0.12f;
                _bounceHeight = 4f;
            }
            else
            {
                // Mode 2: Modern Physical Stepper Motor - Smooth deceleration ramp + crisp zero-bounce lock
                _bounceDuration = 0f;
                _bounceHeight = 0f;
            }

            _state = ReelState.Braking;
        }

        /// <summary>
        /// Snaps instantly to display the symbol without spinning.
        /// </summary>
        public void SnapToSymbol(SymbolType symbol, SymbolData[] catalog = null)
        {
            if (_baseY == null || _baseY.Length != (_items != null ? _items.Length : 0))
            {
                InitializeItems();
            }

            ResolveCatalogAndRng(catalog, null);

            _distance = 0f;
            _speed = 0f;
            _lastTickDistance = 0f;
            _state = ReelState.Idle;
            _isStopping = false;

            int count = _items.Length;
            SymbolData targetData = GetSymbolData(symbol);
            float spacing = ItemSpacing;

            for (int i = 0; i < count; i++)
            {
                int offset = i - _centerSlot;
                float y = offset * spacing;
                _items[i].SetAnchoredY(y);
                _baseY[i] = y;

                if (i == _centerSlot)
                {
                    _items[i].SetSymbol(targetData);
                }
                else
                {
                    _items[i].SetSymbol(GetRandomSymbol());
                }
                _items[i].ResetVisualState();
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

                case ReelState.Spinning:
                    UpdateSpinning();
                    break;

                case ReelState.Braking:
                    UpdateBraking();
                    break;

                case ReelState.Bouncing:
                    UpdateBouncing();
                    break;

                case ReelState.Idle:
                default:
                    enabled = false;
                    break;
            }
        }

        private void UpdateSpinUp()
        {
            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / SpinUpDuration);

            _speed = Mathf.Lerp(0f, _maxSpeed, t * t);
            _distance += _speed * Time.deltaTime;

            UpdateRatchetAudio(_speed);
            ApplyPositions();

            if (t >= 1f)
            {
                _speed = _maxSpeed;
                _state = ReelState.Spinning;
            }
        }

        private void UpdateSpinning()
        {
            _speed = _maxSpeed;
            _distance += _maxSpeed * Time.deltaTime;

            UpdateRatchetAudio(_speed);
            ApplyPositions();
        }

        private void UpdateBraking()
        {
            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / _brakeDuration);

            float totalDist = _targetDistance - _startDistance;
            _distance = _startDistance + (totalDist * (1f - (1f - t) * (1f - t)));

            // Instantaneous velocity drops smoothly from initial speed to 0
            _speed = Mathf.Max(0f, (2f * totalDist / _brakeDuration) * (1f - t));

            UpdateRatchetAudio(_speed);
            ApplyPositions();

            if (t >= 1f)
            {
                _distance = _targetDistance;
                _speed = 0f;

                if (_bounceDuration > 0f)
                {
                    _state = ReelState.Bouncing;
                    _timer = 0f;
                }
                else
                {
                    FinishStop();
                }
            }
        }

        private void UpdateBouncing()
        {
            _timer += Time.deltaTime;
            float tau = Mathf.Clamp01(_timer / _bounceDuration);

            // Damped spring oscillation: decaying micro-bounce on pawl lock
            float bounce = _bounceHeight * (1f - tau) * (1f - tau) * Mathf.Sin(3f * Mathf.PI * tau);
            _distance = _targetDistance + bounce;
            ApplyPositions();

            if (tau >= 1f)
            {
                _distance = _targetDistance;
                FinishStop();
            }
        }

        private void UpdateRatchetAudio(float currentSpeed)
        {
            // 2 ratchet teeth per symbol height (e.g. 80px / 2 = 40px per click)
            float tickInterval = Mathf.Max(10f, ItemSpacing * 0.5f);
            if (_distance - _lastTickDistance >= tickInterval)
            {
                _lastTickDistance += tickInterval;
                if (_distance - _lastTickDistance > tickInterval)
                {
                    _lastTickDistance = _distance;
                }

                float speedRatio = _maxSpeed > 0f ? Mathf.Clamp01(currentSpeed / _maxSpeed) : 0f;
                SlotMachine.Audio.AudioManager.Instance?.PlayRatchetTick(speedRatio);
            }
        }

        private void ApplyPositions()
        {
            int count = _items.Length;

            for (int i = 0; i < count; i++)
            {
                float y = Mathf.Repeat(_baseY[i] - _distance + _halfSpan, _totalSpan) - _halfSpan;
                _items[i].SetAnchoredY(y);

                // Detect when item wraps bottom-to-top
                int cycle = Mathf.FloorToInt((_baseY[i] - _distance + _halfSpan) / _totalSpan);
                if (cycle != _lastCycles[i])
                {
                    _lastCycles[i] = cycle;

                    SymbolData sym;
                    if (_isStopping && i == _targetSlot)
                    {
                        sym = GetSymbolData(_targetSymbol);
                    }
                    else
                    {
                        sym = GetRandomSymbol();
                    }

                    if (sym != null)
                    {
                        _items[i].SetSymbol(sym);
                    }
                }
            }
        }

        private void FinishStop()
        {
            int count = _items.Length;
            float spacing = ItemSpacing;
            SymbolData targetData = GetSymbolData(_targetSymbol);

            for (int i = 0; i < count; i++)
            {
                int offset = (i - _targetSlot + count) % count;
                if (offset > count / 2) offset -= count;

                float y = offset * spacing;
                _items[i].SetAnchoredY(y);
                _baseY[i] = y;

                if (i == _targetSlot)
                {
                    _items[i].SetSymbol(targetData);
                }
                else if (_items[i].CurrentSymbolData == null)
                {
                    _items[i].SetSymbol(GetRandomSymbol());
                }
            }

            _centerSlot = _targetSlot;
            _distance = 0f;
            _state = ReelState.Idle;
            _isStopping = false;
            ResetCycles();
            enabled = false;

            OnSpinStopped?.Invoke(this);
        }

        public SlotSymbolItem GetPaylineItem()
        {
            if (_items == null || _items.Length == 0) return null;

            int best = 0;
            float minAbsY = float.MaxValue;

            for (int i = 0; i < _items.Length; i++)
            {
                float absY = Mathf.Abs(_items[i].RectTransform.anchoredPosition.y);
                if (absY < minAbsY)
                {
                    minAbsY = absY;
                    best = i;
                }
            }

            return _items[best];
        }

        public SymbolType GetPaylineSymbolType()
        {
            SlotSymbolItem item = GetPaylineItem();
            return item != null ? item.CurrentSymbolType : SymbolType.Symbol1;
        }

        private void ResolveCatalogAndRng(SymbolData[] catalog, IRandomProvider rng)
        {
            if (catalog != null && catalog.Length > 0)
            {
                _catalog = catalog;
            }

            if (_catalog == null || _catalog.Length == 0)
            {
                var table = PayoutTable;
                if (table == null)
                {
                    var controller = FindFirstObjectByType<SlotMachineController>();
                    if (controller != null)
                    {
                        table = controller.PayoutTable;
                    }
                }

                if (table != null && table.Symbols != null && table.Symbols.Count > 0)
                {
                    var list = table.Symbols;
                    _catalog = new SymbolData[list.Count];
                    for (int i = 0; i < list.Count; i++)
                    {
                        _catalog[i] = list[i];
                    }
                }
            }

            _rng = rng ?? _rng ?? new UnityRngProvider();
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

            var table = PayoutTable;
            if (table != null)
            {
                return table.GetSymbolData(type);
            }

            return null;
        }

        private SymbolData GetRandomSymbol()
        {
            if (_catalog == null || _catalog.Length == 0) return null;
            int randomIndex = _rng.Range(0, _catalog.Length);
            return _catalog[randomIndex];
        }
    }
}
