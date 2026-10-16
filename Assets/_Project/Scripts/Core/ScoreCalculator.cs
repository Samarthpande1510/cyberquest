namespace CyberQuest.Core
{
    /// <summary>
    /// Computes the weighted 0-100 score per CLAUDE.md section 4. Each of the
    /// five categories scores 0 / 0.5 / 1 based on response quality; an event
    /// the player never reached scores 0.5 (neutral - neither punished nor
    /// rewarded for running out of time).
    /// </summary>
    public static class ScoreCalculator
    {
        public static int Compute(GameState state)
        {
            float phishing = ScorePhishing(state);
            float physical = ScoreUsb(state);
            float social = ScorePriya(state);
            float impersonation = ScoreImposter(state);
            float mfa = ScoreMfa(state);

            float weighted = (phishing + physical + social + impersonation + mfa) / 5f;
            return UnityEngine.Mathf.RoundToInt(weighted * 100f);
        }

        private static float ScorePhishing(GameState state)
        {
            if (state.loginResult == "entered")
            {
                return 0f;
            }
            if (state.emailAction == "forwarded" || state.loginResult == "reported")
            {
                return 1f;
            }
            if (state.emailAction == "ignored" || state.loginResult == "closed")
            {
                return 0.5f;
            }
            return 0.5f; // not-reached
        }

        private static float ScoreUsb(GameState state)
        {
            return state.usbAction switch
            {
                "reported" => 1f,
                "ignored" => 0.5f,
                "plugged" => 0f,
                _ => 0.5f, // not-reached
            };
        }

        private static float ScorePriya(GameState state)
        {
            return state.priyaResponse switch
            {
                "helped-properly" => 1f,
                "refused" => 0.5f,
                "let-in-unverified" => 0f,
                _ => 0.5f, // not-reached
            };
        }

        private static float ScoreImposter(GameState state)
        {
            return state.imposterResponse switch
            {
                "verified" => 1f,
                "delayed-then-verified" => 1f,
                "asked-own-login" => 0.5f,
                "complied" => 0f,
                _ => 0.5f, // not-reached
            };
        }

        private static float ScoreMfa(GameState state)
        {
            return state.mfaResponse switch
            {
                "denied" => 1f,
                "approved" => 0f,
                _ => 0.5f, // not-reached
            };
        }
    }
}
