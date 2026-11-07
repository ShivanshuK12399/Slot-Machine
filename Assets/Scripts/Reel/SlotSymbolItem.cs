using UnityEngine;
using UnityEngine.UI;
using SlotMachine.Config;

namespace SlotMachine.Reel
{
    /// <summary>
    /// Visual item component representing a single symbol row/cell inside a reel aperture.
    /// Manages sprite rendering, anchored position offsets, and win highlight presentation.
    /// Hot paths are zero-allocation.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SlotSymbolItem : MonoBehaviour
    {
        [Header("Components")]
        [Tooltip("Image component used to display the symbol graphic.")]
        [SerializeField] private Image _symbolImage;

        private RectTransform _rectTransform;
        private SymbolData _symbolData;
        private bool _isHighlighted;
        private Vector3 _initialScale = Vector3.one;
        private Color _initialColor = Color.white;

        public RectTransform RectTransform => _rectTransform;
        public SymbolData CurrentSymbolData => _symbolData;
        public SymbolType CurrentSymbolType => _symbolData != null ? _symbolData.SymbolType : SymbolType.Symbol1;
        public bool IsHighlighted => _isHighlighted;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();

            if (_symbolImage == null)
            {
                _symbolImage = GetComponentInChildren<Image>();
            }

            if (_symbolImage != null)
            {
                _initialColor = _symbolImage.color;
                _symbolImage.preserveAspect = true;
            }

            _initialScale = transform.localScale;
        }

        /// <summary>
        /// Binds and displays the specified SymbolData asset.
        /// </summary>
        public void SetSymbol(SymbolData symbolData)
        {
            _symbolData = symbolData;

            if (_symbolImage == null) return;

            if (symbolData != null && symbolData.SymbolSprite != null)
            {
                _symbolImage.sprite = symbolData.SymbolSprite;
                _symbolImage.enabled = true;
            }
            else
            {
                _symbolImage.sprite = null;
                _symbolImage.enabled = false;
            }
        }

        /// <summary>
        /// Sets the vertical anchored position of this item inside the reel.
        /// </summary>
        public void SetAnchoredY(float y)
        {
            Vector2 pos = _rectTransform.anchoredPosition;
            pos.y = y;
            _rectTransform.anchoredPosition = pos;
        }

        /// <summary>
        /// Applies highlight coloring for win line celebrations.
        /// </summary>
        public void SetHighlight(bool highlight, Color highlightColor)
        {
            _isHighlighted = highlight;

            if (_symbolImage != null)
            {
                _symbolImage.color = highlight ? highlightColor : _initialColor;
            }
        }

        /// <summary>
        /// Applies default highlight coloring (Gold/Yellow) for win celebrations.
        /// </summary>
        public void SetHighlight(bool highlight)
        {
            SetHighlight(highlight, new Color(1f, 0.85f, 0.2f, 1f));
        }

        /// <summary>
        /// Scales the item uniformly (e.g. for pulsing celebration animations).
        /// </summary>
        public void SetScale(float scaleMultiplier)
        {
            transform.localScale = _initialScale * scaleMultiplier;
        }

        /// <summary>
        /// Resets all visual modifiers (highlight color, scale) back to defaults.
        /// </summary>
        public void ResetVisualState()
        {
            _isHighlighted = false;
            transform.localScale = _initialScale;

            if (_symbolImage != null)
            {
                _symbolImage.color = _initialColor;
            }
        }
    }
}

