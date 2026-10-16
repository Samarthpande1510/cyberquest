namespace CyberQuest.Core
{
    /// <summary>
    /// Resolves the final ending tier from GameState per CLAUDE.md 2.3.
    /// Called exactly once, at the exit terminal.
    /// </summary>
    public static class EndingResolver
    {
        public enum EndingType
        {
            Breach,
            ContainedButExposed,
            SocialMiscalibration,
            FullContainment
        }

        public static EndingType Resolve(GameState state)
        {
            if (state.credentialsCompromised && state.mfaResponse == "approved")
            {
                return EndingType.Breach;
            }

            if (state.credentialsCompromised && state.mfaResponse == "denied")
            {
                return EndingType.ContainedButExposed;
            }

            if (!state.credentialsCompromised && state.priyaResponse == "refused")
            {
                return EndingType.SocialMiscalibration;
            }

            return EndingType.FullContainment;
        }
    }
}
