using System.IO;
using CyberQuest.Core;
using NUnit.Framework;

namespace CyberQuest.Tests
{
    public class TelemetryTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            GameState.ResetForNewSession();
            SessionLog.Reset();
            PhysicalSecurityTelemetryLogger.ParticipantId = null;
            _dir = Path.Combine(Path.GetTempPath(), "cq_telemetry_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            PhysicalSecurityTelemetryLogger.ParticipantId = null;
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        private static GameState MixedRun()
        {
            var s = new GameState();
            s.emailOpened = true;
            s.qrScanned = true;
            s.login = LoginResult.Entered;
            s.usb = UsbAction.Reported;
            s.colleague = ColleagueResponse.RefusedOutright;
            s.intercom = IntercomResponse.BuzzedIn;
            s.mfa = MfaResponse.DeniedAndReported;
            s.completionOrder.AddRange(new[]
            {
                SituationId.EmailQr, SituationId.FakeLogin, SituationId.Usb, SituationId.Colleague, SituationId.Intercom,
                SituationId.Mfa,
            });
            return s;
        }

        [Test]
        public void BreachRate_IsCompromisesOverAttacksReached()
        {
            var s = MixedRun();
            Assert.AreEqual(4, PhysicalSecurityTelemetryLogger.AttacksReached(s));
            Assert.AreEqual(2, s.CompromiseSources);
            Assert.AreEqual(0.5f, PhysicalSecurityTelemetryLogger.BreachRate(s), 1e-5f);
            Assert.IsTrue(PhysicalSecurityTelemetryLogger.CredentialDisclosure(s));
        }

        [Test]
        public void BreachRate_ZeroWhenNothingReached()
        {
            Assert.AreEqual(0f, PhysicalSecurityTelemetryLogger.BreachRate(new GameState()));
            Assert.IsEmpty(PhysicalSecurityTelemetryLogger.BuildTrials(new GameState()));
        }

        [Test]
        public void Trials_ClassifyFalsePositiveAndTailgating()
        {
            var trials = PhysicalSecurityTelemetryLogger.BuildTrials(MixedRun());
            Assert.AreEqual(6, trials.Count);
            var colleague = trials.Find(t => t.situation == SituationId.Colleague);
            Assert.AreEqual("FP", colleague.classification);
            Assert.IsTrue(colleague.legitimate);
            Assert.IsFalse(colleague.isCompliant);
            var intercom = trials.Find(t => t.situation == SituationId.Intercom);
            Assert.AreEqual("FN", intercom.classification);
            Assert.IsTrue(intercom.tailgating);
            Assert.IsTrue(intercom.compromise);
            var usb = trials.Find(t => t.situation == SituationId.Usb);
            Assert.AreEqual("TP", usb.classification);
            Assert.IsTrue(usb.isCompliant);
            Assert.IsTrue(trials.Find(t => t.situation == SituationId.FakeLogin).credentialDisclosure);
        }

        [Test]
        public void Trials_IncludeSituationsFinalisedWithoutADecision()
        {
            var s = new GameState { usb = UsbAction.Ignored, workstation = WorkstationResult.Ignored };
            var trials = PhysicalSecurityTelemetryLogger.BuildTrials(s);
            Assert.AreEqual(2, trials.Count);
            Assert.IsFalse(trials[0].latency.HasValue);
            Assert.AreEqual("FN", trials.Find(t => t.situation == SituationId.Workstation).classification);
        }

        [Test]
        public void RecordedLatency_IsDecisionMinusRequest()
        {
            var s = new GameState { usb = UsbAction.Plugged };
            s.completionOrder.Add(SituationId.Usb);
            PhysicalSecurityTelemetryLogger.RecordDecision(SituationId.Usb, "plugged", 30f, 42.5f);
            var trial = PhysicalSecurityTelemetryLogger.BuildTrials(s)[0];
            Assert.AreEqual(12.5f, trial.latency.Value, 1e-4f);
            Assert.AreEqual("plugged", trial.action);
        }

        [Test]
        public void Escape_QuotesCommasAndQuotes()
        {
            Assert.AreEqual("plain", PhysicalSecurityTelemetryLogger.Escape("plain"));
            Assert.AreEqual("\"a, b\"", PhysicalSecurityTelemetryLogger.Escape("a, b"));
            Assert.AreEqual("\"say \"\"hi\"\"\"", PhysicalSecurityTelemetryLogger.Escape("say \"hi\""));
        }

        [Test]
        public void Write_CreatesThreeFilesAndNumbersParticipants()
        {
            SessionLog.Decision("Usb: plugged, then regretted it");
            var s = MixedRun();
            Assert.IsNotNull(PhysicalSecurityTelemetryLogger.Write(_dir, s, EndingType.Breach, 40));
            Assert.AreEqual("P01", PhysicalSecurityTelemetryLogger.ParticipantId);

            string[] trials = File.ReadAllLines(Path.Combine(_dir, PhysicalSecurityTelemetryLogger.TrialsFile));
            Assert.AreEqual(PhysicalSecurityTelemetryLogger.TrialsHeader, trials[0]);
            Assert.AreEqual(7, trials.Length);
            int columns = PhysicalSecurityTelemetryLogger.TrialsHeader.Split(',').Length;
            Assert.AreEqual(columns, trials[1].Split(',').Length);

            string[] summary = File.ReadAllLines(Path.Combine(_dir, PhysicalSecurityTelemetryLogger.SummaryFile));
            Assert.AreEqual(2, summary.Length);
            StringAssert.StartsWith("P01,", summary[1]);
            StringAssert.Contains(",Breach,40,", summary[1]);

            var audits = Directory.GetFiles(_dir, "audit_P01_*.csv");
            Assert.AreEqual(1, audits.Length);
            StringAssert.Contains("\"Usb: plugged, then regretted it\"", File.ReadAllText(audits[0]));

            // A new app launch (no participant set) continues the numbering.
            PhysicalSecurityTelemetryLogger.ParticipantId = null;
            PhysicalSecurityTelemetryLogger.Write(_dir, new GameState(), EndingType.FullContainment, 0);
            Assert.AreEqual("P02", PhysicalSecurityTelemetryLogger.ParticipantId);
            Assert.AreEqual(3, File.ReadAllLines(Path.Combine(_dir, PhysicalSecurityTelemetryLogger.SummaryFile)).Length);
        }
    }
}
