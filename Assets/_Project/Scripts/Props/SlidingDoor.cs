using CyberQuest.UI;
using UnityEngine;

namespace CyberQuest.Props
{
    /// <summary>Door leaf that slides sideways when opened; its collider moves with it.</summary>
    public class SlidingDoor : MonoBehaviour
    {
        [SerializeField] private Vector3 openOffset = new Vector3(1f, 0f, 0f);
        [SerializeField] private float speed = 1.2f;

        private Vector3 _closed;
        private bool _open;

        public bool IsOpen => _open;

        private void Awake()
        {
            _closed = transform.localPosition;
        }

        public void Configure(Vector3 offset)
        {
            openOffset = offset;
        }

        public void Open()
        {
            if (_open)
            {
                return;
            }
            _open = true;
            UiTheme.Play(t => t.unlock, transform.position, 0.9f);
        }

        public void Close()
        {
            _open = false;
        }

        private void Update()
        {
            Vector3 target = _open ? _closed + openOffset : _closed;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, speed * Time.deltaTime);
        }
    }
}
