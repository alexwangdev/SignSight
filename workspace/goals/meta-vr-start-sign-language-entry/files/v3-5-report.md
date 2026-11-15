# SignSight Learn v3.5 — 12 dialogue words (Lesson 6 · Café) + Café Final Exam (2026-11-15)

Started from `ba722ae` ("SignSight Learn v3.4: Finger Relax mini-game (between lessons + standalone)"; clean tree).
No APK was built, no Android module was installed, no Unity / URP / Meta SDK / XR Hands version or setting was changed,
no package / font / model / network call was added. Checked in Unity Editor Play Mode with the Meta XR Simulator (XR
active). Screenshots and check logs: `v35-screens/`.

## 0. Counts (verified, not copied from the brief)
- Before: `templates.json` 50 templates, 47 scored, 3 TEACH-ONLY (GOOD-MORNING, TEACHER, TODAY) — as the brief said.
- After: 62 templates, **54 scored** (47 + 7 new), 8 TEACH-ONLY (3 + 5 new). `lessons.json` 6 lessons, 62 words;
  `LessonManager.ExpectedWordCount = 62`, the assert passes (no error at load). `word_sources.json` 62 entries.
- No new movement detector: every scored new word uses an existing generic detector (§2.3 of the brief:
  "at most one new" — zero were needed). `TrajectoryTracker.cs`, `SignScorerV31.cs` and the other protected files are
  unchanged.

## 1. Task A — Lesson 6 · Café (12 words)

Each word was looked up on Lifeprint first (URL + quote in `word_sources.json`, new `quote` field), then the template and
the lesson cue were written from that text only.

| # | Word | Lifeprint source | Scored? | Detector / reason |
|---|---|---|---|---|
| 1 | ME (I / me) | lifeprint.com/asl101/pages-signs/m/me.htm — "you just point at yourself or touch your index finger to your chest" | scored | tap, index on the chest |
| 2 | YOU | lifeprint.com/asl101/pages-layout/indexing.htm — "you just point at the person you are talking to" (y/you.htm is video only) | **TEACH-ONLY** | a held point collided with GO in the teacher self-test (GO was recognised as YOU) |
| 3 | ONE | lifeprint.com/asl101/topics/signingnotes.htm — "the number "1" uses an index finger handshape"; pages-layout/numbers.htm — "as a general rule do 1-5 palm back" | scored | hold, shoulder height |
| 4 | TWO | lifeprint.com/asl101/pages-signs/t/two.htm — "done with the index finger and middle finger ... palm back" | scored | hold, shoulder height |
| 5 | THREE | lifeprint.com/asl101/pages-signs/t/three.htm — "done with the thumb, index finger, and middle finger ... palm back" | scored | hold, shoulder height |
| 6 | BIG | lifeprint.com/asl101/pages-signs/b/big.htm — "flat hands ... palms facing inward. Move your hands beyond the width of your body" | scored | stroke, both hands, mirrored apart |
| 7 | **PAY** (replaces SMALL) | lifeprint.com/asl101/pages-signs/p/pay.htm — "place the tip of your right index finger on the coin, then flick the coin to the person whom you are paying"; "a directional sign" | **TEACH-ONLY** | directional (the flick goes toward the payee) |
| 8 | MORE | lifeprint.com/asl101/pages-signs/m/more.htm — "flattened "O" hands. Bring both "O" hands together." | scored | converge (one meeting — Lifeprint describes no repeat) |
| 9 | FINISH | lifeprint.com/asl101/pages-signs/f/finish.htm — "Each hand should face you, with your fingers pointing upward ... Twist both hands quickly" | scored | turn, both hands (palms in -> forward); one twist ("seems more final") |
| 10 | **HERE** (replaces TEA) | lifeprint.com/asl101/pages-signs/h/here.htm — "relaxed flat hand shape ... palms facing up ... about belly height ... Each hand moves in a small circle" | **TEACH-ONLY** | both hands circle; the circle detector follows the right hand only |
| 11 | MONEY | lifeprint.com/asl101/pages-signs/m/money.htm — "a flattened "O" ... smacks it (lightly) twice onto the palm of the base hand" | **TEACH-ONLY** | scored as a tap onto the left palm it collided with BREAD in the self-test (BREAD was recognised as MONEY) |
| 12 | HOW-MUCH | lifeprint.com/asl101/pages-signs/h/how-much.htm — "moves upward while changing a palm-up "O" into a loose-5 handshape"; "furrow your eyebrows a bit" | **TEACH-ONLY** | the hand opens while it rises — no generic detector for a handshape change during a lift |

**Dropped / replaced (Lifeprint-first rule):**
- **TEA -> HERE.** t/tea.htm gives only the handshapes ("an "F" handshape on your dominant hand and a "C" (or an "O")
  handshape on your non-dominant hand"); the movement is shown only in the animation, and Lesson 27 / the practice
  sentence page add no text. NAME (first reserve) was skipped: n/name.htm gives the H handshapes and "a double
  movement" but no contact point or orientation. HERE (second reserve) is clear.
- **SMALL -> PAY.** s/small.htm gives "palms facing each other" and "bring them together using a relatively quick
  movement" but no handshape — not guessed. PAY (third reserve) is clear.
- Consequence for the exam: TEA is not on the menu; the drinks are **COFFEE · MILK · WATER**. BIG has no partner, so
  there is no size turn (as the brief says).

**Faces:** HOW-MUCH: furrowed brows + head tilt (WH-question; Lifeprint quote above), caption only, never scored.
Every other new word: neutral, `face_source: "none — neutral by default"` (numbers stay neutral; FINISH's basic sign
mentions no expression).

**Fixes found by the checks (data only):**
- MORE first failed the self-test (the teacher's fingertips did not meet): its start points were moved to the midline,
  as PAIN's are.
- ONE / TWO / THREE as first written (hold at chest height, 0.14 m radius, tolerance 0.35) let the Simulator's
  *resting* hands fire THREE attempts: a still hand "performs" a hold, and with handshape weighted 0.4 a wrong handshape
  still totalled 0.30. Now they are held at **shoulder height**, radius 0.07 m, tolerance 0.20: the resting-hand probe
  recorded **zero** recognitions afterwards, and the self-test still passes them (totals 0.04–0.06).

**Lesson list:** six lesson buttons fit at the existing 0.13 m discs; the row's spacing on the lesson list is now
0.145 m (was 0.175 m) so LESSON 1 and LESSON 6 stay inside the view (`v35-lessonlist.png`). Other button rows unchanged.

**Coverage / model verification (§2.4):** cut first, as the brief allows. `check_coverage.py` was not updated and
**new-word teacher verification: not run, Lifeprint only.** No model check is claimed for the 12 words.

## 2. Task B — Café Final Exam

- `Mode.Exam`, `StartExamMode()` (Start card) and `StartExamFromSummary()` (button **EXAM**, cup icon, on the Lesson 6
  summary next to RELAX and FINISH; shortened from "TAKE THE FINAL EXAM" — the long label covered the icon on a 0.13 m
  disc). No gating. The summary entry keeps the LessonSummary phase (as v3.4 Relax does), so the lesson score and streak
  are not reset; the exam never writes to ScoreManager, LearnerProfile or the Tutor.
- `Assets/Scripts/Exam/ExamMode.cs` (one file; the café props and the teacher move are folded in):
  - `Turn[] Script` with `expected: string[]`; the 7 turns below. The clerk signs with
    `TeacherAvatarDriver.Demonstrate` (same call as Learn); the question is also text on the word card. The card never
    shows the answer word before HINT (checked).
  - Correct = the lesson rule `ScoreManager.IsCorrect` against any word in the turn's set. 2 attempts: +10 / +5 / 0.
    10 s with no recognition -> 0. After a miss the clerk shows a valid answer once, then the next turn.
  - **HINT rule** (in code and here): HINT shows the valid answers; that turn scores at most +5 (SecondTryPoints) even if
    the next attempt is right.
  - Wrong sign = "Looks more like {X} — try again" (the feedback panel's wording); a sign outside the set is wrong, never
    "close enough".
  - Own total and stars: `ScoreManager.Stars(total)` on the exam's total (≥70 = 3, ≥50 = 2, ≥30 = 1).
  - End: the clerk signs FINISH (receptive), then the end screen: `Café Exam done — {total} / 70`, "N of 3 stars", one line
    per turn (set, 1st try / 2nd try / missed, the chosen word on choice turns, "(hint)", "missed (time) · clerk showed
    X"). Buttons RETRY EXAM (Replay slot) and MENU.
  - Recognition is on only during the learner's turn.

| Turn | Clerk signs (receptive) | Learner (scored) — valid set |
|---|---|---|
| 1 | HELLO, GOOD-MORNING | HELLO |
| 2 | WHAT, WANT | WANT |
| 3 | — (waits; menu board) | COFFEE / MILK / WATER |
| 4 | — (nods) | PLEASE |
| 5 | HOT, COLD | HOT / COLD |
| 6 | ONE, TWO | ONE / TWO / THREE |
| 7 | THANK-YOU | THANK-YOU |
| end | FINISH | — |

## 3. Task C — café props
- **Model: Unity primitives (the brief's final fallback).** The Sketchfab pack needs an account sign-in to download,
  which I cannot do on the user's behalf, and the KayKit download was not attempted in this session (time; the brief says
  the model must never block the exam). No third-party model was added, so there is no licence to credit; the README
  says "built from Unity primitives — no third-party model".
- Passthrough stays on; no room, walls, floor or skybox. Props (LearnUI local), built by `SignSightModesSetup`:
  - **Counter:** box at (-0.30, -0.78, 1.25), 0.80 x 0.50 x 0.25 m, top at y -0.53.
  - **Menu board:** dark rounded panel at (-0.95, -0.22, 1.45), lower left, clear of MENU. It lists COFFEE / MILK / WATER;
    no prices.
  - **Cup:** white cylinder with a coffee top, on the counter at (-0.08, -0.53, 1.16); it appears after turn 3.
- **Teacher:** moved behind the counter to (-0.30, -1.67, 1.60) while the exam runs and put back on exit (same avatar).
  Face and word card are unobstructed (`v35-exam-cafe-fullview.png`, `v35-exam-cup.png`).
- **Budget:** 5 renderers + 1 text = **6 draw calls**, **240 triangles** (+ the text quads); no lights, no
  post-processing.
- The menu text is put on the board by `ExamMode` when the exam starts (the saved scene dropped the text's x / y under
  the plain `CafeProps` parent). On the end screen RETRY EXAM sits 0.12 m left of centre so it does not cover the
  breakdown.
- In the game-view capture (a cropped strip of the eye view) the counter is below the frame. The camera-source captures
  (`*-fullview.png`, `v35-exam-cup.png`) show the whole eye view.

## 4. Start screen: 2 x 2 grid (no fallback needed)

| Card | Position (LearnUI local) | Viewport (left eye) |
|---|---|---|
| Learn & Test | (0.06, 0.075, 0.90) | x 0.47–0.59, y 0.44–0.57 |
| Explore | (0.31, 0.075, 0.90) | x 0.60–0.71, y 0.44–0.57 |
| Finger Relax | (0.06, -0.235, 0.90) | x 0.47–0.59, y 0.30–0.43 |
| Final Exam | (0.31, -0.235, 0.90) | x 0.60–0.71, y 0.30–0.43 |

- **Cards:** discs 0.165 m (as v3.4); panels 0.23 x 0.28 m. Final Exam: cup icon, `Order a coffee · Talk to the clerk`,
  `7 answers · 70 points`. The Learn card now says `62 words · 6 lessons`.
- **Clearances (viewport):**
  - Title backing at y 0.58–0.63, above the top row; the title (0.30, 0.36, 1.35) is not moved.
  - Instruction moved under the grid to (0.155, -0.38, 0.70), viewport y 0.26–0.27.
  - Teacher's head at x 0.40; the grid starts at 0.47.
- **Screenshots:**
  - `v35-start.png`: game view; it crops the bottom row.
  - `v35-start-fullview.png`: camera view, shows all four cards.

## 5. Checks (Editor Play Mode + Meta XR Simulator, 0 errors)
Logs:
- `v35-exam-check.txt` (`Assets/Scripts/DevTools/ExamCheck.cs`), `v35-lesson6-smoke.txt`, `v35-lesson1-smoke.txt`
- `v35-explore-check.txt`, `v35-explore-camera-check.txt`, `v35-relax-check-taskA.txt`, `v35-relax-check-taskB.txt`
- `v35-selftest.txt`
- The regressions ran back to back with `V35Regression.cs`.

| Check | Result |
|---|---|
| Lesson 6 in the Learn loop (demonstrate -> copy -> score -> feedback -> summary) | PASS — 12 words (7 scored, 5 practice only), score 80, 0 errors, 42 s |
| 62-word assert | PASS (no error at load) |
| Start: 4 cards, pinchable (the cards' select events), no overlap, teacher's face clear | PASS (viewport table above) |
| Exam happy path: Start card -> 7 first-try answers -> 70 / 70, 3 stars, 7 "1st try" lines -> MENU -> Start | PASS (37 s; `v35-exam-end.png`) |
| Exam realistic path: 2nd try (+5), two wrong (0, clerk shows COFFEE), HINT then right (+5), 10 s timeout (0, clerk shows HOT) | PASS — total 40 = 10+5+0+5+0+10+10, 1 star = Stars(40); breakdown lists each case |
| Choice turns: WATER / MILK for the drink, COLD / HOT, TWO / THREE / ONE score the same | PASS |
| Sign outside the set (WATER for HOT? COLD?) -> wrong attempt, "Looks more like WATER" | PASS |
| Answer word hidden before HINT; HINT shows it ("this turn max +5") | PASS |
| RETRY EXAM -> fresh total; MENU -> Start, teacher back, café hidden | PASS |
| EXAM on the Lesson 6 summary -> exam, lesson score unchanged; not shown on Lesson 5's summary | PASS |
| Resting Simulator hands during an exam turn | 0 recognitions; the turn ended by the 10 s timeout |
| Regression: full Lesson 1 | PASS — 10 words, score 95, 0 errors (same as v3.3 / v3.4) |
| Regression: Explore virtual objects (`PreferCamera = false`), apple -> APPLE -> demo -> Replay | PASS |
| Regression: Explore YOLO mock camera | PASS — apple 0.74 / cup 0.80 / bottle 0.92, 7.2 fps |
| Regression: Relax standalone (18 checks) + inter-lesson (10 checks, incl. RELAX -> Lesson 2, last lesson (now Lesson 6) -> Complete) | PASS |
| Teacher self-test | PASS — **54 / 54** scored signs (47 old + 7 new) |

**How the checks were driven (stated plainly):**
- **Exam and Lesson 6 / Lesson 1 runs:** the learner's attempts were injected as recogniser results (as in v3.1–v3.4);
  the clerk / teacher demonstrations, timers, scoring and UI ran for real. **No answer was signed with real webcam hands
  in the Simulator.**
- **Summary shortcut:** the Lesson 6 and Lesson 5 summary cases used the test shortcut (start the lesson, set the summary
  phase); Lesson 6 itself was also played in full.
- **Learner profile:** backed up, reset for the full-lesson runs, and restored.

**Problem found and fixed during the work:**
- **The cause:** the teacher self-test poses the avatar's bones in Edit Mode. Saving the scene right after (the setup
  script) wrote 40 posed bone rotations into `Main.unity`. The teacher then rested in a wrong pose, and BAD / BREAD /
  PAIN failed (51 / 54).
- **The fix:** the scene was restored from `ba722ae` and the setup re-applied without a self-test before saving. The
  self-test is back to 54 / 54.
- **Rule from now on:** never save the scene after a self-test in the same session.

## 6. Not verified / limits
- **Real hands:** none of the 12 new signs and no exam answer was tried with real hands (webcam or Quest). The
  number signs' shoulder-height hold, BIG's stroke, FINISH's turn and MORE's converge are verified only against the
  teacher's own motion.
- **Explore wording:** Explore still says "Not in my 50 words yet" (Explore files are protected); with 62 words this
  wording is now slightly stale.
- **No café model:** primitives only.
- **TGCN / coverage:** not run for the new words.
