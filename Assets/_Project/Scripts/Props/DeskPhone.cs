using System;
using CyberQuest.Core;
using CyberQuest.UI;
using Oculus.Interaction;
using UnityEngine;

namespace CyberQuest.Props
{
    /// <summary>
    /// Desk landline. Rings at the start of the shift; picking up the handset (or
    /// pressing Answer) plays the manager's subtitled voicemail (CLAUDE.md 3).
    /// Later it's how the player calls the IT service desk (ext 2100).
    /// </summary>
    public class DeskPhone : MonoBehaviour
    {
        public static readonly string[] Voicemail =
        {
            "Hi, it's Dan. Sorry to dump this on you.",
            "The client report just needs the final numbers dropped in and sent.",
            "Building maintenance starts at 6:30, so head out before then. Thanks!",
        };

        public static readonly string[] ServiceDeskLine =
        {
            "You've reached the NEXORA IT service desk. This line is automated after hours.",
            "No engineers have been dispatched tonight.",
            "If someone claims otherwise, do not let them in. Goodbye.",
        };

        [SerializeField] private AudioSource ringer;
        [SerializeField] private PointableUnityEventWrapper handset;
        [SerializeField] private Transform panelAnchor;
        [SerializeField] private Transform subtitleAnchor;

        public static DeskPhone Instance { get; private set; }

        public event Action VoicemailFinished;
        public event Action ServiceDeskCalled;

        private ChoicePanel _panel;
        private CallerSubtitle _subtitle;
        private bool _ringing;
        private bool _answered;

        public bool Answered => _answered;
        public bool Ringing => _ringing;
        public CallerSubtitle Subtitle => _subtitle;
        public ChoicePanel Panel => _panel;

        private void Awake()
        {
            Instance = this;
            _panel = ChoicePanel.Create(panelAnchor != null ? panelAnchor : transform, "DeskPhonePanel", new Vector2(0.42f, 0.36f),
                "Desk phone  -  x2300", UiTheme.Slate, "", 0.8f, verticalButtons: true, bodySize: 0.16f, buttonHeight: 0.07f);
            _subtitle = CallerSubtitle.Create(subtitleAnchor != null ? subtitleAnchor : transform, "Subtitle", new Vector2(0.7f, 0.22f), 0.9f);
            Idle();
            if (handset != null)
            {
                handset.WhenSelect.AddListener(_ => Answer());
            }
        }

        private void Idle()
        {
            _panel.SetBody(_answered ? "" : "No calls.");
            _panel.SetOptions(
                new ChoicePanel.Option("Call IT service desk (2100)", CallServiceDesk),
                new ChoicePanel.Option("Replay voicemail", PlayVoicemail));
            _panel.Show();
        }

        public void StartRinging()
        {
            if (_answered || _ringing)
            {
                return;
            }
            _ringing = true;
            if (ringer != null)
            {
                ringer.loop = true;
                ringer.clip = UiTheme.Instance?.deskRing;
                ringer.Play();
            }
            _panel.SetBody("<b>Incoming call: Dan Whitfield (Manager)</b>\nPick up the handset or press Answer.");
            _panel.SetOptions(new ChoicePanel.Option("Answer", Answer));
            GameDirector.Notify("Your desk phone is ringing.");
        }

        public void Answer()
        {
            if (!_ringing)
            {
                return;
            }
            _ringing = false;
            _answered = true;
            if (ringer != null)
            {
                ringer.Stop();
            }
            SessionLog.Event("Voicemail answered");
            Idle();
            PlayVoicemail();
        }

        public void PlayVoicemail()
        {
            _subtitle.Play("Voicemail  -  Dan Whitfield (Manager)", Voicemail, 3.2f, () => VoicemailFinished?.Invoke());
        }

        public void CallServiceDesk()
        {
            SessionLog.Decision("Called IT service desk (2100)");
            _subtitle.Play("IT service desk  -  ext 2100 (automated)", ServiceDeskLine, 3f);
            ServiceDeskCalled?.Invoke();
        }
    }
}
