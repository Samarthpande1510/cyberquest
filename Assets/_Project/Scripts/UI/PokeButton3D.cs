using System.Collections;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace CyberQuest.UI
{
    /// <summary>
    /// Pressable 3D button (poke with a finger, or point a ray and pull the
    /// trigger). Wraps Meta Interaction SDK's PointableUnityEventWrapper so event
    /// scripts just listen for OnPressed. Highlights on hover, clicks on press and
    /// ignores the double-fire you get when poke and ray select in the same moment.
    /// </summary>
    [RequireComponent(typeof(PointableUnityEventWrapper))]
    public class PokeButton3D : MonoBehaviour
    {
        private const float Cooldown = 0.4f;

        public UnityEvent OnPressed = new UnityEvent();

        private TextMeshPro _label;
        private Renderer _visual;
        private BoundsClipper _clipper;
        private BoxCollider _rayCollider;
        private int _hovers;
        private float _lastPress = -10f;
        private bool _cached;

        private void Awake()
        {
            Cache();
            var wrapper = GetComponent<PointableUnityEventWrapper>();
            wrapper.WhenSelect.AddListener(_ => Press());
            wrapper.WhenHover.AddListener(_ => Hover(true));
            wrapper.WhenUnhover.AddListener(_ => Hover(false));
        }

        private void OnEnable()
        {
            _hovers = 0;
            Cache();
            UiPanel.SetColor(_visual, UiTheme.ButtonColor);
        }

        private void Cache()
        {
            if (_cached)
            {
                return;
            }
            _cached = true;
            _label = GetComponentInChildren<TextMeshPro>(true);
            _clipper = GetComponentInChildren<BoundsClipper>(true);
            _rayCollider = GetComponentInChildren<BoxCollider>(true);
            var visual = transform.Find("Visual");
            _visual = visual != null ? visual.GetComponent<Renderer>() : null;
        }

        public string Label
        {
            get
            {
                Cache();
                return _label != null ? _label.text : null;
            }
        }

        public void SetLabel(string text)
        {
            Cache();
            if (_label != null)
            {
                _label.text = text;
            }
        }

        public void SetSize(Vector2 size)
        {
            Cache();
            if (_visual != null)
            {
                var s = _visual.transform.localScale;
                _visual.transform.localScale = new Vector3(size.x, size.y, s.z);
            }
            if (_clipper != null)
            {
                _clipper.Size = new Vector3(size.x, size.y, 0.03f);
            }
            if (_rayCollider != null)
            {
                _rayCollider.size = new Vector3(size.x, size.y, _rayCollider.size.z);
            }
            if (_label != null)
            {
                _label.rectTransform.sizeDelta = new Vector2(size.x - 0.02f, size.y - 0.01f);
                _label.fontSizeMax = Mathf.Min(0.24f, size.y * 3.2f);
                _label.fontSizeMin = 0.12f;
            }
        }

        /// <summary>Called by the poke wrapper here and by the ray child (PressForwarder).</summary>
        public void Press()
        {
            if (Time.unscaledTime - _lastPress < Cooldown)
            {
                return;
            }
            _lastPress = Time.unscaledTime;

            var theme = UiTheme.Instance;
            if (theme != null && theme.click != null)
            {
                AudioSource.PlayClipAtPoint(theme.click, transform.position, 0.6f);
            }
            if (isActiveAndEnabled)
            {
                StartCoroutine(Flash());
            }
            OnPressed.Invoke();
        }

        public void Hover(bool on)
        {
            _hovers = Mathf.Max(0, _hovers + (on ? 1 : -1));
            UiPanel.SetColor(_visual, _hovers > 0 ? UiTheme.ButtonHover : UiTheme.ButtonColor);
        }

        private IEnumerator Flash()
        {
            UiPanel.SetColor(_visual, Color.white);
            yield return new WaitForSecondsRealtime(0.12f);
            UiPanel.SetColor(_visual, _hovers > 0 ? UiTheme.ButtonHover : UiTheme.ButtonColor);
        }
    }
}
