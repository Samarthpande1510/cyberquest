using System.Collections;
using CyberQuest.Core;
using CyberQuest.UI;
using TMPro;
using UnityEngine;

namespace CyberQuest.Events
{
    /// <summary>
    /// MFA push-notification attack, delivered on the work tablet
    /// (CLAUDE.md section 3, "MFA attack" row). Triggers once the login page
    /// has been reached in some form, or the imposter has already compromised
    /// the machine - never before either exists, since approving/denying an
    /// MFA push only makes sense once there's a login attempt to react to.
    /// </summary>
    public class MfaEvent : MonoBehaviour
    {
        [SerializeField] private Transform panelAnchor;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip buzzClip;

        private UiPanel _panel;
        private bool _triggered;

        private void Awake()
        {
            var parent = panelAnchor != null ? panelAnchor : transform;
            _panel = UiPanel.Create(parent, "Panel", FakeLoginEvent.ScreenSize, "NEXORA Authenticator", UiTheme.Amber);
            _panel.Root.transform.localPosition = new Vector3(0f, 0f, -0.024f);
            BuildPanel();
            _panel.Root.SetActive(false);
        }

        private void Update()
        {
            if (_triggered)
            {
                return;
            }

            bool loginReached = GameState.Current.loginEventTriggered;
            bool imposterComplied = GameState.Current.imposterResponse == "complied";

            if ((loginReached || imposterComplied) && GameFlow.TryBegin("mfa", panelAnchor, breather: 2.5f))
            {
                Trigger();
            }
        }

        private void Trigger()
        {
            _triggered = true;
            GameState.Current.mfaEventTriggered = true;

            if (audioSource != null && buzzClip != null)
            {
                audioSource.PlayOneShot(buzzClip);
            }

            _panel.Root.SetActive(true);
            StartCoroutine(Buzz());
        }

        private IEnumerator Buzz()
        {
            var t = _panel.Root.transform;
            Vector3 home = t.localPosition;
            for (float e = 0f; e < 0.8f; e += Time.deltaTime)
            {
                t.localPosition = home + new Vector3(Mathf.Sin(e * 90f) * 0.004f, 0f, 0f);
                yield return null;
            }
            t.localPosition = home;
        }

        private void BuildPanel()
        {
            float y = _panel.ContentTop;
            _panel.Body("<b>Sign-in request</b>", y, 0.05f, 0.22f, TextAlignmentOptions.Top);
            y -= 0.06f;
            _panel.Body("Someone is signing in to your NEXORA account.\n\n" +
                        "Device:  Windows PC\nLocation:  unknown\nTime:  just now",
                y, 0.2f, 0.17f);
            y -= 0.21f;
            _panel.ButtonColumn(y, 0.065f,
                ("Approve", () => Resolve("approved")),
                ("Deny & report", () => Resolve("denied")));
        }

        private void Resolve(string response)
        {
            GameState.Current.mfaResponse = response;
            _panel.Root.SetActive(false);
            GameFlow.End("mfa", response == "approved"
                ? new Feedback(Verdict.Risky,
                    "You approved a sign-in you didn't start. Attackers who have your password rely on exactly that.")
                : new Feedback(Verdict.Good,
                    "Correct! Never approve a sign-in request you didn't make yourself - and report it."));
        }
    }
}
