using TMPro;
using UnityEngine;

namespace CyberQuest.UI
{
    /// <summary>
    /// The player's cover task: drop the final numbers into the client report and
    /// send it (CLAUDE.md 3). Not scored - it gives the player something to do.
    /// </summary>
    [RequireComponent(typeof(MonitorScreen))]
    public class ReportApp : MonoBehaviour
    {
        private MonitorScreen _monitor;
        private UiPanel _view;
        private int _filled;
        private bool _sent;

        public bool Sent => _sent;

        private static readonly string[] Lines =
        {
            "Revenue (Q3)", "Operating costs", "Net margin",
        };

        private static readonly string[] Values =
        {
            "$1,284,000", "$911,500", "29.0 %",
        };

        private void Start()
        {
            _monitor = GetComponent<MonitorScreen>();
            Render();
        }

        public void FillNext()
        {
            if (_filled < Lines.Length)
            {
                _filled++;
                Render();
            }
        }

        public void Send()
        {
            if (_filled < Lines.Length || _sent)
            {
                return;
            }
            _sent = true;
            Core.SessionLog.Event("Client report sent");
            _monitor.SetObjective("Report sent. Leave before 6:30 - the exit is in the lobby.");
            Render();
        }

        private void Render()
        {
            if (_view != null)
            {
                _view.Root.SetActive(false);
                Destroy(_view.Root);
            }
            var size = MonitorScreen.ContentSize;
            _view = UiPanel.Create(_monitor.App("Report").Root.transform, "View", size, "Client report  -  Harbor Logistics Q3",
                UiTheme.Blue, MonitorScreen.ViewDistance);
            _view.Root.transform.localPosition = new Vector3(0f, 0f, -0.003f);

            float y = _view.ContentTop;
            for (int i = 0; i < Lines.Length; i++)
            {
                string value = i < _filled ? $"<color=#86EFAC>{Values[i]}</color>" : "<color=#94A3B8>[ missing ]</color>";
                _view.Text($"{Lines[i]}<pos=55%>{value}", new Rect(_view.Left, y - 0.05f, _view.Width, 0.05f), 0.19f,
                    TextAlignmentOptions.MidlineLeft);
                y -= 0.06f;
            }

            string status = _sent ? "<color=#86EFAC>Sent to client.</color>"
                : _filled < Lines.Length ? "Copy the final numbers in from the shared sheet." : "Ready to send.";
            _view.Body(status, y - 0.01f, 0.05f, 0.17f, color: UiTheme.MutedText);

            if (!_sent)
            {
                if (_filled < Lines.Length)
                {
                    _view.Button("Insert next number", new Vector2(0f, _view.Bottom + 0.035f), new Vector2(0.36f, 0.07f), FillNext);
                }
                else
                {
                    _view.Button("Send report", new Vector2(0f, _view.Bottom + 0.035f), new Vector2(0.36f, 0.07f), Send);
                }
            }
        }
    }
}
