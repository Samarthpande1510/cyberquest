using CyberQuest.Core;
using TMPro;
using UnityEngine;

namespace CyberQuest.UI
{
    /// <summary>
    /// Lazy-follow strip below the eye line: game clock, the current task and the
    /// latest notification ("Your phone is buzzing"). It states what's happening,
    /// never which clue to look at (CLAUDE.md 5.9).
    /// </summary>
    public class ShiftHud : MonoBehaviour
    {
        // Close and small rather than far and big: same angular size, but it doesn't sink into desks.
        private const float Distance = 0.55f;
        private const float Drop = 0.32f;
        private const float Scale = 0.5f;
        private const float NoticeSeconds = 9f;

        public static ShiftHud Instance { get; private set; }

        private UiPanel _panel;
        private TextMeshPro _clock;
        private TextMeshPro _task;
        private TextMeshPro _notice;
        private float _noticeUntil;
        private bool _snapped;
        private string _objective = "Press START SHIFT on the welcome screen.";

        public string TaskText => _task != null ? _task.text : "";
        public string NoticeText => _notice != null ? _notice.text : "";

        private void Awake()
        {
            Instance = this;
            _panel = UiPanel.Create(transform, "Hud", new Vector2(0.8f, 0.2f), null, UiTheme.Teal, Distance, Scale);
            _clock = _panel.Text("", new Rect(_panel.Left, 0.03f, 0.22f, 0.06f), 0.24f, TextAlignmentOptions.MidlineLeft,
                new Color(0.55f, 0.85f, 1f), bold: true);
            _task = _panel.Text("", new Rect(_panel.Left + 0.24f, 0.01f, _panel.Width - 0.24f, 0.08f), 0.17f,
                TextAlignmentOptions.MidlineLeft);
            _notice = _panel.Text("", new Rect(_panel.Left, -0.09f, _panel.Width, 0.08f), 0.18f, TextAlignmentOptions.MidlineLeft,
                new Color(1f, 0.85f, 0.35f), bold: true);
            GameDirector.Notification += OnNotify;
        }

        private void OnDestroy()
        {
            GameDirector.Notification -= OnNotify;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SetObjective(string text) => _objective = text;

        private void OnNotify(string message)
        {
            _notice.text = message;
            _noticeUntil = Time.time + NoticeSeconds;
        }

        private void LateUpdate()
        {
            var director = GameDirector.Instance;
            // Only pops up while there's a fresh notification. The monitor and wall clocks already
            // show the time and task, and a permanent strip would sit over whatever you look down at.
            bool show = director != null && director.Running && Time.time <= _noticeUntil;
            if (_panel.Root.activeSelf != show)
            {
                _panel.Root.SetActive(show);
            }
            _clock.text = director != null ? director.ClockText : "5:45 PM";
            _task.text = director != null && director.LockedOut ? "6:30 - the building has locked out." : _objective;
            if (Time.time > _noticeUntil)
            {
                _notice.text = "";
            }

            var viewer = FaceViewer.Viewer;
            if (viewer != null)
            {
                Follow(viewer, !_snapped);
                _snapped = true;
            }
        }

        /// <summary>Snaps in front of an arbitrary view (used for test screenshots).</summary>
        public void SnapTo(Transform view) => Follow(view, true);

        private void Follow(Transform view, bool snap)
        {
            Vector3 fwd = view.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude < 0.0001f ? Vector3.forward : fwd.normalized;
            Vector3 targetPos = view.position + fwd * Distance + Vector3.down * Drop;
            var targetRot = Quaternion.LookRotation(fwd) * Quaternion.Euler(22f, 0f, 0f);
            float k = snap ? 1f : 1f - Mathf.Exp(-2.5f * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, k);
        }
    }
}
