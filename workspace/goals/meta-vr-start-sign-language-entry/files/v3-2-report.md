# SignSight Learn v3.2 — UI polish + teacher facial expressions (2026-11-14)

No APK was built, no Android module was installed, no Unity / URP / Meta SDK / XR setting was changed. Everything was
checked in Unity Editor Play Mode with the Meta XR Simulator (XR active). Screenshots: `v32-screens/`.

## 1. Task A — UI polish

| Item | Landed | Where | Simulator observation |
|---|---|---|---|
| A1 celebration | **Yes** | new `Assets/Scripts/Learn/CelebrationFX.cs` (chosen over putting particles in FeedbackPanel; FeedbackPanel only calls it) | Correct: one reused ParticleSystem, 40 gold + green confetti, 1.0 s, no audio; title pops 0.8x -> 1.15x -> 1.0x over 0.4 s; stars pop in one at a time (3 first try, 2 second try); streak line `Streak ×N +5` pops with the title. A full lesson fired it 9 times for 9 correct answers, never for the wrong attempt; NEXT stayed visible and unblocked (particles have no colliders). `v32-celebration*.png` |
| Wrong answers | unchanged | — | No confetti, no flash, no shake; ghost-hand correction as in v3 §8 (`v32-wrong.png`) |
| A2 backings | **Yes** | new `TextBacking.cs`, `RoundedPanel.cs`, `UIStyle.cs` | One style everywhere: dark rounded panel, 75 % opacity (`Resources/UI/UIPanel.mat`, DarkPanel set to the same), same corner radius (0.012 m), sized to the text. TopBar, Instruction, Tutor panel, WordCard, feedback text column, FACE caption all have it; the ghost-hand backing is rounded too. Cue line now white; amber `#FFD27F` only for the face line. |
| A3 buttons | **Yes** | `SignSightUiSetup.cs` (editor, applied to Main.unity once), `ButtonIcon.cs`, `LessonUI.cs` | Discs 0.10 -> 0.13 (1.3x), labels 0.15 -> 0.19, icon above every label: ↻ Replay, → Next, ↺ Try Again, ☰ Menu (on Next at the end), chevrons + `½× / 1×` Speed, book Lesson 1-5, face FACE. Spacing 0.14 -> 0.175. |
| Tutor panel | **Yes** | real scene object `LearnUI/TutorPanel` | `TUTOR` title in small caps, backing, body at the Instruction line's font size (0.26), at (-0.72, 0.08, 1.35) — left of the teacher, no overlap. No more cloning of the instruction text. |
| Speed button | **Yes** | real scene object `SpeedButton` | Same disc style + icon; at (-0.30, -0.12, 0.60) (see row height below). |
| Menu | **Yes** | runtime clones of Lesson1Button kept (they work) | `LESSON n` + topic on the second line, same size / icon on all five, no overlap (`v32-menu.png`). |

Changes made while checking in the Simulator:
- **Button row lowered from y = -0.05 to -0.12** (all buttons; Speed / FACE use the same height). At -0.05 the larger
  discs covered the teacher's face and the word card; -0.16 was cut off at the bottom of the Simulator view.
- **Feedback text sizes:** Hint 0.30 -> 0.22, LooksLike / movement line 0.22 -> 0.18, so NEXT no longer covers the
  last line (`v32-wrong.png`). Ghost-hand colours unchanged.
- **Word line auto-shrinks** to one line (sign words keep 0.9; "Learn 50 signs" no longer runs into the meaning).
- **Stars:** the project fonts have no ★ glyph (and no font may be added) — the old completion line rendered as
  missing-glyph boxes. Stars are now drawn icons in the celebration; the completion screen says "N of 3 stars".

Texts still without a backing: none of the panels; the TARGET / YOU labels sit on the ghost-hand backing and the "3s"
label sits inside the progress ring (both already on a dark shape).

## 2. Task B — teacher facial expressions

Data: `templates.json` `face` extended (`browInnerUp, browDown, browOuterUp, eyeSquint, eyeWide, mouthFrown,
mouthPress, mouthPucker, cheekSquint, noseSneer` + `preset`, `name`); old `{smile, jawOpen}` still loads. Learner's
user file gets the shipped face / head on load. Code: `FaceDriver.SetExpression(SignFace)` (blendshapes by ARKit name
suffix, left / right together, 0.3 s fade, idle blink kept, `Neutral()` kept), `TeacherAvatarDriver` (new face call,
head `tilt` / `bow`, `DemonstrateFace`).

**22 / 50 words have a non-neutral face** (target 15). Each has `face_source` + `face_note` in `word_sources.json`:

| source | words |
|---|---|
| lifeprint (quoted from the word's page) — 13 + THANK-YOU | HAPPY ("you've got to use your face"), SAD ("Tilt your head forward slightly, and make a sad face"), PAIN ("USE FACIAL EXPRESSION ... show it on your face"), GOOD ("change your facial expression to match your meaning"), BAD ("a frown or scowl"), PLEASE ("hopeful / pleading facial expression"), COLD ("use more facial expression"), WHAT ("wh-q expression"), WHERE / WHEN / WHY (where.htm: "furrowed eyebrows for all of the wh- question types"), WHO ("furrowing your eyebrows"), HOW ("furrow your eyebrows"), THANK-YOU ("Smile", v3 values kept) |
| emotion (plain meaning; Lifeprint silent) — 7 | TIRED, SICK, LOVE, SORRY (apologetic, not a smile), HELLO, GOODBYE, GOOD-MORNING (greeting warmth) |
| v3 kept — 1 | HOT (jaw open; Lifeprint's HOT does not mention blowing, so no pucker) |

**28 words stay neutral** (`none — neutral by default`): YES, NO (their v3 head nod / shake unchanged), MOTHER, FATHER,
FRIEND, BABY, MAN, WOMAN, BOY, GIRL, TEACHER, DOCTOR, WATER, MILK, EAT, DRINK, BREAD, APPLE, HUNGRY, THIRSTY, COFFEE,
HELP, LIKE, WANT, NEED, TODAY, TOMORROW, GO. Lifeprint describes no face for them. Specifically: HUNGRY / THIRSTY
(no mouth mentioned; a stronger face turns THIRSTY into CRAVE), NEED (a stronger face only adds degree), HELP (raised
eyebrows only for the question "Will you help me?").

Differences from the brief's suggested values (Lifeprint wins): **PLEASE** = hopeful / pleading (inner brows up 35 +
smile 20) instead of smile 30, because Lifeprint says "pleading". Old free-text face captions (SAD, WH) were replaced
by the generated expression line.

**Head (B3):** YES nod and NO shake unchanged. WH-words: roll tilt 8°, eased in, held (~0.8 s), eased out — the brief's
B3 instruction; the Lifeprint WH pages quote the eyebrows only, recorded as such. SAD: forward bow 8° (Lifeprint "Tilt
your head forward slightly"). SORRY: no bow (Lifeprint does not mention one). No other head motion.

**Face visibility:** at the brief's preset values the Rocketbox ARKit shapes were almost invisible (contact sheet:
a smile of 60 barely moves the mouth). FaceDriver has one display gain, `intensity = 1.8`, applied to every weight
(clamped at 100); data and preset proportions are unchanged. HAPPY's smile is still modest even at 100 (Rocketbox's
smile shape is subtle).

**B4 UI:** WordCard expression line only for non-neutral faces, in amber, naming brows / eyes / mouth
(e.g. "Face: eyebrows furrowed — WH-question — brows pulled together, eyes narrowed") followed by
"Not scored — watch and copy". FACE button (scene object, right of Replay) only during Demonstrate for those words,
label `FACE` + expression name, caption "Not scored — watch and copy"; it replays face + head only at 0.5x with the
hands at rest and underlines the expression line while it plays. FeedbackPanel and the Tutor Agent never mention the
face; there is no face score, face star or face progress anywhere.

**B6 checks (watched, not read from JSON)** — `v32-screens/faces-in-sign.png` (mid-sign close-ups) and
`faces-face-replay.png` (FACE replay, hands at rest): THANK-YOU smiles with the mouth slightly open; SAD inner brows up,
mouth down, head bowed; WHAT brows pulled together + head tilt; PAIN brows down + pressed lips; SORRY inner brows up;
BAD frown; HAPPY smile (modest). YES shows its nod. During SAD's sign the hands pass in front of the face — the FACE
replay is how the face is seen.

## 3. Presets
Used: **Smile** (HAPPY), **Sad** (SAD), **Furrowed** (WHAT, WHERE, WHEN, WHO, WHY, HOW), **Tired** (TIRED),
**Pain** (PAIN). Deleted as unused: **Raised** (no yes/no-question form among the 50 — "Raised: no word uses it yet"),
**Pucker** (HOT's Lifeprint page does not mention blowing).

## 4. Regression and checks
- Teacher self-test 47 / 47 scored words (all 8 v3 words included); Tutor Agent checks (repeat after two misses,
  demote after two first-try, review, persistence) PASS.
- Full 10-word lessons in Play Mode + Simulator: Lesson 1 and Lesson 5, 0 errors, XR active.
- THANK-YOU smile and HOT jaw kept; YES nod / NO shake unchanged.

## 5. Not done / limits
- Nothing cut from the brief. FACE button, particles and icons all landed.
- The Simulator view is narrower than a Quest: the button row height (-0.12) and the Tutor panel font (0.26 at
  1.35 m, small) were judged in the Simulator only; check on a headset if one becomes available.
- HAPPY's smile is subtle on this avatar even at full weight.
- WH head tilt follows the brief; Lifeprint's WH pages only quote the eyebrows.

## 6. Needs a human / Deaf-community check before submission
- All face assignments, especially the 7 `emotion` ones (TIRED, SICK, LOVE, SORRY, HELLO, GOODBYE, GOOD-MORNING) and
  the WH head tilt.
- Form wording: "Sign definitions sourced from Lifeprint / ASL University (Dr. Bill Vicars); not yet reviewed by Deaf
  signers — corrections welcome." and "Facial expressions are teacher demonstration only — Quest 3 / 3S cannot track
  the learner's face." Never claim "validated by Deaf users".
