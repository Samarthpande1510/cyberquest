using CyberQuest.Core;
using CyberQuest.UI;
using UnityEngine;

namespace CyberQuest.Events
{
    /// <summary>
    /// Priya, the false-positive NPC (CLAUDE.md section 3, "Priya" row and
    /// section 1's false-positive requirement). Triggers after a time fallback
    /// or once the player has dealt with earlier events, not gated on which
    /// specific one (section 2.2). No pathfinding (section 7): she simply
    /// appears at the door rather than walking in.
    /// </summary>
    public class PriyaEvent : MonoBehaviour
    {
        // Conference-length session: fall back to time if the player stalls.
        private const float TriggerTimeSeconds = 4f * 60f;
        private const float IdleBeforeArriving = 25f;

        [SerializeField] private GameObject visualsRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform panelAnchor;

        private static readonly int TalkTrigger = Animator.StringToHash("Talk");
        private static readonly int EndTalkTrigger = Animator.StringToHash("EndTalk");

        private UiPanel _panel;
        private bool _triggered;

        private void Awake()
        {
            if (visualsRoot != null)
            {
                visualsRoot.SetActive(false);
            }

            _panel = UiPanel.Create(panelAnchor != null ? panelAnchor : transform, "Panel",
                new Vector2(0.86f, 0.56f), "Priya  (Marketing)  -  at the door", UiTheme.Purple);
            BuildPanel();
            _panel.Root.SetActive(false);
        }

        private void Update()
        {
            if (_triggered)
            {
                return;
            }

            var s = GameState.Current;
            bool timeElapsed = s.sessionTimeElapsed >= TriggerTimeSeconds;
            bool usbDone = s.usbAction != "not-reached";
            bool emailDoneAndQuiet = s.emailAction != "not-reached" && GameFlow.IdleSeconds >= IdleBeforeArriving;

            if ((timeElapsed || usbDone || emailDoneAndQuiet) &&
                GameFlow.TryBegin("priya", visualsRoot != null ? visualsRoot.transform : transform, breather: 3f))
            {
                Trigger();
            }
        }

        private void Trigger()
        {
            _triggered = true;
            GameState.Current.priyaEventTriggered = true;

            if (visualsRoot != null)
            {
                visualsRoot.SetActive(true);
            }
            if (animator != null)
            {
                animator.SetTrigger(TalkTrigger);
            }
            var notify = UiTheme.Instance?.notify;
            if (notify != null && visualsRoot != null)
            {
                AudioSource.PlayClipAtPoint(notify, visualsRoot.transform.position + Vector3.up * 1.6f);
            }

            _panel.Root.SetActive(true);
        }

        private void BuildPanel()
        {
            _panel.Body("\"Hi! Sorry - I left my laptop charger in the meeting room and my badge " +
                        "won't open the door this late. Could you let me in for a minute?\"",
                _panel.ContentTop, 0.16f, 0.21f);
            _panel.ButtonColumn(_panel.Bottom + 0.246f, 0.07f,
                ("Check her badge, then help", () => Resolve("helped-properly")),
                ("Sorry, I can't", () => Resolve("refused")),
                ("Sure, come in", () => Resolve("let-in-unverified")));
        }

        private void Resolve(string response)
        {
            GameState.Current.priyaResponse = response;
            _panel.Root.SetActive(false);

            if (animator != null)
            {
                animator.SetTrigger(EndTalkTrigger);
            }

            var feedback = response switch
            {
                "helped-properly" => new Feedback(Verdict.Good,
                    "Nice balance! Her badge and the directory checked out - she really is Priya from Marketing."),
                "refused" => new Feedback(Verdict.Okay,
                    "Safe, but Priya is a real colleague. Good security means verifying - then helping."),
                _ => new Feedback(Verdict.Risky,
                    "She was genuine this time, but letting people in unchecked is exactly how tailgating attacks work."),
            };
            GameFlow.End("priya", feedback);
        }
    }
}
