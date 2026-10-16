using CyberQuest.Core;
using UnityEngine;

namespace CyberQuest.UI
{
    /// <summary>Start-of-session briefing: story, how to play, START SHIFT.</summary>
    public class BriefingPanel : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(1.0f, 0.68f);

        private UiPanel _panel;

        private void Awake()
        {
            _panel = UiPanel.Create(transform, "Panel", Size, "NEXORA  |  NIGHT SHIFT BRIEFING", UiTheme.Blue);
            _panel.Body(
                "You're the last one left in the office tonight. Attackers will try to trick you - " +
                "by email, through your tablet, with a USB stick and in person.\n\n" +
                "Stop the threats, but don't treat real colleagues like criminals.",
                _panel.ContentTop, 0.27f, 0.21f);
            _panel.Body(
                "<b>HOW TO PLAY</b>\n" +
                "Point at a button and pull the trigger - or just poke it.\n" +
                "Push the thumbstick forward to teleport.",
                _panel.ContentTop - 0.29f, 0.17f, 0.18f, color: UiTheme.MutedText);
            _panel.Button("START SHIFT", new Vector2(0f, _panel.Bottom + 0.045f), new Vector2(0.4f, 0.09f), Begin);
        }

        private void Begin()
        {
            GameFlow.StartShift();
            _panel.Root.SetActive(false);
        }
    }
}
