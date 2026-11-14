# SignSight Learn v3.1 — report (2026-11-14)

Scope: SPEC v3.1 task brief (50 words, teacher verification, Adaptive Tutor Agent). No APK was built, no Android module was
installed, no Unity / URP / Meta SDK version was changed. Verified in Unity Editor Play Mode + Meta XR Simulator and with
the offline Python scripts only.

## 1. Words

- **50 asserted?** Yes, twice: `tools/build_words.py` asserts `len(WORDS) == 50` (unique) before writing anything, and
  `LessonManager` asserts / logs an error at load if `lessons.json` does not hold exactly `ExpectedWordCount = 50`.
- Lessons: 5 × 10, all unlocked (`Assets/StreamingAssets/lessons.json`), in the exact order of §2.1.
- **scored: true — 47. TEACH-ONLY — 3** (GOOD-MORNING, TEACHER, TODAY: compounds with two beats and a handshape /
  location change; the recogniser judges one movement). **DROP-CANDIDATE — 0, no swaps** (every word has a Lifeprint page;
  none is fingerspelled or distinguished only by a facial marker).
- Sources: `Assets/StreamingAssets/word_sources.json` — Lifeprint URL, handshape, location, movement, palm, non-manual and
  notes for all 50. Pages that are video-only (MOTHER, FATHER, BYE, BOY, TOMORROW) are sourced from Lifeprint's own text on
  `momdad.htm`, `boygirl.htm`, `topics/parameters-asl.htm` / `lessons/lesson06.htm`; BYE from the page label — marked
  "needs a human check".
- Templates: `Assets/Resources/templates.json`, v2 schema, 50 entries, `scored` + `teachOnlyReason` per word, generated from
  one table (`tools/build_words.py`), so templates, lessons and sources cannot drift apart. The 8 v3 entries are copied
  unchanged.

### Scope change approved by the user: new movement types
The 8-type enum could score about 14 / 50 words; most real Lifeprint movements are taps, strokes, circles, twists. On
2026-11-14 the user chose **"allow new movement types"**. Added (all data-driven, no per-word code):
`tap, stroke, circle, twist, turn, squeeze, converge, hold, rock` — `Assets/Scripts/Hand/SignScorerV31.cs`, plus named
body anchors shared by recogniser and teacher (`SignAnchors.cs`: forehead, temple, side-head, cheek, jaw, nose, face, mouth,
chin, throat, upper-chest, chest, stomach, neutral, and points on the left hand).

**TrajectoryTracker.cs changes (recorded as required):** 3 lines only — `SignScorer` made `partial`; the `default:` of the
movement switch calls `DetectV31` (was: "no movement"); `LocationError` uses the anchor when a template has one. The 8 v3
movement detectors, their thresholds and the v3 location rules are untouched (the v3 words have no anchor).
**Location weight: not changed** (0.2). MOTHER / FATHER (and EAT / DRINK, HUNGRY / THIRSTY, MAN / WOMAN) are told apart
without changing it: location is now the distance of the named contact point (e.g. thumb tip) to its anchor, and the tap
/ stroke detectors only start at their own anchor. Old value = new value = 0.2.

Other code changed (§5): TeacherAvatarDriver (anchor placement measured on the rig, second beat for compounds, speed
0.5x / 1x), SignMotion (motion for the new types, shared with the ghost hands), TemplateStore (new fields, user files get
new words merged in), SignRecognizer (TEACH-ONLY words are never candidates), FeedbackPanel (calls the Tutor Agent for the
hint text and the single-finger highlight; the brief listed GhostHand, but the finger colours are set in FeedbackPanel, so
GhostHand.cs was not touched). ScoreManager.cs was not changed. CurlCalculator.cs, the Rocketbox FBX / import script, rig,
blendshapes, SPEC files, Main.unity and all build / XR settings were not touched.

### Fixes forced by the self-test (v3 words protected)
The first self-test showed new words stealing v3 words (THANK-YOU read as BAD). Rule "if a new word breaks an old word,
fix or revert the new word" — fixed in the new words only:
BAD moved from v3 `flick-out` to `stroke` with "ends palm down" (Lifeprint); GOOD "ends palm up into the left palm"
(Lifeprint); strokes must start from a still hand at their own anchor and are penalised for sideways travel
(MAN / WOMAN, TOMORROW / GIRL); an unfinished new-type movement can never score as a perfect movement.

**Self-test result** (`teacher-verification/v31-selftest.txt`, `Assets/Editor/SignSelfTest.cs`): the teacher performs each
sign, its own hands go through the recogniser — **47 / 47 scored words Top-1 with a completed movement, including all 8 v3
words.** This proves teacher, definitions and recogniser agree. It is **not** a test with real hands: the new words have
**not** been tried with webcam hands in the Simulator yet (needs the user in front of the camera).

## 2. Coverage (WLASL, `teacher-verification/coverage_result_2026-11-14.txt`)

| asl100 | asl300 | asl1000 | asl2000 |
|---|---|---|---|
| 18 / 50 | 36 / 50 | **49 / 50** | 49 / 50 |

Only GOOD-MORNING is in no subset (model cannot judge). v3 check reproduced: asl100 covers 4 / 8 v3 words, asl1000 8 / 8.
Note: the brief's `check_coverage.py` and `coverage_result_2026-11-14.txt` did not exist on this machine; both were
written from scratch here (aliases listed in the script: THANK-YOU, GOODBYE, PAIN→hurt, MOTHER/mom, FATHER/dad).

## 3. Verification (`teacher-verification/teacher_verification_50.md`, `.csv`)

- Teacher recorded in Editor Play Mode (`Assets/Scripts/DevTools/TeacherRecorder.cs`, editor-only), front view, plain
  background, 30 fps, **speed 1x** (MediaPipe found the hands in ≥ 0.93 of frames for 48 / 50 clips, so 0.5x was not needed).
- TGCN asl1000 (`sharonn18/tgcn-wlasl`), MediaPipe Holistic → 55 keypoints → 50 frames, judged on Top-5.
- **Teacher: expected sign in Top-5 for 11 / 49.** PASS: NO, BAD, FATHER, WATER, HUNGRY, SAD, SICK, LOVE, WANT, NEED, WHAT.
- **Calibration on real human WLASL clips (same pipeline): 26 / 41 in Top-5.** 13 words are *model-unreliable-for-this-word*
  (the model misses the human too): YES, GOODBYE, BABY, WOMAN, BOY, GIRL, MILK, EAT, DRINK, APPLE, PAIN, WHO, WHY.
- 22 words REVIEW: the model gets the human but not the teacher, and the template matches Lifeprint (incl. all v3 words
  except NO). The model found only 1 of those 26 human-recognised words on the teacher — this looks like a systematic
  avatar-vs-human gap, not 22 template errors. Following the rule, **no template was changed because the model disagreed.**
- **FAIL — fixed:** PAIN (missing twist; added, re-recorded, re-run — still unreliable for the model). **FAIL — not fixed:**
  FRIEND (the hand swap between the two links is not shown; a swap was tried and broke the rig pose, reverted), TEACHER and
  TODAY (TEACH-ONLY compounds, simplified second beat).
- Pipeline calibration issue found and fixed first: missing hands written as zeros made the model collapse (0 / 10 on humans);
  filling gaps from the nearest frame gave 6 / 10 before any teacher clip was scored.
- **SignRep: not runnable in 30 min — limitation, not a pass.** Only a feature-extractor checkpoint is published
  (`ryanwongsa/SignRep` v0.0.1, CC BY-NC-SA 4.0); no ASL Citizen classifier. No SignRep numbers are claimed.

### Real human video replay (added after the first report; `teacher-verification/human_replay.md`)
143 real WLASL clips (49 words, up to 3 each) -> MediaPipe 3D hands in the signer frame -> replayed through the
recogniser, firing like the app. **20 / 137 scored clips recognised (15 %), 14 / 47 words with at least one clip.**
Four detector problems found and fixed (strokes firing inside circles, strokes needing a full stop, taps needing an
exact touch, turns firing while the hand is raised); the teacher self-test is still 47 / 47 afterwards. Most remaining
failures come from video depth (8-20 cm error; xy within 1-5 cm), rough MediaPipe finger curls and WLASL sign variants,
so 15 % is a lower bound for this pipeline, not a Quest prediction. The webcam test and a headset are still needed.

## 4. Tutor Agent (`Assets/Scripts/Learn/TutorAgent.cs`, `LearnerProfile.cs`)

Rule-based, on-device, no LLM, no API key, no network. Memory: **JSON file** in `Application.persistentDataPath`
(`learner_profile.json`).

- Perceive: every scored attempt (word, attempts, first-try result, worst finger and its curl, movement error, time taken).
- Decide: (a) failed twice → repeat next; (b) same finger red twice → repeat with only that finger highlighted;
  (c) not practised for 2+ lessons → review; (d) right first try twice in a row → skipped this round ("shown less often");
  (e) next word of the lesson.
- Act: next word + reason on a visible **Tutor panel** ("Next: YES — because you missed it twice (your index finger both
  times) / Focus: your index finger"), and hints by error type, e.g. "Index: bend more — yours is 5% bent, target is 100%",
  "Looks more like NO — check your index finger — bent in YES", "Bottom hand: keep it still — it moved 9 cm",
  "Flick faster — out and down in under 0.7 s". No generic "try again" text exists in the code.

Acceptance (§4.3), Play Mode (`Assets/Editor/TutorPlayTest.cs`, log `teacher-verification/v31-tutor-test.txt`):
| Check | Result |
|---|---|
| Fail YES twice → repeated next, panel names the reason | **PASS** |
| Word right first try twice → demoted next round | **PASS** |
| Spaced review after 2 lessons | **PASS** |
| Profile persists across Play Mode restart | **PASS** |
| Hints specific (finger / movement / word) | **PASS** (all hints in the log name a finger with numbers, the confused word and the feature, or the movement) |

Attempts in these tests were injected as recogniser results (the lesson flow, scoring, feedback and agent ran for real).

Other UI (LessonUI): 5 lesson buttons, **SPEED 1x / 0.5x** toggle (teacher timing only), "Practice only — not scored" for
TEACH-ONLY words (not counted toward score or stars), WH-word eyebrow caption on the word card ("Eyebrows: furrowed —
watch the teacher (not scored)"). Everything is visual; nothing uses sound.

**Full lesson in Play Mode with the Meta XR Simulator (XR active):** Lesson 1, 10 words, real-time teacher demonstrations,
one wrong-then-right attempt, GOOD-MORNING practised — **0 errors** (`teacher-verification/v31-lesson-smoke.txt`).

## 5. Not done, and why

- **Voice input** ("Show me COFFEE"): out of scope for this task (§4.2) — future step.
- **SignRep**: no runnable classifier (above).
- **Real-hand test of the 42 new words** in the Simulator: needs the user at the webcam. The Simulator's single-camera depth
  jitter will hurt the new forward / backward strokes (WANT, GO, LIKE, TOMORROW) and sagittal circles (HAPPY) most.
- **FRIEND** hand swap, **TEACHER / TODAY / GOOD-MORNING** second beats: simplified (TEACH-ONLY for the compounds).
- UI seen from the Simulator's default head height: the v3 button row partly covers the word card (v3 layout, unchanged).
- Nothing was committed or pushed.

## 6. Needs a human / Deaf-community check before submission

- All 50 definitions (sourced from Lifeprint / ASL University; not reviewed by Deaf signers). Start with the 22 REVIEW
  words where the model recognises humans but not the teacher, then FRIEND, then the video-only sources (BYE, BOY, MOTHER,
  FATHER, TOMORROW).
- Never write "validated by Deaf users". Form text: **"Sign definitions sourced from Lifeprint / ASL University
  (Dr. Bill Vicars); not yet reviewed by Deaf signers — corrections welcome."**
