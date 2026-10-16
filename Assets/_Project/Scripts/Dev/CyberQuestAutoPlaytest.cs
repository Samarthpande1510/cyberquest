#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CyberQuest.Core;
using CyberQuest.Props;
using CyberQuest.Situations;
using CyberQuest.UI;
using TMPro;
using UnityEngine;

namespace CyberQuest.Dev
{
    /// <summary>
    /// Editor-only automated playthroughs in Meta XR Simulator (started by
    /// CyberQuest > 6 or the playtest request file). Drives every situation
    /// through its public choice methods and the same physical rules a player
    /// uses (moving the rig into trigger range, holding the phone to the QR,
    /// dropping the USB in the box), reaches all four endings plus a do-nothing
    /// lockout run, checks GameState after each step, checks every visible panel
    /// for text overflow, legibility (angular size) and overlapping buttons, and
    /// saves screenshots to Builds/playtest.
    /// </summary>
    public class CyberQuestAutoPlaytest : MonoBehaviour
    {
        public const string ActiveFlag = "Temp/cq_playtest.active";
        public const string OutDir = "Builds/playtest";
        private const float MinTextDegrees = 0.75f;

        private readonly StringBuilder _report = new StringBuilder();
        private readonly List<string> _errors = new List<string>();
        private int _failures;
        private int _checks;
        private int _shot;
        private float _minDegrees = 99f;
        private string _minDegreesWhere = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (File.Exists(ActiveFlag) && FindFirstObjectByType<CyberQuestAutoPlaytest>() == null)
            {
                var go = new GameObject("[AutoPlaytest]");
                DontDestroyOnLoad(go);
                go.AddComponent<CyberQuestAutoPlaytest>();
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
                (type == LogType.Warning && (msg.Contains("has no ") || msg.Contains("UiPanel"))))
            {
                _errors.Add($"[{type}] {msg}");
            }
        }

        private IEnumerator Start()
        {
            float wait = 0f;
            while (!UnityEngine.XR.XRSettings.isDeviceActive && wait < 20f)
            {
                wait += Time.deltaTime;
                yield return null;
            }
            Log($"XR device active: {UnityEngine.XR.XRSettings.isDeviceActive} ({UnityEngine.XR.XRSettings.loadedDeviceName}) after {wait:0.0}s");
            yield return new WaitForSeconds(2f);

            yield return Run("1  Full containment (+ tour screenshots)", FullContainmentRun);
            yield return Run("2  Breach (fake login + approved MFA + plugged USB, lockout, wrong deduction)", BreachRun);
            yield return Run("3  Contained but exposed (plugged USB only)", ContainedRun);
            yield return Run("4  Social miscalibration (different order: workstation + USB before email)", SocialRun);
            yield return Run("5  Do nothing until 6:30 lockout", IdleRun);

            Finish();
        }

        private IEnumerator Run(string name, Func<IEnumerator> body)
        {
            Log("");
            Log("=== RUN " + name);
            yield return WaitForFreshScene();
            int before = _failures;
            yield return body();
            Log(_failures == before ? "--- run passed" : $"--- run had {_failures - before} failure(s)");
            var ending = FindFirstObjectByType<EndingScreen>();
            if (ending != null && ending.Visible)
            {
                ending.ChoosePlayAgain(); // tests Play Again / scene reload
            }
            else
            {
                EndingFlow.Restart();
            }
            yield return null;
        }

        private IEnumerator WaitForFreshScene()
        {
            float t = 0f;
            while ((GameDirector.Instance == null || GameDirector.Instance.Running || FindFirstObjectByType<OVRCameraRig>() == null) && t < 10f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(1f);
            Check("fresh session: GameState clean", GameState.Current.CompletedCount == 0 && GameState.Current.login == LoginResult.NotReached);
        }

        // ------------------------------------------------------------------ runs

        private IEnumerator FullContainmentRun()
        {
            var eye = Camera.main.transform.position;
            Log($"Headset (center eye) position: {eye}");
            Check("headset at standing height (y between 1.0 and 2.1)", eye.y > 1.0f && eye.y < 2.1f);

            yield return MoveTo(OfficeSpawn, new Vector3(3f, 1.4f, 4f));
            Shot("start_welcome", new Vector3(3f, 1.6f, 2.35f), new Vector3(3f, 1.45f, 3.2f), hud: true);
            Layout("welcome");
            yield return StartShift();
            Check("desk phone ringing", Find<DeskPhone>().Ringing);
            Find<DeskPhone>().Answer();
            yield return new WaitForSeconds(0.5f);
            Shot("voicemail_subtitles", new Vector3(3f, 1.6f, 2.35f), new Vector3(2.7f, 1.4f, 3.2f), hud: true);
            Layout("voicemail");
            Check("voicemail subtitle shown", Find<DeskPhone>().Subtitle.CurrentText.Contains("Dan"));

            var report = Find<ReportApp>();
            report.FillNext(); report.FillNext(); report.FillNext(); report.Send();
            Check("report sent", report.Sent);

            // Walking into things: a wall and the desk must stop the player.
            yield return MoveTo(new Vector3(3f, 0f, 1.0f), new Vector3(3f, 1.4f, -1f));
            yield return Walk(new Vector3(0f, 0f, -1f), 1.5f);
            float z = Camera.main.transform.position.z;
            Log($"  walked into the south window wall: head stopped at z = {z:0.00} (wall face at 0.06)");
            Check("cannot walk through a wall", z > 0.15f);
            yield return MoveTo(OfficeSpawn, new Vector3(3f, 1.4f, 4f));
            yield return Walk(new Vector3(0f, 0f, 1f), 1.5f);
            z = Camera.main.transform.position.z;
            Log($"  walked into the desk: head stopped at z = {z:0.00} (desk edge at 2.625)");
            Check("cannot walk through the desk", z < 2.6f);
            yield return MoveTo(OfficeSpawn, new Vector3(3f, 1.4f, 4f));

            // Situation 1: the only thing ready at 20 s is the email.
            yield return SkipTo(21f);
            var email = Find<EmailQrSituation>();
            Check("email triggered at 20 s", email.Triggered);
            Find<MonitorScreen>().ShowApp("Mail");
            yield return null;
            Shot("mail_inbox", new Vector3(3f, 1.6f, 2.45f), new Vector3(3f, 1.3f, 3.3f));
            Layout("mail inbox");
            email.OpenEmail();
            yield return null;
            Check("emailOpened", GameState.Current.emailOpened);
            email.RevealSender();
            yield return null;
            Shot("mail_sender_revealed", new Vector3(3f, 1.6f, 2.45f), new Vector3(3f, 1.3f, 3.3f));
            Layout("email open");
            email.OpenInvoice();
            yield return null;
            Shot("invoice_qr", new Vector3(3f, 1.6f, 2.45f), new Vector3(3f, 1.3f, 3.3f));
            Layout("invoice");
            email.Forward();
            yield return null;
            Check("email forwarded to security", GameState.Current.emailForwardedToSecurity && GameState.Current.IsCompleted(SituationId.EmailQr));
            Check("cooldown running after a situation", Director.CooldownRemaining > 40f);
            Check("fake login never triggered (defused)", !Find<FakeLoginSituation>().Triggered);

            // Situation 4: one completed situation -> Priya (intercom still needs two).
            yield return Await(Find<ColleagueSituation>(), null);
            Find<MonitorScreen>().ShowApp("Chat");
            yield return null;
            Shot("chat_priya", new Vector3(3f, 1.6f, 2.45f), new Vector3(3f, 1.3f, 3.3f));
            Layout("chat");
            Find<ColleagueSituation>().Acknowledge();
            yield return MoveTo(new Vector3(21f, 0f, 7.6f), new Vector3(21f, 1.55f, 9.9f));
            yield return LookAt(new Vector3(21f, 1.54f, 9.9f), 2.2f);
            Check("directory entry read by gaze -> colleagueDirectoryChecked", GameState.Current.colleagueDirectoryChecked);
            Shot("lobby_directory", new Vector3(21f, 1.6f, 7.4f), new Vector3(21f, 1.6f, 9.93f));
            Shot("door_panel", new Vector3(8.75f, 1.6f, 8.6f), new Vector3(8.75f, 1.4f, 9.92f));
            Layout("door panel");
            Check("door panel unlock offered", Find<ColleagueSituation>().DoorPanel.Choose("Unlock door"));
            Check("colleague == VerifiedThenHelped", GameState.Current.colleague == ColleagueResponse.VerifiedThenHelped);
            yield return new WaitForSeconds(1.5f);
            Shot("meetingroom3_open", new Vector3(10f, 1.6f, 8.6f), new Vector3(10f, 1.2f, 12.5f));

            // Tour of the floor.
            Shot("tour_openplan", new Vector3(8.5f, 1.6f, 0.8f), new Vector3(3f, 1.2f, 4.5f));
            Shot("tour_window_skyline", new Vector3(3f, 1.6f, 1.2f), new Vector3(1f, 2f, -10f));
            Shot("tour_corridor", new Vector3(0.8f, 1.6f, 9f), new Vector3(18f, 1.5f, 9f));
            Shot("tour_breakroom", new Vector3(12.6f, 1.6f, 1.0f), new Vector3(17.9f, 1.4f, 4.5f));
            Shot("tour_lobby", new Vector3(18.6f, 1.6f, 1.0f), new Vector3(22.5f, 1.4f, 8f));
            Shot("tour_it_door", new Vector3(15.2f, 1.6f, 8.3f), new Vector3(16.5f, 1.8f, 10f));

            // Situation 7: walk into the Finance pod.
            yield return MoveTo(new Vector3(3.25f, 0f, 12.4f), new Vector3(3.25f, 1.1f, 14f));
            yield return WaitUntil(() => Find<WorkstationSituation>().Triggered, 3f);
            Check("workstation triggered by walking up to it", Find<WorkstationSituation>().Triggered);
            yield return new WaitForSeconds(0.3f);
            Shot("workstation", new Vector3(3.25f, 1.6f, 12.3f), new Vector3(3.7f, 1.15f, 13.8f));
            Layout("workstation");
            Check("workstation option offered", Find<WorkstationSituation>().Panel.Choose("Take the note and report it"));
            Check("workstation == Reported", GameState.Current.workstation == WorkstationResult.Reported);

            // Situation 3: walk to the empty desk, the drive drops; carry it to the box.
            yield return MoveTo(new Vector3(5.6f, 0f, 2.0f), new Vector3(6.2f, 0.4f, 3f));
            yield return WaitUntil(() => Find<UsbSituation>().Triggered, 3f);
            Check("USB dropped when player came within 2 m", Find<UsbSituation>().Triggered);
            yield return new WaitForSeconds(1.2f);
            var drive = Find<UsbSituation>().Drive;
            Log($"USB came to rest at {drive.transform.position}");
            Check("USB landed on the open-plan floor", drive.transform.position.y < 0.3f && drive.transform.position.x < 10f);
            Shot("usb_on_floor", new Vector3(5.2f, 1.6f, 1.8f), drive.transform.position);
            Find<UsbSituation>().PickUp();
            yield return null;
            Check("manager nudge posted while holding the USB", Find<ChatApp>().LastMessage.Contains("Almost done"));
            drive.GetComponent<Rigidbody>().isKinematic = true;
            drive.transform.position = new Vector3(15.3f, 0.95f, 9.6f); // carried to the Found devices box
            yield return null;
            yield return null;
            Check("usb == Reported via drop box", GameState.Current.usb == UsbAction.Reported);
            Shot("found_devices_box", new Vector3(15.3f, 1.6f, 8.4f), new Vector3(15.3f, 0.6f, 9.6f));

            // Maintenance notice in the break room (gaze).
            yield return MoveTo(new Vector3(16.3f, 0f, 4.8f), new Vector3(17.93f, 1.57f, 4.82f));
            yield return LookAt(new Vector3(17.9f, 1.57f, 4.82f), 2.2f);
            Check("maintenance notice read by gaze", GameState.Current.maintenanceNoticeRead);
            Shot("bulletin_board", new Vector3(16.4f, 1.6f, 4.5f), new Vector3(17.93f, 1.55f, 4.5f));

            // Situation 5: intercom (two completed; MFA not ready yet).
            yield return Await(Find<IntercomSituation>(), null);
            Shot("terminal_ready", new Vector3(19.5f, 1.6f, 3.6f), new Vector3(20.6f, 1.5f, 3.6f));
            yield return MoveTo(new Vector3(22.6f, 0f, 7.6f), new Vector3(23.9f, 1.5f, 7.4f));
            yield return WaitUntil(() => Find<IntercomSituation>().Answered, 3f);
            Check("intercom answered by walking up to it", Find<IntercomSituation>().Answered);
            yield return new WaitForSeconds(0.5f);
            Shot("intercom_answered", new Vector3(22.5f, 1.6f, 7.2f), new Vector3(23.9f, 1.45f, 7.3f));
            Layout("intercom");
            Check("badge option offered", Find<IntercomSituation>().Choices.Choose("Ask him to show his badge"));
            yield return null;
            Check("badge visible + flag", Find<IntercomSituation>().BadgeVisible && GameState.Current.intercomBadgeInspected);
            Shot("intercom_badge", new Vector3(22.7f, 1.6f, 7.4f), new Vector3(23.9f, 1.55f, 7.6f));
            Layout("intercom badge");
            Check("maintenance-notice option offered once notice read",
                Find<IntercomSituation>().Choices.Choose("The notice says no IT work tonight"));
            Check("intercom == VerifiedExternally", GameState.Current.intercom == IntercomResponse.VerifiedExternally);

            // Situation 6: MFA at the 600 s fallback.
            yield return MoveTo(OfficeSpawn, new Vector3(3f, 1.2f, 4f));
            yield return SkipTo(601f);
            yield return Await(Find<MfaSituation>(), null);
            yield return HoldPhoneUp();
            Shot("mfa_push_on_phone", Camera.main.transform.position, Find<PhoneDevice>().transform.position);
            Layout("mfa");
            Find<MfaSituation>().DenyAndReport();
            Check("mfa == DeniedAndReported", GameState.Current.mfa == MfaResponse.DeniedAndReported);

            // Ending: terminal active (>= 3 completed).
            yield return MoveTo(new Vector3(19.6f, 0f, 3.6f), new Vector3(20.6f, 1.5f, 3.6f));
            Check("security terminal active", Find<EndingFlow>().Active);
            var term = Find<EndingFlow>().Terminal;
            Check("start check-out", term.Choose("Start check-out"));
            Shot("terminal_timeline", new Vector3(19.5f, 1.6f, 3.6f), new Vector3(20.6f, 1.5f, 3.6f));
            Layout("terminal timeline");
            Check("continue", term.Choose("Continue"));
            Shot("terminal_question", new Vector3(19.5f, 1.6f, 3.6f), new Vector3(20.6f, 1.5f, 3.6f));
            Layout("terminal question");
            Check("answer C", term.Choose(EndingFlow.AnswerC));
            Check("deductionCorrect", GameState.Current.deductionCorrect);
            Check("continue to report", term.Choose("Continue to incident report"));
            Find<EndingFlow>().Toggle(SituationId.EmailQr);
            Shot("terminal_report_form", new Vector3(19.5f, 1.6f, 3.6f), new Vector3(20.6f, 1.5f, 3.6f));
            Layout("report form");
            Check("submit", term.Choose("Submit report"));
            yield return EndingChecks(EndingType.FullContainment, expectedScore: 100, shots: true);
        }

        private IEnumerator BreachRun()
        {
            yield return StartShift();
            yield return SkipTo(21f);
            var email = Find<EmailQrSituation>();
            Check("email triggered", email.Triggered);
            email.OpenInvoice();
            yield return null;
            // Hold the real phone up to the QR on the monitor.
            var phone = Find<PhoneDevice>();
            phone.GetComponent<Rigidbody>().isKinematic = true;
            var qr = Find<MailApp>().QrTransform;
            Check("QR visible on monitor", qr != null);
            if (qr != null)
            {
                phone.transform.SetPositionAndRotation(qr.position - qr.forward * 0.25f, Quaternion.LookRotation(qr.forward));
            }
            yield return null;
            yield return null;
            Check("QR scanned by holding the phone within 0.4 m", GameState.Current.qrScanned);
            yield return WaitUntil(() => Find<FakeLoginSituation>().Triggered, 2f);
            Check("fake login opened on phone immediately (no cooldown)", Find<FakeLoginSituation>().Triggered);
            yield return HoldPhoneUp();
            Shot("fake_login_on_phone", Camera.main.transform.position, phone.transform.position);
            Layout("fake login");
            Find<FakeLoginSituation>().LogIn();
            Check("login == Entered", GameState.Current.login == LoginResult.Entered);
            yield return Await(Find<MfaSituation>(), BuzzInIfIntercomOrUnlockIfColleague);
            Find<MfaSituation>().Approve();
            Check("mfa == Approved", GameState.Current.mfa == MfaResponse.Approved);

            yield return MoveTo(new Vector3(5.6f, 0f, 2f), new Vector3(6.2f, 0.4f, 3f));
            yield return WaitUntil(() => Find<UsbSituation>().Triggered, 3f);
            yield return new WaitForSeconds(1f);
            var drive = Find<UsbSituation>().Drive;
            drive.GetComponent<Rigidbody>().isKinematic = true;
            drive.transform.position = GameObject.Find("PcPort").transform.position;
            yield return null;
            yield return null;
            Check("usb == Plugged via the dock port", GameState.Current.usb == UsbAction.Plugged);
            yield return new WaitForSeconds(0.6f);
            Shot("usb_install_bar", new Vector3(3f, 1.6f, 2.45f), new Vector3(3f, 1.3f, 3.3f));
            Check("CompromiseSources >= 3", GameState.Current.CompromiseSources >= 3);

            // Lockout with the player in the open plan: the terminal comes to them.
            yield return MoveTo(OfficeSpawn, new Vector3(3f, 1.4f, 4f));
            yield return SkipTo(15f * 60f + 1f);
            Check("lockout at 6:30", Director.LockedOut);
            Check("terminal active at lockout", Find<EndingFlow>().Active);
            var term = Find<EndingFlow>().Terminal;
            Shot("lockout_terminal_in_front", Camera.main.transform.position, term.transform.position, hud: true);
            Layout("lockout timeline");
            Check("continue", term.Choose("Continue"));
            Check("answer A (wrong)", term.Choose(EndingFlow.AnswerA));
            Check("deductionCorrect false", !GameState.Current.deductionCorrect);
            Shot("terminal_correction", Camera.main.transform.position, term.transform.position);
            Layout("correction");
            Check("continue to report", term.Choose("Continue to incident report"));
            Check("submit", term.Choose("Submit report"));
            yield return EndingChecks(EndingType.Breach, expectedScore: -1, shots: true);
        }

        private IEnumerator ContainedRun()
        {
            yield return StartShift();
            yield return MoveTo(new Vector3(5.6f, 0f, 2f), new Vector3(6.2f, 0.4f, 3f));
            yield return WaitUntil(() => Find<UsbSituation>().Triggered, 3f);
            yield return new WaitForSeconds(0.8f);
            Find<UsbSituation>().PlugIn();
            Check("usb == Plugged", GameState.Current.usb == UsbAction.Plugged);
            yield return new WaitForSeconds(0.6f);
            Shot("install_bar_contained", new Vector3(3f, 1.6f, 2.45f), new Vector3(3f, 1.3f, 3.3f));
            // One completed situation and the email isn't due until 20 s: Priya asks first.
            yield return Await(Find<ColleagueSituation>(), null);
            Find<ColleagueSituation>().UnlockDoor();
            Check("colleague == UnlockedBlindly", GameState.Current.colleague == ColleagueResponse.UnlockedBlindly);
            yield return SkipTo(21f);
            yield return Await(Find<EmailQrSituation>(), RefuseIntercom);
            Find<EmailQrSituation>().OpenEmail();
            Find<EmailQrSituation>().Ignore();
            Check("email ignored", GameState.Current.emailIgnored);
            yield return SkipTo(601f);
            yield return Await(Find<MfaSituation>(), RefuseIntercom);
            Find<MfaSituation>().Deny();
            Check("mfa == Denied", GameState.Current.mfa == MfaResponse.Denied);
            Check("CompromiseSources == 1", GameState.Current.CompromiseSources == 1);
            yield return CheckOut(EndingFlow.AnswerC);
            yield return EndingChecks(EndingType.ContainedButExposed, expectedScore: -1, shots: false);
        }

        private IEnumerator SocialRun()
        {
            yield return StartShift();
            // Different order: the optional workstation first, before the email even arrives.
            yield return MoveTo(new Vector3(3.25f, 0f, 12.4f), new Vector3(3.25f, 1.1f, 14f));
            yield return WaitUntil(() => Find<WorkstationSituation>().Triggered, 3f);
            Find<WorkstationSituation>().Lock();
            Check("workstation == Locked", GameState.Current.workstation == WorkstationResult.Locked);
            yield return Await(Find<ColleagueSituation>(), null);
            Check("colleague asked before the email arrived (order differs)", !Find<EmailQrSituation>().Triggered);
            Find<MonitorScreen>().ShowApp("Chat");
            Check("refuse via chat reply", Find<ChatApp>().Reply("Sorry, I can't"));
            Check("colleague == RefusedOutright", GameState.Current.colleague == ColleagueResponse.RefusedOutright);
            yield return MoveTo(new Vector3(5.6f, 0f, 2f), new Vector3(6.2f, 0.4f, 3f));
            yield return WaitUntil(() => Find<UsbSituation>().Triggered, 3f);
            yield return new WaitForSeconds(0.8f);
            Find<UsbSituation>().HandIn();
            Check("usb == Reported", GameState.Current.usb == UsbAction.Reported);
            yield return SkipTo(21f);
            yield return Await(Find<IntercomSituation>(), ForwardEmail);
            Find<IntercomSituation>().AnswerIntercom();
            Find<DeskPhone>().CallServiceDesk();
            Check("intercom == VerifiedExternally via the IT service desk", GameState.Current.intercom == IntercomResponse.VerifiedExternally);
            if (!Find<EmailQrSituation>().Resolved)
            {
                yield return Await(Find<EmailQrSituation>(), null);
                Find<EmailQrSituation>().Forward();
            }
            Log("completionOrder: " + string.Join(", ", GameState.Current.completionOrder));
            Check("completion order starts Workstation, Colleague",
                GameState.Current.completionOrder.Take(2).SequenceEqual(new[] { SituationId.Workstation, SituationId.Colleague }));
            yield return CheckOut(EndingFlow.AnswerC);
            yield return EndingChecks(EndingType.SocialMiscalibration, expectedScore: -1, shots: true);
        }

        private IEnumerator IdleRun()
        {
            yield return StartShift();
            yield return SkipTo(15f * 60f + 1f);
            Check("lockout reached", Director.LockedOut);
            Check("USB dropped at the 150 s fallback", Find<UsbSituation>().Triggered);
            Check("colleague asked by the 180 s fallback", Find<ColleagueSituation>().Triggered);
            Check("intercom rang by the 360 s fallback", Find<IntercomSituation>().Triggered);
            Check("unanswered intercom stays NotReached", GameState.Current.intercom == IntercomResponse.NotReached);
            Check("MFA fired by the 600 s fallback", Find<MfaSituation>().Triggered);
            Check("unanswered MFA == Ignored", GameState.Current.mfa == MfaResponse.Ignored);
            Check("workstation untouched stays NotReached", GameState.Current.workstation == WorkstationResult.NotReached);
            var term = Find<EndingFlow>().Terminal;
            term.Choose("Continue");
            term.Choose(EndingFlow.AnswerD);
            term.Choose("Continue to incident report");
            term.Choose("Submit report");
            yield return EndingChecks(EndingType.FullContainment, expectedScore: -1, shots: false);
            Check("USB left lying = Ignored at the end", GameState.Current.usb == UsbAction.Ignored);
        }

        // ------------------------------------------------------------------ situation helpers

        /// <summary>
        /// Waits for <paramref name="target"/> to get the floor. Several situations can be ready at
        /// once and which one the director grants first is not fixed by the spec, so if another one
        /// gets there first it is resolved with <paramref name="resolveOther"/> and we try again.
        /// </summary>
        private IEnumerator Await(SituationTrigger target, Func<SituationTrigger, IEnumerator> resolveOther)
        {
            for (int attempt = 0; attempt < 4 && !target.Triggered; attempt++)
            {
                Director.ClearCooldown();
                yield return WaitUntil(() => target.Triggered || (Director.Active != null && Director.Active != target), 3f);
                var other = Director.Active;
                if (!target.Triggered && other != null && other != target)
                {
                    Log($"  note: {other.Id} got the floor before {target.Id}");
                    if (resolveOther == null)
                    {
                        break;
                    }
                    yield return resolveOther(other);
                }
            }
            Check($"{target.Id} triggered", target.Triggered);
        }

        private IEnumerator BuzzInIfIntercomOrUnlockIfColleague(SituationTrigger other)
        {
            if (other is IntercomSituation intercom)
            {
                intercom.AnswerIntercom();
                intercom.BuzzIn();
            }
            else if (other is ColleagueSituation colleague)
            {
                colleague.UnlockDoor();
            }
            yield return null;
        }

        private IEnumerator RefuseIntercom(SituationTrigger other)
        {
            if (other is IntercomSituation intercom)
            {
                intercom.AnswerIntercom();
                intercom.Refuse();
            }
            yield return null;
        }

        private IEnumerator ForwardEmail(SituationTrigger other)
        {
            if (other is EmailQrSituation email)
            {
                email.Forward();
            }
            yield return null;
        }

        private IEnumerator CheckOut(string answer)
        {
            yield return WaitUntil(() => Find<EndingFlow>().Active, 2f);
            var term = Find<EndingFlow>().Terminal;
            Check("start check-out", term.Choose("Start check-out"));
            term.Choose("Continue");
            term.Choose(answer);
            term.Choose("Continue to incident report");
            Check("submit report", term.Choose("Submit report"));
        }

        private IEnumerator EndingChecks(EndingType expected, int expectedScore, bool shots)
        {
            yield return null;
            var flow = Find<EndingFlow>();
            Check($"ending == {expected} (got {flow.Result?.Ending})", flow.Result != null && flow.Result.Ending == expected);
            Check("EndingResolver ran exactly once", flow.ResolveCount == 1);
            if (expectedScore >= 0)
            {
                Check($"score == {expectedScore} (got {flow.Score?.Total})", flow.Score != null && flow.Score.Total == expectedScore);
            }
            Log($"Score {flow.Score?.Total}; debrief items {flow.Result?.Debrief.Count}");
            Check("session log written", flow.SessionLogPath != null && File.Exists(flow.SessionLogPath));
            if (flow.SessionLogPath != null && File.Exists(flow.SessionLogPath))
            {
                string json = File.ReadAllText(flow.SessionLogPath);
                Check("session log has decisions", json.Contains("\"decision\""));
            }
            var mainDoor = GameObject.Find("MainDoor")?.GetComponent<SlidingDoor>();
            Check("exit (main door) unlocked", mainDoor != null && mainDoor.IsOpen);
            var screen = Find<EndingScreen>();
            Check("ending screen visible", screen.Visible);
            yield return new WaitForSeconds(0.2f);
            var cam = Camera.main.transform;
            if (shots)
            {
                Shot($"ending_{expected}_page1", cam.position, screen.transform.position, hud: true);
            }
            Layout("ending page 1");
            for (int p = 1; p < screen.PageCount; p++)
            {
                screen.Next();
                yield return null;
                if (shots && p <= 2)
                {
                    Shot($"ending_{expected}_debrief{p}", cam.position, screen.transform.position);
                }
                Layout($"ending debrief page {p}");
            }
        }

        // ------------------------------------------------------------------ helpers

        private static readonly Vector3 OfficeSpawn = new Vector3(3f, 0f, 2.35f);

        private GameDirector Director => GameDirector.Instance;

        private static T Find<T>() where T : UnityEngine.Object => FindFirstObjectByType<T>();

        private IEnumerator StartShift()
        {
            Find<WelcomePanel>().Begin();
            yield return null;
            Check("shift started", Director.Running);
        }

        private IEnumerator SkipTo(float seconds)
        {
            while (Director.Elapsed < seconds)
            {
                Director.SkipSeconds(Mathf.Min(2f, seconds - Director.Elapsed));
                yield return null;
            }
        }

        /// <summary>Moves the rig like a player walking (small steps every frame).</summary>
        private static IEnumerator Walk(Vector3 direction, float metres)
        {
            var rig = FindFirstObjectByType<OVRCameraRig>();
            for (float d = 0f; d < metres; d += 0.03f)
            {
                rig.transform.position += direction.normalized * 0.03f;
                yield return null;
            }
            yield return null;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float t = 0f;
            while (!condition() && t < timeout)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>Teleports the rig so the head stands at <paramref name="stand"/>, facing <paramref name="lookAt"/>.</summary>
        private static IEnumerator MoveTo(Vector3 stand, Vector3 lookAt)
        {
            var rig = FindFirstObjectByType<OVRCameraRig>();
            var eye = rig.centerEyeAnchor;
            Vector3 dir = lookAt - stand;
            dir.y = 0f;
            float eyeYaw = eye.eulerAngles.y - rig.transform.eulerAngles.y;
            rig.transform.rotation = Quaternion.Euler(0f, Quaternion.LookRotation(dir).eulerAngles.y - eyeYaw, 0f);
            yield return null;
            Vector3 offset = eye.position - rig.transform.position;
            offset.y = 0f;
            rig.transform.position = new Vector3(stand.x - offset.x, 0f, stand.z - offset.z);
            yield return null;
            yield return null;
        }

        /// <summary>Points the head at a target for a while (pitch via the rig, as a seated user tilting their head).</summary>
        private IEnumerator LookAt(Vector3 target, float seconds)
        {
            var rig = FindFirstObjectByType<OVRCameraRig>();
            var eye = rig.centerEyeAnchor;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                // Rotate the rig so the (tracked) eye ends up looking straight at the target,
                // keeping the eye where it is - like the player turning their head.
                Vector3 eyePos = eye.position;
                var eyeRelative = Quaternion.Inverse(rig.transform.rotation) * eye.rotation;
                rig.transform.rotation = Quaternion.LookRotation(target - eyePos) * Quaternion.Inverse(eyeRelative);
                rig.transform.position += eyePos - eye.position;
                yield return null;
            }
            var cam = Camera.main.transform;
            Log($"  gaze: camera '{cam.name}' at {cam.position}, {Vector3.Angle(cam.forward, target - cam.position):0.0} deg off target, " +
                $"{Vector3.Distance(cam.position, target):0.00} m away");
            rig.transform.rotation = Quaternion.Euler(0f, rig.transform.eulerAngles.y, 0f);
        }

        private IEnumerator HoldPhoneUp()
        {
            var phone = Find<PhoneDevice>();
            var cam = Camera.main.transform;
            phone.GetComponent<Rigidbody>().isKinematic = true;
            Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            phone.transform.position = cam.position + fwd * 0.33f + Vector3.down * 0.12f;
            phone.transform.rotation = Quaternion.LookRotation(phone.transform.position - cam.position);
            yield return null;
        }

        private void Shot(string name, Vector3 from, Vector3 lookAt, bool hud = false)
        {
            var go = new GameObject("PlaytestCam");
            var cam = go.AddComponent<Camera>();
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 600f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.transform.position = from;
            cam.transform.LookAt(lookAt);
            if (hud)
            {
                ShiftHud.Instance?.SnapTo(cam.transform);
            }
            const int w = 1600, h = 900;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes($"{OutDir}/{++_shot:00}_{name}.png", tex.EncodeToPNG());
            cam.targetTexture = null;
            Destroy(rt);
            Destroy(tex);
            Destroy(go);
            if (hud && Camera.main != null)
            {
                ShiftHud.Instance?.SnapTo(Camera.main.transform);
            }
        }

        /// <summary>Every visible panel: text inside its panel, big enough to read at its distance, buttons not overlapping.</summary>
        private void Layout(string stage)
        {
            int problems = 0;
            foreach (var pb in FindObjectsByType<UiPanelBounds>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Vector2 half = pb.size / 2f + new Vector2(0.006f, 0.006f);
                foreach (var tmp in pb.GetComponentsInChildren<TextMeshPro>(false))
                {
                    if (tmp.GetComponentInParent<UiPanelBounds>() != pb || string.IsNullOrWhiteSpace(tmp.text))
                    {
                        continue;
                    }
                    tmp.ForceMeshUpdate();
                    var b = tmp.textBounds;
                    if (b.size.sqrMagnitude < 1e-8f)
                    {
                        continue;
                    }
                    Vector3 a = pb.transform.InverseTransformPoint(tmp.transform.TransformPoint(b.min));
                    Vector3 c = pb.transform.InverseTransformPoint(tmp.transform.TransformPoint(b.max));
                    float minX = Mathf.Min(a.x, c.x), maxX = Mathf.Max(a.x, c.x), minY = Mathf.Min(a.y, c.y), maxY = Mathf.Max(a.y, c.y);
                    string where = $"{Path(pb.transform)} '{Short(tmp.text)}'";
                    if (minX < -half.x || maxX > half.x || minY < -half.y || maxY > half.y)
                    {
                        problems++;
                        Log($"  LAYOUT [{stage}] text outside panel: {where} x[{minX:0.000},{maxX:0.000}] y[{minY:0.000},{maxY:0.000}] half {half}");
                    }
                    bool isButton = tmp.GetComponentInParent<PokeButton3D>() != null;
                    if (isButton && tmp.isTextOverflowing)
                    {
                        problems++;
                        Log($"  LAYOUT [{stage}] button label overflows: {where}");
                    }
                    float em = tmp.fontSize * 0.1f * tmp.transform.lossyScale.y;
                    float deg = Mathf.Atan2(em, pb.viewDistance) * Mathf.Rad2Deg;
                    bool smallPrintByDesign = tmp.text.Contains("nexora-auth.com");
                    if (!smallPrintByDesign && deg < _minDegrees)
                    {
                        _minDegrees = deg;
                        _minDegreesWhere = $"[{stage}] {where}";
                    }
                    if (!smallPrintByDesign && deg < MinTextDegrees)
                    {
                        problems++;
                        Log($"  LEGIBILITY [{stage}] {deg:0.00} deg < {MinTextDegrees}: {where}");
                    }
                }

                var buttons = pb.GetComponentsInChildren<PokeButton3D>(false).Where(x => x.GetComponentInParent<UiPanelBounds>() == pb).ToArray();
                for (int i = 0; i < buttons.Length; i++)
                {
                    for (int j = i + 1; j < buttons.Length; j++)
                    {
                        if (Rect(buttons[i]).Overlaps(Rect(buttons[j])))
                        {
                            problems++;
                            Log($"  LAYOUT [{stage}] buttons overlap: '{buttons[i].Label}' / '{buttons[j].Label}' in {Path(pb.transform)}");
                        }
                    }
                }
            }
            Check($"layout + legibility clean: {stage}", problems == 0);
        }

        private static Rect Rect(PokeButton3D b)
        {
            var v = b.transform.Find("Visual");
            Vector2 size = new Vector2(v.localScale.x, v.localScale.y) - new Vector2(0.002f, 0.002f);
            Vector2 p = b.transform.localPosition;
            return new Rect(p - size / 2f, size);
        }

        private static string Short(string s)
        {
            s = s.Replace("\n", " ");
            return s.Length > 40 ? s.Substring(0, 40) + "..." : s;
        }

        private static string Path(Transform t)
        {
            var parts = new List<string>();
            for (int i = 0; t != null && i < 4; i++, t = t.parent)
            {
                parts.Insert(0, t.name);
            }
            return string.Join("/", parts);
        }

        private void Check(string what, bool ok)
        {
            _checks++;
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
            _report.AppendLine($"Smallest text found: {_minDegrees:0.00} deg at {_minDegreesWhere}");
            _report.AppendLine($"Errors/warnings logged during play: {_errors.Count}");
            foreach (var e in _errors.Distinct().Take(60)) _report.AppendLine("  " + e);
            _report.AppendLine(_failures == 0 ? $"RESULT: ALL {_checks} CHECKS PASSED" : $"RESULT: {_failures} OF {_checks} CHECK(S) FAILED");
            File.WriteAllText($"{OutDir}/report.txt", _report.ToString());
            File.Delete(ActiveFlag);
            UnityEditor.EditorApplication.ExitPlaymode();
        }
    }
}
#endif
