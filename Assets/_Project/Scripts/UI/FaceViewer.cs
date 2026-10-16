using UnityEngine;

namespace CyberQuest.UI
{
    /// <summary>
    /// Keeps a panel (or an NPC) turned toward the player's head around the
    /// vertical axis, so dialogue stays readable after the player teleports.
    /// Panels use forward = away from the viewer; NPCs use forward = toward them.
    /// </summary>
    public class FaceViewer : MonoBehaviour
    {
        [SerializeField] private bool forwardTowardViewer;
        [SerializeField] private float turnSpeed = 6f;

        public static Transform Viewer => Camera.main != null ? Camera.main.transform : null;

        private void LateUpdate()
        {
            var viewer = Viewer;
            if (viewer == null)
            {
                return;
            }
            Vector3 dir = forwardTowardViewer ? viewer.position - transform.position : transform.position - viewer.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
            {
                return;
            }
            var target = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }

        /// <summary>Moves <paramref name="t"/> in front of the viewer at a comfortable reading distance.</summary>
        public static void PlaceInFrontOfViewer(Transform t, float distance, float dropBelowEyes)
        {
            var viewer = Viewer;
            if (viewer == null)
            {
                return;
            }
            Vector3 fwd = viewer.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude < 0.0001f ? Vector3.forward : fwd.normalized;
            t.position = viewer.position + fwd * distance + Vector3.down * dropBelowEyes;
            t.rotation = Quaternion.LookRotation(fwd);
        }
    }
}
