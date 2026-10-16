using System.Collections;
using CyberQuest.Core;
using CyberQuest.Props;
using CyberQuest.UI;
using TMPro;
using UnityEngine;

namespace CyberQuest.Situations
{
    /// <summary>
    /// Situation 2 (CLAUDE.md 5.2): only after the QR is scanned. The phone opens
    /// a Nexora-styled login whose address bar (nexora-auth.com) is small enough
    /// to need bringing the phone close. "Log in" leads to a silent compromise.
    /// </summary>
    public class FakeLoginSituation : SituationTrigger
    {
        public const float SilentCompromiseDelay = 30f;

        [SerializeField] private PhoneDevice phone;
        [SerializeField] private MonitorScreen monitor;

        protected override bool HasCustomCondition => true;
        protected override bool CustomCondition() => State.qrScanned;

        protected override void OnTriggered()
        {
            var page = phone.OpenApp("Browser", UiTheme.Purple);
            float y = page.ContentTop;
            page.Quad("AddressBar", new Vector2(0f, y - 0.025f), new Vector2(page.Width, 0.05f), new Color(0.88f, 0.9f, 0.93f), -0.0005f);
            // Deliberately small (about 3 mm on the real screen): you have to bring the phone close.
            page.Text("https://nexora-auth.com/verify", new Rect(page.Left + 0.01f, y - 0.05f, page.Width - 0.02f, 0.05f), 0.15f,
                TextAlignmentOptions.MidlineLeft, new Color(0.15f, 0.17f, 0.22f));
            y -= 0.08f;
            page.Text("NEXORA", new Rect(page.Left, y - 0.07f, page.Width, 0.07f), 0.5f, TextAlignmentOptions.Center,
                new Color(0.35f, 0.75f, 1f), bold: true);
            y -= 0.08f;
            page.Body("Sign in to verify the payment status.", y, 0.08f, 0.26f, TextAlignmentOptions.Top);
            y -= 0.09f;
            page.Quad("Password", new Vector2(0f, y - 0.03f), new Vector2(page.Width, 0.06f), new Color(0.2f, 0.25f, 0.34f), -0.0005f);
            page.Text("Password", new Rect(page.Left + 0.015f, y - 0.06f, page.Width, 0.06f), 0.24f, TextAlignmentOptions.MidlineLeft, UiTheme.MutedText);
            y -= 0.075f;
            page.ButtonColumn(y, 0.08f,
                ("Log in", LogIn),
                ("Close", Close),
                ("Report as phishing", Report));

            phone.Buzz(1);
            GameDirector.Notify("Your phone opened a web page.");
        }

        public void LogIn()
        {
            if (Resolved)
            {
                return;
            }
            State.login = LoginResult.Entered;
            Complete("entered credentials on the fake login");
            StartCoroutine(AfterLogin());
        }

        private IEnumerator AfterLogin()
        {
            var page = phone.OpenApp("Browser", UiTheme.Purple);
            var status = page.Body("Authenticating...", page.ContentTop - 0.2f, 0.2f, 0.3f, TextAlignmentOptions.Center);
            yield return new WaitForSeconds(2f);
            status.text = "<b>NEXORA Portal</b>\n\nPayment status: VERIFIED\nThank you.";
            yield return new WaitForSeconds(4f);
            phone.CloseApp();
            yield return new WaitForSeconds(SilentCompromiseDelay - 6f);
            monitor.FlashWindow("svchost_upd.exe", "Remote process started...\nSyncing credentials", 1.6f);
            SessionLog.Event("Silent compromise: process window flashed after login");
        }

        public void Close()
        {
            if (Resolved)
            {
                return;
            }
            State.login = LoginResult.Closed;
            Complete("closed the fake login");
            phone.CloseApp();
        }

        public void Report()
        {
            if (Resolved)
            {
                return;
            }
            State.login = LoginResult.Reported;
            Complete("reported the fake login as phishing");
            phone.CloseApp();
            phone.ShowToast("IT-Security", "Thanks - the page has been reported.", 4f);
        }
    }
}
