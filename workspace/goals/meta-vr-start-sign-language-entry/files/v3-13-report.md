# SignSight Learn v3.13 — last motion prompt before the video

Branch `v3.10-motion-room`. **Not committed.** YES, BAD and HOW are still wrong in the shipped state, so the brief's
commit gate (all three human) is not met. Shipped state: clamp OFF, swivel + roll on.

## Task 1 — Lifeprint check (fetched 15 Nov 2026 from lifeprint.com)

| Word | Lifeprint says | Template | Verdict |
|---|---|---|---|
| YES | "The sign for "yes" uses an "S" hand." "When you raise and lower your fist, bending at the wrist it represents your head nodding "yes."" | Wrist nod (self-test hint: "Nod your wrist — up and down, twice") | **Matches** on handshape and movement. No change. |
| BAD | "Make the sign for "bad" by placing the fingers of your dominant hand against your lips." "Move your hand down and away." "End with your palm facing downward." | Ends palm down (self-test hint: "End with your palm facing down") | **Matches** the ending. Lifeprint gives no finger direction, so there is no contradiction to correct. No change. |
| HOW | Two curved "c" hands, "Place your hands together with the knuckles touching." "Roll the hands forward until the 'arches' are upside down--ending with your hands palm-up in 'cupping' handshapes." | Forearms crossed in front of the chest, hands near the opposite elbow | **Does not match the layout** (hands meet knuckle to knuckle, not crossed at the elbows). This is a position difference, not a BoneRot error. The brief allows only BoneRot corrections, so the template was not changed. |

## Task 2 — shoulder rotation (clavicle about Z), YES, BAD, HOW only
Test: +15° shoulder rotation, hold pose, shipped otherwise. Baseline = v3.12 clamp-off numbers.

| Word | Total before → after (right) | Left | Change | Kept? |
|---|---|---|---|---|
| YES | 77.8 → 71.5 (−6.3°) | — | yaw −66.1 → −51.3 | No: under 10° |
| BAD | 109.5 → 100.4 (−9.1°) | — | yaw −112 → −102 | No: under 10° |
| HOW | 72.9 → 67.1 (−5.8°) | 74.9 → 67.9 (−7.0°) | yaw 64.0 → 50.5 | No: under 10° |

Reverted for all three. The test lever was removed from the driver.

## Task 3 — hand-position move (≤ 5 cm)
**Not attempted.** Tasks 1–2 did not fix the words, and the shoulder test showed that single arm levers move the hold
by 6–9°, far short of the 10° threshold. Under the brief's hard cut-off, the words stay as they are.

## Task 4 — visual check
- **Hold frames from v3.12b** (shipped state, unchanged for these words): YES and BAD look wrong (wrist bent at a hard
  angle, palm sideways); HOW looks wrong (both forearms crossed, wrists bent steeply).
- Mid-motion frames for these words were **not re-captured** in this prompt (cut-off).
- Verdict: **all three still wrong.**

## Task 5 — regressions (run now, ungated)

| Check | Result |
|---|---|
| Self-test, clamp OFF (shipped) | **54 / 54** (`v313-selftest-shipped-clamp-off.txt`) |
| Exam happy path, Start card, MENU | **19 / 19 PASS, 0 errors** — after making two café-prop checks wait for the fade (see below) |
| Lesson 1 full run | PASS: 10 words, score 95, 0 errors |
| Lesson 6 full run | PASS: 13 words, score 90, 0 errors (same Tutor review pick as v3.9 / v3.10) |
| Explore (apple, camera check) | PASS, 0 errors |
| Relax standalone + inter-lesson | PASS, 0 errors |
| Task A fade check | PASS: classroom shown at start (alpha 0.20), old room kept until black, café only at black (alpha 0.98), fade ends clean |

**Exam check note:** the first V35 run had two FAILs (café props on / hidden). Both checks read the props the frame after
the switch. Task A now shows them at black, after the fade. I changed the two checks in `ExamCheck.cs` to wait for the
fade to finish; no game behaviour changed. The rerun passed 19 / 19.

## Words the video MUST NOT show (still wrong)
- **YES** (Lesson 1)
- **BAD** (Lesson 1)
- **HOW** (Lesson 5)

Use HELLO, THANK-YOU, COFFEE, HOT, COLD, HELP, WHERE and the Café Exam for the video instead (HELLO, WHERE and DRINK
were judged human in the v3.12b holds; the others are the fallback list from the brief, not re-judged here).

## Per-axis clamp (diagnostic only, off)
Pitch 70° / yaw 30° / twist tolerance 15°. Clamp-on self-test 48 / 54 (v3.12c). Not a shipping gate.

## Not done
- Commit (gate not met).
- Real-headset passthrough toggle — **UNVALIDATED**; no Quest owned.
- Steps 3–4 (per-joint lag, phase damping) — not started, as the brief says.
