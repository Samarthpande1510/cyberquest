using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CyberQuest.UI
{
    /// <summary>
    /// The desk workstation: objective/clock bar, app tabs (Report, Mail, Chat),
    /// an app content area, chat-ping toasts and the overlay windows used for
    /// "silent compromise" moments (process flash, install bar, remote session).
    /// </summary>
    public class MonitorScreen : MonoBehaviour
    {
        public static readonly Vector2 ScreenSize = new Vector2(0.96f, 0.58f);
        public static readonly Vector2 ContentSize = new Vector2(0.77f, 0.49f);
        public static readonly Vector2 ContentCenter = new Vector2(0.085f, -0.03f);
        public const float ViewDistance = 0.85f;

        public event Action<string> AppOpened;

        private UiPanel _base;
        private TextMeshPro _objective;
        private TextMeshPro _clock;
        private readonly Dictionary<string, UiPanel> _apps = new Dictionary<string, UiPanel>();
        private readonly Dictionary<string, PokeButton3D> _tabs = new Dictionary<string, PokeButton3D>();
        private readonly Dictionary<string, int> _badges = new Dictionary<string, int>();
        private ToastNotification _toast;
        private UiPanel _overlay;
        private TextMeshPro _overlayText;
        private Renderer _progressFill;
        private bool _built;

        public string CurrentApp { get; private set; }
        public string ObjectiveText => _objective != null ? _objective.text : "";
        public ToastNotification Toast => _toast;
        public bool OverlayVisible => _overlay != null && _overlay.Root.activeSelf;

        private void Awake()
        {
            Build();
        }

        public void Build()
        {
            if (_built)
            {
                return;
            }
            _built = true;

            _base = UiPanel.Create(transform, "Workstation", ScreenSize, null, UiTheme.Slate, ViewDistance);
            float top = ScreenSize.y / 2f;
            _base.Quad("TopBar", new Vector2(0f, top - 0.03f), new Vector2(ScreenSize.x, 0.06f), new Color(0.1f, 0.14f, 0.22f), 0.002f);
            _objective = _base.Text("", new Rect(-ScreenSize.x / 2f + 0.02f, top - 0.06f, ScreenSize.x - 0.2f, 0.06f), 0.16f,
                TextAlignmentOptions.MidlineLeft);
            _clock = _base.Text("", new Rect(ScreenSize.x / 2f - 0.17f, top - 0.06f, 0.15f, 0.06f), 0.18f,
                TextAlignmentOptions.MidlineRight, new Color(0.55f, 0.85f, 1f), bold: true);

            float tabX = -ScreenSize.x / 2f + 0.085f;
            int i = 0;
            foreach (var app in new[] { "Report", "Mail", "Chat" })
            {
                string name = app;
                var btn = _base.Button(app, new Vector2(tabX, top - 0.12f - i * 0.09f), new Vector2(0.15f, 0.075f), () => ShowApp(name));
                _tabs[app] = btn;
                _badges[app] = 0;

                var panel = UiPanel.Create(transform, "App_" + app, ContentSize, null, UiTheme.Blue, ViewDistance);
                panel.Root.transform.localPosition = new Vector3(ContentCenter.x, ContentCenter.y, -0.004f);
                panel.Root.SetActive(false);
                _apps[app] = panel;
                i++;
            }

            // Pops up above the monitor so it never covers the app's buttons.
            _toast = ToastNotification.Create(transform, "ChatToast", new Vector2(0.62f, 0.22f), ViewDistance, 0.17f);
            _toast.transform.localPosition = new Vector3(ScreenSize.x / 2f - 0.31f, ScreenSize.y / 2f + 0.15f, -0.01f);

            _overlay = UiPanel.Create(transform, "Overlay", new Vector2(0.62f, 0.3f), "Window", UiTheme.Red, ViewDistance);
            _overlay.Root.transform.localPosition = new Vector3(0.05f, 0f, -0.045f);
            _overlayText = _overlay.Body("", _overlay.ContentTop, 0.12f, 0.17f, color: new Color(0.7f, 1f, 0.7f));
            _overlay.Quad("ProgressBack", new Vector2(0f, -0.1f), new Vector2(_overlay.Width, 0.035f), new Color(0.15f, 0.18f, 0.25f), -0.0005f);
            _progressFill = _overlay.Quad("ProgressFill", new Vector2(0f, -0.1f), new Vector2(_overlay.Width, 0.035f), UiTheme.Green, -0.001f);
            _overlay.Root.SetActive(false);

            ShowApp("Report");
        }

        private void Update()
        {
            var director = Core.GameDirector.Instance;
            if (director != null)
            {
                _clock.text = director.ClockText;
            }
        }

        public UiPanel App(string name)
        {
            Build();
            return _apps[name];
        }

        public void SetObjective(string text)
        {
            Build();
            _objective.text = "<b>Objective:</b>  " + text;
        }

        public void ShowApp(string name)
        {
            Build();
            foreach (var kv in _apps)
            {
                kv.Value.Root.SetActive(kv.Key == name);
            }
            CurrentApp = name;
            SetBadge(name, 0);
            AppOpened?.Invoke(name);
        }

        public void SetBadge(string app, int count)
        {
            _badges[app] = count;
            if (_tabs.TryGetValue(app, out var tab) && tab != null)
            {
                tab.SetLabel(count > 0 ? $"{app} ({count})" : app);
            }
        }

        public void AddBadge(string app)
        {
            if (CurrentApp == app)
            {
                return;
            }
            SetBadge(app, _badges[app] + 1);
        }

        public void ShowToast(string title, string message, float seconds = 6f)
        {
            Build();
            _toast.Show(title, message, seconds);
        }

        /// <summary>Brief suspicious window that appears then vanishes (silent compromise).</summary>
        public void FlashWindow(string title, string text, float seconds)
        {
            StartCoroutine(Flash(title, text, seconds, showProgress: false));
        }

        /// <summary>Fake "installing update" window with a progress bar.</summary>
        public void ShowInstallBar(string title, string text, float seconds)
        {
            StartCoroutine(Flash(title, text, seconds, showProgress: true));
        }

        private IEnumerator Flash(string title, string text, float seconds, bool showProgress)
        {
            // Screen flicker first.
            for (int f = 0; f < 3; f++)
            {
                _base.Root.SetActive(false);
                yield return new WaitForSeconds(0.06f);
                _base.Root.SetActive(true);
                yield return new WaitForSeconds(0.08f);
            }
            _overlay.Title.text = title;
            _overlayText.text = text;
            _progressFill.transform.parent.Find("ProgressBack").gameObject.SetActive(showProgress);
            _progressFill.gameObject.SetActive(showProgress);
            _overlay.Root.SetActive(true);
            float w = _overlay.Width;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (showProgress)
                {
                    float p = Mathf.Clamp01(t / seconds);
                    _progressFill.transform.localScale = new Vector3(w * p, 0.035f, 1f);
                    _progressFill.transform.localPosition = new Vector3(-w / 2f + w * p / 2f, -0.1f, -0.001f);
                }
                yield return null;
            }
            _overlay.Root.SetActive(false);
        }
    }
}
