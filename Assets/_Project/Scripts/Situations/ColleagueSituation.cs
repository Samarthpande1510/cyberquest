using CyberQuest.Core;
using CyberQuest.Props;
using CyberQuest.UI;
using UnityEngine;

namespace CyberQuest.Situations
{
    /// <summary>
    /// Situation 4 (CLAUDE.md 5.4), the false positive. Priya Nair (Finance) asks
    /// in chat for Meeting Room 3 to be unlocked from the corridor door panel.
    /// She is legitimate - nothing here may imply otherwise. Whether the player
    /// read her lobby directory entry first decides VerifiedThenHelped vs
    /// UnlockedBlindly.
    /// </summary>
    public class ColleagueSituation : SituationTrigger
    {
        private const string Priya = "Priya Nair - Finance";

        [SerializeField] private ChatApp chat;
        [SerializeField] private Transform doorPanelAnchor;
        [SerializeField] private SlidingDoor meetingRoomDoor;

        private ChoicePanel _doorPanel;

        public ChoicePanel DoorPanel => _doorPanel;

        private void Awake()
        {
            _doorPanel = ChoicePanel.Create(doorPanelAnchor != null ? doorPanelAnchor : transform, "MeetingRoom3Panel",
                new Vector2(0.5f, 0.36f), "Meeting Room 3", UiTheme.Teal, "Locked after hours.", 0.8f,
                verticalButtons: true, bodySize: 0.18f, buttonHeight: 0.08f);
            _doorPanel.SetOptions(new ChoicePanel.Option("Unlock door", UnlockDoor));
            _doorPanel.Show();
        }

        protected override void OnTriggered()
        {
            chat.Post(Priya, "PN",
                "Hi, I didn't realise anyone was still in. I left my charger in Meeting Room 3 and my badge won't open it " +
                "after hours. Could you unlock it from the door panel?",
                verified: true);
            chat.SetReplies(
                new ChoicePanel.Option("Sure, on my way", Acknowledge),
                new ChoicePanel.Option("Sorry, I can't", Refuse),
                new ChoicePanel.Option("Please ask Security", AskSecurity));
            _doorPanel.SetBody("Access request pending: Priya Nair (Finance).");
            GameDirector.Notify("New chat message on your monitor.");
        }

        // --- player choices (public for tests / debug window)

        public void Acknowledge()
        {
            chat.PostFromPlayer("Sure, on my way.");
            chat.SetReplies();
            SessionLog.Decision("Colleague: replied 'on my way'");
        }

        public void Refuse() => RefuseWith("Sorry, I can't help with that.", "Oh - okay, no worries. I'll grab it tomorrow.");

        public void AskSecurity() => RefuseWith("Please ask Security.", "Okay, I'll try the security desk. Thanks anyway!");

        private void RefuseWith(string playerLine, string reply)
        {
            if (Resolved)
            {
                return;
            }
            State.colleague = ColleagueResponse.RefusedOutright;
            Complete("refused Priya's request");
            chat.PostFromPlayer(playerLine);
            chat.SetReplies();
            chat.Post(Priya, "PN", reply, verified: true);
            _doorPanel.SetBody("Locked after hours.");
        }

        public void UnlockDoor()
        {
            if (!Triggered)
            {
                _doorPanel.SetBody("Locked after hours. No pending access requests.");
                return;
            }
            if (Resolved)
            {
                return;
            }
            bool verified = State.colleagueDirectoryChecked;
            State.colleague = verified ? ColleagueResponse.VerifiedThenHelped : ColleagueResponse.UnlockedBlindly;
            Complete(verified ? "checked the directory, then unlocked Meeting Room 3" : "unlocked Meeting Room 3 without checking");
            if (meetingRoomDoor != null)
            {
                meetingRoomDoor.Open();
            }
            _doorPanel.SetBody("Unlocked for Priya Nair (Finance).");
            _doorPanel.SetOptions();
            chat.SetReplies();
            chat.Post(Priya, "PN", "Got it, thank you so much! Have a good night.", verified: true);
        }
    }
}
