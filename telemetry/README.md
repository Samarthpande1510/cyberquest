# telemetry/

Raw research data from **real participants only**, plus the script that turns it into the
dossier's Section 4 numbers. Nothing in this folder may be simulated or typed in by hand,
except `sus_responses.csv`, which is copied from the signed paper SUS forms.

## What the game writes

At the end of every session (after the security terminal resolves the ending) the game writes to
`Application.persistentDataPath/telemetry/`. On a Quest that is
`/sdcard/Android/data/<package name>/files/telemetry/`.

| File | One row per | Key columns |
|---|---|---|
| `session_summary.csv` | session (appended) | `participant_id`, `attempt`, `ending`, `score`, `breach_rate`, `credential_disclosure`, `unauthorised_entry`, `mean_challenge_latency_sec`, `false_positives`, `data_source` |
| `security_audit_real_trials.csv` | situation reached (appended) | same columns as the course scaffold (`trial_id ... tailgating_permitted`) plus `outcome`, `outcome_score`, `classification` (TP/FN/TN/FP), `compromise`, `credential_disclosure` |
| `audit_<participant>_<session>.csv` | event | every timestamped event, decision and room entry/exit |

- **Participant IDs** are anonymised and automatic: P01, P02 and so on, one per app launch.
  Restart the app for each new participant. Play Again keeps the ID and increases `attempt`.
- **`data_source`** is `Headset` on the Quest and `Editor` in Unity (including automated playtests).
  The analysis script drops `Editor` rows unless you pass `--include-editor`.
- **Challenge latency** = t_decision − t_request, in real seconds. For the intercom, t_request is
  when the player answers it, not the first buzz.
- **Breach rate** = compromise sources ÷ attack situations reached (phishing, USB, intercom, MFA).

## Collecting it

```
adb pull /sdcard/Android/data/<package name>/files/telemetry/ telemetry/
python telemetry/analyse_cyberquest.py telemetry --plots
```

`<package name>` is the Android package ID in Player Settings (`adb shell pm list packages | findstr -i cyberquest`).
For SUS, create `sus_responses.csv` with the header `participant_id,q1,q2,q3,q4,q5,q6,q7,q8,q9,q10`.
