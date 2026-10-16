using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CyberQuest.Core
{
    public enum Verdict
    {
        Good,
        Okay,
        Risky,
    }

    public readonly struct Feedback
    {
        public readonly Verdict Verdict;
        public readonly string Detail;

        public Feedback(Verdict verdict, string detail)
        {
            Verdict = verdict;
            Detail = detail;
        }
    }

    /// <summary>
    /// Paces the session so only one event asks the player for a decision at a
    /// time. Events still own their own trigger rules (GameState); they just ask
    /// GameFlow for the floor before showing anything, and hand it back with a
    /// piece of feedback once the player has chosen.
    /// </summary>
    public static class GameFlow
    {
        public static bool Started { get; private set; }
        public static bool Finished { get; private set; }
        public static string ActiveEvent { get; private set; }
        public static Transform Focus { get; private set; }

        private static float _idleSince;

        public static event Action Changed;
        public static event Action<Feedback> FeedbackRaised;

        public static bool IsFree => Started && !Finished && ActiveEvent == null;

        /// <summary>Seconds since the last event finished (0 while one is running).</summary>
        public static float IdleSeconds => IsFree ? Time.time - _idleSince : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Started = false;
            Finished = false;
            ActiveEvent = null;
            Focus = null;
            _idleSince = 0f;
            Changed = null;
            FeedbackRaised = null;
        }

        public static void StartShift()
        {
            if (Started)
            {
                return;
            }
            Started = true;
            _idleSince = Time.time;
            Changed?.Invoke();
        }

        /// <summary>Claims the floor for an event once nothing else is running and the
        /// player has had <paramref name="breather"/> seconds since the last decision.</summary>
        public static bool TryBegin(string id, Transform focus, float breather = 0f)
        {
            if (!IsFree || IdleSeconds < breather)
            {
                return false;
            }
            ActiveEvent = id;
            Focus = focus;
            Changed?.Invoke();
            return true;
        }

        public static void End(string id, Feedback feedback)
        {
            if (ActiveEvent == id)
            {
                ActiveEvent = null;
                Focus = null;
            }
            _idleSince = Time.time;
            FeedbackRaised?.Invoke(feedback);
            Changed?.Invoke();
        }

        public static void Finish()
        {
            Finished = true;
            ActiveEvent = null;
            Focus = null;
            Changed?.Invoke();
        }

        /// <summary>Fresh session for the next player: clears all state and reloads the scene.</summary>
        public static void Restart()
        {
            ResetStatics();
            GameState.ResetForNewSession();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
