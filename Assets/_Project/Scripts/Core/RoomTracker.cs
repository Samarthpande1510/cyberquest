using UnityEngine;

namespace CyberQuest.Core
{
    /// <summary>
    /// Logs which room the player's head is in: entries, exits and dwell time per
    /// room go to SessionLog (CLAUDE.md 5.9). Research data only, never scored.
    /// </summary>
    public class RoomTracker : MonoBehaviour
    {
        private RoomZone[] _zones;
        private RoomZone _current;

        public string CurrentRoom => _current != null ? _current.RoomName : null;

        private void Start()
        {
            _zones = FindObjectsByType<RoomZone>(FindObjectsSortMode.None);
        }

        private void Update()
        {
            var cam = Camera.main;
            var director = GameDirector.Instance;
            if (cam == null || _zones == null || director == null || !director.Running || director.Finished)
            {
                return;
            }
            Tick(cam.transform.position, Time.deltaTime);
        }

        /// <summary>Public so tests can drive it without a camera.</summary>
        public void Tick(Vector3 head, float dt)
        {
            _zones ??= FindObjectsByType<RoomZone>(FindObjectsSortMode.None);
            RoomZone zone = null;
            foreach (var z in _zones)
            {
                if (z != null && z.Contains(head) && (zone == null || z.Priority > zone.Priority))
                {
                    zone = z;
                }
            }

            if (zone != _current)
            {
                if (_current != null)
                {
                    SessionLog.Add("room", "exit " + _current.RoomName);
                }
                _current = zone;
                if (_current != null)
                {
                    SessionLog.Add("room", "enter " + _current.RoomName);
                    SessionLog.AddRoomTime(_current.RoomName, 0f, true);
                }
            }
            else if (_current != null)
            {
                SessionLog.AddRoomTime(_current.RoomName, dt, false);
            }
        }
    }
}
