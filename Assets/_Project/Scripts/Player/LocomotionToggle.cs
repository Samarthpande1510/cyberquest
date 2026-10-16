using Oculus.Interaction.Locomotion;
using UnityEngine;

namespace CyberQuest.Player
{
    /// <summary>
    /// Switches the player between teleport (default) and continuous smooth-move
    /// locomotion. Only one mode is ever active, since both read the same
    /// thumbstick axis and would otherwise fight each other.
    /// </summary>
    public class LocomotionToggle : MonoBehaviour
    {
        [SerializeField] private SmoothLocomotion smoothLocomotion;
        [SerializeField] private Transform teleportInteractorsRoot;

        private TeleportInteractor[] _teleportInteractors;
        private bool _smoothMoveEnabled;

        public bool SmoothMoveEnabled => _smoothMoveEnabled;

        private TeleportInteractor[] TeleportInteractors =>
            _teleportInteractors ??= teleportInteractorsRoot != null
                ? teleportInteractorsRoot.GetComponentsInChildren<TeleportInteractor>(true)
                : GetComponentsInChildren<TeleportInteractor>(true);

        private void Awake()
        {
            SetSmoothMove(false);
        }

        public void SetSmoothMove(bool enabled)
        {
            _smoothMoveEnabled = enabled;

            if (smoothLocomotion != null)
            {
                smoothLocomotion.enabled = enabled;
            }

            foreach (var interactor in TeleportInteractors)
            {
                interactor.enabled = !enabled;
            }
        }
    }
}
