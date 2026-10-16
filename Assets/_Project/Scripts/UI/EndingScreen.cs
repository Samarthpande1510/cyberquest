using System;
using System.Collections.Generic;
using CyberQuest.Core;
using TMPro;
using UnityEngine;

namespace CyberQuest.UI
{
    /// <summary>
    /// End-of-session screen (CLAUDE.md 5.8 / 6 / 7): ending tier, per-category
    /// score breakdown, then the per-situation debrief, two items per page.
    /// </summary>
    public class EndingScreen : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(1.2f, 0.84f);
        private const float ViewDistance = 1.2f;

        public event Action PlayAgain;

        private UiPanel _panel;
        private EndingResult _result;
        private ScoreBreakdown _score;
        private int _page;
        private readonly List<GameObject> _pageObjects = new List<GameObject>();

        public int Page => _page;
        public int PageCount => _result == null ? 0 : 1 + Mathf.CeilToInt(_result.Debrief.Count / 2f);
        public bool Visible => _panel != null && _panel.Root.activeSelf;

        private void Awake()
        {
            _panel = UiPanel.Create(transform, "Panel", Size, "SHIFT REPORT", UiTheme.Blue, ViewDistance);
            _panel.Root.SetActive(false);
        }

        public void Show(EndingResult result, ScoreBreakdown score)
        {
            _result = result;
            _score = score;
            _panel.SetAccent(result.Ending switch
            {
                EndingType.FullContainment => UiTheme.Green,
                EndingType.Breach => UiTheme.Red,
                _ => UiTheme.Amber,
            });
            _panel.Root.SetActive(true);
            ShowPage(0);
        }

        public void Next() => ShowPage(Mathf.Min(_page + 1, PageCount - 1));

        public void Back() => ShowPage(Mathf.Max(_page - 1, 0));

        public void ChoosePlayAgain() => PlayAgain?.Invoke();

        private void ShowPage(int page)
        {
            _page = page;
            foreach (var go in _pageObjects)
            {
                go.SetActive(false);
                Destroy(go);
            }
            _pageObjects.Clear();

            if (page == 0)
            {
                _panel.Title.text = "ENDING:  " + TitleFor(_result.Ending);
                Track(_panel.Body(SummaryFor(_result.Ending), _panel.ContentTop, 0.1f, 0.2f));
                Track(_panel.Text($"Score  {_score.Total} / 100", new Rect(_panel.Left, _panel.ContentTop - 0.2f, _panel.Width, 0.09f),
                    0.42f, TextAlignmentOptions.MidlineLeft, bold: true));
                string lines =
                    Line("Phishing (email, QR, login)", _score.Phishing) +
                    Line("Physical security (USB, workstation)", _score.Physical) +
                    Line("Social calibration (colleague)", _score.Social) +
                    Line("Impersonation (intercom)", _score.Impersonation) +
                    Line("MFA push", _score.Mfa);
                Track(_panel.Body(lines, _panel.ContentTop - 0.22f, 0.3f, 0.19f));
            }
            else
            {
                _panel.Title.text = $"DEBRIEF  ({page} of {PageCount - 1})";
                float y = _panel.ContentTop;
                for (int i = (page - 1) * 2; i < _result.Debrief.Count && i < page * 2; i++)
                {
                    var d = _result.Debrief[i];
                    string color = d.Grade == 2 ? "#4ADE80" : d.Grade == 1 ? "#FBBF24" : "#F87171";
                    Track(_panel.Body(
                        $"<color={color}><b>{d.Situation}</b></color>\n" +
                        $"<b>What you did:</b> {d.WhatHappened}\n" +
                        $"<b>Why it mattered:</b> {d.WhyItMattered}\n" +
                        $"<b>Next time:</b> {d.NextTime}",
                        y, 0.27f, 0.165f));
                    y -= 0.28f;
                }
            }

            float by = _panel.Bottom + 0.04f;
            var size = new Vector2(0.26f, 0.08f);
            if (page > 0)
            {
                Track(_panel.Button("< Back", new Vector2(_panel.Left + 0.13f, by), size, Back).gameObject);
            }
            if (page < PageCount - 1)
            {
                Track(_panel.Button(page == 0 ? "See debrief >" : "Next >", new Vector2(-_panel.Left - 0.13f, by), size, Next).gameObject);
            }
            Track(_panel.Button("Play again", new Vector2(0f, by), size, ChoosePlayAgain).gameObject);
        }

        private void Track(TextMeshPro t) => _pageObjects.Add(t.gameObject);

        private void Track(GameObject go) => _pageObjects.Add(go);

        private static string Line(string name, float? value)
        {
            string v = value.HasValue
                ? $"<color={(value.Value >= 0.99f ? "#4ADE80" : value.Value >= 0.5f ? "#FBBF24" : "#F87171")}>{Mathf.RoundToInt(value.Value * 100f)}%</color>"
                : "<color=#94A3B8>not reached</color>";
            return $"{name}<pos=72%>{v}\n";
        }

        public static string TitleFor(EndingType ending) => ending switch
        {
            EndingType.Breach => "BREACH",
            EndingType.ContainedButExposed => "CONTAINED BUT EXPOSED",
            EndingType.SocialMiscalibration => "SOCIAL MISCALIBRATION",
            _ => "FULL CONTAINMENT",
        };

        private static string SummaryFor(EndingType ending) => ending switch
        {
            EndingType.Breach => "The attacker got into NEXORA's systems tonight. See the debrief for how.",
            EndingType.ContainedButExposed => "The final attack was stopped - but one earlier mistake exposed your credentials.",
            EndingType.SocialMiscalibration => "Nothing got through, but a genuine colleague was turned away. Caution isn't free.",
            _ => "Every attack was contained, and you still helped the colleague who really needed it.",
        };
    }
}
