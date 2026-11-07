using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SlotMachine.Core;
using SlotMachine.Audio;

namespace SlotMachine.UI
{
    /// <summary>
    /// Modal celebration popup displayed on winning spins.
    /// Presents winning payout announcements, animated scale popup,
    /// and an interactive Claim button to dismiss and award credits.
    /// </summary>
    public class WinCelebrationView : MonoBehaviour
    {
        [Header("Controller Binding")]
        [SerializeField] private SlotMachineController _controller;

        [Header("UI Components")]
        [Tooltip("Root container of the popup modal.")]
        [SerializeField] private GameObject _popupRoot;

        [Tooltip("RectTransform animated during show/hide.")]
        [SerializeField] private RectTransform _popupTransform;

        [Tooltip("CanvasGroup controlling modal opacity.")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Tooltip("Text displaying celebration banner (e.g. 'BIG WIN!', 'JACKPOT!').")]
        [SerializeField] private TMP_Text _titleText;

        [Tooltip("Text displaying the won payout amount.")]
        [SerializeField] private TMP_Text _payoutText;

        [Tooltip("Interactive button to claim winnings and dismiss the modal.")]
        [SerializeField] private Button _claimButton;

        [Header("Animation Settings")]
        [SerializeField] private float _animDuration = 0.35f;
        [SerializeField] private float _autoCloseDuration = 4.0f;

        private Coroutine _activeRoutine;

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = FindFirstObjectByType<SlotMachineController>();
            }

            if (_popupTransform == null)
            {
                _popupTransform = GetComponent<RectTransform>();
            }

            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (_claimButton != null)
            {
                _claimButton.onClick.AddListener(OnClaimClicked);
            }

            // Initially hidden via CanvasGroup & scale (keeps GameObject active for events & coroutines)
            HideImmediate();
        }

        private void OnEnable()
        {
            SubscribeController();
        }

        private void Start()
        {
            SubscribeController();
        }

        private void OnDisable()
        {
            UnsubscribeController();
        }

        private void OnDestroy()
        {
            UnsubscribeController();
        }

        private void SubscribeController()
        {
            if (_controller == null)
            {
                _controller = FindFirstObjectByType<SlotMachineController>();
            }

            if (_controller != null)
            {
                _controller.OnWinCelebrationRequested -= HandleWinCelebration;
                _controller.OnWinCelebrationRequested += HandleWinCelebration;
            }
        }

        private void UnsubscribeController()
        {
            if (_controller != null)
            {
                _controller.OnWinCelebrationRequested -= HandleWinCelebration;
            }
        }

        private void HandleWinCelebration(SpinOutcome outcome)
        {
            if (!outcome.IsWin) return;
            Show(outcome.PayoutAmount, outcome.Message);
        }

        public void Show(int payout, string message = null)
        {
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }

            if (_titleText != null)
            {
                string banner = message;
                if (string.IsNullOrEmpty(banner) || banner.Contains("Symbol") || banner.Contains("3x"))
                {
                    banner = payout >= 500 ? "JACKPOT!" : payout >= 200 ? "MEGA WIN!" : payout >= 100 ? "SUPER WIN!" : "BIG WIN!";
                }
                _titleText.text = banner;
            }

            if (_payoutText != null)
            {
                _payoutText.text = $"+{payout}";
            }

            AudioManager.Instance?.PlayWin();
            _activeRoutine = StartCoroutine(ShowRoutine());
        }

        public void Dismiss()
        {
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
            }

            _activeRoutine = StartCoroutine(DismissRoutine());
        }

        private void OnClaimClicked()
        {
            AudioManager.Instance?.PlayButtonClick();
            Dismiss();
        }

        private IEnumerator ShowRoutine()
        {
            float elapsed = 0f;
            Vector3 startScale = Vector3.zero;
            Vector3 targetScale = Vector3.one;

            if (_popupTransform != null) _popupTransform.localScale = startScale;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }

            while (elapsed < _animDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _animDuration);

                // Overshoot bounce easing
                float bounce = 1f + 0.15f * Mathf.Sin(t * Mathf.PI);
                if (_popupTransform != null)
                {
                    _popupTransform.localScale = targetScale * (t * bounce);
                }

                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = t;
                }

                yield return null;
            }

            if (_popupTransform != null) _popupTransform.localScale = targetScale;
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;

            // Auto-close countdown if player doesn't click
            if (_autoCloseDuration > 0f)
            {
                yield return new WaitForSeconds(_autoCloseDuration);
                yield return DismissRoutine();
            }

            _activeRoutine = null;
        }

        private IEnumerator DismissRoutine()
        {
            float elapsed = 0f;
            Vector3 startScale = _popupTransform != null ? _popupTransform.localScale : Vector3.one;

            while (elapsed < _animDuration * 0.7f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / (_animDuration * 0.7f));

                if (_popupTransform != null)
                {
                    _popupTransform.localScale = Vector3.Lerp(startScale, Vector3.zero, t * t);
                }

                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 1f - t;
                }

                yield return null;
            }

            HideImmediate();

            if (_controller != null)
            {
                _controller.DismissWinCelebration();
            }

            _activeRoutine = null;
        }

        private void HideImmediate()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            if (_popupTransform != null)
            {
                _popupTransform.localScale = Vector3.zero;
            }
        }
    }
}
