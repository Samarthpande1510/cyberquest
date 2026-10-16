using CyberQuest.Core;
using CyberQuest.UI;
using Oculus.Interaction;
using TMPro;
using UnityEngine;

namespace CyberQuest.Events
{
    /// <summary>
    /// Planted USB drive on the empty desk (CLAUDE.md section 3, "USB" row).
    /// Available on spawn, grabbable via Meta Interaction SDK's GrabInteractable
    /// (not XR Interaction Toolkit) so the player can pick it up and rotate it
    /// freely in hand; it can also be selected from a distance with the ray.
    /// The decision panel appears in front of the player the first time it's
    /// picked up (waiting its turn if another event is mid-decision).
    /// </summary>
    [RequireComponent(typeof(PointableUnityEventWrapper))]
    public class UsbEvent : MonoBehaviour
    {
        [SerializeField] private Transform panelAnchor;
        [SerializeField] private PointableUnityEventWrapper raySelect;
        [SerializeField] private GameObject highlight;

        private UiPanel _panel;
        private bool _pending;
        private bool _shown;

        private void Awake()
        {
            var parent = panelAnchor != null ? panelAnchor : transform;
            _panel = UiPanel.Create(parent, "Panel", new Vector2(0.82f, 0.4f), "Found: USB drive", UiTheme.Amber);
            BuildPanel();
            _panel.Root.SetActive(false);

            GetComponent<PointableUnityEventWrapper>().WhenSelect.AddListener(_ => OnGrabbed());
            if (raySelect != null)
            {
                raySelect.WhenSelect.AddListener(_ => OnGrabbed());
            }
        }

        private void Update()
        {
            if (highlight != null)
            {
                bool hint = !GameState.Current.usbFound && GameFlow.IsFree && GameState.Current.emailAction != "not-reached";
                highlight.SetActive(hint);
                if (hint)
                {
                    float s = 1f + 0.12f * Mathf.Sin(Time.time * 4f);
                    highlight.transform.localScale = new Vector3(s, 1f, s);
                }
            }

            if (_pending && !_shown && GameFlow.TryBegin("usb", panelAnchor))
            {
                _shown = true;
                _pending = false;
                if (panelAnchor != null)
                {
                    FaceViewer.PlaceInFrontOfViewer(panelAnchor, 0.95f, 0.15f);
                }
                _panel.Root.SetActive(true);
            }
        }

        private void OnGrabbed()
        {
            GameState.Current.usbFound = true;
            if (!_shown)
            {
                _pending = true;
            }
        }

        private void BuildPanel()
        {
            _panel.Body("The sticker says <b>\"PAYROLL - Q4 BONUSES\"</b>. Nobody sits at this desk. " +
                        "What do you do with it?", _panel.ContentTop, 0.15f, 0.21f);
            _panel.ButtonRow(_panel.Bottom + 0.04f, 0.08f,
                ("Plug it in", () => Resolve("plugged")),
                ("Hand to Security", () => Resolve("reported")),
                ("Leave it", () => Resolve("ignored")));
        }

        private void Resolve(string action)
        {
            GameState.Current.usbAction = action;
            GameState.Current.EvaluateCompromise();
            _panel.Root.SetActive(false);

            var feedback = action switch
            {
                "plugged" => new Feedback(Verdict.Risky,
                    "Found USB drives are a favourite attacker bait - plugging one in can install malware instantly."),
                "ignored" => new Feedback(Verdict.Okay,
                    "Not plugging it in is good - but the next person might. Hand found drives to Security."),
                _ => new Feedback(Verdict.Good,
                    "Perfect. Unknown drives go to Security - a juicy label like 'PAYROLL' is a classic lure."),
            };
            GameFlow.End("usb", feedback);
        }
    }
}
