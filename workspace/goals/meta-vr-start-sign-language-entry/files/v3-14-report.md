# SignSight Learn v3.14 — YES, BAD, HOW arm fix

Branch `v3.14-motion-fable`, committed, **not merged**. Shipped state: wrist clamp OFF, self-test 54 / 54.
All three words now look human in my review of the full-size captures (hold, mid-movement, move to start).
The fixes apply to YES, BAD and HOW only (`armFixWords` in `TeacherAvatarDriver.cs`); the other words keep the v3.13 arm.

## What the earlier rounds missed
1. **The swivel search ignored the forearm roll that runs after it.** It scored each elbow position by total wrist
   deviation, twist included, although the roll search removes the twist afterwards. So it picked elbows that traded
   twist for a sideways wrist bend. This is YES's −66° yaw.
2. **BAD's end pose pointed the fingers back at the signer.** The movement turns the palm from "toward self" to "down"
   by the shortest turn, which takes fingers-up to fingers-toward-the-signer. No arm can carry that. The template fixes
   the end palm, not the end finger direction.
3. **HOW's layout had the same fault.** Fingers forward, then a 140° pitch about the wrist: the fingers end pointing
   down and back at the signer, with the wrist in front of them.
4. **The move-to-start frames were never judged.** In v3.12b the BAD frame has the elbow at the neck and the HOW frame
   has the arms crossed (`v312-screens/v312b-BAD-move.png`, `v312b-HOW-move.png`). Half-way between the rest
   orientation and the sign orientation there is no arm that carries the hand.

## Hypotheses

| Hypothesis | Words | Wrist bend at the hold, before → after (total; right arm) | Kept? |
|---|---|---|---|
| H1 HOW layout | HOW | 72.9° → 34.2° (left 74.9° → 34.2°); yaw 64.0° → 32.3° | **Kept** (−38.7°) |
| H2 frame / conversion | YES, BAD vs HELLO | No frame error: `FrameToBone` and the palm conversion are the same for all four. The fault is the end orientation (point 2), fixed for BAD: 109.5° → 27.0°; yaw −112° → 15.0° | **Kept** for BAD (−82.5°). Conversion code unchanged. |
| H3 swivel objective | YES, BAD, HOW | YES 77.8° → 63.5°; yaw −66.1° → −13.3°, pitch −71.6° → −57.8° | **Kept** (yaw −52.8°) |
| H4 hand move ≤ 5 cm | YES | Best of 6 moves: 5 cm up, 62.3° → 52.8° (−9.5°). Toward the body: −1.5°. Toward the mid-line: worse | **Not kept** (under 10°) |

Numbers are from `MeasureHold` (the settled end pose); the "before" column reproduces the v3.13 figures exactly.
File: `v314-screens/v314-hold-measure.txt`.

### H1 detail — HOW
- Lifeprint: "Place your hands together with the knuckles touching." "Roll the hands forward until the 'arches' are
  upside down--ending with your hands palm-up in 'cupping' handshapes."
- Template change (one field): `fingerDir` (0, −0.5, 0.85) → **(−1, 0, 0)**. The hands now point at each other, palms
  down, and the 140° pitch rolls them over about the finger line to palms up. Movement type, amplitude, palm, anchor,
  offsets and curls are unchanged. The scorer does not read `fingerDir`.
- Driver: for these words, a `"mirror"` left hand is the mirror image of the right hand (before, the two hands used
  different contact points, `knuckle` and `palm`, and sat unevenly).
- Wrist position at the hold (teacher space, metres): right (0.04, 1.40, 0.29) → (0.12, 1.40, 0.39); left
  (−0.04, 1.40, 0.29) → (−0.12, 1.40, 0.39). The contact point on the chest anchor did not move; the wrists moved
  because the hands turned.
- Limit: the fingertips meet at the mid-line. It is not an exact "backs of the fingers pressed together" contact.

### H2 / BAD detail
- Lifeprint: "Move your hand down and away." "End with your palm facing downward." No end finger direction is given.
- Driver only, no template change: when the shortest turn would leave the fingers pointing back at the signer
  (z < −0.5), the hand also turns about the end palm normal, so the fingers end at `endFingerTarget` (−1, 0, 0.6):
  across the body and away. The end palm is still exactly down.
- Tried targets: (−0.5, 0, 1) 50.0°, (−1, 0, 1) 35.3°, (−1, 0, 0.6) 23.5°, (−1, 0, 0.3) 13.0°. I took (−1, 0, 0.6):
  it keeps the hand pointing partly away, as the sign does.

### H3 detail
- New objective for these words: the wrist swing left after the forearm roll (±90°) has taken the twist, plus an
  elbow prior of 0.3° per degree away from the v3.10 pole (keeps the elbow out of the body and below the head).
- A pitch / yaw weighted objective was also tried. It cut YES's yaw to −4° but put BAD's elbow inside the chest. Not used.
- During the travel between rest and the sign, the hand first follows the forearm and turns into the sign's
  orientation over the last part (fix for point 4). The start pose, the movement and the hold are untouched.

## Visual verdicts (900 × 945 eye view, `v314-screens/v314-<WORD>-<moment>.png`)

| Word | Move to start | Mid-movement | Hold |
|---|---|---|---|
| YES | PASS: hand rising at the side, wrist straight | PASS | PASS: fist at chest height, forearm rising under it, wrist bent back but not sideways |
| BAD | PASS: flat hand rising in front of the chest (was: elbow at the neck) | PASS: hand at the mouth, forearm under it | PASS: forearm across the chest, palm down, wrist straight |
| HOW | PASS: both hands rising, elbows at the sides (was: arms crossed) | PASS: knuckles together, palms toward self | PASS: hands together at the chest, palms up; elbows near shoulder height |
| HELLO (control) | unchanged | unchanged | unchanged, still human (43.8°, same as v3.13) |

Limits of this check: three stills per word from one camera, judged by me. No clip was recorded. YES is the weakest
of the three: 63.5° total, of which 57.8° is wrist extension (inside the 70° range) and 26.2° is twist the roll search
cannot reach.

## Verification

| Check | Result |
|---|---|
| Self-test, clamp OFF | **54 / 54**, five runs from the rest pose (`v314-selftest-clamp-off.txt`) |
| Self-test, clamp ON (information only) | 47 / 54. Failing: HELLO, YES, BAD, DRINK, TIRED, WHERE, HOW (v3.12c: 48 / 54; HOW is new, yaw 32° against the 30° limit) |
| Exam happy path, Start card, MENU | 19 / 19 PASS, 70 / 70, 0 errors |
| Lesson 1 full run | PASS: 10 words, score 95, 0 errors |
| Lesson 6 full run | PASS: 12 words, score 80, 0 errors. Run on a reset profile, same as the v3.6 log. The 13 words / 90 in v3.13 included a Tutor review word |
| Explore (apple, camera check) | PASS, 0 errors |
| Relax standalone + inter-lesson | PASS, 0 errors |
| Task A fade check | PASS: classroom at alpha 0.21, café only at black (alpha 0.97), no empty frame |

One odd run: a self-test started straight after my hold measurements, without resetting the teacher, gave 53 / 54
(GOOD tied with THANK-YOU at 0.05). GOOD is not one of the changed words. From the rest pose it was 54 / 54 five
times, with the fix on and with it off. Cause not established.

## Words the video must not show
**None of the three is excluded now.** If you want the safest set, prefer BAD and HOW over YES.

## Not done
- **Not merged.** Merge to main is your call.
- Real-headset passthrough toggle: **UNVALIDATED** (no Quest owned).
- v3.10 steps 3–4 (per-joint lag, phase damping): not started.
- The other 51 words are untouched and still not judged in motion. The same objective would cut HELLO from 43.8° to
  18.6° (measured, not applied).
