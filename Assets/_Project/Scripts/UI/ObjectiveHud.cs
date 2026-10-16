using CyberQuest.Core;
using TMPro;
using UnityEngine;

namespace CyberQuest.UI
{
    /// <summary>
    /// Lazy-follow objective strip below the player's eye line: tells them what to
    /// do next, which way to turn, and shows feedback after every decision.
    /// </summary>
    public class ObjectiveHud : MonoBehaviour
    {
        private const float Distance = 1.15f;
        private const float Drop = 0.5f;
        private const float FeedbackSeconds = 7f;

        [SerializeField] private Transform usbTarget;
        [SerializeField] private Transform exitTarget;
        [SerializeField] private Transform briefingTarget;

        public static ObjectiveHud Instance { get; private set; }

        private UiPanel _objective;
        private TextMeshPro _objectiveText;
        private TextMeshPro _turnText;
        private TextMeshPro _progressText;
        private UiPanel _feedback;
        private TextMeshPro _feedbackText;
        private float _feedbackUntil;
        private bool _snapped;

        public string ObjectiveText => _objectiveText != null ? _objectiveText.text : "";
        public bool FeedbackVisible => _feedback != null && _feedback.Root.activeSelf;

        private void Awake()
        {
            Instance = this;

            _objective = UiPanel.Create(transform, "Objective", new Vector2(0.86f, 0.25f), null, UiTheme.Teal);
            _objective.Text("OBJECTIVE", new Rect(_objective.Left, 0.065f, 0.3f, 0.045f), 0.15f,
                TextAlignmentOptions.MidlineLeft, UiTheme.MutedText, bold: true);
            _progressText = _objective.Text("", new Rect(_objective.Left, 0.065f, _objective.Width, 0.045f), 0.15f,
                TextAlignmentOptions.MidlineRight, UiTheme.MutedText);
            _objectiveText = _objective.Text("", new Rect(_objective.Left, -0.06f, _objective.Width, 0.12f), 0.21f,
                TextAlignmentOptions.TopLeft);
            _turnText = _objective.Text("", new Rect(_objective.Left, -0.115f, _objective.Width, 0.05f), 0.2f,
                TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.3f), bold: true);

            _feedback = UiPanel.Create(transform, "Feedback", new Vector2(0.86f, 0.3f), "GOOD CALL", UiTheme.Green);
            _feedback.Root.transform.localPosition = new Vector3(0f, 0.29f, 0f);
            _feedbackText = _feedback.Body("", _feedback.ContentTop, 0.17f, 0.19f);
            _feedback.Root.SetActive(false);

            GameFlow.FeedbackRaised += ShowFeedback;
        }

        private void OnDestroy()
        {
            GameFlow.FeedbackRaised -= ShowFeedback;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void LateUpdate()
        {
            var viewer = FaceViewer.Viewer;
            if (viewer != null)
            {
                Follow(viewer, !_snapped);
                _snapped = true;
            }

            _objectiveText.text = CurrentObjective(out var target);
            _turnText.text = TurnHint(viewer, target);
            _progressText.text = $"Decisions made: {DecisionsMade()}";

            if (_feedback.Root.activeSelf && Time.time > _feedbackUntil)
            {
                _feedback.Root.SetActive(false);
            }
        }

        /// <summary>Snaps the HUD in front of an arbitrary view (used for test screenshots).</summary>
        public void SnapTo(Transform view)
        {
            Follow(view, true);
        }

        private void Follow(Transform view, bool snap)
        {
            Vector3 fwd = view.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude < 0.0001f ? Vector3.forward : fwd.normalized;
            Vector3 targetPos = view.position + fwd * Distance + Vector3.down * Drop;
            var targetRot = Quaternion.LookRotation(fwd) * Quaternion.Euler(18f, 0f, 0f);
            float k = snap ? 1f : 1f - Mathf.Exp(-3f * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, k);
        }

        private void ShowFeedback(Feedback fb)
        {
            (string title, Color color, AudioClip clip) = fb.Verdict switch
            {
                Verdict.Good => ("GOOD CALL!", UiTheme.Green, UiTheme.Instance?.good),
                Verdict.Okay => ("OKAY - COULD BE BETTER", UiTheme.Amber, UiTheme.Instance?.okay),
                _ => ("RISKY MOVE!", UiTheme.Red, UiTheme.Instance?.bad),
            };
            _feedback.Title.text = title;
            _feedback.SetAccent(color);
            _feedbackText.text = fb.Detail;
            _feedback.Root.SetActive(true);
            _feedbackUntil = Time.time + FeedbackSeconds;
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, transform.position, 0.8f);
            }
        }

        private string CurrentObjective(out Transform target)
        {
            var s = GameState.Current;
            target = GameFlow.Focus;
            if (GameFlow.Finished)
            {
                target = null;
                return "Shift complete! Check your results.";
            }
            if (!GameFlow.Started)
            {
                target = briefingTarget;
                return "Read the briefing, then press START SHIFT.";
            }
            switch (GameFlow.ActiveEvent)
            {
                case "email": return "A new email arrived. Read it on your monitor and decide what to do.";
                case "login": return "Your tablet opened a sign-in page. Look at it closely.";
                case "mfa": return "Your tablet is buzzing - a sign-in request needs an answer.";
                case "usb": return "You picked up a USB drive. What will you do with it?";
                case "priya": return "Someone is at the door asking for help.";
                case "imposter": return "A visitor from 'IT' wants to use your PC.";
            }
            if (s.usbAction == "not-reached" && s.emailAction != "not-reached")
            {
                target = usbTarget;
                return "Something was left on the empty desk to your left. Pick it up.";
            }
            if (s.imposterResponse != "not-reached" || s.imposterEventTriggered)
            {
                target = exitTarget;
                return "Shift's over. Clock out at the EXIT terminal behind you.";
            }
            target = null;
            return "Stay alert - something is about to happen...";
        }

        private static string TurnHint(Transform viewer, Transform target)
        {
            if (viewer == null || target == null)
            {
                return "";
            }
            Vector3 to = target.position - viewer.position;
            to.y = 0f;
            Vector3 fwd = viewer.forward;
            fwd.y = 0f;
            float angle = Vector3.SignedAngle(fwd, to, Vector3.up);
            if (Mathf.Abs(angle) < 40f)
            {
                return "";
            }
            if (Mathf.Abs(angle) > 140f)
            {
                return "TURN AROUND";
            }
            return angle > 0f ? "LOOK RIGHT  >>>" : "<<<  LOOK LEFT";
        }

        private static int DecisionsMade()
        {
            var s = GameState.Current;
            int n = 0;
            if (s.emailAction != "not-reached") n++;
            if (s.loginResult != "not-reached") n++;
            if (s.mfaResponse != "not-reached") n++;
            if (s.usbAction != "not-reached") n++;
            if (s.priyaResponse != "not-reached") n++;
            if (s.imposterResponse != "not-reached") n++;
            return n;
        }
    }
}
