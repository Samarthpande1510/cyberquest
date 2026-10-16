using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CyberQuest.UI
{
    /// <summary>
    /// Mail app on the desk monitor: inbox list, reading view (with a "Sender
    /// details" reveal of the real address) and an attachment view that can show
    /// a QR code. Situations add emails and their action buttons.
    /// </summary>
    [RequireComponent(typeof(MonitorScreen))]
    public class MailApp : MonoBehaviour
    {
        public class Email
        {
            public string Id;
            public string FromName;
            public string ShownAddress;
            public string RealAddress;
            public string Subject;
            public string Body;
            public string Attachment;
            public string AttachmentText;
            public bool AttachmentHasQr;
            public string QrCaption;
            public bool Unread = true;
            public bool SenderRevealed;
            public ChoicePanel.Option[] Actions = Array.Empty<ChoicePanel.Option>();
        }

        private const float ViewZ = -0.003f;

        public event Action<Email> EmailOpened;
        public event Action<Email> AttachmentOpened;
        public event Action<Email> SenderRevealed;
        public event Action InboxViewed;

        private MonitorScreen _monitor;
        private UiPanel _view;
        private readonly List<Email> _emails = new List<Email>();

        public Transform QrTransform { get; private set; }
        public string OpenEmailId { get; private set; }
        public IReadOnlyList<Email> Emails => _emails;

        private Vector2 Size => MonitorScreen.ContentSize;

        private void Start()
        {
            _monitor = GetComponent<MonitorScreen>();
            _monitor.AppOpened += app =>
            {
                if (app == "Mail")
                {
                    ShowInbox();
                }
            };
            if (_view == null)
            {
                ShowInbox();
            }
        }

        private MonitorScreen Monitor => _monitor != null ? _monitor : (_monitor = GetComponent<MonitorScreen>());

        public void AddEmail(Email email, bool notify)
        {
            _emails.Insert(0, email);
            if (notify)
            {
                Monitor.AddBadge("Mail");
                Monitor.ShowToast("New email", $"{email.FromName}: {email.Subject}");
            }
            if (OpenEmailId == null)
            {
                ShowInbox();
            }
        }

        public Email Find(string id) => _emails.Find(e => e.Id == id);

        public void SetActions(string id, params ChoicePanel.Option[] actions)
        {
            var email = Find(id);
            if (email == null)
            {
                return;
            }
            email.Actions = actions;
            if (OpenEmailId == id && QrTransform == null)
            {
                Open(id);
            }
        }

        private UiPanel NewView(string title)
        {
            if (_view != null)
            {
                _view.Root.SetActive(false);
                Destroy(_view.Root);
            }
            QrTransform = null;
            _view = UiPanel.Create(Monitor.App("Mail").Root.transform, "View", Size, title, UiTheme.Blue, MonitorScreen.ViewDistance);
            _view.Root.transform.localPosition = new Vector3(0f, 0f, ViewZ);
            return _view;
        }

        private void BackButton(string label, Action onPress)
        {
            _view.Button(label, new Vector2(Size.x / 2f - 0.1f, Size.y / 2f - UiPanel.HeaderHeight / 2f),
                new Vector2(0.17f, 0.055f), () => onPress());
        }

        public void ShowInbox()
        {
            OpenEmailId = null;
            var v = NewView($"Inbox  -  {_emails.Count} messages");
            float y = v.ContentTop;
            if (_emails.Count == 0)
            {
                v.Body("No messages.", y, 0.05f, 0.18f, color: UiTheme.MutedText);
            }
            for (int i = 0; i < _emails.Count && i < 4; i++)
            {
                var e = _emails[i];
                string label = (e.Unread ? "NEW   " : "") + $"{e.FromName}  -  {e.Subject}";
                v.Button(label, new Vector2(0f, y - 0.035f), new Vector2(v.Width, 0.07f), () => Open(e.Id));
                y -= 0.085f;
            }
            InboxViewed?.Invoke();
        }

        public void Open(string id)
        {
            var e = Find(id);
            if (e == null)
            {
                return;
            }
            e.Unread = false;
            OpenEmailId = id;
            var v = NewView(e.Subject);
            BackButton("< Inbox", ShowInbox);

            string address = e.SenderRevealed && !string.IsNullOrEmpty(e.RealAddress) ? e.RealAddress : e.ShownAddress;
            v.Text($"From:  {e.FromName}  <{address}>", new Rect(v.Left, 0.1f, 0.5f, 0.045f), 0.16f,
                TextAlignmentOptions.MidlineLeft, e.SenderRevealed ? new Color(1f, 0.85f, 0.4f) : UiTheme.MutedText);
            if (!string.IsNullOrEmpty(e.RealAddress) && !e.SenderRevealed)
            {
                v.Button("Sender details", new Vector2(Size.x / 2f - UiPanel.Pad - 0.09f, 0.1225f), new Vector2(0.18f, 0.045f),
                    () => RevealSender(id));
            }
            v.Text(e.Body, new Rect(v.Left, -0.075f, v.Width, 0.165f), 0.18f, TextAlignmentOptions.TopLeft);
            if (!string.IsNullOrEmpty(e.Attachment))
            {
                v.Button("Open " + e.Attachment, new Vector2(v.Left + 0.18f, -0.105f), new Vector2(0.36f, 0.05f), () => OpenAttachment(id));
            }
            int n = e.Actions.Length;
            if (n > 0)
            {
                const float gap = 0.02f;
                float w = (v.Width - gap * (n - 1)) / n;
                for (int i = 0; i < n; i++)
                {
                    var opt = e.Actions[i];
                    v.Button(opt.Label, new Vector2(v.Left + w / 2f + i * (w + gap), v.Bottom + 0.035f), new Vector2(w, 0.07f),
                        () => opt.OnChosen?.Invoke());
                }
            }
            EmailOpened?.Invoke(e);
        }

        public void RevealSender(string id)
        {
            var e = Find(id);
            if (e == null)
            {
                return;
            }
            e.SenderRevealed = true;
            SenderRevealed?.Invoke(e);
            if (OpenEmailId == id)
            {
                Open(id);
            }
        }

        public void OpenAttachment(string id)
        {
            var e = Find(id);
            if (e == null)
            {
                return;
            }
            OpenEmailId = id;
            var v = NewView(e.Attachment);
            BackButton("< Email", () => Open(id));
            v.Text(e.AttachmentText, new Rect(v.Left, -0.2f, 0.4f, 0.34f), 0.17f, TextAlignmentOptions.TopLeft);
            if (e.AttachmentHasQr)
            {
                var qr = v.Quad("QRCode", new Vector2(0.2f, 0.0f), new Vector2(0.22f, 0.22f), Color.white, -0.0005f);
                var theme = UiTheme.Instance;
                if (theme != null && theme.qrCode != null)
                {
                    var mat = new Material(qr.sharedMaterial);
                    mat.SetTexture("_BaseMap", theme.qrCode);
                    mat.mainTexture = theme.qrCode;
                    qr.sharedMaterial = mat;
                }
                QrTransform = qr.transform;
                v.Text(e.QrCaption, new Rect(0.04f, -0.2f, 0.32f, 0.07f), 0.15f, TextAlignmentOptions.Center, UiTheme.MutedText);
            }
            AttachmentOpened?.Invoke(e);
        }
    }
}
