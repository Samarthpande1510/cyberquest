using System;
using System.Collections;
using CyberQuest.UI;
using Oculus.Interaction;
using TMPro;
using UnityEngine;

namespace CyberQuest.Props
{
    /// <summary>
    /// The player's grabbable work phone. Hosts the browser (fake login), the
    /// authenticator push (MFA) and notification toasts. UI is authored at 5x
    /// size and scaled down onto the 8 x 16 cm screen. Buzzes with spatial audio
    /// and vibrates whichever controller is holding it.
    /// </summary>
    public class PhoneDevice : MonoBehaviour
    {
        public const float UiScale = 0.2f;
        public static readonly Vector2 VirtualSize = new Vector2(0.4f, 0.8f);
        public const float ViewDistance = 0.35f;

        [SerializeField] private Transform screenAnchor;
        [SerializeField] private AudioSource speaker;

        public event Action Grabbed;

        private UiPanel _home;
        private TextMeshPro _homeClock;
        private UiPanel _app;
        private ToastNotification _toast;
        private int _holders;

        public static PhoneDevice Instance { get; private set; }

        public bool IsHeld => _holders > 0;
        public UiPanel CurrentApp => _app;
        public ToastNotification Toast => _toast;

        private void Awake()
        {
            Instance = this;
            var root = screenAnchor != null ? screenAnchor : transform;
            _home = UiPanel.Create(root, "Home", VirtualSize, null, new Color(0.12f, 0.16f, 0.26f), ViewDistance, UiScale);
            _homeClock = _home.Text("5:45", new Rect(_home.Left, 0.12f, _home.Width, 0.16f), 0.9f, TextAlignmentOptions.Center, bold: true);
            _home.Text("NEXORA work phone", new Rect(_home.Left, 0.06f, _home.Width, 0.06f), 0.24f, TextAlignmentOptions.Center, UiTheme.MutedText);
            _home.Text("Hold me up to a QR code to scan it.", new Rect(_home.Left, -0.3f, _home.Width, 0.16f), 0.24f,
                TextAlignmentOptions.Center, UiTheme.MutedText);

            _toast = ToastNotification.Create(root, "PhoneToast", new Vector2(0.38f, 0.3f), ViewDistance, 0.24f, UiScale);
            _toast.transform.localPosition = new Vector3(0f, 0.045f, -0.004f);

            var wrapper = GetComponent<PointableUnityEventWrapper>();
            if (wrapper != null)
            {
                wrapper.WhenSelect.AddListener(_ => OnGrab());
                wrapper.WhenUnselect.AddListener(_ => _holders = Mathf.Max(0, _holders - 1));
            }
        }

        private void Update()
        {
            var director = Core.GameDirector.Instance;
            if (director != null)
            {
                _homeClock.text = director.ClockText.Replace(" PM", "");
            }
        }

        private void OnGrab()
        {
            _holders++;
            Grabbed?.Invoke();
        }

        /// <summary>Opens a full-screen app (replacing any open one) and returns it to be filled in.</summary>
        public UiPanel OpenApp(string title, Color accent)
        {
            CloseApp();
            var root = screenAnchor != null ? screenAnchor : transform;
            _app = UiPanel.Create(root, "App_" + title, VirtualSize, title, accent, ViewDistance, UiScale);
            _app.Root.transform.localPosition = new Vector3(0f, 0f, -0.002f);
            return _app;
        }

        public void CloseApp()
        {
            if (_app != null)
            {
                _app.Root.SetActive(false);
                Destroy(_app.Root);
                _app = null;
            }
        }

        public void ShowToast(string title, string message, float seconds = 6f)
        {
            _toast.Show(title, message, seconds);
        }

        public void Buzz(int times, float interval = 0.6f)
        {
            StartCoroutine(BuzzRoutine(times, interval));
        }

        private IEnumerator BuzzRoutine(int times, float interval)
        {
            var theme = UiTheme.Instance;
            for (int i = 0; i < times; i++)
            {
                if (speaker != null && theme != null && theme.phoneBuzz != null)
                {
                    speaker.PlayOneShot(theme.phoneBuzz);
                }
                if (IsHeld)
                {
                    var hand = NearestController();
                    OVRInput.SetControllerVibration(0.6f, 0.8f, hand);
                    yield return new WaitForSeconds(0.3f);
                    OVRInput.SetControllerVibration(0f, 0f, hand);
                    yield return new WaitForSeconds(interval - 0.3f);
                }
                else
                {
                    yield return new WaitForSeconds(interval);
                }
            }
        }

        private OVRInput.Controller NearestController()
        {
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig == null)
            {
                return OVRInput.Controller.RTouch;
            }
            float l = Vector3.Distance(rig.leftHandAnchor.position, transform.position);
            float r = Vector3.Distance(rig.rightHandAnchor.position, transform.position);
            return l < r ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        }
    }
}
