using UnityEngine;

namespace CyberQuest.Player
{
    /// <summary>
    /// Stops the player walking through walls, closed doors and furniture. A headset
    /// can't physically stop someone walking forward, so when the tracked head
    /// moves into a wall the whole rig is pushed back by the same amount and the
    /// view stays on the near side. Large single-frame jumps are teleports (the
    /// teleport arc is already blocked by walls) and are let through.
    /// </summary>
    public class HeadBlocker : MonoBehaviour
    {
        private const float Radius = 0.15f;
        private const float KneeHeight = 0.35f;
        private const float TeleportJump = 0.6f;

        [SerializeField] private Transform head;
        [SerializeField] private LayerMask walls;

        private Vector3 _last;
        private bool _hasLast;

        public void Configure(Transform headTransform, LayerMask wallMask)
        {
            head = headTransform;
            walls = wallMask;
        }

        private void LateUpdate()
        {
            if (head == null)
            {
                return;
            }
            Vector3 p = head.position;
            if (!_hasLast)
            {
                _last = p;
                _hasLast = true;
                return;
            }

            Vector3 move = p - _last;
            move.y = 0f;
            float dist = move.magnitude;
            // A body-height capsule (knees to head) under the head, so desks block as well as walls.
            Vector3 low = new Vector3(_last.x, transform.position.y + KneeHeight, _last.z);
            if (dist > 0.0001f && dist < TeleportJump && _last.y > low.y &&
                Physics.CapsuleCast(low, _last, Radius, move / dist, out var hit, dist, walls, QueryTriggerInteraction.Ignore))
            {
                Vector3 allowed = _last + move / dist * Mathf.Max(0f, hit.distance - 0.01f);
                allowed.y = p.y;
                transform.position += allowed - p;
                p = allowed;
            }
            _last = p;
        }
    }
}
