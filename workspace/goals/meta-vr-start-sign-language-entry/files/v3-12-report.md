# SignSight Learn v3.12 — per-axis wrist limits (partial)

Branch `v3.10-motion-room`. **Not done. Nothing committed.** Stopped after Task 1 (measurement and per-axis clamp).
Not run: Task 2 (clavicle), Task 3 (full-size visual check), Task 4 (regressions). Shipped state stays the v3.11 default:
clamp OFF, self-test 54 / 54.

## What changed in code (`TeacherAvatarDriver.cs`)
- Clamp is now per axis: **pitch (flexion / extension) 70°, yaw (radial / ulnar) 30°, twist 0°**. Values follow standard
  wrist range of motion (flexion ~70–80, extension ~60–70, radial ~20, ulnar ~30–35), not the test results.
- Pitch and yaw are measured from the finger direction in the wrist's own frame (forward = finger, up = palm normal,
  from `FrameToBone`): pitch is the angle in the forearm–palm-normal plane, yaw the angle in the forearm–lateral plane.
  This is an approximation: the two components don't add up exactly to the total at large angles (see YES, BAD).
- `HoldReport` now shows pitch, yaw, twist and total for both arms.
- Forearm roll search and swivel search (v3.11) unchanged, still on. `wristClampEnabled` still false by default.

## Task 1 — per-axis measurement at the hold (clamp off, degrees)

| Word | R pitch | R yaw | R twist | R total | Verdict vs 70 / 30 |
|---|---|---|---|---|---|
| HELLO | 12.6 | 43.3 | 1.7 | 43.8 | yaw over by 13 — near miss |
| YES | −71.6 | −66.1 | 3.0 | 77.8 | yaw over by 36, mostly yaw — impossible by the brief's rule |
| BAD | 125.5 | −112.2 | 0.4 | 109.5 | pitch over by 56, yaw over by 82 — impossible |
| DRINK | −99.6 | −97.5 | 2.2 | 97.8 | pitch over 30, yaw over 68 — impossible |
| WHERE | −42.6 | −19.9 | 17.0 | 48.2 | **inside pitch and yaw**; twist 17 is the only excess |
| HOW | 73.5 | 64.0 | 0.4 | 72.9 | yaw over by 34, mostly yaw — impossible (left arm: pitch 75.5, yaw −68) |

Clamp-on self-test with these per-axis limits (`v312-selftest-axis-clamp-on.txt`): **45 / 54**.
- Still failing: HELLO, YES, BAD, DRINK, WHERE, HOW.
- Newly failing (passed under the v3.11 total-swing clamp): BREAD, TIRED, PAIN, ME.
- Clamp off (`v312-selftest-clamp-off.txt`): 54 / 54.

Reading of the result:
- **WHERE** is inside pitch and yaw. It fails because the twist limit of 0 removes its leftover 17° of twist, which the
  forearm roll search did not fully cancel. That is a roll-search shortfall, not a wrist fault.
- **YES, BAD, DRINK, HOW** are yaw-dominated beyond 30°. Under the brief's rule these are genuinely impossible for a human
  wrist and stay clamp off.
- **HELLO** is a yaw near miss (13° over). It still fails with the clamp on, so even small wrist corrections break the sign.
- **BREAD, TIRED, PAIN, ME** were not measured per axis in this run. They fail with the per-axis clamp, so they are
  probably yaw- or twist-dominated too. Not checked.

## BREAD anomaly (one line, not chased)
Under the v3.11 total-swing clamp, BREAD had a 121.5° correction and still passed the clamp-on self-test. Reason not
established. Under the per-axis clamp BREAD fails, which fits a large yaw component.

## Task 2 — clavicle
**Not attempted.** Only HELLO and WHERE qualified, and both are small residuals that the per-axis clamp didn't close.
The clavicle change would need its own test pass in this prompt, and it didn't get one.

## Task 3 — full-size visual check
**Not run.** Captures for the six words were not taken, so there are no `v312-*.png` verdicts.

## Task 4 — regressions
**Not run.**

## Words left with the clamp off (recommended for the shipped state)
- YES, BAD, DRINK, HOW: yaw beyond 30° (human wrist cannot do it).
- BREAD, TIRED, PAIN, ME: need measurement before deciding.
- HELLO, WHERE: near misses. Per-axis clamp still fails them; clavicle not tried.

## Next steps (not done here)
1. Measure pitch and yaw for BREAD, TIRED, PAIN and ME, and decide clamp on or off for each.
2. Finer forearm roll (more steps, or a wider search) to cut WHERE's 17° leftover twist, then re-test WHERE.
3. Clavicle test on HELLO and WHERE only.
4. Full-size captures (`v312-<word>-<moment>.png`) and regressions, then a decision on the commit.
