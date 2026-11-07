using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using SlotMachine.Core;

namespace SlotMachine.UI
{
    /// <summary>
    /// Master UI HUD controller.
    /// Manages player button inputs (Bet Up, Bet Down, Max Bet), keyboard shortcuts,
    /// dynamic text display readouts for balance, bet, payout, and lever state synchronization.
    /// Strictly event-driven with zero GC hot-path updates.
    /// </summary>
    public class SlotMachineView : MonoBehaviour
    {
        [Header("Controller Binding")]
        [Tooltip("The central game controller coordinating state and economy.")]
        [SerializeField] private SlotMachineController _controller;

        [Header("Interactive Buttons")]
        [Tooltip("Main button triggering spin (optional if playing via lever pull).")]
        [SerializeField] private Button _spinButton;

        [Tooltip("Button incrementing current bet.")]
        [SerializeField] private Button _betUpButton;

        [Tooltip("Button decrementing current bet.")]
        [SerializeField] private Button _betDownButton;

        [Tooltip("Button maximizing current bet within balance constraints.")]
        [SerializeField] private Button _maxBetButton;

        [Header("Lever Synchronization (Optional)")]
        [Tooltip("The upright lever arm object.")]
        [SerializeField] private GameObject _leverArmObject;

        [Tooltip("The pulled-down lever base object.")]
        [SerializeField] private GameObject _leverBaseObject;

        [Header("Text Displays (TMP)")]
        [Tooltip("Readout displaying player's current credit balance.")]
        [SerializeField] private TMP_Text _balanceText;

        [Tooltip("Readout displaying current wager amount.")]
        [SerializeField] private TMP_Text _betText;

        [Tooltip("Readout displaying payout from the most recent spin.")]
        [SerializeField] private TMP_Text _lastWinText;

        [Tooltip("Banner text communicating status, prompt instructions, or win announcements.")]
        [SerializeField] private TMP_Text _statusText;

        [Header("String Formatting Templates")]
        [SerializeField] private string _balanceFormat = "{0}";
        [SerializeField] private string _betFormat = "{0}";
        [SerializeField] private string _winFormat = "{0}";
        [SerializeField] private string _idlePromptMessage = "PULL LEVER TO PLAY";
        [SerializeField] private string _spinningMessage = "GOOD LUCK!";

        private int _cachedBalance = -1;
        private int _cachedBet = -1;
        private int _cachedWin = -1;

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = FindFirstObjectByType<SlotMachineController>();
            }

            if (_leverArmObject == null)
            {
                var arm = transform.Find("CabinetContainer/LeverArm");
                if (arm != null) _leverArmObject = arm.gameObject;
            }

            if (_leverBaseObject == null)
            {
                var lbase = transform.Find("CabinetContainer/LeverBase");
                if (lbase != null) _leverBaseObject = lbase.gameObject;
            }

            BindButtonCallbacks();
        }

        private void OnEnable()
        {
            SubscribeControllerEvents();
            RefreshAllDisplays();
        }

        private void OnDisable()
        {
            UnsubscribeControllerEvents();
        }

        private void BindButtonCallbacks()
        {
            if (_spinButton != null)
            {
                _spinButton.onClick.AddListener(OnSpinClicked);
            }

            if (_betUpButton != null)
            {
                _betUpButton.onClick.AddListener(OnBetUpClicked);
            }

            if (_betDownButton != null)
            {
                _betDownButton.onClick.AddListener(OnBetDownClicked);
            }

            if (_maxBetButton != null)
            {
                _maxBetButton.onClick.AddListener(OnMaxBetClicked);
            }
        }

        private void SubscribeControllerEvents()
        {
            if (_controller == null) return;

            _controller.OnStateChanged += HandleStateChanged;
            _controller.OnBalanceChanged += HandleBalanceChanged;
            _controller.OnBetChanged += HandleBetChanged;
            _controller.OnSpinStarted += HandleSpinStarted;
            _controller.OnSpinCompleted += HandleSpinCompleted;
            _controller.OnWinCelebrationRequested += HandleWinCelebration;
        }

        private void UnsubscribeControllerEvents()
        {
            if (_controller == null) return;

            _controller.OnStateChanged -= HandleStateChanged;
            _controller.OnBalanceChanged -= HandleBalanceChanged;
            _controller.OnBetChanged -= HandleBetChanged;
            _controller.OnSpinStarted -= HandleSpinStarted;
            _controller.OnSpinCompleted -= HandleSpinCompleted;
            _controller.OnWinCelebrationRequested -= HandleWinCelebration;
        }

        private void Update()
        {
            HandleKeyboardInput();
        }

        /// <summary>
        /// Provides smooth keyboard accessibility supporting both the new Input System and Legacy input.
        /// </summary>
        private void HandleKeyboardInput()
        {
            if (_controller == null) return;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                OnSpinClicked();
            }
            else if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame || keyboard.equalsKey.wasPressedThisFrame)
            {
                OnBetUpClicked();
            }
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame || keyboard.minusKey.wasPressedThisFrame)
            {
                OnBetDownClicked();
            }
            else if (keyboard.mKey.wasPressedThisFrame)
            {
                OnMaxBetClicked();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Space))
            {
                OnSpinClicked();
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Equals))
            {
                OnBetUpClicked();
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Minus))
            {
                OnBetDownClicked();
            }
            else if (Input.GetKeyDown(KeyCode.M))
            {
                OnMaxBetClicked();
            }
#endif
        }

        private void OnSpinClicked()
        {
            if (_controller == null) return;

            if (_controller.State == SlotMachineState.Idle)
            {
                _controller.Spin();
            }
            else if (_controller.State == SlotMachineState.WinCelebration)
            {
                _controller.DismissWinCelebration();
            }
        }

        private void OnBetUpClicked()
        {
            if (_controller != null && _controller.CanChangeBet)
            {
                _controller.IncreaseBet();
            }
        }

        private void OnBetDownClicked()
        {
            if (_controller != null && _controller.CanChangeBet)
            {
                _controller.DecreaseBet();
            }
        }

        private void OnMaxBetClicked()
        {
            if (_controller != null && _controller.CanChangeBet)
            {
                _controller.SetMaxBet();
            }
        }

        private void HandleStateChanged(SlotMachineState state)
        {
            UpdateButtonStates(state);

            switch (state)
            {
                case SlotMachineState.Idle:
                    SetStatusText(_idlePromptMessage);
                    SetLeverState(true, false);
                    break;

                case SlotMachineState.Spinning:
                    SetStatusText(_spinningMessage);
                    SetLeverState(false, true);
                    break;

                case SlotMachineState.EvaluatingWin:
                    SetLeverState(true, false);
                    break;

                case SlotMachineState.WinCelebration:
                    SetLeverState(true, false);
                    break;
            }
        }

        private void HandleBalanceChanged(int balance)
        {
            if (_balanceText != null && _cachedBalance != balance)
            {
                _cachedBalance = balance;
                _balanceText.SetText(_balanceFormat, balance);
            }

            UpdateButtonStates(_controller != null ? _controller.State : SlotMachineState.Idle);
        }

        private void HandleBetChanged(int bet)
        {
            if (_betText != null && _cachedBet != bet)
            {
                _cachedBet = bet;
                _betText.SetText(_betFormat, bet);
            }

            UpdateButtonStates(_controller != null ? _controller.State : SlotMachineState.Idle);
        }

        private void HandleSpinStarted(SpinOutcome outcome)
        {
            SetStatusText(_spinningMessage);
            SetLeverState(false, true);
        }

        private void HandleSpinCompleted(SpinOutcome outcome)
        {
            SetLeverState(true, false);

            if (outcome.IsWin)
            {
                UpdateLastWinText(outcome.PayoutAmount);
                SetStatusText(string.IsNullOrEmpty(outcome.Message) ? "WINNER!" : outcome.Message);
            }
            else
            {
                UpdateLastWinText(0);
                SetStatusText(outcome.Message);
            }
        }

        private void HandleWinCelebration(SpinOutcome outcome)
        {
            SetLeverState(true, false);
            UpdateLastWinText(outcome.PayoutAmount);
            SetStatusText(string.IsNullOrEmpty(outcome.Message) ? "WINNER!" : outcome.Message);
        }

        private void SetLeverState(bool armActive, bool baseActive)
        {
            if (_leverArmObject != null)
            {
                _leverArmObject.SetActive(armActive);
            }

            if (_leverBaseObject != null)
            {
                _leverBaseObject.SetActive(baseActive);
            }
        }

        private void UpdateButtonStates(SlotMachineState state)
        {
            if (_controller == null) return;

            bool isIdle = (state == SlotMachineState.Idle);

            if (_spinButton != null)
            {
                if (isIdle)
                {
                    _spinButton.interactable = _controller.CanSpin;
                }
                else
                {
                    _spinButton.interactable = false;
                }
            }

            if (_betUpButton != null)
            {
                _betUpButton.interactable = isIdle &&
                                            _controller.Bet < _controller.MaxBet &&
                                            _controller.Balance > _controller.Bet;
            }

            if (_betDownButton != null)
            {
                _betDownButton.interactable = isIdle &&
                                              _controller.Bet > _controller.MinBet;
            }

            if (_maxBetButton != null)
            {
                _maxBetButton.interactable = isIdle &&
                                             _controller.Bet < _controller.MaxBet &&
                                             _controller.Balance > _controller.Bet;
            }
        }

        private void UpdateLastWinText(int winAmount)
        {
            if (_lastWinText != null && _cachedWin != winAmount)
            {
                _cachedWin = winAmount;
                _lastWinText.SetText(_winFormat, winAmount);
            }
        }

        private void SetStatusText(string message)
        {
            if (_statusText != null && message != null)
            {
                _statusText.text = message;
            }
        }

        private void RefreshAllDisplays()
        {
            if (_controller == null) return;

            HandleBalanceChanged(_controller.Balance);
            HandleBetChanged(_controller.Bet);
            HandleStateChanged(_controller.State);
        }
    }
}
