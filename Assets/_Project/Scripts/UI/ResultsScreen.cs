using CyberQuest.Core;
using TMPro;
using UnityEngine;

namespace CyberQuest.UI
{
    /// <summary>
    /// Debrief shown once at the exit terminal (CLAUDE.md section 2.3 endings,
    /// section 4 scoring) with a per-threat breakdown and Play Again. Purely a
    /// display - EndingResolver and ScoreCalculator compute the values it shows.
    /// </summary>
    public class ResultsScreen : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(1.15f, 0.86f);

        private UiPanel _panel;
        private TextMeshPro _score;
        private TextMeshPro _debrief;
        private TextMeshPro _breakdown;

        private void Awake()
        {
            _panel = UiPanel.Create(transform, "Panel", Size, "SHIFT REPORT", UiTheme.Blue);
            float top = _panel.ContentTop;
            _score = _panel.Text("", new Rect(_panel.Left, top - 0.12f, _panel.Width, 0.12f), 0.5f,
                TextAlignmentOptions.Center, bold: true);
            _debrief = _panel.Body("", top - 0.13f, 0.14f, 0.2f, TextAlignmentOptions.Top);
            _breakdown = _panel.Body("", top - 0.29f, 0.3f, 0.2f);
            _panel.Button("PLAY AGAIN", new Vector2(0f, _panel.Bottom + 0.045f), new Vector2(0.4f, 0.09f), GameFlow.Restart);
            _panel.Root.SetActive(false);
        }

        public void Show(EndingResolver.EndingType ending, int score)
        {
            Color accent = ending switch
            {
                EndingResolver.EndingType.FullContainment => UiTheme.Green,
                EndingResolver.EndingType.Breach => UiTheme.Red,
                _ => UiTheme.Amber,
            };
            _panel.SetAccent(accent);
            _panel.Title.text = TitleFor(ending);
            _score.text = $"{score}<size=60%> / 100   {Rank(score)}</size>";
            _debrief.text = DebriefFor(ending);
            _breakdown.text = Breakdown(GameState.Current);
            _panel.Root.SetActive(true);
        }

        private static string Rank(int score)
        {
            if (score >= 90) return "SECURITY CHAMPION";
            if (score >= 70) return "SHARP EYE";
            if (score >= 45) return "LEARNING THE ROPES";
            return "EASY TARGET";
        }

        private static string Breakdown(GameState s)
        {
            return
                Line("Phishing email", s.loginResult == "entered" ? 0 : s.emailAction == "forwarded" || s.loginResult == "reported" ? 2 : s.emailAction == "not-reached" ? -1 : 1) +
                Line("MFA push request", s.mfaResponse == "denied" ? 2 : s.mfaResponse == "approved" ? 0 : -1) +
                Line("Found USB drive", s.usbAction == "reported" ? 2 : s.usbAction == "plugged" ? 0 : s.usbAction == "ignored" ? 1 : -1) +
                Line("Priya at the door", s.priyaResponse == "helped-properly" ? 2 : s.priyaResponse == "let-in-unverified" ? 0 : s.priyaResponse == "refused" ? 1 : -1) +
                Line("'IT' visitor", s.imposterResponse == "verified" || s.imposterResponse == "delayed-then-verified" ? 2 : s.imposterResponse == "complied" ? 0 : s.imposterResponse == "asked-own-login" ? 1 : -1);
        }

        private static string Line(string name, int grade)
        {
            string tag = grade switch
            {
                2 => "<color=#4ADE80>GOOD</color>",
                1 => "<color=#FBBF24>OKAY</color>",
                0 => "<color=#F87171>RISKY</color>",
                _ => "<color=#94A3B8>NOT REACHED</color>",
            };
            return $"{name}<pos=62%>{tag}\n";
        }

        private static string TitleFor(EndingResolver.EndingType ending)
        {
            return ending switch
            {
                EndingResolver.EndingType.Breach => "ENDING: BREACH",
                EndingResolver.EndingType.ContainedButExposed => "ENDING: CONTAINED BUT EXPOSED",
                EndingResolver.EndingType.SocialMiscalibration => "ENDING: SOCIAL MISCALIBRATION",
                EndingResolver.EndingType.FullContainment => "ENDING: FULL CONTAINMENT",
                _ => "SESSION COMPLETE",
            };
        }

        private static string DebriefFor(EndingResolver.EndingType ending)
        {
            return ending switch
            {
                EndingResolver.EndingType.Breach =>
                    "Your credentials were compromised and the follow-up MFA prompt was approved - the attacker got in.",
                EndingResolver.EndingType.ContainedButExposed =>
                    "The final attack was stopped, but an earlier mistake left your credentials exposed.",
                EndingResolver.EndingType.SocialMiscalibration =>
                    "No threat got through - but a real colleague was turned away. Caution isn't free either.",
                EndingResolver.EndingType.FullContainment =>
                    "Every threat was spotted and handled, and you still helped a real colleague. Well done!",
                _ => "",
            };
        }
    }
}
