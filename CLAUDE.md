# CLAUDE.md — CyberQuest: The Compromised Office (v2)

Read this fully at the start of every session. It is the source of truth. If a request
conflicts with it, say so and ask before deviating.

---

## 1. Overview

- VR cybersecurity-awareness escape room. ~15 minutes. Quest standalone. Single player.
- Unity 6 LTS, URP, Meta XR All-in-One SDK (Interaction SDK for grab, ray, poke, teleport).
- Research question: "How can a Virtual Reality social-engineering simulation improve
  non-technical employees' threat-recognition ability for organisations like NEXORA
  Corporation?"
- The player is a new NEXORA employee, alone in the office from 5:45 PM to 6:30 PM.
  The attacker never hacks the machine. They manipulate the player through an email, a
  QR code, a planted USB, a colleague-style chat message, a door intercom, and an MFA
  prompt.
- VR must be load-bearing: spatial search, grabbing and inspecting objects, walking to
  terminals, reading small print up close, and pressure from events happening around you.

## 2. Hard rules

1. **NO human characters.** No human models, rigs or animations anywhere. People exist
   only as text, subtitles, chat messages, caller labels, beeps, and 2D ID-card images
   shown on screens.
2. **Primitive-first art.** The only imported art/audio packs (the Meta SDK is separate) are: VNB Low-Poly Office Set #1,
   Kenney UI Audio, Kenney Digital Audio. The USB, phone, intercom, badge, sticky notes,
   directory board, door panels and all UI are built from primitives, TextMeshPro and
   generated textures. Placeholder-quality art is acceptable.
3. **One architecture:** GameState + independent situation triggers + EndingResolver
   (Sections 4 and 6). Never build a fixed-sequence controller.
4. Every safe choice has a cost (time or social friction), and one situation is a
   **false positive** (a legitimate request that looks suspicious).
5. **Multiple rooms.** The game is one Unity scene but a whole office floor (Section 5.0),
   not one room. Verifying things costs a real walk, and clues are scattered across rooms.

## 3. Premise and clock

- Game time runs 5:45 PM to 6:30 PM, mapped to 15 real minutes (configurable; tune after
  playtests, because walking between rooms costs real time).
- A wall clock shows game time. At 6:30 the building locks out and the ending resolves
  from whatever state exists.
- Opening: the desk phone rings. Player picks it up (teaches grab). Subtitled manager
  voicemail: "Sorry to dump this on you. Client report just needs the final numbers
  dropped in and sent. Building maintenance starts at 6:30, so head out before then."
  Objective shown on the monitor: *Finish the client report. Leave before 6:30.*

## 4. Architecture

### 4.1 GameState (plain C#, single source of truth, enums not strings)

```csharp
public enum LoginResult        { NotReached, Reported, Closed, Entered }
public enum UsbAction          { NotReached, Reported, Plugged, Ignored }
public enum ColleagueResponse  { NotReached, VerifiedThenHelped, UnlockedBlindly, RefusedOutright }
public enum IntercomResponse   { NotReached, VerifiedExternally, RefusedWithoutVerifying, BuzzedIn }
public enum MfaResponse        { NotReached, DeniedAndReported, Denied, Ignored, Approved }
public enum WorkstationResult  { NotReached, Reported, Locked, Ignored }
public enum SituationId        { EmailQr, FakeLogin, Usb, Colleague, Intercom, Mfa, Workstation }

public class GameState
{
    public bool emailOpened, emailForwardedToSecurity, qrScanned;
    public LoginResult login;
    public UsbAction usb;
    public ColleagueResponse colleague;
    public bool colleagueDirectoryChecked;
    public IntercomResponse intercom;
    public bool intercomBadgeInspected, intercomManagerEmailChecked, maintenanceNoticeRead;
    public MfaResponse mfa;
    public WorkstationResult workstation;
    public bool policyBoardRead;   // start-of-shift orientation, research data only
    public bool deductionCorrect, reportFiled;
    public float gameMinutesAtEnd;
    public List<SituationId> completionOrder = new();   // actual order, for research data

    // Compromise sources: login==Entered, usb==Plugged, intercom==BuzzedIn, mfa==Approved
    public int CompromiseSources { get; }
    public bool CredentialsCompromised => CompromiseSources > 0;
}
```

Every choice writes to this object. Nothing else tracks "what step are we on".

### 4.2 Situation triggers (no sequence controller)

Each situation is its own `SituationTrigger` MonoBehaviour with configurable conditions:
earliest time, fallback time, requires-any-N-situations-completed, optional spatial
trigger. Conditions are evaluated independently.

### 4.3 GameDirector

Owns the game clock. Allows only **one scheduled situation active at a time**, enforces
a **45-second cooldown** between situations, and triggers lockout at 6:30. The USB drop
and the unlocked workstation are physical objects and never block the director.

## 5. Situations

### 5.0 Floor plan (one Unity scene, about 24 m x 16 m, 3 m ceilings, six spaces off a corridor)

- **Open-plan office** (south-west, spawn): player desk with monitor, desk phone, phone
  cradle, sticky note, wall clock; an empty desk 3 m east (USB event); the company policies
  board on the north wall beside the corridor doorway.
- **Corridor**: 2 m wide L-shaped spine linking every space. Holds the Meeting Room 3 door
  panel and the "Found devices" drop box.
- **Lobby** (east): main door (the exit), intercom panel, security terminal, and the wall
  directory board (lists IT INFRASTRUCTURE, the service desk extension, and Priya's entry).
- **Break room** (south-east): bulletin board with the maintenance notice, vending machine,
  ambient hum.
- **Finance pod** (north-west): small room with the colleague desk and the unlocked
  workstation (Situation 7).
- **Meeting Room 3** (north): glass door, locked until unlocked from the corridor panel.
  Contains Priya's charger and a whiteboard.
- **IT room** (north-east): permanently locked. Door sign reads **IT INFRASTRUCTURE**. The
  "Found devices" drop box is beside it.
- Doorways are open openings (no hinged doors) except Meeting Room 3 (glass, unlockable),
  the IT room (locked) and the main door.
- Every walkable floor, corridor included, is a teleport surface. Walls and closed doors
  are colliders.
- Travel between the two farthest spaces should take about 15 to 25 seconds by teleport:
  exploring costs real time without becoming tedious.

### 5.1 Email and QR  (SituationId.EmailQr)
- Trigger: available from spawn.
- Email on the desk monitor. From: `finance@nexora.com`. Subject: *Updated client invoice*.
  Body: "Hi, please review the attached invoice before sending the report. Thanks."
  Attachment `Invoice_0924.pdf` opens an invoice page with a QR labelled "Scan to verify
  payment status".
- Clue: inspecting the sender detail reveals the real address `finance@nexora-invoices.net`.
- Options: scan the QR with the grabbable phone (hold within 0.4 m of the QR), ignore it,
  or press **Forward to IT-Security** (auto-reply "Thanks, we're reviewing. Continue your
  task normally." This defuses the situation; the fake site is never seen).

### 5.2 Fake login  (SituationId.FakeLogin)
- Trigger: only after the QR is scanned.
- Phone browser shows a Nexora-styled login. Address bar reads `nexora-auth.com` in text
  small enough to need bringing the phone close. The sticky note on the monitor reads
  `portal.nexora.com`.
- Options: Log in / Close / Report as phishing.
- **Entered:** "Authenticating…" then a normal portal. About 30 s later the monitor
  flickers and a process window flashes, then returns to normal (silent compromise,
  revealed in debrief).

### 5.3 USB  (SituationId.Usb)
- Trigger: player comes within 2 m of the empty desk, or fallback at 150 s real time.
- A drive skitters out from under the desk with a spatial thud. Generated label:
  **CLIENT DATA — URGENT**. Grabbable and rotatable.
- While the player holds it, one manager chat nudge appears: "Almost done? Client's asking."
- Options: plug into the PC port (socket zone), carry it to the "Found devices" drop box
  outside the IT room (a real walk through the corridor), or leave it.
- **Plugged:** fake "installing update" bar, then normal. Silent compromise.

### 5.4 Colleague request, the false positive  (SituationId.Colleague)
- Trigger: 180 s real time, or after 1 situation completed. Respects the cooldown.
- Chat on the monitor from verified internal account **Priya Nair — Finance** (initials
  avatar, no portrait). Message: "Hi, I didn't realise anyone was still in. I left my
  charger in Meeting Room 3 and my badge won't open it after hours. Could you unlock it
  from the door panel?"
- Directory entry (inspectable) on the lobby wall board: *Priya Nair, Finance, ext 2147,
  after-hours access approved today*. Inspecting it sets `colleagueDirectoryChecked`, so
  verifying means walking to the lobby. The Meeting Room 3 door panel is in the corridor;
  unlocking it opens the glass door.
- She is **legitimate**. Nothing in the UI may imply otherwise.
- Options: unlock now without checking (UnlockedBlindly), check the directory then unlock
  (VerifiedThenHelped), refuse or "ask security" (RefusedOutright, flagged as over-caution).

### 5.5 Intercom visitor, the impersonator  (SituationId.Intercom)
- Trigger: 360 s real time, or after 2 situations completed. Respects the cooldown.
- The door buzzer rings (spatial audio, audible in every room) and repeats every 10 s. The
  situation starts when the player reaches the lobby intercom (within 2 m). If nobody
  answers within 120 s the visitor leaves and `intercom` stays `NotReached`.
- The intercom shows a static "camera" screen and subtitled lines:
  "Hi, infrastructure here. We're seeing auth errors ahead of the maintenance window. Can
  you buzz me up? I'll need to sit at your machine for two minutes."
- Pressure escalates: at +20 s "Your manager already knows. I emailed the team this
  morning." At +40 s "If I can't get in before 6:30 this gets logged as a blocked fix,
  and it'll be on you." No such email exists in the inbox (player can check it, which
  sets `intercomManagerEmailChecked`). Pressure lines also arrive as phone toasts, so
  walking away does not dodge them.
- Options: buzz him in / show badge to camera / look up IT in the directory / call the IT
  service desk from the desk phone (it is in the open-plan office, so this costs a walk) /
  refuse and tell him to email / "Tell him the maintenance notice lists no IT work tonight"
  (only offered if `maintenanceNoticeRead`; counts as VerifiedExternally).
- Badge image on the intercom screen: *NEXORA — IT SERVICES — D. MERCER — ID 40-7731*.
  Directory board lists the department as **IT INFRASTRUCTURE** and the service desk
  extension. Never state the mismatch outright.
- Calling the service desk: automated line "No engineers have been dispatched tonight."
  Intercom goes quiet and a "Visitor has left" notice appears (VerifiedExternally).
- **BuzzedIn:** door unlocks, then a fake remote-session window flashes on the monitor
  and disappears. Silent compromise.

### 5.6 MFA push attack  (SituationId.Mfa)
- Trigger: after `login==Entered` or `intercom==BuzzedIn`, otherwise fallback at 600 s real time.
- Phone buzzes three times (spatial audio plus controller haptics on the holding hand):
  "Approve sign-in to your Nexora account? Location: unknown."
- Options on the phone: Approve / Deny / Deny and report. 60 s after the first buzz with no
  answer = Ignored. The buzz is spatial, so a phone left in another room must be found.
- If credentials are already compromised, pushes repeat faster and the copy is more urgent.

### 5.7 Unlocked workstation, optional  (SituationId.Workstation)
- Always present from spawn. Not director-scheduled. Never blocks anything.
- Located in the Finance pod, a separate room. Colleague monitor is unlocked on "Q3 payroll.xlsx". A sticky note under the keyboard
  reads `pw: Summer2024!`.
- Options: lock the screen / take the note and report to security / ignore.
- `NotReached` if the player never inspects it.

### 5.8 Final deduction and escape
- The security terminal (in the lobby) activates once 3 situations are completed, or at lockout.
- Screen builds a timeline from only the situations that actually occurred, then asks
  "What's actually happening here?": A) System maintenance glitches, B) One employee's
  mistake, C) A coordinated attempt to get into company systems, D) Nothing, a normal
  Tuesday. Correct is C. A wrong answer triggers a short in-world correction, not a fail.
- A short incident report form (tick what happened) sets `reportFiled`, the exit unlocks,
  and the ending screen shows.

### 5.9 Exploration, clues and room tracking
- Clues are never pointed out by UI. They are: the email sender detail, the sticky-note URL,
  the lobby directory board, the IT room door sign (IT INFRASTRUCTURE), and the break-room
  maintenance notice ("Building maintenance 6:30 PM. Plant-room contractors only. No IT work
  scheduled tonight." plus "IT service desk ext 2100"). Reading the notice (grab-and-read or
  1.5 s gaze) sets `maintenanceNoticeRead`.
- `RoomTracker` uses trigger volumes for OpenPlan, Corridor, Lobby, BreakRoom, FinancePod,
  MeetingRoom3 and ItRoomDoor, and logs entry, exit and dwell time per room to SessionLog.
  This is research data only and is never scored.
- **Company policies (orientation).** The welcome screen and a policies board in the open plan
  tell the player where the staff directory (lobby board) and the FOUND DEVICES box (corridor,
  outside the IT room) are, and when the floor locks. They state where resources are, never
  anything about tonight's situations. Reading the board (1.5 s gaze) sets `policyBoardRead`.
- **Research telemetry.** `PhysicalSecurityTelemetryLogger` (Core) writes, per session:
  `session_summary.csv`, `security_audit_real_trials.csv` (course scaffold column names) and an
  `audit_<participant>_<session>.csv` event trail to `persistentDataPath/telemetry`. Challenge
  latency = t_decision - t_request; breach rate = CompromiseSources / attack situations reached.
  Participant IDs are anonymised (P01...). Editor sessions are tagged and excluded from analysis.

## 6. Endings and resolver

`EndingResolver.Resolve(GameState)` runs exactly once. Priority, highest first:

1. **Breach**: `CompromiseSources >= 2`, OR `mfa == Approved`, OR (`CompromiseSources >= 1` AND `mfa == Ignored`).
2. **Contained but Exposed**: `CompromiseSources == 1` (the final attack was stopped, but an earlier mistake is revealed in the debrief).
3. **Social Miscalibration**: `CompromiseSources == 0` AND `colleague == RefusedOutright`.
4. **Full Containment**: everything else.

Debrief lists, per situation reached: what the player did, why it mattered, and what to do
next time. If `reportFiled == false`, add an "incident was never reported" note.

## 7. Scoring

Five categories at 20% each. Unreached situations are excluded and the remaining weights
are renormalised. Score is computed once at the end, never shown live.

| Category | Values |
|---|---|
| Phishing (email, QR, login) | Reported or forwarded 1.0 / ignored 0.75 / closed 0.5 / entered 0 |
| Physical security (USB, workstation, averaged if both reached) | USB: reported 1.0 / ignored 0.5 / plugged 0. Workstation: reported 1.0 / locked 0.75 / ignored 0 |
| Social calibration (colleague) | VerifiedThenHelped 1.0 / UnlockedBlindly 0.5 / RefusedOutright 0.5 |
| Impersonation (intercom) | VerifiedExternally 1.0 / RefusedWithoutVerifying 0.75 / BuzzedIn 0 |
| MFA | DeniedAndReported 1.0 / Denied 0.75 / Ignored 0.25 / Approved 0 |

Display the ending tier plus the per-category breakdown, not just one number.

## 8. Adaptive replay (rule-based, no ML)

`PlayerProfile` is saved as JSON in `Application.persistentDataPath` after each session.
Scenario text comes from a `ScenarioVariant` data object chosen at session start:

- Phishing caught last time: email uses a subtler lookalike domain and a manager name in
  the signature. Phishing failed last time: more obvious domain and typos.
- Impersonation caught last time: visitor uses a named-colleague pretext ("Hi, I'm Daniel.
  Sarah in Finance asked me to check your workstation."). Failed last time: clearer
  mismatch on the badge.
- USB plugged last time: label becomes more overtly suspicious ("VIRUS TEST — DO NOT OPEN").
- Over-cautious last time: colleague request adds the manager in cc and a clearer
  directory approval note.

## 9. Tech stack

- Unity 6 LTS, URP (mobile-friendly settings), Input System, TextMeshPro.
- **Meta XR All-in-One SDK** (Core SDK + Interaction SDK) is the VR stack. Use the Unity
  OpenXR plugin as the XR provider unless the SDK's current docs say otherwise. Use
  OVRCameraRigInteraction (or the equivalent rig prefab in the installed version) and the
  Interaction SDK's grab, ray, poke and teleport components. **Do not add Unity XR
  Interaction Toolkit.**
- Building Blocks and Quick Actions are editor GUI tools. They are used ONCE by the user to
  create template prefabs in `Prefabs/Templates/` (PlayerRig, GrabbableTemplate,
  InteractiveCanvasTemplate). All game objects are then created in code from those
  templates via factory scripts.
- Meta XR Simulator for headset-free testing. Every player choice must also be exposed as
  a public method so tests and the debug window can drive it without XR input.
- Haptics through the Meta haptics API available in the installed SDK (OVRInput vibration
  if no Haptics SDK). Spatial audio through the Meta XR Audio SDK if installed.
- Confirm exact package, prefab and component names by reading the installed packages.
  Never guess them from memory.
- Teleport default, snap turn on, continuous move as a toggle with a tunnelling vignette.
- Unity Test Framework for EditMode and PlayMode tests.
- Multi-room performance: bake lighting, use light probes and baked occlusion culling,
  static batching for office props, one shadow-casting light at most. Report draw calls
  and triangle counts in Prompt 16.
- C#: one class per file, PascalCase types and methods, camelCase fields. Editor-only code
  lives in `Editor/` folders.

## 10. Folder structure

```
Assets/_Project/
  Scripts/  Core/  Situations/  UI/  Editor/
  Scenes/   MainMenu.unity  Office_Main.unity
  Prefabs/  Audio/  Art/
  Tests/    EditMode/  PlayMode/
```

## 11. Working rules

1. After every change, run a batch-mode compile check using the Unity path in
   `UNITY_PATH.txt`, and fix errors before reporting done. (Batch mode fails if the
   project is open in the Editor. Tell the user to close it.)
2. Run EditMode and PlayMode tests. Never report a stage done with failing tests.
3. One git commit per stage: `stage N: <summary>`.
4. Do not rewrite earlier stages except to integrate. List every file touched.
5. No new third-party packages or assets without asking.
6. Anything that needs a GUI click, headset testing, or a manual download: list it back
   to the user as a short numbered "manual steps" list instead of guessing.
7. No new endings or situations without updating this file first.
