using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace CyberQuest.Core
{
    /// <summary>
    /// Research telemetry and audit trail (research data only, never shown or scored live).
    /// Keeps the column names of the course scaffold's PhysicalSecurityTelemetryLogger so
    /// the Group 17 analysis scripts read it unchanged. At the end of a session it writes,
    /// under persistentDataPath/telemetry:
    ///   security_audit_real_trials.csv  one row per situation the player reached (appended)
    ///   session_summary.csv             one row per session: breach rate, latency, ending (appended)
    ///   audit_&lt;participant&gt;_&lt;session&gt;.csv  every timestamped event, decision and room change
    /// Challenge latency = t_decision - t_request, in real seconds.
    /// </summary>
    public static class PhysicalSecurityTelemetryLogger
    {
        public const string TrialsFile = "security_audit_real_trials.csv";
        public const string SummaryFile = "session_summary.csv";

        public const string TrialsHeader =
            "trial_id,participant_id,training_arm,pretext,action_taken,decision_latency_sec,gaze_dwell_badge_sec," +
            "min_distance_m,is_compliant,tailgating_permitted,session_id,attempt,outcome,outcome_score,ground_truth," +
            "classification,compromise,credential_disclosure,data_source";

        public const string SummaryHeader =
            "participant_id,session_id,attempt,training_arm,data_source,started_utc,ending,score,situations_reached," +
            "attacks_reached,compromise_sources,breach_rate,credential_disclosure,unauthorised_entry," +
            "mean_challenge_latency_sec,verification_actions,false_positives,false_negatives,report_filed," +
            "deduction_correct,policy_board_read,game_minutes_at_end";

        public const string AuditHeader = "participant_id,session_id,real_seconds,game_time,kind,detail";

        /// <summary>One row of security_audit_real_trials.csv.</summary>
        public class Trial
        {
            public int trialId;
            public SituationId situation;
            public string action = "";
            public float? latency;
            public float? gazeDwell;
            public float? minDistance;
            public string outcome;
            public float? outcomeScore;
            public bool legitimate;
            public string classification;
            public bool isCompliant;
            public bool tailgating;
            public bool compromise;
            public bool credentialDisclosure;
        }

        private class Decision
        {
            public string action;
            public float latency;
        }

        private static readonly Dictionary<SituationId, Decision> Decisions = new Dictionary<SituationId, Decision>();
        private static readonly Dictionary<SituationId, float> MinDistances = new Dictionary<SituationId, float>();
        private static float _badgeGaze;
        private static int _attempt;

        /// <summary>Anonymised ID (P01, P02...). Auto-assigned on first write; set it to override.</summary>
        public static string ParticipantId { get; set; }

        public static string TrainingArm { get; set; } = "VR_Interactive";

        /// <summary>"Headset" on device, "Editor" otherwise, so analysis can drop editor/test sessions.</summary>
        public static string DataSource { get; set; } = Application.isEditor ? "Editor" : "Headset";

        public static float BadgeGazeSeconds => _badgeGaze;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            ParticipantId = null;
            _attempt = 0;
            ResetSession();
        }

        /// <summary>New session (Play Again keeps the participant ID and bumps the attempt).</summary>
        public static void ResetSession()
        {
            Decisions.Clear();
            MinDistances.Clear();
            _badgeGaze = 0f;
        }

        /// <summary>Called by SituationTrigger.Complete.</summary>
        public static void RecordDecision(SituationId id, string action, float requestedAt, float decidedAt)
        {
            Decisions[id] = new Decision { action = action, latency = Mathf.Max(0f, decidedAt - requestedAt) };
        }

        public static void AddBadgeGaze(float seconds)
        {
            _badgeGaze += Mathf.Max(0f, seconds);
        }

        public static void ObserveDistance(SituationId id, float metres)
        {
            if (!MinDistances.TryGetValue(id, out float d) || metres < d)
            {
                MinDistances[id] = metres;
            }
        }

        // ------------------------------------------------------------------ metrics

        /// <summary>Text label of a situation's final outcome, or null if it was never reached.</summary>
        public static string Outcome(GameState s, SituationId id)
        {
            switch (id)
            {
                case SituationId.EmailQr:
                    if (s.emailForwardedToSecurity) return "ForwardedToSecurity";
                    if (s.qrScanned) return "QrScanned";
                    if (s.emailIgnored) return "Ignored";
                    return null;
                case SituationId.FakeLogin: return s.login == LoginResult.NotReached ? null : s.login.ToString();
                case SituationId.Usb: return s.usb == UsbAction.NotReached ? null : s.usb.ToString();
                case SituationId.Colleague: return s.colleague == ColleagueResponse.NotReached ? null : s.colleague.ToString();
                case SituationId.Intercom: return s.intercom == IntercomResponse.NotReached ? null : s.intercom.ToString();
                case SituationId.Mfa: return s.mfa == MfaResponse.NotReached ? null : s.mfa.ToString();
                case SituationId.Workstation: return s.workstation == WorkstationResult.NotReached ? null : s.workstation.ToString();
            }
            return null;
        }

        /// <summary>Per-situation value from the scoring table (CLAUDE.md 7). Email and login share the phishing value.</summary>
        public static float? OutcomeScore(GameState s, SituationId id)
        {
            switch (id)
            {
                case SituationId.EmailQr:
                case SituationId.FakeLogin:
                    return ScoreCalculator.Phishing(s);
                case SituationId.Usb:
                    return s.usb switch
                    {
                        UsbAction.Reported => 1f,
                        UsbAction.Ignored => 0.5f,
                        UsbAction.Plugged => 0f,
                        _ => (float?)null,
                    };
                case SituationId.Workstation:
                    return s.workstation switch
                    {
                        WorkstationResult.Reported => 1f,
                        WorkstationResult.Locked => 0.75f,
                        WorkstationResult.Ignored => 0f,
                        _ => (float?)null,
                    };
                case SituationId.Colleague: return ScoreCalculator.Social(s);
                case SituationId.Intercom: return ScoreCalculator.Impersonation(s);
                case SituationId.Mfa: return ScoreCalculator.Mfa(s);
            }
            return null;
        }

        /// <summary>True for a situation whose action directly hands the attacker access.</summary>
        public static bool IsCompromise(GameState s, SituationId id)
        {
            switch (id)
            {
                case SituationId.FakeLogin: return s.login == LoginResult.Entered;
                case SituationId.Usb: return s.usb == UsbAction.Plugged;
                case SituationId.Intercom: return s.intercom == IntercomResponse.BuzzedIn;
                case SituationId.Mfa: return s.mfa == MfaResponse.Approved;
            }
            return false;
        }

        /// <summary>
        /// Confusion-matrix cell, treating "suspicious" as the positive class. The colleague
        /// request is the only legitimate one: refusing her is a false positive (FP), helping
        /// is a true negative (TN). For every attack, a compromise or a missed hazard is a
        /// false negative (FN): scanning the QR, entering credentials, plugging the USB,
        /// buzzing the visitor in, approving or ignoring the MFA push, ignoring the
        /// unlocked workstation. Anything else is a true positive (TP).
        /// </summary>
        public static string Classify(GameState s, SituationId id)
        {
            switch (id)
            {
                case SituationId.Colleague:
                    return s.colleague == ColleagueResponse.RefusedOutright ? "FP" : "TN";
                case SituationId.EmailQr:
                    return s.qrScanned ? "FN" : "TP";
                case SituationId.Mfa:
                    return s.mfa == MfaResponse.Approved || s.mfa == MfaResponse.Ignored ? "FN" : "TP";
                case SituationId.Workstation:
                    return s.workstation == WorkstationResult.Ignored ? "FN" : "TP";
                default:
                    return IsCompromise(s, id) ? "FN" : "TP";
            }
        }

        /// <summary>Rows for every reached situation: completion order first, then any finalised at the end.</summary>
        public static List<Trial> BuildTrials(GameState s)
        {
            var order = new List<SituationId>(s.completionOrder);
            foreach (SituationId id in Enum.GetValues(typeof(SituationId)))
            {
                if (!order.Contains(id) && Outcome(s, id) != null)
                {
                    order.Add(id);
                }
            }

            var trials = new List<Trial>();
            foreach (var id in order)
            {
                string outcome = Outcome(s, id);
                if (outcome == null)
                {
                    continue;
                }
                Decisions.TryGetValue(id, out var decision);
                float? score = OutcomeScore(s, id);
                string cls = Classify(s, id);
                var t = new Trial
                {
                    trialId = trials.Count + 1,
                    situation = id,
                    action = decision?.action ?? outcome,
                    latency = decision?.latency,
                    gazeDwell = id == SituationId.Intercom ? _badgeGaze : (float?)null,
                    minDistance = MinDistances.TryGetValue(id, out float d) ? d : (float?)null,
                    outcome = outcome,
                    outcomeScore = score,
                    legitimate = id == SituationId.Colleague,
                    classification = cls,
                    isCompliant = (cls == "TP" || cls == "TN") && score.HasValue && score.Value >= 0.75f,
                    tailgating = id == SituationId.Intercom && s.intercom == IntercomResponse.BuzzedIn,
                    compromise = IsCompromise(s, id),
                    credentialDisclosure = (id == SituationId.FakeLogin && s.login == LoginResult.Entered) ||
                                           (id == SituationId.Mfa && s.mfa == MfaResponse.Approved),
                };
                trials.Add(t);
            }
            return trials;
        }

        /// <summary>Attack situations reached (phishing counts once): the denominator of the breach rate.</summary>
        public static int AttacksReached(GameState s)
        {
            int n = 0;
            if (Outcome(s, SituationId.EmailQr) != null || s.login != LoginResult.NotReached) n++;
            if (s.usb != UsbAction.NotReached) n++;
            if (s.intercom != IntercomResponse.NotReached) n++;
            if (s.mfa != MfaResponse.NotReached) n++;
            return n;
        }

        /// <summary>Breach rate = compromise sources / attacks reached (0 when none were reached).</summary>
        public static float BreachRate(GameState s)
        {
            int attacks = AttacksReached(s);
            return attacks == 0 ? 0f : (float)s.CompromiseSources / attacks;
        }

        public static bool CredentialDisclosure(GameState s) => s.login == LoginResult.Entered || s.mfa == MfaResponse.Approved;

        public static int VerificationActions(GameState s) =>
            (s.colleagueDirectoryChecked ? 1 : 0) + (s.intercomBadgeInspected ? 1 : 0) +
            (s.intercomManagerEmailChecked ? 1 : 0) + (s.maintenanceNoticeRead ? 1 : 0);

        public static float? MeanLatency(List<Trial> trials)
        {
            float sum = 0f;
            int n = 0;
            foreach (var t in trials)
            {
                if (t.latency.HasValue)
                {
                    sum += t.latency.Value;
                    n++;
                }
            }
            return n == 0 ? (float?)null : sum / n;
        }

        // ------------------------------------------------------------------ CSV

        public static string TrialRow(Trial t, string participant, string session, int attempt)
        {
            return Join(t.trialId.ToString(CultureInfo.InvariantCulture), participant, TrainingArm, t.situation.ToString(), t.action,
                Num(t.latency), Num(t.gazeDwell), Num(t.minDistance), Bool(t.isCompliant), Bool(t.tailgating), session,
                attempt.ToString(CultureInfo.InvariantCulture), t.outcome, Num(t.outcomeScore),
                t.legitimate ? "legitimate" : "attack", t.classification, Bool(t.compromise), Bool(t.credentialDisclosure), DataSource);
        }

        public static string SummaryRow(GameState s, List<Trial> trials, EndingType ending, int score, string participant,
            string session, string startedUtc, int attempt)
        {
            int fp = trials.FindAll(t => t.classification == "FP").Count;
            int fn = trials.FindAll(t => t.classification == "FN").Count;
            return Join(participant, session, attempt.ToString(CultureInfo.InvariantCulture), TrainingArm, DataSource, startedUtc,
                ending.ToString(), score.ToString(CultureInfo.InvariantCulture), trials.Count.ToString(CultureInfo.InvariantCulture),
                AttacksReached(s).ToString(CultureInfo.InvariantCulture), s.CompromiseSources.ToString(CultureInfo.InvariantCulture),
                Num(BreachRate(s), "0.###"), Bool(CredentialDisclosure(s)), Bool(s.intercom == IntercomResponse.BuzzedIn),
                Num(MeanLatency(trials)), VerificationActions(s).ToString(CultureInfo.InvariantCulture),
                fp.ToString(CultureInfo.InvariantCulture), fn.ToString(CultureInfo.InvariantCulture), Bool(s.reportFiled),
                Bool(s.deductionCorrect), Bool(s.policyBoardRead), Num(s.gameMinutesAtEnd, "0.0"));
        }

        /// <summary>Writes all three files and returns the telemetry folder (null if writing failed).</summary>
        public static string Write(GameState s, EndingType ending, int score)
        {
            return Write(Path.Combine(Application.persistentDataPath, "telemetry"), s, ending, score);
        }

        public static string Write(string dir, GameState s, EndingType ending, int score)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string summaryPath = Path.Combine(dir, SummaryFile);
                if (string.IsNullOrEmpty(ParticipantId))
                {
                    ParticipantId = NextParticipantId(summaryPath);
                }
                _attempt++;
                string session = SessionLog.SessionId;
                var trials = BuildTrials(s);

                var rows = new StringBuilder();
                foreach (var t in trials)
                {
                    rows.AppendLine(TrialRow(t, ParticipantId, session, _attempt));
                }
                Append(Path.Combine(dir, TrialsFile), TrialsHeader, rows.ToString());
                Append(summaryPath, SummaryHeader,
                    SummaryRow(s, trials, ending, score, ParticipantId, session, SessionLog.StartedUtc, _attempt) + Environment.NewLine);

                var audit = new StringBuilder(AuditHeader).AppendLine();
                foreach (var e in SessionLog.Entries)
                {
                    audit.AppendLine(Join(ParticipantId, session, Num(e.realSeconds, "0.00"), e.gameTime, e.kind, e.detail));
                }
                File.WriteAllText(Path.Combine(dir, $"audit_{ParticipantId}_{session}.csv"), audit.ToString());
                return dir;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Telemetry] Could not write telemetry: " + e.Message);
                return null;
            }
        }

        /// <summary>P01, P02... continuing from the participants already in the summary file.</summary>
        public static string NextParticipantId(string summaryPath)
        {
            var seen = new HashSet<string>();
            if (File.Exists(summaryPath))
            {
                foreach (var line in File.ReadAllLines(summaryPath))
                {
                    int comma = line.IndexOf(',');
                    string id = comma > 0 ? line.Substring(0, comma) : line;
                    if (id.StartsWith("P") && id != "participant_id")
                    {
                        seen.Add(id);
                    }
                }
            }
            return "P" + (seen.Count + 1).ToString("00", CultureInfo.InvariantCulture);
        }

        private static void Append(string path, string header, string rows)
        {
            bool fresh = !File.Exists(path) || new FileInfo(path).Length == 0;
            File.AppendAllText(path, (fresh ? header + Environment.NewLine : "") + rows);
        }

        public static string Escape(string value)
        {
            value ??= "";
            return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        private static string Join(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = Escape(values[i]);
            }
            return string.Join(",", values);
        }

        private static string Num(float? v, string format = "0.00") =>
            v.HasValue ? v.Value.ToString(format, CultureInfo.InvariantCulture) : "";

        private static string Bool(bool v) => v ? "True" : "False";
    }
}
