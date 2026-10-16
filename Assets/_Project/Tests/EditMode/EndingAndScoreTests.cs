using CyberQuest.Core;
using NUnit.Framework;

namespace CyberQuest.Tests
{
    public class EndingAndScoreTests
    {
        private static GameState Clean() => new GameState();

        [Test]
        public void NothingHappened_IsFullContainment()
        {
            Assert.AreEqual(EndingType.FullContainment, EndingResolver.ResolveTier(Clean()));
        }

        [Test]
        public void PerfectRun_IsFullContainment_Score100()
        {
            var s = Clean();
            s.emailOpened = true;
            s.emailForwardedToSecurity = true;
            s.usb = UsbAction.Reported;
            s.colleague = ColleagueResponse.VerifiedThenHelped;
            s.intercom = IntercomResponse.VerifiedExternally;
            s.mfa = MfaResponse.DeniedAndReported;
            s.workstation = WorkstationResult.Reported;
            Assert.AreEqual(EndingType.FullContainment, EndingResolver.ResolveTier(s));
            Assert.AreEqual(100, ScoreCalculator.Compute(s).Total);
        }

        [Test]
        public void TwoCompromiseSources_IsBreach()
        {
            var s = Clean();
            s.login = LoginResult.Entered;
            s.usb = UsbAction.Plugged;
            s.mfa = MfaResponse.DeniedAndReported;
            Assert.AreEqual(2, s.CompromiseSources);
            Assert.AreEqual(EndingType.Breach, EndingResolver.ResolveTier(s));
        }

        [Test]
        public void ApprovedMfa_IsBreach_EvenWithNoOtherSource()
        {
            var s = Clean();
            s.mfa = MfaResponse.Approved;
            Assert.AreEqual(EndingType.Breach, EndingResolver.ResolveTier(s));
        }

        [Test]
        public void CompromisedAndIgnoredMfa_IsBreach()
        {
            var s = Clean();
            s.intercom = IntercomResponse.BuzzedIn;
            s.mfa = MfaResponse.Ignored;
            Assert.AreEqual(EndingType.Breach, EndingResolver.ResolveTier(s));
        }

        [Test]
        public void IgnoredMfa_WithoutCompromise_IsNotBreach()
        {
            var s = Clean();
            s.mfa = MfaResponse.Ignored;
            Assert.AreEqual(EndingType.FullContainment, EndingResolver.ResolveTier(s));
        }

        [Test]
        public void OneSourceAndDeniedMfa_IsContainedButExposed()
        {
            var s = Clean();
            s.login = LoginResult.Entered;
            s.mfa = MfaResponse.Denied;
            Assert.AreEqual(EndingType.ContainedButExposed, EndingResolver.ResolveTier(s));
        }

        [Test]
        public void RefusingColleague_WithNoCompromise_IsSocialMiscalibration()
        {
            var s = Clean();
            s.colleague = ColleagueResponse.RefusedOutright;
            s.intercom = IntercomResponse.VerifiedExternally;
            Assert.AreEqual(EndingType.SocialMiscalibration, EndingResolver.ResolveTier(s));
        }

        [Test]
        public void Compromise_OutranksOverCaution()
        {
            var s = Clean();
            s.colleague = ColleagueResponse.RefusedOutright;
            s.usb = UsbAction.Plugged;
            Assert.AreEqual(EndingType.ContainedButExposed, EndingResolver.ResolveTier(s));
        }

        [Test]
        public void Score_RenormalisesOverReachedCategories()
        {
            var s = Clean();
            s.usb = UsbAction.Reported;          // physical 1.0
            s.mfa = MfaResponse.Ignored;         // mfa 0.25
            var b = ScoreCalculator.Compute(s);
            Assert.AreEqual(2, b.ReachedCount);
            Assert.IsNull(b.Phishing);
            Assert.AreEqual(63, b.Total);        // (1.0 + 0.25) / 2 = 62.5 -> 63
        }

        [Test]
        public void Score_NothingReached_IsZero()
        {
            Assert.AreEqual(0, ScoreCalculator.Compute(Clean()).Total);
        }

        [Test]
        public void Physical_AveragesUsbAndWorkstation()
        {
            var s = Clean();
            s.usb = UsbAction.Ignored;               // 0.5
            s.workstation = WorkstationResult.Locked; // 0.75
            Assert.AreEqual(0.625f, ScoreCalculator.Physical(s).Value, 1e-4);
        }

        [Test]
        public void Phishing_Values()
        {
            var s = Clean();
            s.emailOpened = true;
            s.emailIgnored = true;
            Assert.AreEqual(0.75f, ScoreCalculator.Phishing(s));
            s.qrScanned = true;
            s.login = LoginResult.Closed;
            Assert.AreEqual(0.5f, ScoreCalculator.Phishing(s));
            s.login = LoginResult.Reported;
            Assert.AreEqual(1f, ScoreCalculator.Phishing(s));
            s.login = LoginResult.Entered;
            Assert.AreEqual(0f, ScoreCalculator.Phishing(s));
        }

        [Test]
        public void Debrief_HasEntryPerReachedSituation_AndUnreportedNote()
        {
            var s = Clean();
            s.emailOpened = true;
            s.emailForwardedToSecurity = true;
            s.colleague = ColleagueResponse.UnlockedBlindly;
            var r = EndingResolver.Resolve(s);
            Assert.AreEqual(3, r.Debrief.Count); // email, colleague, "never reported"
            Assert.AreEqual("Incident report", r.Debrief[2].Situation);

            s.reportFiled = true;
            Assert.AreEqual(2, EndingResolver.Resolve(s).Debrief.Count);
        }

        [Test]
        public void CompletionOrder_RecordsOnce()
        {
            var s = Clean();
            s.MarkCompleted(SituationId.Usb);
            s.MarkCompleted(SituationId.EmailQr);
            s.MarkCompleted(SituationId.Usb);
            CollectionAssert.AreEqual(new[] { SituationId.Usb, SituationId.EmailQr }, s.completionOrder);
        }
    }
}
