using CyberQuest.Core;
using CyberQuest.Props;
using CyberQuest.UI;
using TMPro;
using UnityEngine;

namespace CyberQuest.Situations
{
    /// <summary>
    /// Situation 6 (CLAUDE.md 5.6): MFA push attack. Triggers after the login was
    /// entered or the visitor was buzzed in, otherwise at the 600 s fallback.
    /// The phone buzzes three times (spatial audio + haptics on the holding hand);
    /// 60 s without an answer = Ignored. If credentials are already compromised
    /// the pushes repeat faster with more urgent copy.
    /// </summary>
    public class MfaSituation : SituationTrigger
    {
        public const float AnswerWindow = 60f;

        [SerializeField] private PhoneDevice phone;

        private float _nextPush;
        private TextMeshPro _copy;

        protected override bool HasCustomCondition => true;

        protected override bool CustomCondition() =>
            State.login == LoginResult.Entered || State.intercom == IntercomResponse.BuzzedIn;

        private bool Urgent => State.CredentialsCompromised;

        protected override void OnTriggered()
        {
            var app = phone.OpenApp("Authenticator", UiTheme.Amber);
            _copy = app.Body(Copy(), app.ContentTop, 0.33f, 0.26f, TextAlignmentOptions.Top);
            app.ButtonColumn(app.Bottom + 3 * 0.08f + 2 * 0.018f, 0.08f,
                ("Approve", Approve),
                ("Deny", Deny),
                ("Deny and report", DenyAndReport));
            Push();
            GameDirector.Notify("Your phone is buzzing.");
        }

        private string Copy()
        {
            return Urgent
                ? "<color=#FCA5A5><b>URGENT - action required</b></color>\nApprove sign-in to your Nexora account now to avoid a lockout.\nLocation: unknown"
                : "<b>Approve sign-in to your Nexora account?</b>\n\nLocation: unknown\nDevice: Windows PC";
        }

        private void Push()
        {
            phone.Buzz(3);
            _nextPush = Director.Elapsed + (Urgent ? 8f : 20f);
            if (_copy != null)
            {
                _copy.text = Copy();
            }
        }

        protected override void OnActiveUpdate()
        {
            float now = Director.Elapsed;
            if (now - TriggeredAt >= AnswerWindow)
            {
                State.mfa = MfaResponse.Ignored;
                Complete("ignored the MFA push (60 s)");
                phone.CloseApp();
                return;
            }
            if (now >= _nextPush)
            {
                Push();
            }
        }

        // --- player choices (public for tests / debug window)

        public void Approve() => Resolve(MfaResponse.Approved, "approved the MFA push");

        public void Deny() => Resolve(MfaResponse.Denied, "denied the MFA push");

        public void DenyAndReport()
        {
            Resolve(MfaResponse.DeniedAndReported, "denied and reported the MFA push");
            phone.ShowToast("IT-Security", "Thanks - your password will be reset.", 4f);
        }

        private void Resolve(MfaResponse response, string decision)
        {
            if (Resolved || !Triggered)
            {
                return;
            }
            State.mfa = response;
            Complete(decision);
            phone.CloseApp();
        }
    }
}
