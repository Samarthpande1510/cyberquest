using CyberQuest.Core;
using CyberQuest.UI;
using TMPro;
using UnityEngine;

namespace CyberQuest.Events
{
    /// <summary>
    /// Fake login page shown on the work tablet once its QR code is scanned
    /// (CLAUDE.md section 3, "Fake login" row). The nexora-auth.com vs
    /// portal.nexora.com mismatch is only in the address bar text - never
    /// called out to the player until after they decide.
    /// Also owns the tablet's idle home screen, which MfaEvent draws over.
    /// </summary>
    public class FakeLoginEvent : MonoBehaviour
    {
        public static readonly Vector2 ScreenSize = new Vector2(0.42f, 0.58f);

        private UiPanel _home;
        private UiPanel _page;
        private bool _pending;
        private bool _open;

        private void Awake()
        {
            BuildHome();
            _page = UiPanel.Create(transform, "Screen", ScreenSize, "Web Browser", UiTheme.Purple);
            _page.Root.transform.localPosition = new Vector3(0f, 0f, -0.012f);
            BuildPage();
            _page.Root.SetActive(false);
        }

        /// <summary>Called by EmailEvent when the player scans the QR code.</summary>
        public void Open()
        {
            GameState.Current.loginEventTriggered = true;
            _pending = true;
            TryShow();
        }

        private void Update()
        {
            if (_pending && !_open)
            {
                TryShow();
            }
        }

        private void TryShow()
        {
            if (!GameFlow.TryBegin("login", transform))
            {
                return;
            }
            _pending = false;
            _open = true;
            _page.Root.SetActive(true);
        }

        private void BuildHome()
        {
            _home = UiPanel.Create(transform, "Home", ScreenSize, null, new Color(0.12f, 0.16f, 0.24f));
            _home.Text("11:47", new Rect(_home.Left, 0.06f, _home.Width, 0.14f), 0.75f, TextAlignmentOptions.Center, bold: true);
            _home.Text("Thursday  -  Work tablet", new Rect(_home.Left, 0.0f, _home.Width, 0.05f), 0.17f,
                TextAlignmentOptions.Center, UiTheme.MutedText);
            _home.Text("NEXORA Authenticator\nis active on this device", new Rect(_home.Left, -0.2f, _home.Width, 0.1f), 0.16f,
                TextAlignmentOptions.Center, UiTheme.MutedText);
        }

        private void BuildPage()
        {
            float y = _page.ContentTop;

            _page.Quad("AddressBar", new Vector2(0f, y - 0.025f), new Vector2(_page.Width, 0.05f), new Color(0.88f, 0.9f, 0.93f), -0.0005f);
            _page.Text("https://nexora-auth.com/verify", new Rect(_page.Left + 0.01f, y - 0.05f, _page.Width - 0.02f, 0.05f), 0.17f,
                TextAlignmentOptions.MidlineLeft, new Color(0.1f, 0.12f, 0.16f));
            y -= 0.065f;

            _page.Body("<b>NEXORA Single Sign-On</b>\nEnter your password to stay unlocked tonight.", y, 0.09f, 0.17f);
            y -= 0.1f;

            _page.Quad("PasswordField", new Vector2(0f, y - 0.0225f), new Vector2(_page.Width, 0.045f), new Color(0.2f, 0.25f, 0.34f), -0.0005f);
            _page.Text("Password:  ************", new Rect(_page.Left + 0.01f, y - 0.045f, _page.Width - 0.02f, 0.045f), 0.16f,
                TextAlignmentOptions.MidlineLeft, UiTheme.MutedText);
            y -= 0.065f;

            _page.ButtonColumn(y, 0.055f,
                ("Sign in", () => Resolve("entered")),
                ("Close page", () => Resolve("closed")),
                ("Report site", () => Resolve("reported")));
        }

        private void Resolve(string result)
        {
            GameState.Current.loginResult = result;
            GameState.Current.EvaluateCompromise();
            _page.Root.SetActive(false);
            _open = false;

            var feedback = result switch
            {
                "entered" => new Feedback(Verdict.Risky,
                    "You typed your password into nexora-auth.com - an attacker's copy. Our real login is portal.nexora.com."),
                "closed" => new Feedback(Verdict.Okay,
                    "Good instinct to back out. Reporting the site would let Security take it down."),
                _ => new Feedback(Verdict.Good,
                    "Spot on - nexora-auth.com isn't our portal (portal.nexora.com). Always check the address bar."),
            };
            GameFlow.End("login", feedback);
        }
    }
}
