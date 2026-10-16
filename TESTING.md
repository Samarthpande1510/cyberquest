# Testing CyberQuest

## Play it yourself in Meta XR Simulator
1. Open the project in Unity 6000.6 and start Meta XR Simulator.
2. `CyberQuest > 2. Build Office Scene` (regenerates `Assets/_Project/Scenes/Office_Main.unity`).
3. `CyberQuest > 5. Play in Meta XR Simulator`.
4. Press **START SHIFT**, answer the desk phone, then play: monitor (Report / Mail / Chat), work phone
   on the desk, empty desk to the east, corridor, break room, lobby, Finance pod.

Controls: point + trigger (or poke) to press buttons, grip to grab, thumbstick forward to teleport.
Walls, closed doors and furniture block both teleporting and walking.

## Automated checks (no headset needed)
| What | How | Output |
|---|---|---|
| EditMode unit tests (endings, scoring, director, cooldown, clock) | `CyberQuest > 7. Run EditMode Tests` | `Builds/test_results.txt` |
| Five scripted playthroughs (all four endings, a different situation order, do-nothing-until-lockout) + layout / legibility / collision checks + screenshots | `CyberQuest > 6. Automated Playtest in Simulator` | `Builds/playtest/report.txt`, `Builds/playtest/*.png` |

Both can also be triggered from outside the editor while it is open by creating
`Temp/cq_tests.request` or `Temp/cq_playtest.request` (content `rebuild` regenerates the scene first,
`play` starts a normal session instead of the scripted one).

The layout check fails if any text leaves its panel, any button overlaps another, or any text is smaller
than 0.75 degrees at the panel's intended reading distance (the phone's address bar is small on purpose).

## Session logs
Each finished session writes `sessions/session_<timestamp>.json` under `Application.persistentDataPath`
(decisions with game time, room entries and dwell times, completion order, final state, ending, score).
