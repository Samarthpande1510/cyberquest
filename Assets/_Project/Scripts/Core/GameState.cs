using System.Collections.Generic;

namespace CyberQuest.Core
{
    /// <summary>
    /// Single source of truth for every player choice (CLAUDE.md 4.1). Plain C#;
    /// situations write here directly and nothing else tracks "what step are we on".
    /// </summary>
    [System.Serializable]
    public class GameState
    {
        public static GameState Current { get; private set; } = new GameState();

        /// <summary>Fresh session (Play Again, tests, debug window).</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetForNewSession()
        {
            Current = new GameState();
        }

        // Situation 1-2: email, QR, fake login
        public bool emailOpened, emailForwardedToSecurity, emailIgnored, qrScanned;
        public LoginResult login;

        // Situation 3: USB
        public UsbAction usb;

        // Situation 4: colleague (false positive)
        public ColleagueResponse colleague;
        public bool colleagueDirectoryChecked;

        // Situation 5: intercom impersonator
        public IntercomResponse intercom;
        public bool intercomBadgeInspected, intercomManagerEmailChecked, maintenanceNoticeRead;

        // Situation 6: MFA push
        public MfaResponse mfa;

        // Situation 7: unlocked workstation (optional)
        public WorkstationResult workstation;

        // Start-of-shift orientation: the company policies board in the open plan (research data only)
        public bool policyBoardRead;

        // Ending flow
        public bool deductionCorrect, reportFiled;
        public float gameMinutesAtEnd;

        /// <summary>Actual completion order, for research data.</summary>
        public List<SituationId> completionOrder = new List<SituationId>();

        /// <summary>login==Entered, usb==Plugged, intercom==BuzzedIn, mfa==Approved.</summary>
        public int CompromiseSources =>
            (login == LoginResult.Entered ? 1 : 0) +
            (usb == UsbAction.Plugged ? 1 : 0) +
            (intercom == IntercomResponse.BuzzedIn ? 1 : 0) +
            (mfa == MfaResponse.Approved ? 1 : 0);

        public bool CredentialsCompromised => CompromiseSources > 0;

        public int CompletedCount => completionOrder.Count;

        public void MarkCompleted(SituationId id)
        {
            if (!completionOrder.Contains(id))
            {
                completionOrder.Add(id);
            }
        }

        public bool IsCompleted(SituationId id) => completionOrder.Contains(id);
    }
}
