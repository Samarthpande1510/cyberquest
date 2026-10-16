#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CyberQuest.Core;
using CyberQuest.UI;
using Oculus.Interaction;
using TMPro;
using UnityEngine;

namespace CyberQuest.Dev
{
    /// <summary>
    /// Editor-only automated playthrough. Started by CyberQuest/6 (or the playtest
    /// request file) via the Temp/cq_playtest.active flag. Presses every choice through
    /// the same PointableUnityEventWrapper.WhenSelect path a real poke/ray uses,
    /// checks GameState after each step, and saves eye-view screenshots.
    /// </summary>
    public class CyberQuestAutoPlaytest : MonoBehaviour
    {
        public const string ActiveFlag = "Temp/cq_playtest.active";
        public const string OutDir = "Builds/playtest";

        private readonly StringBuilder _report = new StringBuilder();
        private readonly List<string> _errors = new List<string>();
        private int _failures;
        private int _shot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (File.Exists(ActiveFlag))
            {
                new GameObject("[AutoPlaytest]").AddComponent<CyberQuestAutoPlaytest>();
            }
        }

        private void Awake()
        {
            Directory.CreateDirectory(OutDir);
            foreach (var old in Directory.GetFiles(OutDir, "*.png")) File.Delete(old);
            Application.logMessageReceived += OnLog;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
        }

        private void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert ||
                (type == LogType.Warning && msg.Contains("has no ")))
            {
                _errors.Add($"[{type}] {msg}");
            }
        }

        private IEnumerator Start()
        {
            Log($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            float wait = 0f;
            while (!UnityEngine.XR.XRSettings.isDeviceActive && wait < 20f)
            {
                wait += Time.deltaTime;
                yield return null;
            }
            Log($"XR device active: {UnityEngine.XR.XRSettings.isDeviceActive} ({UnityEngine.XR.XRSettings.loadedDeviceName}) after {wait:0.0}s");
            yield return new WaitForSeconds(3f);
            var head = FindFirstObjectByType<OVRCameraRig>()?.centerEyeAnchor.position ?? Vector3.zero;
            Log($"Headset (center eye) position after tracking start: {head}");
            Check("headset at standing height (y between 1.0 and 2.1)", head.y > 1.0f && head.y < 2.1f);

            var eye = new Vector3(0f, 1.6f, 0f);
            Shot("01_start_desk", eye, new Vector3(0.15f, 1.1f, 0.8f));
            Shot("02_room_right", eye, new Vector3(2f, 1.3f, -0.3f));
            Shot("03_exit_behind", eye, new Vector3(0.8f, 1.3f, -1.8f));
            Log($"Buttons spawned: {FindObjectsByType<PokeButton3D>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length}");

            // Event 1-2: email -> scan QR -> fake login -> report
            yield return Press("Scan QR Code");
            Check("emailAction == scanned", GameState.Current.emailAction == "scanned");
            Check("loginEventTriggered", GameState.Current.loginEventTriggered);
            yield return null;
            Shot("04_email_and_fake_login", eye, new Vector3(0.3f, 1.05f, 0.7f));
            yield return Press("Report");
            Check("loginResult == reported", GameState.Current.loginResult == "reported");

            // Event 6: MFA push fires once the login page was reached
            yield return new WaitForSeconds(0.5f);
            Check("mfaEventTriggered", GameState.Current.mfaEventTriggered);
            Shot("05_mfa_push", eye, new Vector3(0.5f, 1.3f, 0.65f));
            yield return Press("Deny & Report");
            Check("mfaResponse == denied", GameState.Current.mfaResponse == "denied");

            // Event 4: Priya appears after any prior event
            yield return new WaitForSeconds(0.5f);
            Check("priyaEventTriggered", GameState.Current.priyaEventTriggered);
            Shot("06_priya", eye, new Vector3(1.7f, 1.3f, -0.6f));
            yield return Press("Help Her In (Verify First)");
            Check("priyaResponse == helped-properly", GameState.Current.priyaResponse == "helped-properly");

            // Event 3: grab the USB (same WhenSelect a hand/controller grab raises)
            var usb = FindFirstObjectByType<CyberQuest.Events.UsbEvent>();
            Check("USB exists", usb != null);
            if (usb != null)
            {
                usb.GetComponent<PointableUnityEventWrapper>().WhenSelect.Invoke(new PointerEvent(9001, PointerEventType.Select, new Pose(usb.transform.position, usb.transform.rotation)));
                yield return null;
                Check("usbFound", GameState.Current.usbFound);
                Shot("07_usb_panel", eye, new Vector3(-1.05f, 1.05f, 0.7f));
                yield return Press("Take to Security");
                Check("usbAction == reported", GameState.Current.usbAction == "reported");
            }

            // Event 5: imposter triggers after 2 resolved events and walks in
            yield return new WaitForSeconds(0.3f);
            Check("imposterEventTriggered", GameState.Current.imposterEventTriggered);
            yield return new WaitForSeconds(1.2f);
            Shot("08_imposter_walking", eye, new Vector3(1.8f, 1.2f, -0.4f));
            float t = 0f;
            while (FindButton("Verify via Directory") == null && t < 15f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Log($"Imposter arrived after {t:0.0}s");
            Shot("09_imposter_panel", eye, new Vector3(0.8f, 1.35f, 0.25f));
            Shot("10_directory_kiosk", new Vector3(1.6f, 1.6f, 0.9f), new Vector3(2.47f, 1.45f, 1.2f));
            yield return Press("Verify via Directory");
            Check("imposterResponse == verified", GameState.Current.imposterResponse == "verified");
            Check("credentials not compromised", !GameState.Current.credentialsCompromised);

            // Exit terminal
            yield return Press("Clock Out");
            Check("score == 100", GameState.Current.score == 100);
            var ending = EndingResolver.Resolve(GameState.Current);
            Check("ending == FullContainment", ending == EndingResolver.EndingType.FullContainment);
            var door = GameObject.Find("ExitDoor");
            Check("exit door unlocked", door == null || !door.activeInHierarchy);
            Shot("11_results", eye, new Vector3(0.6f, 1.45f, -1.7f));

            // Ending logic spot checks (pure logic, no scene)
            var breach = new GameState { loginResult = "entered", mfaResponse = "approved" };
            breach.EvaluateCompromise();
            Check("Breach ending logic", EndingResolver.Resolve(breach) == EndingResolver.EndingType.Breach);
            var social = new GameState { priyaResponse = "refused" };
            Check("SocialMiscalibration ending logic", EndingResolver.Resolve(social) == EndingResolver.EndingType.SocialMiscalibration);

            yield return new WaitForSeconds(0.5f);
            Finish();
        }

        private IEnumerator Press(string label)
        {
            var button = FindButton(label);
            if (button == null)
            {
                Check($"button '{label}' visible", false);
                yield break;
            }
            var wrapper = button.GetComponent<PointableUnityEventWrapper>();
            wrapper.WhenSelect.Invoke(new PointerEvent(1000 + label.Length, PointerEventType.Select, new Pose(button.transform.position, button.transform.rotation)));
            Log($"Pressed '{label}'");
            yield return null;
            yield return null;
        }

        private static PokeButton3D FindButton(string label)
        {
            return FindObjectsByType<PokeButton3D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(b =>
                {
                    var tmp = b.GetComponentInChildren<TextMeshPro>();
                    return tmp != null && tmp.text == label;
                });
        }

        private void Shot(string name, Vector3 from, Vector3 lookAt)
        {
            var go = new GameObject("PlaytestCam");
            var cam = go.AddComponent<Camera>();
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.02f;
            cam.transform.position = from;
            cam.transform.LookAt(lookAt);
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes($"{OutDir}/{++_shot:00}_{name}.png", tex.EncodeToPNG());
            cam.targetTexture = null;
            Destroy(rt);
            Destroy(tex);
            Destroy(go);
        }

        private void Check(string what, bool ok)
        {
            if (!ok) _failures++;
            Log($"{(ok ? "PASS" : "FAIL")}  {what}");
        }

        private void Log(string line)
        {
            _report.AppendLine(line);
            Debug.Log("[Playtest] " + line);
        }

        private void Finish()
        {
            _report.AppendLine();
            _report.AppendLine($"Errors/warnings logged during play: {_errors.Count}");
            foreach (var e in _errors.Distinct().Take(60)) _report.AppendLine("  " + e);
            _report.AppendLine(_failures == 0 ? "RESULT: ALL CHECKS PASSED" : $"RESULT: {_failures} CHECK(S) FAILED");
            File.WriteAllText($"{OutDir}/report.txt", _report.ToString());
            File.Delete(ActiveFlag);
            UnityEditor.EditorApplication.ExitPlaymode();
        }
    }
}
#endif
