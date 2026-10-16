using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CyberQuest.UI
{
    /// <summary>
    /// Data-driven decision panel: title, body and 1-4 buttons, pressable by poke
    /// or ray. Options can be swapped at runtime (e.g. an option that only appears
    /// once a clue has been read). Choose(label) lets tests and the debug window
    /// press an option without XR input.
    /// </summary>
    public class ChoicePanel : MonoBehaviour
    {
        public readonly struct Option
        {
            public readonly string Label;
            public readonly Action OnChosen;

            public Option(string label, Action onChosen)
            {
                Label = label;
                OnChosen = onChosen;
            }
        }

        private UiPanel _panel;
        private TextMeshPro _body;
        private float _bodyHeight;
        private float _buttonHeight;
        private bool _vertical;
        private readonly List<PokeButton3D> _buttons = new List<PokeButton3D>();
        private readonly List<Option> _options = new List<Option>();

        public UiPanel Panel => _panel;
        public bool Visible => _panel != null && _panel.Root.activeSelf;
        public IReadOnlyList<Option> Options => _options;

        public static ChoicePanel Create(Transform parent, string name, Vector2 size, string title, Color accent,
            string body, float viewDistance, bool verticalButtons, float bodySize = UiPanel.BodySize, float buttonHeight = 0.075f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var choice = go.AddComponent<ChoicePanel>();
            choice._panel = UiPanel.Create(go.transform, "Panel", size, title, accent, viewDistance);
            choice._vertical = verticalButtons;
            choice._buttonHeight = buttonHeight;
            choice._bodyHeight = size.y - (string.IsNullOrEmpty(title) ? 0f : UiPanel.HeaderHeight) - 2f * UiPanel.Pad;
            choice._body = choice._panel.Body(body, choice._panel.ContentTop, choice._bodyHeight, bodySize);
            return choice;
        }

        public void SetTitle(string title)
        {
            if (_panel.Title != null)
            {
                _panel.Title.text = title;
            }
        }

        public void SetBody(string text) => _body.text = text;

        public void SetOptions(params Option[] options)
        {
            foreach (var b in _buttons)
            {
                if (b != null)
                {
                    b.gameObject.SetActive(false);
                    Destroy(b.gameObject);
                }
            }
            _buttons.Clear();
            _options.Clear();
            _options.AddRange(options);

            float gap = 0.018f;
            int n = options.Length;
            float block = _vertical ? n * _buttonHeight + (n - 1) * gap : _buttonHeight;
            float top = _panel.Bottom + block;
            _body.rectTransform.sizeDelta = new Vector2(_panel.Width, Mathf.Max(0.04f, _panel.ContentTop - top - 0.02f));
            _body.transform.localPosition = new Vector3(0f, (_panel.ContentTop + top + 0.02f) / 2f, -0.001f);

            for (int i = 0; i < n; i++)
            {
                var opt = options[i];
                Vector2 center, size;
                if (_vertical)
                {
                    center = new Vector2(0f, top - _buttonHeight / 2f - i * (_buttonHeight + gap));
                    size = new Vector2(_panel.Width, _buttonHeight);
                }
                else
                {
                    float w = (_panel.Width - gap * (n - 1)) / n;
                    center = new Vector2(_panel.Left + w / 2f + i * (w + gap), _panel.Bottom + _buttonHeight / 2f);
                    size = new Vector2(w, _buttonHeight);
                }
                var btn = _panel.Button(opt.Label, center, size, () => opt.OnChosen?.Invoke());
                if (btn != null)
                {
                    _buttons.Add(btn);
                }
            }
        }

        public void Show(bool show = true) => _panel.Root.SetActive(show);

        public void Hide() => Show(false);

        /// <summary>Presses the option with this label (tests / debug). Returns false if not offered.</summary>
        public bool Choose(string label)
        {
            if (!Visible)
            {
                return false;
            }
            foreach (var o in _options)
            {
                if (o.Label == label)
                {
                    o.OnChosen?.Invoke();
                    return true;
                }
            }
            return false;
        }
    }
}
