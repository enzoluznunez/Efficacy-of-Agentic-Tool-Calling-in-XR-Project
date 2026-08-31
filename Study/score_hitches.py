#!/usr/bin/env python3
"""Summarise frame health from a StudyLog JSONL file.

Usage:
    python3 score_hitches.py <log.jsonl> [more.jsonl ...]

Reads the events written by HitchLog, FrameBudget and StudySpan, then reports
per phase and per span where the frame budget went.
"""

import collections
import json
import sys


def load(path):
    rows = []
    with open(path) as fh:
        for n, line in enumerate(fh, 1):
            line = line.strip()
            if not line:
                continue
            try:
                rows.append(json.loads(line))
            except json.JSONDecodeError:
                print(f"  skipped malformed line {n}", file=sys.stderr)
    return rows


def percentile(values, fraction):
    if not values:
        return 0.0
    ordered = sorted(values)
    index = min(len(ordered) - 1, max(0, round(fraction * (len(ordered) - 1))))
    return ordered[index]


def attribute(rows):
    """Tag each frame_hitch with the most recent preceding action."""
    anchors = ("user_action", "tool_call", "ui_span", "status", "notice")
    last = None
    pairs = []
    for row in rows:
        kind = row.get("type")
        if kind in anchors:
            label = row.get("name") or row.get("what") or row.get("tool") or kind
            last = f"{kind}:{label}"
        elif kind == "frame_hitch":
            pairs.append((last or "unattributed", row))
    return pairs


def report(path, rows):
    print(f"\n{'=' * 72}\n{path}\n{'=' * 72}")

    config = next((r for r in rows if r.get("type") == "study_config"), None)
    if config:
        print(f"participant {config.get('participant')}  arm {config.get('arm')}  "
              f"dataset {config.get('dataset')}  {config.get('displayHz')} Hz")

    hitches = [r for r in rows if r.get("type") == "frame_hitch"]
    windows = [r for r in rows if r.get("type") == "frame_window"]
    spans = [r for r in rows if r.get("type") == "ui_span"]
    stalls = [r for r in rows if r.get("type") == "frame_stall"]
    settles = [r for r in rows if r.get("type") == "grid_settled"]

    total_frames = sum(w.get("frameFrames", 0) for w in windows)
    print(f"\nframes {total_frames}   hitches {len(hitches)}   "
          f"stalls {len(stalls)}   spans {len(spans)}")

    if windows:
        print("\n-- frame time by phase (ms) --")
        by_phase = collections.defaultdict(list)
        for w in windows:
            by_phase[w.get("phase", "?")].append(w)
        print(f"{'phase':<24}{'frames':>8}{'p50':>8}{'p95':>8}{'p99':>8}"
              f"{'max':>8}{'>1x':>7}{'>2x':>7}{'>4x':>7}")
        for phase, group in sorted(by_phase.items()):
            frames = sum(g.get("frameFrames", 0) for g in group)
            print(f"{phase:<24}{frames:>8}"
                  f"{percentile([g.get('frameP50', 0) for g in group], 0.5):>8.1f}"
                  f"{max((g.get('frameP95', 0) for g in group), default=0):>8.1f}"
                  f"{max((g.get('frameP99', 0) for g in group), default=0):>8.1f}"
                  f"{max((g.get('frameMax', 0) for g in group), default=0):>8.1f}"
                  f"{sum(g.get('frameOver1x', 0) for g in group):>7}"
                  f"{sum(g.get('frameOver2x', 0) for g in group):>7}"
                  f"{sum(g.get('frameOver4x', 0) for g in group):>7}")

    if spans:
        print("\n-- spans by total cost --")
        by_name = collections.defaultdict(list)
        for s in spans:
            by_name[s.get("name", "?")].append(s.get("ms", 0.0))
        print(f"{'span':<24}{'count':>8}{'total':>10}{'mean':>9}{'p95':>9}{'max':>9}")
        for name, ms in sorted(by_name.items(), key=lambda kv: -sum(kv[1])):
            print(f"{name:<24}{len(ms):>8}{sum(ms):>10.1f}"
                  f"{sum(ms) / len(ms):>9.1f}{percentile(ms, 0.95):>9.1f}{max(ms):>9.1f}")

    pairs = attribute(rows)
    if pairs:
        print("\n-- hitches by attributed cause --")
        counts = collections.Counter(cause for cause, _ in pairs)
        lost = collections.Counter()
        for cause, row in pairs:
            lost[cause] += row.get("framesLost", 0)
        print(f"{'cause':<44}{'hitches':>9}{'lost':>7}")
        for cause, count in counts.most_common(10):
            print(f"{cause[:43]:<44}{count:>9}{lost[cause]:>7}")

    if settles:
        retries = [s.get("retries", 0) for s in settles]
        forced = sum(1 for s in settles if s.get("forced"))
        print(f"\n-- grid settling --\nrebuild storms {len(settles)}   "
              f"mean retries {sum(retries) / len(retries):.2f}   "
              f"max {max(retries)}   hit the cap {forced}")

    if stalls:
        print("\n-- stalls --")
        for s in stalls:
            print(f"  {s.get('stalledMs')} ms after {s.get('marks')}")

    tasks = [r for r in rows if r.get("type") == "task_end"]
    if tasks:
        print("\n-- tasks --")
        print(f"{'task':<10}{'seconds':>9}{'p95':>8}{'>2x':>7}{'undo':>7}")
        for t in tasks:
            print(f"{str(t.get('task')):<10}{t.get('durationMs', 0) / 1000:>9.1f}"
                  f"{t.get('frameP95', 0):>8.1f}{t.get('frameOver2x', 0):>7}"
                  f"{t.get('undoSteps', 0):>7}")


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 1
    for path in argv[1:]:
        report(path, load(path))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
