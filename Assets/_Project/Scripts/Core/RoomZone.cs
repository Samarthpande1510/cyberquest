using UnityEngine;

namespace CyberQuest.Core
{
    /// <summary>Axis-aligned room volume used by RoomTracker (centre = transform position).</summary>
    public class RoomZone : MonoBehaviour
    {
        [SerializeField] private string roomName;
        [SerializeField] private Vector3 size = new Vector3(4f, 3f, 4f);
        [Tooltip("Higher wins where zones overlap (e.g. ItRoomDoor inside Corridor).")]
        [SerializeField] private int priority;

        public string RoomName => roomName;
        public int Priority => priority;

        public void Configure(string room, Vector3 volume, int zonePriority = 0)
        {
            roomName = room;
            size = volume;
            priority = zonePriority;
        }

        public bool Contains(Vector3 worldPoint)
        {
            return new Bounds(transform.position, size).Contains(worldPoint);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
