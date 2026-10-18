using System;
using UnityEngine;

namespace CyberQuest.UI
{
    /// <summary>Start-of-session screen: premise, company policies, controls and START SHIFT.</summary>
    public class WelcomePanel : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(1.0f, 0.96f);

        public const string PoliciesText =
            "<b>COMPANY POLICIES</b>  <size=85%>(also on the board by the corridor doorway)</size>\n" +
            "<b>Staff directory:</b> names, departments and extensions are on the directory board in the Lobby, beside the main door.\n" +
            "<b>Lost & found:</b> found devices go in the FOUND DEVICES box in the corridor, outside the IT room.";

        public event Action StartPressed;

        private UiPanel _panel;

        private void Awake()
        {
            _panel = UiPanel.Create(transform, "Panel", Size, "CYBERQUEST  -  The Compromised Office", UiTheme.Blue, 0.9f);
            _panel.Body(
                "You're new at NEXORA and the last one in the office. It's 5:45 PM. " +
                "Finish your manager's client report and leave before the building locks out at 6:30.\n\n" +
                "Keep your eyes open. Not everything tonight is what it seems.",
                _panel.ContentTop, 0.2f, 0.2f);
            _panel.Body(PoliciesText, _panel.ContentTop - 0.2f, 0.32f, 0.18f);
            _panel.Body(
                "<b>CONTROLS</b>\n" +
                "Point and pull the trigger to press buttons (or poke them).\n" +
                "Grip to grab things. Push the thumbstick forward to teleport.",
                _panel.ContentTop - 0.545f, 0.16f, 0.16f, color: UiTheme.MutedText);
            _panel.Button("START SHIFT", new Vector2(0f, _panel.Bottom + 0.045f), new Vector2(0.4f, 0.09f), Begin);
        }

        public void Begin()
        {
            if (!_panel.Root.activeSelf)
            {
                return;
            }
            _panel.Root.SetActive(false);
            StartPressed?.Invoke();
        }
    }
}
