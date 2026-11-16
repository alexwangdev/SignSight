# SignSight Learn v3.17 — BOY, APPLE, LOVE

Branch `v3.17-boy-apple-love` (on top of v3.16), committed, **not merged**. Clamp OFF, self-test 54 / 54 (five runs).

## What was wrong and what changed

| Word | Fault | Fix | Wrist bend before → after |
|---|---|---|---|
| BOY | Fingers pointed forward from the forehead, so the wrist was above the top of the head; no arm reaches that | Template: `fingerDir` (0, 0, 1) → (−1, 0, −0.3), `anchorOffset.y` 0.02 → −0.02 | 127.7° → 78.3° |
| APPLE | Fingers pointed forward from the cheek, so the wrist was beside the ear | Template: `fingerDir` (0, 0.2, 1) → (−1, 0, −0.3) | 131.7° → 52.4° |
| LOVE | Both wrists crossed at the same depth, so the forearms passed through each other | Driver: when the wrists are crossed, the right wrist sits 5 cm in front of the left | 35.8° / 31.9° (unchanged) |

- **Lifeprint check (fetched 15 Nov 2026).** APPLE: "placing the knuckle of your right index finger against your
  cheek", "pivot the hand back and forth". No palm or finger direction given. BOY: the page has no text description
  of the handshape or direction. LOVE: "crossing both hands over the middle of your chest". So nothing here
  contradicts the source; the finger direction is my choice, taken from what the arm can carry.
- **These are sign-definition edits** (two templates). The scorer does not read `fingerDir`. BOY's contact point
  moved 4 cm down, to the forehead proper (it was at the hairline); its location radius is 8 cm.
- The crossed-wrist rule only triggers for LOVE (checked across all two-handed words).

## Visual verdicts (`v317-screens/v317-<WORD>-<moment>-closeup.png`, strips in `strips/`)

| Word | Move | Mid | Hold |
|---|---|---|---|
| BOY | borderline: the elbow swings far out to the side on the way up | PASS | PASS: flat hand at the forehead, forearm rising from the side. Wrist is bent about 80°, which the sign has |
| APPLE | PASS | PASS | PASS, borderline: the fist sits at eye height and partly covers the right eye |
| LOVE | PASS | PASS | PASS: right forearm in front of the left |

Judged by me from stills.

## Verification

| Check | Result |
|---|---|
| Self-test, clamp OFF, from rest, five runs | 54 / 54 each. BOY 0.10, APPLE 0.02, LOVE 0.02, each top-1 |
| Elbow inside its limits at the hold | BOY 0.008 m over (was 0.12); APPLE and LOVE inside |
| Exam happy path, Start card, MENU | 19 / 19 PASS, 70 / 70, 0 errors |
| Lesson 1 / Lesson 6 full runs | PASS: 95, 0 errors / 80, 0 errors |
| Explore, Relax A + B | PASS, 0 errors |
| Fade check | PASS (classroom at alpha 0.20, café at 0.98) |

## Not done
- Not merged (v3.16 is not merged either; this branch contains it).
- The 62-word hold sheets were not re-captured; only these three words changed pose.
- Real-headset passthrough toggle: UNVALIDATED (no Quest owned).
