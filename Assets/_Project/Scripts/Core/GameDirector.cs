using System;
using UnityEngine;

namespace CyberQuest.Core
{
    /// <summary>
    /// Owns the game clock (5:45 PM to 6:30 PM over 15 real minutes by default),
    /// lets only one scheduled situation be active at a time, enforces a cooldown
    /// between situations and triggers the 6:30 lockout (CLAUDE.md 4.3).
    /// Situations decide for themselves when they're ready (SituationTrigger);
    /// the director only grants or refuses the floor.
    /// </summary>
    public class GameDirector : MonoBehaviour
    {
        public const float StartMinutes = 17 * 60 + 45;
        public const float EndMinutes = 18 * 60 + 30;

        [SerializeField] private float realMinutes = 15f;
        [SerializeField] private float cooldownSeconds = 45f;

        public static GameDirector Instance { get; private set; }

        /// <summary>Fired with a short player-facing line ("New chat message on your monitor").</summary>
        public static event Action<string> Notification;

        public event Action LockedOutEvent;
        public event Action StateChanged;

        public bool Running { get; private set; }
        public bool LockedOut { get; private set; }
        public bool Finished { get; private set; }
        public float Elapsed { get; private set; }
        public SituationTrigger Active { get; private set; }
        public float CooldownRemaining => Mathf.Max(0f, _cooldownUntil - Elapsed);
        public float CooldownSeconds => cooldownSeconds;

        public float GameMinutes => GameMinutesAt(Elapsed);

        public float GameMinutesAt(float elapsedSeconds) =>
            StartMinutes + elapsedSeconds / (realMinutes * 60f) * (EndMinutes - StartMinutes);

        public string ClockText => FormatTime(GameMinutes);

        private float _cooldownUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Notification = null;
            Instance = null;
        }

        private void Awake()
        {
            Instance = this;
            SessionLog.Clock = () => (Elapsed, ClockText);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Configure(float minutes, float cooldown)
        {
            realMinutes = Mathf.Max(0.1f, minutes);
            cooldownSeconds = Mathf.Max(0f, cooldown);
        }

        public void StartShift()
        {
            if (Running)
            {
                return;
            }
            if (Instance == null)
            {
                Instance = this; // EditMode tests add the component without Awake running
            }
            Running = true;
            SessionLog.Event("Shift started");
            StateChanged?.Invoke();
        }

        private void Update()
        {
            if (!Running || Finished)
            {
                return;
            }
            Elapsed += Time.deltaTime;
            if (!LockedOut && GameMinutes >= EndMinutes)
            {
                TriggerLockout();
            }
        }

        public bool CanBegin(SituationTrigger situation)
        {
            return Running && !LockedOut && !Finished && Active == null &&
                   (situation.IgnoresCooldown || Elapsed >= _cooldownUntil);
        }

        public bool TryBegin(SituationTrigger situation)
        {
            if (!CanBegin(situation))
            {
                return false;
            }
            Active = situation;
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>Debug/test path: takes the floor regardless of cooldown or other situations.</summary>
        public void ForceBegin(SituationTrigger situation)
        {
            Active = situation;
            StateChanged?.Invoke();
        }

        public void Release(SituationTrigger situation)
        {
            if (Active != situation)
            {
                return;
            }
            Active = null;
            _cooldownUntil = Elapsed + cooldownSeconds;
            StateChanged?.Invoke();
        }

        public void TriggerLockout()
        {
            if (LockedOut)
            {
                return;
            }
            LockedOut = true;
            SessionLog.Event("Building lockout at 6:30 PM");
            LockedOutEvent?.Invoke();
            StateChanged?.Invoke();
        }

        public void Finish()
        {
            Finished = true;
            GameState.Current.gameMinutesAtEnd = GameMinutes - StartMinutes;
            StateChanged?.Invoke();
        }

        public static void Notify(string message)
        {
            SessionLog.Event("Notify: " + message);
            Notification?.Invoke(message);
        }

        // --- debug / tests

        public void SkipSeconds(float seconds)
        {
            Elapsed += seconds;
        }

        public void ClearCooldown()
        {
            _cooldownUntil = Elapsed;
        }

        public static string FormatTime(float minutes)
        {
            int total = Mathf.FloorToInt(minutes);
            int h = total / 60 % 12;
            int m = total % 60;
            return $"{(h == 0 ? 12 : h)}:{m:00} PM";
        }
    }
}
