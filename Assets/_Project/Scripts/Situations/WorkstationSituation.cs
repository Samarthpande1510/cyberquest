using CyberQuest.Core;
using CyberQuest.UI;
using UnityEngine;

namespace CyberQuest.Situations
{
    /// <summary>
    /// Situation 7 (CLAUDE.md 5.7), optional: a colleague's unlocked PC in the
    /// Finance pod showing Q3 payroll, with a password note under the keyboard.
    /// Present from spawn, never director-scheduled, never blocks anything.
    /// Stays NotReached unless the player goes and inspects it.
    /// </summary>
    public class WorkstationSituation : SituationTrigger
    {
        [SerializeField] private Transform panelAnchor;
        [SerializeField] private GameObject unlockedScreen;
        [SerializeField] private GameObject lockedScreen;
        [SerializeField] private GameObject stickyNote;

        private ChoicePanel _panel;

        public ChoicePanel Panel => _panel;

        private void Awake()
        {
            _panel = ChoicePanel.Create(panelAnchor != null ? panelAnchor : transform, "WorkstationChoices",
                new Vector2(0.56f, 0.44f), "Colleague's desk", UiTheme.Slate,
                "This PC is unlocked and nobody's around.", 0.9f, verticalButtons: true, bodySize: 0.18f, buttonHeight: 0.07f);
            _panel.SetOptions(
                new ChoicePanel.Option("Lock the screen", Lock),
                new ChoicePanel.Option("Take the note and report it", Report),
                new ChoicePanel.Option("Leave it", Leave));
            _panel.Hide();
            if (lockedScreen != null)
            {
                lockedScreen.SetActive(false);
            }
        }

        protected override void OnTriggered()
        {
            _panel.Show();
        }

        // --- player choices (public for tests / debug window)

        public void Lock()
        {
            if (Resolve(WorkstationResult.Locked, "locked the colleague's screen"))
            {
                SetLocked();
            }
        }

        public void Report()
        {
            if (Resolve(WorkstationResult.Reported, "took the password note and reported it"))
            {
                SetLocked();
                if (stickyNote != null)
                {
                    stickyNote.SetActive(false);
                }
            }
        }

        public void Leave() => Resolve(WorkstationResult.Ignored, "left the unlocked workstation");

        private bool Resolve(WorkstationResult result, string decision)
        {
            if (Resolved || !Triggered)
            {
                return false;
            }
            State.workstation = result;
            Complete(decision);
            _panel.Hide();
            return true;
        }

        private void SetLocked()
        {
            if (unlockedScreen != null)
            {
                unlockedScreen.SetActive(false);
            }
            if (lockedScreen != null)
            {
                lockedScreen.SetActive(true);
            }
        }

        /// <summary>Called once by the ending flow: inspected but never acted on = Ignored.</summary>
        public void FinalizeAtEnd()
        {
            if (Triggered && !Resolved)
            {
                State.workstation = WorkstationResult.Ignored;
                SessionLog.Decision("Workstation: inspected but left (ignored)");
            }
        }
    }
}
