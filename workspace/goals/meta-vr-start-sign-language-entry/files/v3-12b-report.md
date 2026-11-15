# SignSight Learn v3.12b — per-axis limits, twist tolerance, verification

Branch `v3.10-motion-room`. **Not committed.** The visual check fails on YES, BAD and HOW (shipped state), so the
brief says do not commit. Regressions (Task 5) were not run: they follow Tasks 1–4 and the commit gate.
Shipped state = clamp OFF, swivel + forearm roll ON, per-axis clamp code present and disabled.
Self-test with the clamp OFF: **54 / 54** (`v312d-selftest-shipped-clamp-off.txt`).

## Task 1 — WHERE roll refinement
- Roll search at 36 steps (5°): WHERE residual twist **17.0°**.
- Roll search at 72 steps (2.5°): WHERE residual twist **17.0°** (no change).
- Reason: the roll search minimises the total wrist deviation. 17° of twist is what that minimum leaves behind; finer
  steps can't remove it. Not a search shortfall. Roll steps reverted to 36.
- **Twist tolerance chosen: 15°, one value for all words** (the `wristTwistMaxDeg` default; 0° punished residual
  twist, see Task 3). Not set per word.

## Task 2 — clavicle, HELLO only
- Test: shoulder joint lifted +10° (clavicle local Z), HELLO hold, right arm.
- Yaw: 43.6° → 40.4° (**−3.2°**). Pitch 12.6 → 20.6 (+8, the joint moved the palm).
- Yaw still 10° over the 30° limit. Effect under 5°. **Dropped**; the test code was removed from the driver.

## Task 3 — BREAD, TIRED, PAIN, ME (per-axis at the hold, clamp off, degrees)

| Word | R pitch | R yaw | R twist | Axis that breaks the per-axis clamp | Clamp-on (tol 15) |
|---|---|---|---|---|---|
| BREAD | 171.8 | −157.2 | 0.4 | pitch and yaw, both far over (101 and 127) | pass (see anomaly) |
| TIRED | 62.9 | −46.5 | 1.0 | yaw (16.5 over) | FAIL |
| PAIN | 42.8 | −25.1 | 0.7 | none — inside every limit | pass |
| ME | −173.3 | 128.1 | 0.9 | pitch and yaw, both far over | pass (see anomaly) |

- Hypothesis "twist 0° is the cause for all four": **refuted** for three of them (BREAD, TIRED, ME: twist under 1°).
  For PAIN the only excess was 0.7° of twist, so the 0° limit was the cause there; the 15° tolerance fixes it.
- **BREAD anomaly (15-minute limit):** reason not established. BREAD's clamp correction is very large on both axes, yet it
  passes the clamp-on self-test, and so does ME. The recogniser does not seem to weight wrist corrections the way the
  clamp does. Not chased.
- TIRED, BREAD, ME are impossible for a human wrist on yaw / pitch: clamp off.

## Clamp-on self-test (pitch 70 / yaw 30 / twist tolerance 15)
- Count: **48 / 54** (`v312c-selftest-clamp-on-twist15.txt`).
- Still failing: HELLO, YES, BAD, DRINK, TIRED, WHERE.
- Earlier per-axis run with twist 0: 45 / 54 (`v312-selftest-axis-clamp-on.txt`).

## Task 4 — full-size visual check (hold frames, judged; 900 × 945 eye view)
Files: `v312b-<word>-<moment>.png` (move, mid, hold for the six words; hold only for BREAD, TIRED, PAIN, ME).

| Word | Verdict | What I saw |
|---|---|---|
| HELLO | PASS | Hand at head height, palm out; forearm carries it; elbow out; no sharp wrist bend |
| WHERE | PASS | Index finger raised; forearm near vertical; wrist natural |
| DRINK | PASS (borderline) | Hand at the mouth; forearm up; wrist bent but readable |
| YES | **LOOKS WRONG** | Hand near the waist, palm sideways, wrist bent hard at the hand |
| BAD | **LOOKS WRONG** | Hand across the chest, fingers sideways: a right angle at the wrist (yaw) |
| HOW | **LOOKS WRONG** | Both forearms crossed; wrists bent steeply, hands look unnatural |
| BREAD, TIRED, PAIN, ME | not judged | Hold frames saved; not reviewed (time) |

Move and mid frames for the six words are saved but were not reviewed individually.

## Words left with the clamp off
- YES, BAD, HOW: yaw / pitch impossible for a human wrist; visual check also fails.
- DRINK: yaw about 67° over the 30° limit (impossible on yaw); visually borderline. Clamp off.
- TIRED: yaw 16.5° over, mostly yaw.
- BREAD, ME: pitch and yaw far over.
- HELLO, WHERE: clamp-on still fails (HELLO yaw 13° over; WHERE 2° of twist above the tolerance).
- PAIN: passes clamp-on with the tolerance; may be turned on.

## Final clamp state
- `wristClampEnabled` = **false**. Per-axis limits pitch 70°, yaw 30°, twist tolerance 15° (values from standard wrist
  range of motion; the twist tolerance is a measurement-approximation allowance, not a wrist value).

## Not done
- Regressions (Lesson 1 / 6, Exam 70/70, Explore apple, Relax, fade check) — not run.
- Steps 3–5 (per-joint lag, phase damping, shoulder follow) — not started, as the brief says.
- Real-headset passthrough — UNVALIDATED (no Quest owned).

## Decision needed before the video (Nov 17)
YES, BAD and HOW look wrong in the shipped state. They are the words the video will show as impossible to sign with
these arm levers. Options: (a) film with a note that these three are still being fixed, (b) pick another sign for
the video, or (c) a separate prompt on the arm pose for these three (not the clamp).
