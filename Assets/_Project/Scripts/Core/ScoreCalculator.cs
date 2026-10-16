namespace CyberQuest.Core
{
    /// <summary>
    /// Five categories at 20% each (CLAUDE.md 7). Unreached categories are
    /// excluded and the remaining weights renormalised. Computed once at the end.
    /// </summary>
    public static class ScoreCalculator
    {
        public static ScoreBreakdown Compute(GameState s)
        {
            var b = new ScoreBreakdown
            {
                Phishing = Phishing(s),
                Physical = Physical(s),
                Social = Social(s),
                Impersonation = Impersonation(s),
                Mfa = Mfa(s),
            };

            float sum = (b.Phishing ?? 0f) + (b.Physical ?? 0f) + (b.Social ?? 0f) + (b.Impersonation ?? 0f) + (b.Mfa ?? 0f);
            int reached = b.ReachedCount;
            b.Total = reached == 0 ? 0 : (int)System.Math.Round(sum / reached * 100f, System.MidpointRounding.AwayFromZero);
            return b;
        }

        public static float? Phishing(GameState s)
        {
            if (s.login == LoginResult.Entered) return 0f;
            if (s.emailForwardedToSecurity || s.login == LoginResult.Reported) return 1f;
            if (s.login == LoginResult.Closed || s.qrScanned) return 0.5f;
            if (s.emailIgnored || s.emailOpened) return 0.75f;
            return null;
        }

        public static float? Physical(GameState s)
        {
            float? usb = s.usb switch
            {
                UsbAction.Reported => 1f,
                UsbAction.Ignored => 0.5f,
                UsbAction.Plugged => 0f,
                _ => (float?)null,
            };
            float? ws = s.workstation switch
            {
                WorkstationResult.Reported => 1f,
                WorkstationResult.Locked => 0.75f,
                WorkstationResult.Ignored => 0f,
                _ => (float?)null,
            };
            if (usb.HasValue && ws.HasValue) return (usb.Value + ws.Value) / 2f;
            return usb ?? ws;
        }

        public static float? Social(GameState s)
        {
            return s.colleague switch
            {
                ColleagueResponse.VerifiedThenHelped => 1f,
                ColleagueResponse.UnlockedBlindly => 0.5f,
                ColleagueResponse.RefusedOutright => 0.5f,
                _ => (float?)null,
            };
        }

        public static float? Impersonation(GameState s)
        {
            return s.intercom switch
            {
                IntercomResponse.VerifiedExternally => 1f,
                IntercomResponse.RefusedWithoutVerifying => 0.75f,
                IntercomResponse.BuzzedIn => 0f,
                _ => (float?)null,
            };
        }

        public static float? Mfa(GameState s)
        {
            return s.mfa switch
            {
                MfaResponse.DeniedAndReported => 1f,
                MfaResponse.Denied => 0.75f,
                MfaResponse.Ignored => 0.25f,
                MfaResponse.Approved => 0f,
                _ => (float?)null,
            };
        }
    }
}
