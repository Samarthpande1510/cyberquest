using System.Collections.Generic;
using System.Text;
using CyberQuest.Core;
using CyberQuest.Props;
using CyberQuest.UI;
using UnityEngine;

namespace CyberQuest.Situations
{
    /// <summary>
    /// Final deduction and escape (CLAUDE.md 5.8). The lobby security terminal
    /// activates once 3 situations are completed, or at the 6:30 lockout. It shows
    /// a timeline of what actually happened, asks "What's actually happening
    /// here?" (wrong answers get an in-world correction, not a fail), takes a short
    /// incident report, unlocks the exit and shows the ending. EndingResolver.Resolve
    /// is called exactly once, here.
    /// </summary>
    public class EndingFlow : MonoBehaviour
    {
        public const int SituationsToActivate = 3;

        public const string AnswerA = "A) System maintenance glitches";
        public const string AnswerB = "B) One employee's mistake";
        public const string AnswerC = "C) A coordinated attempt to get into company systems";
        public const string AnswerD = "D) Nothing - a normal Tuesday";

        [SerializeField] private Transform terminalAnchor;
        [SerializeField] private SlidingDoor exitDoor;
        [SerializeField] private EndingScreen endingScreen;
        [SerializeField] private SituationTrigger[] situations;

        private ChoicePanel _terminal;
        private bool _active;
        private bool _resolved;
        private readonly HashSet<SituationId> _ticked = new HashSet<SituationId>();

        public bool Active => _active;
        public bool ResolvedOnce => _resolved;
        public ChoicePanel Terminal => _terminal;
        public EndingResult Result { get; private set; }
        public ScoreBreakdown Score { get; private set; }
        public string SessionLogPath { get; private set; }
        public int ResolveCount { get; private set; }

        private void Awake()
        {
            _terminal = ChoicePanel.Create(terminalAnchor != null ? terminalAnchor : transform, "SecurityTerminal",
                new Vector2(0.9f, 0.9f), "NEXORA Security Terminal", UiTheme.Teal,
                "Terminal idle.\nAvailable once the evening's events have played out, or at 6:30 PM.", 1f,
                verticalButtons: true, bodySize: 0.19f, buttonHeight: 0.06f);
            _terminal.Show();
            if (endingScreen != null)
            {
                endingScreen.PlayAgain += Restart;
            }
        }

        private void Start()
        {
            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.LockedOutEvent += OnLockout;
            }
        }

        private void Update()
        {
            var director = GameDirector.Instance;
            if (_active || director == null || !director.Running)
            {
                return;
            }
            if (GameState.Current.CompletedCount >= SituationsToActivate)
            {
                Activate(lockout: false);
            }
        }

        private void OnLockout()
        {
            if (_resolved)
            {
                return;
            }
            if (_active)
            {
                ShowTimeline(true); // restart check-out with the lockout log, wherever the player got to
            }
            Activate(lockout: true);
            // The player may be anywhere on the floor: bring the terminal to them.
            FaceViewer.PlaceInFrontOfViewer(_terminal.transform, 1.1f, 0.05f);
        }

        public void Activate(bool lockout)
        {
            if (_active)
            {
                return;
            }
            _active = true;
            SessionLog.Event(lockout ? "Security terminal activated by lockout" : "Security terminal activated");
            GameDirector.Notify(lockout
                ? "6:30 PM - the building is locking down. Check out at the security terminal."
                : "The security terminal in the lobby is ready. Check out there when you're done.");
            ShiftHud.Instance?.SetObjective("Check out at the security terminal in the lobby, then leave.");
            if (lockout)
            {
                ShowTimeline(true);
                return;
            }
            _terminal.SetTitle("NEXORA Security Terminal");
            _terminal.SetBody("Ready to check out.\nChecking out ends your shift - finish anything else first.");
            _terminal.SetOptions(new ChoicePanel.Option("Start check-out", () => ShowTimeline(false)));
        }

        public void ShowTimeline(bool lockout)
        {
            var sb = new StringBuilder(lockout ? "<b>6:30 PM lockout.</b> Tonight's activity log:\n" : "<b>Tonight's activity log:</b>\n");
            var happened = new List<SituationTrigger>();
            foreach (var s in situations)
            {
                if (s != null && s.Triggered)
                {
                    happened.Add(s);
                }
            }
            happened.Sort((a, b) => a.TriggeredAt.CompareTo(b.TriggeredAt));
            foreach (var s in happened)
            {
                {
                    float minutes = GameDirector.Instance != null ? GameDirector.Instance.GameMinutesAt(s.TriggeredAt) : GameDirector.StartMinutes;
                    sb.Append(GameDirector.FormatTime(minutes)).Append("   ").Append(Describe(s.Id)).Append('\n');
                }
            }
            _terminal.SetTitle("Security Terminal  -  Activity log");
            _terminal.SetBody(sb.ToString());
            _terminal.SetOptions(new ChoicePanel.Option("Continue", AskQuestion));
            _terminal.Show();
        }

        public static string Describe(SituationId id) => id switch
        {
            SituationId.EmailQr => "Invoice email with a QR code",
            SituationId.FakeLogin => "Sign-in page opened on your phone",
            SituationId.Usb => "Unlabelled USB drive found by a desk",
            SituationId.Colleague => "Chat request: unlock Meeting Room 3",
            SituationId.Intercom => "Visitor on the intercom asking to come up",
            SituationId.Mfa => "Sign-in approval pushes on your phone",
            _ => "Unlocked workstation in the Finance pod",
        };

        public void AskQuestion()
        {
            _terminal.SetTitle("Security Terminal  -  Your assessment");
            _terminal.SetBody("<b>What's actually happening here?</b>");
            _terminal.SetOptions(
                new ChoicePanel.Option(AnswerA, () => Answer(false)),
                new ChoicePanel.Option(AnswerB, () => Answer(false)),
                new ChoicePanel.Option(AnswerC, () => Answer(true)),
                new ChoicePanel.Option(AnswerD, () => Answer(false)));
        }

        public void Answer(bool correct)
        {
            GameState.Current.deductionCorrect = correct;
            SessionLog.Decision("Deduction: " + (correct ? "correct (coordinated attempt)" : "incorrect"));
            _terminal.SetBody(correct
                ? "<b>Correct.</b> Each event looked small on its own. Together they were one coordinated attempt to get into NEXORA's systems."
                : "<b>Not quite.</b> Put the pieces together: a lookalike invoice, a dropped drive, a 'technician' at the door and sign-in pushes, " +
                  "all on one quiet evening. That's a coordinated attempt to get into company systems.");
            _terminal.SetOptions(new ChoicePanel.Option("Continue to incident report", ShowReportForm));
        }

        public void ShowReportForm()
        {
            _terminal.SetTitle("Security Terminal  -  Incident report");
            _terminal.SetBody("Tick everything that happened tonight, then submit.");
            var options = new List<ChoicePanel.Option>();
            foreach (var s in situations)
            {
                if (s != null && s.Triggered && options.Count < 6)
                {
                    var id = s.Id;
                    string box = _ticked.Contains(id) ? "[x] " : "[  ] ";
                    options.Add(new ChoicePanel.Option(box + Describe(id), () => Toggle(id)));
                }
            }
            options.Add(new ChoicePanel.Option("Submit report", () => SubmitReport(true)));
            _terminal.SetOptions(options.ToArray());
        }

        public void Toggle(SituationId id)
        {
            if (!_ticked.Add(id))
            {
                _ticked.Remove(id);
            }
            ShowReportForm();
        }

        public void SubmitReport(bool filed)
        {
            GameState.Current.reportFiled = filed;
            SessionLog.Decision(filed ? $"Incident report filed ({_ticked.Count} items ticked)" : "Incident report skipped");
            ResolveEnding();
        }

        /// <summary>Resolves the ending exactly once and shows it.</summary>
        public void ResolveEnding()
        {
            if (_resolved)
            {
                return;
            }
            _resolved = true;
            ResolveCount++;

            foreach (var s in situations)
            {
                if (s is UsbSituation usb) usb.FinalizeAtEnd();
                if (s is WorkstationSituation ws) ws.FinalizeAtEnd();
            }

            var state = GameState.Current;
            GameDirector.Instance?.Finish();
            Result = EndingResolver.Resolve(state);
            Score = ScoreCalculator.Compute(state);
            SessionLogPath = SessionLog.Write(state, Result.Ending, Score.Total);
            Debug.Log($"[CyberQuest] Ending {Result.Ending}, score {Score.Total}. Session log: {SessionLogPath}");

            _terminal.SetOptions();
            _terminal.Hide();
            exitDoor?.Open();
            ShiftHud.Instance?.SetObjective("Shift over. See your report.");

            if (endingScreen != null)
            {
                var viewer = FaceViewer.Viewer;
                bool atTerminal = viewer != null && terminalAnchor != null &&
                                  Vector3.Distance(viewer.position, terminalAnchor.position) < 2.5f &&
                                  Vector3.Distance(_terminal.transform.position, terminalAnchor.position) < 0.05f;
                if (atTerminal)
                {
                    // Replace the terminal screen in place (its bezel would otherwise hide a panel placed behind it).
                    endingScreen.transform.SetPositionAndRotation(
                        terminalAnchor.position - terminalAnchor.forward * 0.03f, terminalAnchor.rotation);
                }
                else
                {
                    FaceViewer.PlaceInFrontOfViewer(endingScreen.transform, 1.15f, 0.05f);
                }
                endingScreen.Show(Result, Score);
            }
            UiTheme.Play(t => Result.Ending == EndingType.FullContainment ? t.good : t.okay, transform.position, 0.9f);
        }

        public static void Restart()
        {
            GameState.ResetForNewSession();
            SessionLog.Reset();
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
}
