namespace CyberQuest.Core
{
    // Outcome enums from CLAUDE.md section 4.1. NotReached is always the default.

    public enum LoginResult { NotReached, Reported, Closed, Entered }

    public enum UsbAction { NotReached, Reported, Plugged, Ignored }

    public enum ColleagueResponse { NotReached, VerifiedThenHelped, UnlockedBlindly, RefusedOutright }

    public enum IntercomResponse { NotReached, VerifiedExternally, RefusedWithoutVerifying, BuzzedIn }

    public enum MfaResponse { NotReached, DeniedAndReported, Denied, Ignored, Approved }

    public enum WorkstationResult { NotReached, Reported, Locked, Ignored }

    public enum SituationId { EmailQr, FakeLogin, Usb, Colleague, Intercom, Mfa, Workstation }

    public enum EndingType { Breach, ContainedButExposed, SocialMiscalibration, FullContainment }
}
