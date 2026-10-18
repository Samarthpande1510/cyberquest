"""
CyberQuest telemetry analysis: rebuilds every Section 4 number from the raw CSVs.

Inputs (copy them here from the headset, see telemetry/README.md):
  session_summary.csv              one row per session (written by the game)
  security_audit_real_trials.csv   one row per situation reached (written by the game)
  sus_responses.csv                optional, participant_id,q1..q10 (1-5), typed in from paper forms

Usage:
  python analyse_cyberquest.py [folder] [--include-editor] [--first-attempt-only] [--plots]

Editor / automated-playtest sessions are excluded unless --include-editor is given, so test
runs never end up in the human data. Standard library only; --plots needs matplotlib.
"""
import csv
import os
import statistics
import sys


def read(path):
    if not os.path.exists(path):
        return []
    with open(path, newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))


def describe(name, values, unit=""):
    values = [v for v in values if v is not None]
    if not values:
        print(f"{name:<34} N=0")
        return
    sd = statistics.stdev(values) if len(values) > 1 else 0.0
    print(f"{name:<34} N={len(values):<3} mean={statistics.mean(values):.2f}{unit}  "
          f"SD={sd:.2f}  median={statistics.median(values):.2f}{unit}")


def num(value):
    return float(value) if value not in ("", None) else None


def sus_score(row):
    """Odd items: score - 1. Even items: 5 - score. Sum x 2.5 = SUS (0-100)."""
    total = 0
    for i in range(1, 11):
        r = int(row[f"q{i}"])
        if not 1 <= r <= 5:
            raise ValueError(f"{row['participant_id']} q{i}={r} is outside 1-5")
        total += (r - 1) if i % 2 == 1 else (5 - r)
    return total * 2.5


def main(argv):
    folder = next((a for a in argv if not a.startswith("--")), os.path.dirname(os.path.abspath(__file__)))
    include_editor = "--include-editor" in argv
    first_only = "--first-attempt-only" in argv

    def keep(row):
        if not include_editor and row.get("data_source") != "Headset":
            return False
        return not first_only or row.get("attempt") == "1"

    sessions = [r for r in read(os.path.join(folder, "session_summary.csv")) if keep(r)]
    trials = [r for r in read(os.path.join(folder, "security_audit_real_trials.csv")) if keep(r)]
    participants = sorted({r["participant_id"] for r in sessions})
    print(f"Sessions: {len(sessions)}   Participants: {len(participants)}   "
          f"(editor sessions {'included' if include_editor else 'excluded'})\n")

    describe("Breach rate (%)", [num(r["breach_rate"]) * 100 for r in sessions if r["breach_rate"]], "%")
    describe("Credential disclosure (%)", [100.0 if r["credential_disclosure"] == "True" else 0.0 for r in sessions], "%")
    describe("Unauthorised entry (%)", [100.0 if r["unauthorised_entry"] == "True" else 0.0 for r in sessions], "%")
    describe("Mean challenge latency (s)", [num(r["mean_challenge_latency_sec"]) for r in sessions], "s")
    describe("Score (0-100)", [num(r["score"]) for r in sessions])
    describe("Verification actions", [num(r["verification_actions"]) for r in sessions])

    print("\nChallenge latency by situation (s)")
    for pretext in sorted({t["pretext"] for t in trials}):
        describe("  " + pretext, [num(t["decision_latency_sec"]) for t in trials if t["pretext"] == pretext], "s")

    cells = {c: sum(1 for t in trials if t["classification"] == c) for c in ("TP", "FN", "TN", "FP")}
    print(f"\nConfusion matrix (positive = suspicious): TP={cells['TP']} FN={cells['FN']} "
          f"TN={cells['TN']} FP={cells['FP']}")

    endings = {}
    for r in sessions:
        endings[r["ending"]] = endings.get(r["ending"], 0) + 1
    print("Endings:", ", ".join(f"{k}={v}" for k, v in sorted(endings.items())) or "none")

    sus_rows = read(os.path.join(folder, "sus_responses.csv"))
    sus = [sus_score(r) for r in sus_rows]
    print()
    describe("SUS score (0-100)", sus)

    if "--plots" in argv and sessions:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
        fig, axes = plt.subplots(1, 2, figsize=(9, 3.5))
        pretexts = sorted({t["pretext"] for t in trials})
        axes[0].boxplot([[num(t["decision_latency_sec"]) for t in trials
                          if t["pretext"] == p and t["decision_latency_sec"]] or [0] for p in pretexts],
                        labels=pretexts)
        axes[0].set_ylabel("Challenge latency (s)")
        axes[0].tick_params(axis="x", rotation=45)
        axes[1].boxplot(sus or [0])
        axes[1].set_xticklabels(["SUS"])
        axes[1].set_ylim(0, 100)
        fig.tight_layout()
        out = os.path.join(folder, "figure_latency_sus.png")
        fig.savefig(out, dpi=300)
        print("Saved", out)


if __name__ == "__main__":
    main(sys.argv[1:])
