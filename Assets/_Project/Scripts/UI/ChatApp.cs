using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace CyberQuest.UI
{
    /// <summary>
    /// Team chat on the desk monitor. Messages show a sender name, an initials
    /// avatar and an optional "verified internal account" tag (no portraits -
    /// people only exist as text). Situations post messages and reply options.
    /// </summary>
    [RequireComponent(typeof(MonitorScreen))]
    public class ChatApp : MonoBehaviour
    {
        private struct Message
        {
            public string Sender;
            public string Initials;
            public string Text;
            public bool Verified;
            public bool FromPlayer;
        }

        private MonitorScreen _monitor;
        private UiPanel _view;
        private readonly List<Message> _messages = new List<Message>();
        private ChoicePanel.Option[] _replies = Array.Empty<ChoicePanel.Option>();

        public IReadOnlyList<ChoicePanel.Option> Replies => _replies;
        public int MessageCount => _messages.Count;
        public string LastMessage => _messages.Count > 0 ? _messages[_messages.Count - 1].Text : "";

        private MonitorScreen Monitor => _monitor != null ? _monitor : (_monitor = GetComponent<MonitorScreen>());

        private void Start()
        {
            Monitor.AppOpened += app =>
            {
                if (app == "Chat")
                {
                    Render();
                }
            };
            if (_view == null)
            {
                Render();
            }
        }

        public void Post(string sender, string initials, string text, bool verified, bool notify = true)
        {
            _messages.Add(new Message { Sender = sender, Initials = initials, Text = text, Verified = verified });
            if (notify)
            {
                Monitor.AddBadge("Chat");
                Monitor.ShowToast("Chat  -  " + sender, text);
            }
            Render();
        }

        public void PostFromPlayer(string text)
        {
            _messages.Add(new Message { Sender = "You", Initials = "ME", Text = text, FromPlayer = true });
            Render();
        }

        public void SetReplies(params ChoicePanel.Option[] replies)
        {
            _replies = replies;
            Render();
        }

        public bool Reply(string label)
        {
            foreach (var r in _replies)
            {
                if (r.Label == label)
                {
                    r.OnChosen?.Invoke();
                    return true;
                }
            }
            return false;
        }

        private void Render()
        {
            if (_view != null)
            {
                _view.Root.SetActive(false);
                Destroy(_view.Root);
            }
            var size = MonitorScreen.ContentSize;
            _view = UiPanel.Create(Monitor.App("Chat").Root.transform, "View", size, "Team Chat", UiTheme.Blue, MonitorScreen.ViewDistance);
            _view.Root.transform.localPosition = new Vector3(0f, 0f, -0.003f);

            float bottomOfLog = _replies.Length > 0 ? _view.Bottom + 0.09f : _view.Bottom;
            float y = _view.ContentTop;
            // Newest messages that fit, oldest first.
            int start = Mathf.Max(0, _messages.Count - 3);
            for (int i = start; i < _messages.Count; i++)
            {
                var m = _messages[i];
                var avatar = _view.Quad("Avatar", new Vector2(_view.Left + 0.03f, y - 0.03f), new Vector2(0.06f, 0.06f),
                    m.FromPlayer ? UiTheme.Slate : UiTheme.Purple, -0.0005f);
                avatar.name = "Avatar_" + m.Initials;
                _view.Text(m.Initials, new Rect(_view.Left, y - 0.06f, 0.06f, 0.06f), 0.15f, TextAlignmentOptions.Center, bold: true);
                var sb = new StringBuilder();
                sb.Append("<b>").Append(m.Sender).Append("</b>");
                if (m.Verified)
                {
                    sb.Append("  <size=75%><color=#7DD3FC>Verified internal account</color></size>");
                }
                sb.Append('\n').Append(m.Text);
                float h = Mathf.Min(0.13f, y - bottomOfLog);
                _view.Text(sb.ToString(), new Rect(_view.Left + 0.08f, y - h, _view.Width - 0.08f, h), 0.165f, TextAlignmentOptions.TopLeft);
                y -= 0.125f;
                if (y < bottomOfLog + 0.04f)
                {
                    break;
                }
            }

            int n = _replies.Length;
            if (n > 0)
            {
                const float gap = 0.02f;
                float w = (_view.Width - gap * (n - 1)) / n;
                for (int i = 0; i < n; i++)
                {
                    var r = _replies[i];
                    _view.Button(r.Label, new Vector2(_view.Left + w / 2f + i * (w + gap), _view.Bottom + 0.035f), new Vector2(w, 0.07f),
                        () => r.OnChosen?.Invoke());
                }
            }
        }
    }
}
