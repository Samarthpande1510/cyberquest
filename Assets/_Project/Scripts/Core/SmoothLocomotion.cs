using UnityEngine;

namespace CyberQuest.Core
{
    /// <summary>
    /// Continuous ("smooth") movement alternative to teleport, driven by the left
    /// thumbstick (translation, relative to head facing) and right thumbstick
    /// (discrete snap-turn). Disabled by default — see LocomotionToggle.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class SmoothLocomotion : MonoBehaviour
    {
        [SerializeField] private Transform headTransform;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float snapTurnDegrees = 45f;
        [SerializeField] private float snapTurnDeadzone = 0.6f;
        [SerializeField] private float snapTurnCooldown = 0.3f;

        private CharacterController _characterController;
        private float _snapTurnTimer;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (!enabled || headTransform == null)
            {
                return;
            }

            Move();
            SnapTurn();
        }

        private void Move()
        {
            Vector2 input = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
            if (input.sqrMagnitude < 0.01f)
            {
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(headTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(headTransform.right, Vector3.up).normalized;
            Vector3 move = (forward * input.y + right * input.x) * moveSpeed * Time.deltaTime;

            _characterController.Move(move + Physics.gravity * Time.deltaTime);
        }

        private void SnapTurn()
        {
            _snapTurnTimer -= Time.deltaTime;

            Vector2 input = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
            if (_snapTurnTimer > 0f || Mathf.Abs(input.x) < snapTurnDeadzone)
            {
                return;
            }

            float direction = Mathf.Sign(input.x);
            transform.RotateAround(headTransform.position, Vector3.up, direction * snapTurnDegrees);
            _snapTurnTimer = snapTurnCooldown;
        }
    }
}
