namespace CyberQuest.Core
{
    /// <summary>Per-category scores 0..1 (null = never reached, excluded from the total).</summary>
    public class ScoreBreakdown
    {
        public float? Phishing;
        public float? Physical;
        public float? Social;
        public float? Impersonation;
        public float? Mfa;

        /// <summary>0..100 after renormalising over reached categories (CLAUDE.md 7).</summary>
        public int Total;

        public int ReachedCount =>
            (Phishing.HasValue ? 1 : 0) + (Physical.HasValue ? 1 : 0) + (Social.HasValue ? 1 : 0) +
            (Impersonation.HasValue ? 1 : 0) + (Mfa.HasValue ? 1 : 0);
    }
}
