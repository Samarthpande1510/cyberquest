using CyberQuest.Core;
using CyberQuest.UI;
using UnityEngine;

namespace CyberQuest.Events
{
    /// <summary>
    /// The impersonator (CLAUDE.md section 3, "Imposter" row). Triggers after a
    /// time fallback, once Priya has been dealt with, or once two prior events
    /// are resolved and things have gone quiet (section 2.2). Enters through the
    /// door on a fixed walk path (section 7 permits fixed paths, just not
    /// navmesh/pathfinding). His badge reads "IT SERVICES" against the staff
    /// directory's "IT INFRASTRUCTURE" - both are static, always-readable labels;
    /// the mismatch is only explained after the player decides.
    /// </summary>
    public class ImposterEvent : MonoBehaviour
    {
        private const float TriggerTimeSeconds = 6f * 60f;
        private const int RequiredResolvedEvents = 2;
        private const float IdleBeforeArriving = 30f;

        [SerializeField] private GameObject visualsRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform doorPoint;
        [SerializeField] private Transform stopPoint;
        [SerializeField] private float walkSpeed = 1.1f;
        [SerializeField] private Transform panelAnchor;

        private static readonly int ArrivedTrigger = Animator.StringToHash("Arrived");
        private static readonly int EndTalkTrigger = Animator.StringToHash("EndTalk");

        private UiPanel _panel;
        private bool _triggered;
        private bool _arrived;
        private float _walkTime;

        private void Awake()
        {
            if (visualsRoot != null)
            {
                visualsRoot.SetActive(false);
            }

            _panel = UiPanel.Create(panelAnchor != null ? panelAnchor : transform, "Panel",
                new Vector2(0.86f, 0.5f), "Visitor  -  says he's from IT", UiTheme.Red);
            BuildPanel();
            _panel.Root.SetActive(false);
        }

        private void Update()
        {
            if (!_triggered)
            {
                var s = GameState.Current;
                bool timeElapsed = s.sessionTimeElapsed >= TriggerTimeSeconds;
                bool priyaDone = s.priyaResponse != "not-reached";
                bool quietAfterTwo = CountResolvedEvents() >= RequiredResolvedEvents && GameFlow.IdleSeconds >= IdleBeforeArriving;
                if ((timeElapsed || priyaDone || quietAfterTwo) &&
                    GameFlow.TryBegin("imposter", visualsRoot != null ? visualsRoot.transform : transform, breather: 3f))
                {
                    Trigger();
                }
                return;
            }

            if (!_arrived)
            {
                WalkTowardStopPoint();
            }
        }

        private int CountResolvedEvents()
        {
            int count = 0;
            if (GameState.Current.emailAction != "not-reached") count++;
            if (GameState.Current.usbAction != "not-reached") count++;
            if (GameState.Current.priyaResponse != "not-reached") count++;
            return count;
        }

        private void Trigger()
        {
            _triggered = true;
            GameState.Current.imposterEventTriggered = true;

            if (visualsRoot != null && doorPoint != null)
            {
                visualsRoot.transform.position = doorPoint.position;
                visualsRoot.transform.rotation = doorPoint.rotation;
                visualsRoot.SetActive(true);
            }
        }

        private void WalkTowardStopPoint()
        {
            if (visualsRoot == null || stopPoint == null)
            {
                Arrive();
                return;
            }

            _walkTime += Time.deltaTime;
            Vector3 current = visualsRoot.transform.position;
            Vector3 target = stopPoint.position;
            current.y = target.y;

            Vector3 next = Vector3.MoveTowards(current, target, walkSpeed * Time.deltaTime);
            Vector3 facing = target - current;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f)
            {
                visualsRoot.transform.rotation = Quaternion.LookRotation(facing);
            }

            // Small step bob so he reads as walking rather than sliding.
            next.y = target.y + Mathf.Abs(Mathf.Sin(_walkTime * 7f)) * 0.03f;
            visualsRoot.transform.position = next;

            if (Vector3.Distance(new Vector3(next.x, target.y, next.z), target) < 0.03f)
            {
                visualsRoot.transform.SetPositionAndRotation(target, stopPoint.rotation);
                Arrive();
            }
        }

        private void Arrive()
        {
            _arrived = true;
            if (animator != null)
            {
                animator.SetTrigger(ArrivedTrigger);
            }
            _panel.Root.SetActive(true);
        }

        private void BuildPanel()
        {
            _panel.Body("\"Hey - IT Infrastructure. We're patching every machine tonight and I'm way behind. " +
                        "Mind if I hop on your PC for five minutes?\"",
                _panel.ContentTop, 0.17f, 0.21f);
            float rowTop = _panel.Bottom + 0.15f;
            float w = (_panel.Width - 0.02f) / 2f;
            float lx = _panel.Left + w / 2f;
            float rx = -lx;
            var size = new Vector2(w, 0.075f);
            _panel.Button("Sure, go ahead", new Vector2(lx, rowTop - 0.0375f), size, () => Resolve("complied"));
            _panel.Button("Use your own login", new Vector2(rx, rowTop - 0.0375f), size, () => Resolve("asked-own-login"));
            _panel.Button("Check the staff directory", new Vector2(lx, rowTop - 0.1125f - 0.02f), size, () => Resolve("verified"));
            _panel.Button("Stall him, call IT first", new Vector2(rx, rowTop - 0.1125f - 0.02f), size, () => Resolve("delayed-then-verified"));
        }

        private void Resolve(string response)
        {
            GameState.Current.imposterResponse = response;
            GameState.Current.EvaluateCompromise();
            _panel.Root.SetActive(false);

            if (animator != null)
            {
                animator.SetTrigger(EndTalkTrigger);
            }
            if (visualsRoot != null && response != "complied")
            {
                visualsRoot.SetActive(false); // caught out - he leaves
            }

            var feedback = response switch
            {
                "verified" => new Feedback(Verdict.Good,
                    "Busted! His badge says IT SERVICES, but the directory lists IT INFRASTRUCTURE. He was an impostor."),
                "delayed-then-verified" => new Feedback(Verdict.Good,
                    "Smart - stalling bought time to check. Real IT had nobody on site; his badge didn't match the directory."),
                "asked-own-login" => new Feedback(Verdict.Okay,
                    "Better than handing over your session, but you never checked who he was. His badge didn't match."),
                _ => new Feedback(Verdict.Risky,
                    "You gave a stranger your logged-in PC. His badge (IT SERVICES) didn't match the directory."),
            };
            GameFlow.End("imposter", feedback);
        }
    }
}
