using CyberQuest.Core;
using NUnit.Framework;
using UnityEngine;

namespace CyberQuest.Tests
{
    public class DirectorTests
    {
        private class DummySituation : SituationTrigger
        {
            public int Started;
            protected override void OnTriggered() => Started++;
            public void Finish() => Complete("done");
        }

        private GameObject _go;
        private GameDirector _director;

        [SetUp]
        public void SetUp()
        {
            GameState.ResetForNewSession();
            _go = new GameObject("Director");
            _director = _go.AddComponent<GameDirector>();
            _director.Configure(15f, 45f);
            _director.StartShift();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        private DummySituation Make(SituationId id, float earliest, float fallback, int completed)
        {
            var s = _go.AddComponent<DummySituation>();
            s.Configure(id, earliest, fallback, completed, null, 2f, true, false, 120f);
            return s;
        }

        [Test]
        public void Clock_RunsFrom545To630()
        {
            Assert.AreEqual("5:45 PM", _director.ClockText);
            _director.SkipSeconds(15f * 60f / 2f);
            Assert.AreEqual("6:07 PM", _director.ClockText);
            _director.SkipSeconds(15f * 60f / 2f);
            Assert.AreEqual("6:30 PM", _director.ClockText);
        }

        [Test]
        public void OnlyOneScheduledSituation_AtATime_ThenCooldown()
        {
            var a = Make(SituationId.Colleague, 0f, -1f, 0);
            var b = Make(SituationId.Intercom, 0f, -1f, 0);

            Assert.IsTrue(_director.TryBegin(a));
            Assert.IsFalse(_director.TryBegin(b), "second situation must wait while one is active");

            _director.Release(a);
            Assert.IsFalse(_director.TryBegin(b), "cooldown must block the next situation");
            Assert.AreEqual(45f, _director.CooldownRemaining, 0.01f);

            _director.SkipSeconds(45f);
            Assert.IsTrue(_director.TryBegin(b));
        }

        [Test]
        public void Conditions_FallbackOrCompletedCount()
        {
            var s = Make(SituationId.Colleague, 0f, 180f, 1);
            Assert.IsFalse(s.ConditionsMet(_director));
            GameState.Current.MarkCompleted(SituationId.EmailQr);
            Assert.IsTrue(s.ConditionsMet(_director), "one completed situation is enough");

            GameState.ResetForNewSession();
            Assert.IsFalse(s.ConditionsMet(_director));
            _director.SkipSeconds(180f);
            Assert.IsTrue(s.ConditionsMet(_director), "fallback time is enough");
        }

        [Test]
        public void Conditions_EarliestTimeGates()
        {
            var s = Make(SituationId.EmailQr, 10f, -1f, 0);
            Assert.IsFalse(s.ConditionsMet(_director));
            _director.SkipSeconds(10f);
            Assert.IsTrue(s.ConditionsMet(_director));
        }

        [Test]
        public void Complete_RecordsOrderAndFreesDirector()
        {
            var a = Make(SituationId.Usb, 0f, -1f, 0);
            a.ForceTrigger();
            Assert.AreEqual(1, a.Started);
            Assert.AreSame(a, _director.Active);
            a.Finish();
            Assert.IsNull(_director.Active);
            CollectionAssert.AreEqual(new[] { SituationId.Usb }, GameState.Current.completionOrder);
        }

        [Test]
        public void Lockout_BlocksNewSituations()
        {
            var a = Make(SituationId.Intercom, 0f, -1f, 0);
            _director.TriggerLockout();
            Assert.IsFalse(_director.TryBegin(a));
            Assert.IsTrue(_director.LockedOut);
        }
    }
}
