using System.Collections;
using System.Collections.Generic;
using CyberQuest.Core;
using CyberQuest.Props;
using CyberQuest.UI;
using TMPro;
using UnityEngine;

namespace CyberQuest.Situations
{
    /// <summary>
    /// Situation 5 (CLAUDE.md 5.5), the impersonator - no human model, only a
    /// static camera feed, subtitles and a 2D badge image. The buzzer repeats
    /// every 10 s until the player reaches the lobby intercom (2 m); unanswered
    /// for 120 s, the visitor leaves and the outcome stays NotReached. Pressure
    /// lines at +20 s / +40 s also arrive on the phone. Independent checks
    /// (service desk call, maintenance notice) count as VerifiedExternally.
    /// </summary>
    public class IntercomSituation : SituationTrigger
    {
        public const float BuzzInterval = 10f;
        public const float UnansweredTimeout = 120f;
        public const float ConversationTimeout = 120f;
        public const float AnswerRadius = 2f;

        private const string Speaker = "Lobby intercom  -  visitor";

        [SerializeField] private AudioSource buzzer;
        [SerializeField] private Transform intercomAnchor;
        [SerializeField] private Transform screenAnchor;
        [SerializeField] private SlidingDoor mainDoor;
        [SerializeField] private MonitorScreen monitor;
        [SerializeField] private MailApp mail;

        private ChoicePanel _choices;
        private CallerSubtitle _subtitle;
        private UiPanel _camera;
        private UiPanel _badge;
        private bool _answered;
        private float _answeredAt;
        private float _nextBuzz;
        private int _pressureStage;

        public bool Answered => _answered;
        public ChoicePanel Choices => _choices;
        public CallerSubtitle Subtitle => _subtitle;
        public bool BadgeVisible => _badge != null && _badge.Root.activeSelf;

        private void Awake()
        {
            var screen = screenAnchor != null ? screenAnchor : transform;
            _camera = UiPanel.Create(screen, "CameraFeed", new Vector2(0.5f, 0.36f), "LOBBY DOOR  -  CAM 01", UiTheme.Slate, 1f);
            var feed = _camera.Quad("Static", new Vector2(0f, -0.035f), new Vector2(0.46f, 0.26f), new Color(0.18f, 0.2f, 0.22f), -0.0005f);
            feed.name = "StaticFeed";
            _camera.Text("NO VIDEO  -  AUDIO ONLY", new Rect(_camera.Left, -0.08f, _camera.Width, 0.08f), 0.2f,
                TextAlignmentOptions.Center, UiTheme.MutedText, bold: true);
            _camera.Root.SetActive(false);

            _badge = UiPanel.Create(screen, "BadgeImage", new Vector2(0.5f, 0.36f), "Visitor badge (held to camera)", UiTheme.Slate, 1f);
            _badge.Root.transform.localPosition = new Vector3(0f, 0f, -0.004f);
            _badge.Quad("Card", new Vector2(0f, -0.04f), new Vector2(0.4f, 0.24f), new Color(0.95f, 0.96f, 0.98f), -0.0005f);
            _badge.Quad("Photo", new Vector2(-0.12f, -0.04f), new Vector2(0.11f, 0.15f), new Color(0.6f, 0.65f, 0.72f), -0.001f);
            _badge.Text("<b>NEXORA</b>\n<size=130%><b>IT SERVICES</b></size>\nD. MERCER\nID 40-7731",
                new Rect(-0.06f, -0.15f, 0.25f, 0.22f), 0.18f, TextAlignmentOptions.MidlineLeft, new Color(0.08f, 0.12f, 0.3f));
            _badge.Root.SetActive(false);

            _subtitle = CallerSubtitle.Create(screen, "Subtitle", new Vector2(0.62f, 0.22f), 1f, 0.21f);
            _subtitle.transform.localPosition = new Vector3(0f, -0.32f, -0.01f);

            _choices = ChoicePanel.Create(screen, "IntercomChoices", new Vector2(0.62f, 0.56f), "Intercom", UiTheme.Teal, "",
                1f, verticalButtons: true, bodySize: 0.17f, buttonHeight: 0.065f);
            _choices.transform.localPosition = new Vector3(0.6f, -0.05f, 0f);
            _choices.Hide();
        }

        private void OnEnable()
        {
            if (DeskPhone.Instance != null)
            {
                DeskPhone.Instance.ServiceDeskCalled += OnServiceDeskCalled;
            }
        }

        private void Start()
        {
            if (DeskPhone.Instance != null)
            {
                DeskPhone.Instance.ServiceDeskCalled -= OnServiceDeskCalled;
                DeskPhone.Instance.ServiceDeskCalled += OnServiceDeskCalled;
            }
            if (mail != null)
            {
                mail.InboxViewed += OnInboxViewed;
            }
        }

        private void OnDisable()
        {
            if (DeskPhone.Instance != null)
            {
                DeskPhone.Instance.ServiceDeskCalled -= OnServiceDeskCalled;
            }
        }

        protected override void OnTriggered()
        {
            _nextBuzz = 0f;
            GameDirector.Notify("Someone is buzzing the main door (lobby).");
        }

        protected override void OnActiveUpdate()
        {
            float now = Director.Elapsed;
            if (!_answered)
            {
                if (now >= _nextBuzz)
                {
                    _nextBuzz = now + BuzzInterval;
                    if (buzzer != null && UiTheme.Instance != null)
                    {
                        buzzer.PlayOneShot(UiTheme.Instance.doorBuzzer);
                    }
                }
                if (intercomAnchor != null && PlayerWithin(intercomAnchor.position, AnswerRadius))
                {
                    AnswerIntercom();
                }
                else if (now - TriggeredAt > UnansweredTimeout)
                {
                    Abandon("nobody answered the intercom - visitor left");
                    GameDirector.Notify("The door buzzer has stopped.");
                }
                return;
            }

            float talk = now - _answeredAt;
            if (_pressureStage == 0 && talk >= 20f)
            {
                _pressureStage = 1;
                Pressure("Your manager already knows. I emailed the team this morning.");
            }
            else if (_pressureStage == 1 && talk >= 40f)
            {
                _pressureStage = 2;
                Pressure("If I can't get in before 6:30 this gets logged as a blocked fix, and it'll be on you.");
            }
            else if (talk >= ConversationTimeout)
            {
                State.intercom = IntercomResponse.RefusedWithoutVerifying;
                Complete("left the visitor waiting until he gave up");
                EndConversation("The visitor has left.");
            }
        }

        private void Pressure(string line)
        {
            _subtitle.Say(Speaker, "\"" + line + "\"");
            PhoneDevice.Instance?.ShowToast("Intercom (lobby)", line, 8f);
            PhoneDevice.Instance?.Buzz(1);
        }

        // --- player choices (public for tests / debug window)

        public void AnswerIntercom()
        {
            if (_answered || Resolved || !Triggered)
            {
                return;
            }
            _answered = true;
            _answeredAt = Director != null ? Director.Elapsed : 0f;
            SessionLog.Decision("Intercom: answered");
            _camera.Root.SetActive(true);
            _subtitle.Play(Speaker, new[]
            {
                "\"Hi, infrastructure here. We're seeing auth errors ahead of the maintenance window.\"",
                "\"Can you buzz me up? I'll need to sit at your machine for two minutes.\"",
            }, 3.5f);
            RefreshOptions();
            _choices.Show();
        }

        private void RefreshOptions()
        {
            var options = new List<ChoicePanel.Option>
            {
                new ChoicePanel.Option("Buzz him in", BuzzIn),
            };
            if (!State.intercomBadgeInspected)
            {
                options.Add(new ChoicePanel.Option("Ask him to show his badge", ShowBadge));
            }
            options.Add(new ChoicePanel.Option("Refuse - tell him to email", Refuse));
            if (State.maintenanceNoticeRead)
            {
                options.Add(new ChoicePanel.Option("The notice says no IT work tonight", CiteMaintenanceNotice));
            }
            _choices.SetBody("What do you do?");
            _choices.SetOptions(options.ToArray());
        }

        private bool _noticeOffered;

        protected override void Update()
        {
            base.Update();
            if (_answered && !Resolved && State.maintenanceNoticeRead && !_noticeOffered)
            {
                _noticeOffered = true;
                RefreshOptions();
            }
        }

        public void ShowBadge()
        {
            if (!_answered || Resolved)
            {
                return;
            }
            State.intercomBadgeInspected = true;
            SessionLog.Decision("Intercom: asked to see badge");
            _badge.Root.SetActive(true);
            _subtitle.Say(Speaker, "\"Sure, here you go. See? IT.\"");
            RefreshOptions();
        }

        public void BuzzIn()
        {
            if (!_answered || Resolved)
            {
                return;
            }
            State.intercom = IntercomResponse.BuzzedIn;
            Complete("buzzed the visitor in");
            EndConversation("\"Thanks, appreciate it. Coming up.\"");
            StartCoroutine(AfterBuzzIn());
        }

        private IEnumerator AfterBuzzIn()
        {
            if (mainDoor != null)
            {
                mainDoor.Open();
                yield return new WaitForSeconds(4f);
                mainDoor.Close();
            }
            yield return new WaitForSeconds(6f);
            if (monitor != null)
            {
                monitor.FlashWindow("Remote Desktop", "Remote session started by 10.0.4.77\nUser: IT-SVC", 2.5f);
            }
            SessionLog.Event("Silent compromise: remote-session window flashed after buzz-in");
        }

        public void Refuse()
        {
            if (!_answered || Resolved)
            {
                return;
            }
            State.intercom = IntercomResponse.RefusedWithoutVerifying;
            Complete("refused the visitor without verifying");
            EndConversation("\"Fine. Your call.\"");
        }

        public void CiteMaintenanceNotice()
        {
            if (!_answered || Resolved || !State.maintenanceNoticeRead)
            {
                return;
            }
            State.intercom = IntercomResponse.VerifiedExternally;
            Complete("cited the maintenance notice (no IT work tonight)");
            EndConversation("\"...Right. I'll, uh, check my ticket.\"\n\nVisitor has left.");
        }

        private void OnServiceDeskCalled()
        {
            if (!Triggered || Resolved || !_answered)
            {
                return;
            }
            State.intercom = IntercomResponse.VerifiedExternally;
            Complete("verified with the IT service desk (no engineers dispatched)");
            EndConversation("The intercom goes quiet.\n\nVisitor has left.");
        }

        private void OnInboxViewed()
        {
            if (_answered && !Resolved && _pressureStage >= 1 && !State.intercomManagerEmailChecked)
            {
                State.intercomManagerEmailChecked = true;
                SessionLog.Event("Intercom: checked inbox for the claimed manager email (none)");
            }
        }

        private void EndConversation(string finalText)
        {
            _choices.SetBody(finalText);
            _choices.SetOptions();
            _subtitle.Hide();
            StartCoroutine(HideLater());
        }

        private IEnumerator HideLater()
        {
            yield return new WaitForSeconds(6f);
            _choices.Hide();
            _badge.Root.SetActive(false);
            _camera.Root.SetActive(false);
        }
    }
}
