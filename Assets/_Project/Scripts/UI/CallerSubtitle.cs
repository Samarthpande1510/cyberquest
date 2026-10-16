using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace CyberQuest.UI
{
    /// <summary>
    /// Timed subtitle box with a speaker label and a short beep per line. People
    /// in CyberQuest only exist as text like this (CLAUDE.md 2, rule 1).
    /// </summary>
    public class CallerSubtitle : MonoBehaviour
    {
        private UiPanel _panel;
        private TextMeshPro _line;
        private Coroutine _routine;

        public bool Playing => _routine != null;
        public string CurrentText => _line != null ? _line.text : "";

        public static CallerSubtitle Create(Transform parent, string name, Vector2 size, float viewDistance, float fontSize = 0.21f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sub = go.AddComponent<CallerSubtitle>();
            sub._panel = UiPanel.Create(go.transform, "Panel", size, "Caller", UiTheme.Slate, viewDistance);
            sub._line = sub._panel.Body("", sub._panel.ContentTop, size.y - UiPanel.HeaderHeight - 2f * UiPanel.Pad, fontSize);
            sub._panel.Root.SetActive(false);
            return sub;
        }

        public void Play(string speaker, string[] lines, float secondsPerLine, Action onDone = null, bool keepLastLine = true)
        {
            Stop();
            _routine = StartCoroutine(Run(speaker, lines, secondsPerLine, onDone, keepLastLine));
        }

        /// <summary>Shows one line immediately (no timing).</summary>
        public void Say(string speaker, string line)
        {
            Stop();
            _panel.Title.text = speaker;
            _line.text = line;
            _panel.Root.SetActive(true);
            UiTheme.Play(t => t.beep, transform.position, 0.5f);
        }

        public void Stop()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }

        public void Hide()
        {
            Stop();
            _panel.Root.SetActive(false);
        }

        private IEnumerator Run(string speaker, string[] lines, float secondsPerLine, Action onDone, bool keepLastLine)
        {
            _panel.Title.text = speaker;
            _panel.Root.SetActive(true);
            foreach (var l in lines)
            {
                _line.text = l;
                UiTheme.Play(t => t.beep, transform.position, 0.5f);
                // Longer lines stay up longer.
                yield return new WaitForSeconds(Mathf.Max(secondsPerLine, l.Length * 0.06f));
            }
            if (!keepLastLine)
            {
                _panel.Root.SetActive(false);
            }
            _routine = null;
            onDone?.Invoke();
        }
    }
}
