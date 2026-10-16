# CyberQuest — Game Flow Reference (for humans and LLMs debugging the code)

Unity 6000.6 / URP / Meta XR SDK (Interaction SDK v207). VR security-awareness game, one scene:
`Assets/_Project/Scenes/Office_Main.unity`, generated entirely by code
(`Assets/_Project/Scripts/Editor/OfficeSceneBuilder.cs`, menu `CyberQuest > 2. Build Office Scene`).
Design source of truth: `CLAUDE.md` (project root). Never edit the scene by hand; edit the builder and rebuild.

---

## 1. Architecture in one paragraph

All player choices are written to ONE plain C# object, `GameState.Current`
(`Scripts/Core/GameState.cs`). Each of the 7 situations is an independent `SituationTrigger`
MonoBehaviour (`Scripts/Core/SituationTrigger.cs`, subclasses in `Scripts/Situations/`) that decides
for itself when it is ready. `GameDirector` (`Scripts/Core/GameDirector.cs`) owns the clock and only
lets ONE "scheduled" situation be active at a time, with a 45 s cooldown between them. There is NO
fixed sequence controller — order emerges from trigger conditions. At the end, `EndingFlow`
(`Scripts/Situations/EndingFlow.cs`) calls `EndingResolver.Resolve` exactly once and
`ScoreCalculator.Compute`, then shows `EndingScreen`.

```
WelcomePanel.START SHIFT -> ShiftStart.Begin() -> GameDirector.StartShift() -> DeskPhone rings
        |
        v   every frame, each SituationTrigger.Update():
   ConditionsMet()?  --no-->  wait
        | yes
   directorScheduled? --yes--> GameDirector.TryBegin(this)  (fails if another is Active or cooldown running)
        | granted / not scheduled
   OnTriggered()  -> situation shows its UI
        |
   player choice -> writes GameState field(s) -> Complete("decision")
        -> GameState.MarkCompleted(id) + SessionLog.Decision + Director.Release (starts 45 s cooldown)
        |
   EndingFlow: activates when CompletedCount >= 3 OR lockout (6:30)
   -> timeline -> deduction question -> incident report -> EndingResolver.Resolve (once) -> EndingScreen
```

---

## 2. Clock and pacing (`GameDirector`)

| Setting | Value |
|---|---|
| Game time | 5:45 PM -> 6:30 PM (45 game minutes) |
| Real duration | 15 real minutes (1 real min = 3 game min). `realMinutes` field |
| Clock starts | when START SHIFT is pressed (`StartShift()`), not on scene load |
| One scheduled situation at a time | `Active` holds it; `TryBegin` refuses others |
| Cooldown | 45 s after any scheduled situation completes (`cooldownSeconds`) |
| Lockout | at 6:30 (`Elapsed >= 900 s`): `LockedOut = true`, `LockedOutEvent` fires, no new situations start |
| Debug helpers | `SkipSeconds(s)`, `ClearCooldown()`, `ForceBegin()` (tests only) |

### Trigger rule (`SituationTrigger.ConditionsMet`)
Ready when `elapsed >= earliestTime` AND (`fallbackTime` reached OR every configured primary condition holds).
Primary conditions: `requiresCompleted` (count of completed situations), `spatialTarget` (player head within
`spatialRadius`, horizontal), and the subclass `CustomCondition()`. If none are configured, ready at `earliestTime`.
`releaseAfter`: if a scheduled situation is undecided for that long, the director slot is released (situation stays open).

### Configured triggers (set in `OfficeSceneBuilder.BuildGameplay`)

| Situation (`SituationId`) | Class | Ready when | Scheduled? | Notes |
|---|---|---|---|---|
| EmailQr | `EmailQrSituation` | 20 s after start | yes | release after 120 s |
| FakeLogin | `FakeLoginSituation` | `qrScanned == true` | yes, **ignores cooldown** | opens instantly after scan |
| Usb | `UsbSituation` | player within 2 m of empty desk (6.2, 3) OR 150 s | **no** (physical) | never blocks director |
| Colleague | `ColleagueSituation` | 1 completed OR 180 s | yes | release after 150 s |
| Intercom | `IntercomSituation` | 2 completed OR 360 s | yes | manages own timeouts |
| Mfa | `MfaSituation` | `login==Entered` OR `intercom==BuzzedIn` OR 600 s | yes | manages own 60 s window |
| Workstation | `WorkstationSituation` | player within 1.6 m of Finance-pod monitor | **no** (optional) | never blocks director |

When several are ready at the same frame, whichever `Update()` runs first gets the floor (by design; the
spec allows any order). The automated test handles both orders.

---

## 3. Map (x = east, z = north, metres; spawn at (3, 0, 2.35) facing north)

| Space | Bounds x / z | What's there |
|---|---|---|
| Open plan (spawn) | 0–10 / 0–8 | Player desk: monitor (Report/Mail/Chat), desk phone, work phone (on mat), USB dock, sticky note "portal.nexora.com". Empty desk 3 m east (USB drop). |
| Corridor (L-shaped) | 0–18 / 8–10 and 10–12 / 2–8 | Meeting Room 3 door panel (x≈8.75), "Found devices" drop box (x≈15.3), IT room door + sign "IT INFRASTRUCTURE" (x=16.5) |
| Break room | 12–18 / 0–8 | Bulletin board with MAINTENANCE NOTICE (east wall, z≈4.8) |
| Lobby | 18–24 / 0–10 | Main door/exit (x=24, z=5), intercom (x≈23.9, z≈7.6), security terminal (x≈20.6, z≈3.6), directory board (north wall, x≈21) |
| Finance pod | 0–6.5 / 10–16 | Colleague's unlocked PC + password sticky note (Situation 7) |
| Meeting Room 3 | 6.5–13.5 / 10–16 | Glass door (locked until unlocked from corridor panel), Priya's charger |
| IT room | 13.5–20 / 10–16 | Locked, not enterable |

Walls, glass, closed doors and furniture are on layer `Environment`: they block the teleport arc
(`TeleportInteractable.AllowTeleport = false`) and walking (`Scripts/Player/HeadBlocker.cs`, capsule cast).

---

## 4. Full sequence, situation by situation (every click and where it leads)

### 0. Opening
1. Spawn facing the desk. `WelcomePanel` shows story + controls. **START SHIFT** -> `ShiftStart.Begin()`:
   clock starts, monitor objective set, `DeskPhone.StartRinging()`.
2. Desk phone panel shows **Answer** (or grab the handset). -> `DeskPhone.Answer()` -> subtitled voicemail from
   Dan Whitfield (manager): finish the client report, leave before 6:30. Objective: "Finish the client report. Leave before 6:30."
3. Optional cover task: monitor **Report** tab -> **Insert next number** x3 -> **Send report** (`ReportApp`, not scored).

### 1. Invoice email + QR (EmailQr) — at 20 s
Monitor **Mail** gets "Finance – Updated client invoice" (toast + notification).
- **Open email** -> `emailOpened = true`. Shown sender: `finance@nexora.com`.
- **Sender details** -> reveals real address `finance@nexora-invoices.net` (clue; no state change).
- **Open Invoice_0924.pdf** -> invoice page with QR "Scan to verify payment status".
- Choices:
  | Action | GameState | Next |
  |---|---|---|
  | **Forward to IT-Security** | `emailForwardedToSecurity = true` | auto-reply email; situation done (fake login never appears) |
  | **Ignore it** | `emailIgnored = true` | situation done |
  | Hold the **work phone within 0.4 m of the QR** | `qrScanned = true` | situation done -> **FakeLogin starts immediately** |

### 2. Fake login on phone (FakeLogin) — only if QR scanned
Phone shows a NEXORA-style login; address bar (small text) reads `https://nexora-auth.com/verify`.
| Button | GameState | Consequence |
|---|---|---|
| **Log in** | `login = Entered` (compromise source) | "Authenticating…" then normal; ~30 s later the monitor flickers + process window (silent compromise). Unlocks MFA trigger. |
| **Close** | `login = Closed` | nothing else |
| **Report as phishing** | `login = Reported` | toast "page has been reported" |

### 3. USB drive (Usb) — walk within 2 m of the empty desk, or at 150 s
A drive labelled **CLIENT DATA – URGENT** skitters out with a thud. Grabbable (grip).
First time it's held: manager chat "Almost done? Client's asking."
| Action | GameState | Consequence |
|---|---|---|
| Put it within 0.1 m of the **USB dock** on your desk | `usb = Plugged` (compromise source) | fake "Installing update" bar |
| Drop it in the **Found devices** box (corridor, by IT room) | `usb = Reported` | box label: "Device received" |
| Leave it | `usb = Ignored` (set at the end if it dropped but was never handled) | — |

### 4. Priya's charger — the false positive (Colleague) — 1 completed situation or 180 s
Monitor **Chat**: "Priya Nair – Finance" (Verified internal account) asks you to unlock Meeting Room 3 from the corridor door panel. **She is genuine.**
- Clue: lobby **directory board** entry "Priya Nair – ext 2147 – after-hours access approved today". Looking at it for 1.5 s (`ClueReadable`) sets `colleagueDirectoryChecked = true`.
| Action | GameState |
|---|---|
| Chat **Sure, on my way** | nothing (acknowledgement only) |
| Corridor panel **Unlock door** after reading directory | `colleague = VerifiedThenHelped` (best) — glass door opens |
| Corridor panel **Unlock door** without reading it | `colleague = UnlockedBlindly` — glass door opens |
| Chat **Sorry, I can't** / **Please ask Security** | `colleague = RefusedOutright` (over-caution) |
Pressing Unlock before Priya has asked: "No pending access requests" (no effect).

### 5. Visitor at the door — the impersonator (Intercom) — 2 completed or 360 s
Door buzzer rings every 10 s (audible everywhere). Notification: "Someone is buzzing the main door (lobby)."
- Walk within **2 m of the lobby intercom** -> answered. If not answered within **120 s**: visitor leaves, `intercom` stays `NotReached`.
- Subtitles: "Hi, infrastructure here… Can you buzz me up? I'll need to sit at your machine for two minutes."
- Pressure at +20 s ("Your manager already knows. I emailed the team this morning.") and +40 s ("…it'll be on you"); also arrive as phone toasts. There is NO such email (opening Mail inbox after +20 s sets `intercomManagerEmailChecked`).
- Clues: badge says **IT SERVICES**; directory board and IT room sign say **IT INFRASTRUCTURE**; maintenance notice says **no IT work scheduled tonight** and gives IT service desk **ext 2100**.
| Action | GameState | Consequence |
|---|---|---|
| **Ask him to show his badge** | `intercomBadgeInspected = true` | badge image (IT SERVICES, D. MERCER, ID 40-7731); option removed |
| **Buzz him in** | `intercom = BuzzedIn` (compromise source) | door opens briefly; ~10 s later "Remote Desktop" window flashes on monitor. Unlocks MFA trigger. |
| **Refuse – tell him to email** | `intercom = RefusedWithoutVerifying` | visitor leaves |
| **The notice says no IT work tonight** (only appears after reading the break-room notice, `maintenanceNoticeRead`) | `intercom = VerifiedExternally` (best) | visitor leaves |
| Desk phone **Call IT service desk (2100)** while the conversation is open | `intercom = VerifiedExternally` (best) | automated line: "No engineers have been dispatched tonight." visitor leaves |
| Answer, then do nothing for 120 s | `intercom = RefusedWithoutVerifying` | visitor gives up |

### 6. MFA push attack (Mfa) — after login entered / visitor buzzed in, or 600 s
Phone buzzes 3x (spatial audio + controller haptics if held). Authenticator: "Approve sign-in to your Nexora account? Location: unknown".
If credentials are already compromised: pushes repeat every 8 s (else 20 s) with URGENT copy.
| Button | GameState |
|---|---|
| **Approve** | `mfa = Approved` (compromise source; always Breach) |
| **Deny** | `mfa = Denied` |
| **Deny and report** | `mfa = DeniedAndReported` (best) |
| no answer for 60 s | `mfa = Ignored` |

### 7. Unlocked workstation (Workstation, optional) — walk up to the Finance pod PC
PC shows "Q3 payroll.xlsx"; sticky note under the keyboard "pw: Summer2024!". Panel appears beside it.
| Button | GameState |
|---|---|
| **Lock the screen** | `workstation = Locked` |
| **Take the note and report it** | `workstation = Reported` (best) |
| **Leave it** | `workstation = Ignored` (also set at the end if inspected but no choice) |
| never go there | `NotReached` (excluded from score) |

### 8. Check-out and ending (EndingFlow) — at 3 completed situations, or at 6:30 lockout
Lobby **security terminal** (at lockout it is moved in front of the player wherever they are).
1. **Start check-out** (not shown at lockout) -> activity log of every situation that triggered, in time order -> **Continue**.
2. "What's actually happening here?" — A) maintenance glitches, B) one employee's mistake, **C) a coordinated attempt to get into company systems (correct)**, D) nothing. Sets `deductionCorrect`. Wrong answers get a correction, never a fail.
3. Incident report: tick items (optional), **Submit report** -> `reportFiled = true`.
4. `EndingResolver.Resolve` runs ONCE, score computed, session JSON written, main door unlocks, `EndingScreen` shows ending + score breakdown, then **See debrief >** pages (what you did / why it mattered / next time), **Play again** reloads the scene.

---

## 5. Endings (`Scripts/Core/EndingResolver.cs`, priority top-down)

`CompromiseSources` = count of: `login==Entered`, `usb==Plugged`, `intercom==BuzzedIn`, `mfa==Approved`.

| # | Ending | Condition |
|---|---|---|
| 1 | **BREACH** | sources >= 2, OR `mfa==Approved`, OR (sources >= 1 AND `mfa==Ignored`) |
| 2 | **CONTAINED BUT EXPOSED** | sources == 1 |
| 3 | **SOCIAL MISCALIBRATION** | sources == 0 AND `colleague==RefusedOutright` |
| 4 | **FULL CONTAINMENT** | everything else |

### How to reach each ending (quick recipes)
- **Full containment (100/100):** Forward the email; drop the USB in the Found devices box; read the lobby directory, then unlock Meeting Room 3; read the break-room notice and use that option at the intercom (or call 2100); Deny and report the MFA push (it arrives at ~600 s); Report the Finance-pod note; check out, answer C, submit.
- **Breach:** Scan the QR and press Log in, then Approve the MFA push. (Or plug in the USB AND buzz the visitor in.)
- **Contained but exposed:** exactly one mistake — e.g. plug in the USB — and Deny the MFA push.
- **Social miscalibration:** make no security mistakes, but refuse Priya in chat.

## 6. Scoring (`Scripts/Core/ScoreCalculator.cs`)
Five categories, 20% each; categories never reached are excluded and the rest renormalised. Shown only at the end.

| Category | Values |
|---|---|
| Phishing (email/QR/login) | forwarded or login Reported 1.0 · ignored 0.75 · closed (or scanned without deciding) 0.5 · login Entered 0 |
| Physical (USB, workstation; averaged if both) | USB: Reported 1 · Ignored 0.5 · Plugged 0. Workstation: Reported 1 · Locked 0.75 · Ignored 0 |
| Social (colleague) | VerifiedThenHelped 1 · UnlockedBlindly 0.5 · RefusedOutright 0.5 |
| Impersonation (intercom) | VerifiedExternally 1 · RefusedWithoutVerifying 0.75 · BuzzedIn 0 |
| MFA | DeniedAndReported 1 · Denied 0.75 · Ignored 0.25 · Approved 0 |

---

## 7. Where to look when debugging

| Symptom | Look at |
|---|---|
| Situation never starts | its row in §2 table; `SituationTrigger.ConditionsMet`; `GameDirector.CanBegin` (Active / cooldown / LockedOut); values passed to `Configure(...)` in `OfficeSceneBuilder.BuildGameplay` |
| Two things at once / nothing for a long time | 45 s cooldown in `GameDirector`; `releaseAfter` per situation |
| Clue doesn't register | `Scripts/Props/ClueReadable.cs` (1.5 s gaze, max distance, angle); clue object positions in `BuildGameplay` |
| Wrong ending/score | `EndingResolver.ResolveTier`, `ScoreCalculator`; EditMode tests in `Assets/_Project/Tests/EditMode/` |
| Text misplaced in headset | text must use the URP shader-graph material `Materials/TextURP.mat` (`UiTheme.fontMaterial`); legacy TMP shaders render with the wrong eye in single-pass stereo |
| Walking/teleporting through walls | layer `Environment`, `MarkWall` / `AssignBlockingLayer` in builder, `Scripts/Player/HeadBlocker.cs` |
| UI layout | `Scripts/UI/UiPanel.cs` (all panels), `ChoicePanel`, `MonitorScreen` (+ `MailApp`, `ChatApp`, `ReportApp`), `Props/PhoneDevice.cs` (phone UI authored at 5x, scaled 0.2) |
| What the player did | session JSON in `Application.persistentDataPath/sessions/` (`SessionLog`) |

### Tests
- EditMode: `CyberQuest > 7. Run EditMode Tests` -> `Builds/test_results.txt` (21 tests: endings, scoring, director, cooldown, clock).
- Scripted playthroughs in Meta XR Simulator: `CyberQuest > 6. Automated Playtest in Simulator` -> `Builds/playtest/report.txt` + screenshots. 5 runs: Full containment, Breach (with lockout + wrong deduction), Contained, Social miscalibration (different order), do-nothing-until-lockout. Also checks wall/desk collision, text overflow, legibility (>= 0.75°), button overlap.
- `CyberQuest > 5. Play in Meta XR Simulator` = normal play (no automation).

### Simulator keys (Meta XR Simulator v207 defaults)
Look: right-mouse drag or arrows · Move head: WASD, R/F up/down · Trigger (press buttons): **T** · Grip (grab): **U** ·
Teleport: hold **Y**, release · Snap turn: G/J · Switch controller: [ ] · Tip: enable *Point & click* so the ray follows the mouse.
