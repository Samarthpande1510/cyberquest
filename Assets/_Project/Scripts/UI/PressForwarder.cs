using Oculus.Interaction;
using UnityEngine;

namespace CyberQuest.UI
{
    /// <summary>
    /// Lets a PokeButton3D also be pressed (and highlighted) with a controller or
    /// hand ray: forwards this object's pointable events to the parent button.
    /// </summary>
    [RequireComponent(typeof(PointableUnityEventWrapper))]
    public class PressForwarder : MonoBehaviour
    {
        private void Awake()
        {
            var button = GetComponentInParent<PokeButton3D>(true);
            if (button == null)
            {
                return;
            }
            var wrapper = GetComponent<PointableUnityEventWrapper>();
            wrapper.WhenSelect.AddListener(_ => button.Press());
            wrapper.WhenHover.AddListener(_ => button.Hover(true));
            wrapper.WhenUnhover.AddListener(_ => button.Hover(false));
        }
    }
}
