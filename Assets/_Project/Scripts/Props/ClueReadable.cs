using CyberQuest.Core;
using UnityEngine;

namespace CyberQuest.Props
{
    /// <summary>
    /// A clue the player can read by looking at it for 1.5 s from close enough
    /// (CLAUDE.md 5.9). Never highlighted or pointed out by UI. Sets the matching
    /// GameState flag the first time it's read.
    /// </summary>
    public class ClueReadable : MonoBehaviour
    {
        public enum Clue
        {
            MaintenanceNotice,
            PriyaDirectoryEntry,
            ItDirectoryEntry,
        }

        public const float GazeSeconds = 1.5f;

        [SerializeField] private Clue clue;
        [SerializeField] private float radius = 0.25f;
        [SerializeField] private float maxDistance = 3f;

        private float _gaze;

        public Clue Kind => clue;
        public bool HasBeenRead { get; private set; }

        public void Configure(Clue kind, float targetRadius, float distance)
        {
            clue = kind;
            radius = targetRadius;
            maxDistance = distance;
        }

        private void Update()
        {
            if (HasBeenRead)
            {
                return;
            }
            var cam = Camera.main;
            var director = GameDirector.Instance;
            if (cam == null || director == null || !director.Running)
            {
                return;
            }
            if (IsLookedAt(cam.transform))
            {
                _gaze += Time.deltaTime;
                if (_gaze >= GazeSeconds)
                {
                    Read();
                }
            }
            else
            {
                _gaze = Mathf.Max(0f, _gaze - Time.deltaTime);
            }
        }

        public bool IsLookedAt(Transform eye)
        {
            Vector3 to = transform.position - eye.position;
            float dist = to.magnitude;
            if (dist > maxDistance || dist < 0.05f)
            {
                return false;
            }
            float allowed = Mathf.Max(6f, Mathf.Atan2(radius, dist) * Mathf.Rad2Deg);
            return Vector3.Angle(eye.forward, to) <= allowed;
        }

        /// <summary>Public so tests and the debug window can mark it read.</summary>
        public void Read()
        {
            if (HasBeenRead)
            {
                return;
            }
            HasBeenRead = true;
            var s = GameState.Current;
            switch (clue)
            {
                case Clue.MaintenanceNotice:
                    s.maintenanceNoticeRead = true;
                    break;
                case Clue.PriyaDirectoryEntry:
                    s.colleagueDirectoryChecked = true;
                    break;
            }
            SessionLog.Event("Clue read: " + clue);
        }
    }
}
