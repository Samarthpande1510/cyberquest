namespace CyberQuest.Core
{
    /// <summary>
    /// Single source of truth for player choices across all events. A plain C#
    /// class (not a MonoBehaviour) per CLAUDE.md 2.1 — event scripts read/write
    /// this directly instead of tracking their own parallel state.
    /// </summary>
    public class GameState
    {
        public static GameState Current { get; private set; } = new GameState();

        /// <summary>Resets to a fresh session. Used by Play Again, the debug menu (Stage 10) and tests.</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetForNewSession()
        {
            Current = new GameState();
        }

        // Event 1-2: Email / QR / Fake login
        public bool emailOpened;
        public bool qrScanned;
        public string emailAction = "not-reached"; // "ignored" | "forwarded" | "scanned" | "not-reached"
        public bool loginEventTriggered;
        public string loginResult = "not-reached"; // "reported" | "closed" | "entered" | "not-reached"

        // Event 3: USB
        public bool usbFound;
        public string usbAction = "not-reached"; // "reported" | "plugged" | "ignored" | "not-reached"

        // Event 4: False positive (Priya)
        public bool priyaEventTriggered;
        public string priyaResponse = "not-reached"; // "helped-properly" | "refused" | "let-in-unverified" | "not-reached"

        // Event 5: Imposter
        public bool imposterEventTriggered;
        public string imposterResponse = "not-reached"; // "verified" | "complied" | "delayed-then-verified" | "asked-own-login" | "not-reached"

        // Event 6: MFA attack
        public bool mfaEventTriggered;
        public string mfaResponse = "not-reached"; // "denied" | "approved" | "not-reached"

        // Derived / meta
        public bool credentialsCompromised;
        public float sessionTimeElapsed;
        public int score;

        /// <summary>Centralized compromise rule per CLAUDE.md 2.3 — call after any field
        /// that feeds it changes. Events not yet implemented stay at "not-reached" and
        /// simply don't contribute.</summary>
        public void EvaluateCompromise()
        {
            credentialsCompromised =
                loginResult == "entered" ||
                usbAction == "plugged" ||
                imposterResponse == "complied";
        }
    }
}
