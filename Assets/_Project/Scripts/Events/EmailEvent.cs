using CyberQuest.Core;
using CyberQuest.UI;
using TMPro;
using UnityEngine;

namespace CyberQuest.Events
{
    /// <summary>
    /// Phishing email on the desk monitor (CLAUDE.md section 3, "Email / QR"
    /// row). Arrives as soon as the shift starts. Scanning hands off to the
    /// FakeLoginEvent on the tablet; ignoring or forwarding resolves the event.
    /// </summary>
    public class EmailEvent : MonoBehaviour
    {
        public static readonly Vector2 ScreenSize = new Vector2(0.9f, 0.54f);

        [SerializeField] private FakeLoginEvent fakeLoginEvent;

        private UiPanel _lock;
        private UiPanel _mail;
        private TextMeshPro _lockStatus;
        private bool _shown;

        private void Awake()
        {
            _lock = UiPanel.Create(transform, "LockScreen", ScreenSize, null, new Color(0.1f, 0.14f, 0.22f));
            _lock.Text("NEXORA", new Rect(_lock.Left, 0.02f, _lock.Width, 0.12f), 0.6f, TextAlignmentOptions.Center,
                new Color(0.35f, 0.75f, 1f), bold: true);
            _lockStatus = _lock.Text("Workstation ready  -  waiting for your shift to start", new Rect(_lock.Left, -0.08f, _lock.Width, 0.06f),
                0.18f, TextAlignmentOptions.Center, UiTheme.MutedText);

            _mail = UiPanel.Create(transform, "Screen", ScreenSize, "NEXORA Mail  -  Inbox (1 new)", UiTheme.Blue);
            BuildMail();
            _mail.Root.SetActive(false);
        }

        private void Update()
        {
            if (!_shown && GameFlow.TryBegin("email", transform, breather: 1f))
            {
                _shown = true;
                _lock.Root.SetActive(false);
                _mail.Root.SetActive(true);
                var notify = UiTheme.Instance?.notify;
                if (notify != null)
                {
                    AudioSource.PlayClipAtPoint(notify, transform.position, 0.9f);
                }
            }
        }

        private void BuildMail()
        {
            float top = _mail.ContentTop;
            _mail.Body("From:  IT Support  <it-support@nexora-corp.net>", top, 0.04f, 0.18f, color: UiTheme.MutedText);
            _mail.Body("<b>URGENT: Verify your account before tonight's lockout</b>", top - 0.045f, 0.045f, 0.2f);
            _mail.Text(
                "Your account must be re-verified before tonight's maintenance. " +
                "Scan the QR code with your work tablet in the next 30 minutes or you will be locked out.",
                new Rect(_mail.Left, -0.15f, 0.56f, 0.21f), 0.2f, TextAlignmentOptions.TopLeft);

            var qr = _mail.Quad("QRCode", new Vector2(0.3f, -0.04f), new Vector2(0.19f, 0.19f), Color.white, -0.0005f);
            var theme = UiTheme.Instance;
            if (theme != null && theme.qrCode != null)
            {
                var mat = new Material(qr.sharedMaterial);
                mat.SetTexture("_BaseMap", theme.qrCode);
                mat.mainTexture = theme.qrCode;
                qr.sharedMaterial = mat;
            }

            _mail.ButtonRow(_mail.Bottom + 0.0375f, 0.075f,
                ("Scan the QR code", OnScanQr),
                ("Ignore it", OnIgnore),
                ("Report to Security", OnForward));
        }

        private void OnScanQr()
        {
            GameState.Current.emailOpened = true;
            GameState.Current.qrScanned = true;
            GameState.Current.emailAction = "scanned";
            Close(new Feedback(Verdict.Risky,
                "That email came from nexora-corp.net - not our real domain. Urgency + a QR code is a classic phishing trick."));

            if (fakeLoginEvent != null)
            {
                fakeLoginEvent.Open();
            }
            else
            {
                Debug.LogWarning("EmailEvent has no fakeLoginEvent assigned.", this);
            }
        }

        private void OnIgnore()
        {
            GameState.Current.emailOpened = true;
            GameState.Current.emailAction = "ignored";
            Close(new Feedback(Verdict.Okay,
                "You didn't fall for it - but a colleague might. Reporting it lets Security block it for everyone."));
        }

        private void OnForward()
        {
            GameState.Current.emailOpened = true;
            GameState.Current.emailAction = "forwarded";
            Close(new Feedback(Verdict.Good,
                "Great catch! Fake sender domain (nexora-corp.net), false urgency and a QR code = phishing."));
        }

        private void Close(Feedback feedback)
        {
            _mail.Root.SetActive(false);
            _lockStatus.text = "Inbox  -  no new messages";
            _lock.Root.SetActive(true);
            GameFlow.End("email", feedback);
        }
    }
}
