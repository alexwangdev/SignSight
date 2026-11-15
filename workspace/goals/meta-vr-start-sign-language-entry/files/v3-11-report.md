# SignSight Learn v3.11 — arm carries the angle (9 words)

Branch `v3.10-motion-room`. **Not done: self-test is 48 / 54 with the clamp on, not 54 / 54. Nothing committed.**
Task A is on main as `b336b52`, not part of this report. Clamp switch: **OFF** (default).

## What the arm now does (in code, all in `TeacherAvatarDriver.cs`)
- **Swivel search** (`swivelSearch`, on): the elbow plane is turned about the shoulder–hand line in 10° steps, and the
  swivel whose forearm best carries the hand orientation wins. Damped (8/s). Moves the upper arm and elbow only.
- **Forearm roll search** (`forearmRoll`, on): the forearm turns about its own axis (pronation / supination), ±90°, in 5°
  steps, damped. Added because the swivel cannot roll the forearm. This is an arm joint, not a change to the wrist.
- Hand position and hand orientation at the end pose are unchanged (no `BoneRot`, `SignMotion` or template edits).
- Measurement: each hold writes the wrist deviation from the forearm (swing, twist, total) for both arms (`HoldReport`).

## Per-word table (hold pose, degrees; "corr" = what the 35° / 45° clamp would still have to take off)

| Word | Before (no search) swing / twist → clamp corr | After swivel + roll swing / twist → clamp corr | Moved | Self-test, clamp ON |
|---|---|---|---|---|
| HELLO | 36.0 / 64.3 → 1.0 / 19.3 | 43.8 / 1.7 → 8.8 / 0 | swivel, roll | FAIL (hint: "Wave it") |
| YES | 58.4 / 116.2 → 23.4 / 71.2 | 77.8 / 3.0 → 42.8 / 0 | swivel, roll | FAIL |
| BAD | 154.1 / 8.2 → 119.1 / 0 | 109.5 / 0.4 → 74.5 / 0 | swivel | FAIL |
| MAN | 49.8 / 58.6 → 14.8 / 13.6 | 46.7 / 0.7 → 11.7 / 0 | swivel, roll | **PASS** |
| DRINK | 26.5 / 150.0 → 0 / 105 | 97.8 / 2.2 → 62.8 / 0 | swivel, roll | FAIL |
| BREAD | 159.2 / 85.3 → 124.2 / 40.3 | 156.5 / 0.4 → 121.5 / 0 | swivel, roll | **PASS** |
| NEED | 25.8 / 124.9 → 0 / 79.9 | 39.7 / 31.7 → 4.7 / 0 | swivel, roll | **PASS** |
| WHERE | 46.4 / 109.4 → 11.4 / 64.4 | 45.3 / 17.0 → 10.3 / 0 | swivel, roll | FAIL |
| HOW | 167.5 / 74.8 → 132.5 / 29.8 | 72.9 / 0.4 → 37.9 / 0 (left arm 74.9 → 39.9) | swivel, roll | FAIL |

Self-test with the clamp ON (`v311-selftest-clamp-on.txt`): **48 / 54**. Still failing: HELLO, YES, BAD, DRINK, WHERE, HOW.
Self-test with the clamp OFF (`v311-selftest-clamp-off.txt`): **54 / 54** — the committed default state.
The earlier v3.10 clamp-on run was 45 / 54.

## Why the rest cannot be fixed by the arm alone
Once the wrist position is fixed (end-pose target), the forearm direction is limited to a cone around the
shoulder–wrist line. The swivel moves around that cone, and the roll turns the forearm, but neither changes the cone.
What is left is wrist swing: the angle between the forearm and the direction the hand has to point. For BAD (74.5° of
clamp correction), DRINK (62.8°), YES (42.8°) and HOW (37.9°) that is far beyond 35°. HELLO (8.8°) and WHERE (10.3°)
are close, but the recogniser still fails them with the clamp on, so even small corrections break the sign.
Moving the shoulder joint (clavicle) would change the cone only a little. Not tried in this prompt.

## Visual check
**Not run.** It is gated on a 54 / 54 self-test with the clamp on, which was not met. No full-size verdicts exist for
v3.11, and nothing has been claimed.

## Final clamp state
- `wristClampEnabled` = **false** (default). Limits 35° swing / 45° twist kept on record; not raised.
- Swivel search and forearm roll: **on** (default). Self-test with them on and the clamp off: 54 / 54.
- Regressions (Lesson 1 / 6, Exam, Explore, Relax, fade check): **not run**, since the 54 / 54 gate was not met.

## Decision for the next step
Words that still fail with the clamp on need either (a) a human-range decision on the wrist limit for those words, or
(b) a different pose path for the target (not an end-pose change), or (c) the shoulder joint (clavicle) lever tried
on those words. This prompt does not choose between them.

## Logs
`v311-screens/`: `measure-before/`, `measure-after-swivel/`, `measure-after-roll/` (per-word `motion-log.txt`,
hold numbers), `v311-selftest-clamp-on.txt`, `v311-selftest-clamp-off.txt`.
