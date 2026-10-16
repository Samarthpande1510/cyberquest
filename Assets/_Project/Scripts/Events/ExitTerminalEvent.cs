using CyberQuest.Core;
using CyberQuest.UI;
using UnityEngine;

namespace CyberQuest.Events
{
    /// <summary>
    /// Exit security terminal. Calls EndingResolver.Resolve and
    /// ScoreCalculator.Compute exactly once (CLAUDE.md section 2.3), shows the
    /// results screen, and unlocks the exit door only after that resolution.
    /// </summary>
    public class ExitTerminalEvent : MonoBehaviour
    {
        public static readonly Vector2 ScreenSize = new Vector2(0.56f, 0.34f);

        [SerializeField] private Transform buttonAnchor;
        [SerializeField] private ResultsScreen resultsScreen;
        [SerializeField] private GameObject exitDoorBlocker;
        [SerializeField] private GameObject exitSignLocked;
        [SerializeField] private GameObject exitSignOpen;

        private bool _resolved;

        private void Awake()
        {
            var panel = UiPanel.Create(buttonAnchor != null ? buttonAnchor : transform, "Screen", ScreenSize,
                "EXIT SECURITY", UiTheme.Teal);
            panel.Body("Done for the night? Clock out to end your shift and see your report.",
                panel.ContentTop, 0.1f, 0.18f);
            panel.Button("CLOCK OUT", new Vector2(0f, panel.Bottom + 0.045f), new Vector2(0.32f, 0.09f), ClockOut);
            if (exitSignOpen != null)
            {
                exitSignOpen.SetActive(false);
            }
        }

        private void ClockOut()
        {
            if (_resolved)
            {
                return;
            }
            _resolved = true;

            var ending = EndingResolver.Resolve(GameState.Current);
            int score = ScoreCalculator.Compute(GameState.Current);
            GameState.Current.score = score;
            GameFlow.Finish();

            if (resultsScreen != null)
            {
                resultsScreen.Show(ending, score);
            }

            if (exitDoorBlocker != null)
            {
                exitDoorBlocker.SetActive(false);
            }
            if (exitSignLocked != null)
            {
                exitSignLocked.SetActive(false);
            }
            if (exitSignOpen != null)
            {
                exitSignOpen.SetActive(true);
            }

            var theme = UiTheme.Instance;
            var clip = ending == EndingResolver.EndingType.FullContainment ? theme?.good : theme?.okay;
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, transform.position + Vector3.up, 0.9f);
            }
        }
    }
}
