using System;
using UnityEngine;
using UnityEngine.EventSystems;
using SlotMachine.Core;

namespace SlotMachine.UI
{
    /// <summary>
    /// Interactive mechanical slot machine lever arm.
    /// Supports downward dragging past a threshold to trigger a spin.
    /// Toggles visibility between LeverArm (upright) and LeverBase (pulled down).
    /// </summary>
    public class SlotMachineLever : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Controller Binding")]
        [Tooltip("Reference to the SlotMachineController.")]
        [SerializeField] private SlotMachineController _controller;

        [Header("Lever GameObjects")]
        [Tooltip("The GameObject representing the upright lever arm.")]
        [SerializeField] private GameObject _leverArmObject;

        [Tooltip("The GameObject representing the pulled-down lever base.")]
        [SerializeField] private GameObject _leverBaseObject;

        [Header("Drag Settings")]
        [Tooltip("Minimum downward drag distance (in screen pixels) required to trigger the pull.")]
        [SerializeField] private float _dragThreshold = 40f;

        private Vector2 _dragStartPosition;
        private bool _isDragging;

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = FindFirstObjectByType<SlotMachineController>();
            }

            if (_leverArmObject == null)
            {
                _leverArmObject = gameObject;
            }

            if (_leverBaseObject == null && transform.parent != null)
            {
                var baseTrans = transform.parent.Find("LeverBase");
                if (baseTrans != null)
                {
                    _leverBaseObject = baseTrans.gameObject;
                }
            }
        }

        private void Start()
        {
            SubscribeControllerEvents();
            ResetToUpright();
        }

        private void OnDestroy()
        {
            UnsubscribeControllerEvents();
        }

        private void OnDisable()
        {
            _isDragging = false;
        }

        private void SubscribeControllerEvents()
        {
            if (_controller != null)
            {
                _controller.OnSpinStarted -= HandleSpinStarted;
                _controller.OnSpinStarted += HandleSpinStarted;

                _controller.OnSpinCompleted -= HandleSpinCompleted;
                _controller.OnSpinCompleted += HandleSpinCompleted;

                _controller.OnStateChanged -= HandleStateChanged;
                _controller.OnStateChanged += HandleStateChanged;
            }
        }

        private void UnsubscribeControllerEvents()
        {
            if (_controller != null)
            {
                _controller.OnSpinStarted -= HandleSpinStarted;
                _controller.OnSpinCompleted -= HandleSpinCompleted;
                _controller.OnStateChanged -= HandleStateChanged;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_controller == null) return;

            if (!_controller.CanSpin)
            {
                if (_controller.State == SlotMachineState.Idle && _controller.Balance < _controller.Bet && !_controller.IsInFreeSpins)
                {
                    _controller.NotifyInsufficientCredits();
                }
                return;
            }

            _isDragging = true;
            _dragStartPosition = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;

            // Downward drag means start y > current y
            float dragDeltaY = _dragStartPosition.y - eventData.position.y;
            if (dragDeltaY >= _dragThreshold)
            {
                _isDragging = false;
                ExecutePull();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isDragging = false;
        }

        /// <summary>
        /// Executes the lever pull action: triggers spin and swaps to pulled-down LeverBase.
        /// </summary>
        public void ExecutePull()
        {
            if (_controller == null || !_controller.CanSpin) return;

            SetPulledDown();
            SlotMachine.Audio.AudioManager.Instance?.PlayLeverPull();
            _controller.Spin();
        }

        private void HandleSpinStarted(SpinOutcome outcome)
        {
            SetPulledDown();
        }

        private void HandleSpinCompleted(SpinOutcome outcome)
        {
            ResetToUpright();
        }

        private void HandleStateChanged(SlotMachineState state)
        {
            if (state != SlotMachineState.Spinning)
            {
                ResetToUpright();
            }
        }

        public void SetPulledDown()
        {
            if (_leverBaseObject != null)
            {
                _leverBaseObject.SetActive(true);
            }

            if (_leverArmObject != null)
            {
                _leverArmObject.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public void ResetToUpright()
        {
            if (_leverBaseObject != null)
            {
                _leverBaseObject.SetActive(false);
            }

            if (_leverArmObject != null)
            {
                _leverArmObject.SetActive(true);
            }
            else
            {
                gameObject.SetActive(true);
            }
        }
    }
}
