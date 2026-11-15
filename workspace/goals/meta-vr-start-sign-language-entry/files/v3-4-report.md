# SignSight Learn v3.4 — Finger Relax mini-game (standalone + between lessons) (2026-11-14)

Started from `5755c27` ("SignSight Learn v3.3: Start screen with two modes, Explore with YOLO mock camera"; checked with
`git log --oneline -1`, clean working tree). No APK was built, no Android module was installed, no Unity / URP / Meta
SDK / XR Hands version or setting was changed. `lessons.json` and `templates.json` are unchanged; no word, sign,
template, score rule or Tutor rule changed. No package, font, model or network call was added. Checked in Unity Editor
Play Mode with the Meta XR Simulator (XR active). Screenshots and check logs: `v34-screens/`.

Finger Relax is a gentle ~1 minute stretch break for fingers that feel stiff after signing. It is not a lesson and not
therapy, and nothing in the app, README or this report claims it prevents or treats anything.

## 1. Task A — Finger Relax as a third mode (done)

### Mode and flow
- `LessonManager.Mode` is now `{ Start, Learn, Explore, Relax }`. `StartRelaxMode()` sits next to `StartLearnMode()` /
  `StartExploreMode()`. `SwitchMode()` turns RelaxMode on / off, sets `recognizer.Listening = false` and rests the
  teacher when entering Relax; `GoStart()` turns Relax off. `ModeChanged` fires, so `LessonUI` refreshes.
- **Mode is now set before Explore / Relax are switched on or off** (they notify the UI from `SetActive`). Found while
  testing: `GoStart()` from inside a lesson threw a NullReferenceException in `LessonUI.Refresh` because the UI still
  saw Learn mode with no word. v3.3 never reached that path from the UI (MENU is not shown mid-lesson).
- Buttons in Relax reuse the existing row: Try Again -> **SKIP** (`CopyAgain()` -> `RelaxMode.Skip()`), Replay ->
  **DONE** on the end screen (`Replay()` -> `RelaxMode.Done()`), Next -> **MENU** in the top-left corner, as in the
  lesson list (`Next()` -> `GoStart()`). SKIP is visible on every step; DONE appears on the end screen.
- Standalone: DONE and MENU both go to `GoStart()`. Relax never lands on the lesson list.
- In Relax there is no top bar (score / streak), no stars, no Tutor panel and no "wrong" state; the teacher rests while the
  ghost hand demonstrates (the teacher does not perform stretches).

### The game — `Assets/Scripts/Relax/RelaxMode.cs`
One component; the five steps are a data table (`Steps`) and every threshold is a named constant at the top of the
file. Hand data: the existing `XRHandsProvider` (`IHandProvider`) + `CurlCalculator`; the wrist and curl history: a
`TrajectoryTracker` instance (its ring buffer, reused; no second buffer). `SignRecognizer` is not used.
Either hand counts: the first tracked hand is used, the other one if it is lost.

| # | Step | Target (thresholds actually used) | Completes | Timeout |
|---|---|---|---|---|
| 1 | Open & Close | mean curl of index..pinky `< 0.25` held 1 s, then `> 0.75` held 1 s; 2 cycles | 4 holds | 20 s |
| 2 | Spread Wide | `Spread > 0.6` and mean finger curl `< 0.4`, held 2 s; 2 holds, letting go in between | 2 x 2 s | 15 s |
| 3 | Finger Piano | thumb tip -> index / middle / ring / pinky tip, in that order, `< 0.3` x hand size (wrist -> middle knuckle, the same normalisation as `CurlCalculator`'s pinch); taps >= 0.3 s apart | 4 taps | 20 s |
| 4 | Shake It Out | side-to-side wrist travel (body frame x, from `TrajectoryTracker`) `>= 3 cm` and `>= 4` direction changes within 2 s (a change counts after 0.8 cm back) | 1 bout | 12 s |
| 5 | Wiggle & Rest | any finger's curl changing `>= 0.3` within 1 s shows "Nice wiggling"; the step always ends at 8 s, hand or no hand | 8 s | 8 s |

- Each step shows: step name + one instruction line on the word card, "Step n of 5" and a status line ("Open wide",
  "Hold 1 of 2", "Thumb to middle", ...), a ghost hand with the target pose (open / soft fist / fingers fanned / thumb
  toward the target finger / a shaking hand / wiggling fingers), and the v3 ring filling toward the target.
- **No fail state.** A step ends by its target or by its timeout; both show `Nice — moving on` (0.6 s), then the ghost
  hand moves to the next pose (0.5 s). Never "Wrong" / "Try again".
- **No hand tracked:** the ring pauses and the card shows `Show me your hand` (amber). The timeout keeps running, so
  nobody gets stuck. Nothing counts as a miss.
- End: word card `All loose — nice work`, one soft sparkle (`CelebrationFX` with 0 stars, burst cut to 12 particles, no
  points), then DONE.
- Every Relax screen: `Fingers feeling stiff? Stretch, shake it out — no score here.` and, smaller, `Stop if it hurts —
  gentle only, not medical advice.` (instruction line).
- Full run: 25 s when every target is met quickly; 81 s when every step times out (20 + 15 + 20 + 12 + 8 s + transitions).

Small supporting changes:
- `GhostHand.Spread` (new, default 0 = unchanged look) fans the fingers for Spread Wide; the feedback panel's hands never
  set it.
- `UIStyle`: code-drawn `hand` and `check` icons (no font glyph).
- `LessonUI`: the hold ring moves next to the ghost hand in Relax and hides Learn's "3s" label there.

### Start screen: three cards (`Assets/Editor/SignSightModesSetup.cs`, the one setup script, run and saved)
The three-across layout fitted, so the 2 + 1 fallback was not needed. The teacher, the title and the instruction did not
move. `CardLayout()` re-applies the layout on every run (idempotent).

| Card | Position (LearnUI local) | Disc | Title | Icon | Panel |
|---|---|---|---|---|---|
| Learn & Test | (-0.035, 0.05, 0.90) | 0.165 m | 0.21 | book 0.048 | 0.215 x 0.31 m, centre y -0.06 |
| Explore | (0.20, 0.05, 0.90) | 0.165 m | 0.21 | eye 0.048 | same |
| Finger Relax | (0.435, 0.05, 0.90) | 0.165 m | 0.21 | hand 0.048 | same |

Subtitles 0.15 in a 0.195 m wide box. Finger Relax: `Stretch your fingers · Shake it out` /
`About 1 minute · No score`.

Viewport check in the Simulator (left eye, `WorldToViewportPoint`):
- **Panels:** x 0.43–0.54 / 0.55–0.65 / 0.66–0.77, y 0.42–0.57.
- **Title backing:** y 0.58–0.63, above the panels.
- **Instruction:** y 0.39–0.40, below the panels.
- **Teacher's head:** x 0.40, left of the first panel.
- **Subtitles:** none overflow (3–4 lines).

Screenshot: `v34-start.png` (the game-view capture is stretched sideways, as noted in v3.3).

Relax screen placement:
- **Ghost hand:** (-0.62, -0.03, 0.70), size 0.8, left of the teacher.
- **Ring:** (-0.44, -0.05, 0.70), next to the ghost hand.
- **Word card, instruction and SKIP:** at their v3 places. MENU sits top-left.

## 2. Task B — Relax between lessons (done)
- **RELAX button:** the lesson summary now shows **RELAX** (hand icon) next to **NEXT LESSON** / FINISH. It is a runtime
  copy of a row button and calls `LessonManager.StartRelaxThenNextLesson()`.
- **Entering Relax from the summary:** it records `_pendingLessonAfterRelax = LessonIndex + 1`, or -1 after the last
  lesson, and sets `RelaxMode.ReturnToLesson`.
- **DONE on Relax's end screen (this entry only):** starts the pending lesson, or the Complete screen after Lesson 5.
  The end screen says `Pinch DONE to start Lesson 2` / `Pinch DONE to see your results`.
- **MENU out of Relax:** clears the flag and goes to Start; nothing starts.
- **NEXT LESSON** keeps the v3.3 behaviour exactly. Relax is never played automatically.
- **Score and streak are not touched by Relax.** Found while testing: switching mode normally passes through
  `Phase.Menu`, and `ScoreManager` resets score and streak on Menu (95 -> 0 in the first run).
  - Fix: the inter-lesson entry and the return keep the `LessonSummary` phase, so the score carries on exactly as with
    NEXT LESSON (95 -> 95 after Relax and into Lesson 2).
  - Relax raises no word attempt, so the Tutor records nothing.

## 3. Checks (Editor Play Mode + Meta XR Simulator, 0 errors)
Logs: `v34-relax-check-taskA.txt`, `v34-relax-check-taskB.txt` (`Assets/Scripts/DevTools/RelaxCheck.cs`),
`v34-lesson1-smoke.txt`, `v34-explore-check.txt`, `v34-explore-camera-check.txt`, `v34-selftest.txt`.

| Check | Result |
|---|---|
| Start shows 3 cards, all pinchable (the Relax card's select event starts Relax), no overlap, teacher's face clear | PASS (`v34-start.png`, viewport numbers above) |
| Standalone, **target path:** Start -> Relax -> all 5 steps end by their targets -> `All loose — nice work` + sparkle -> DONE -> Start | PASS (25 s, endings target x5) |
| Standalone, **timeout path:** a tracked hand that never matches (curl 0.5, spread 0.3) -> every step times out, no "wrong" -> end screen -> DONE -> Start | PASS (timeout x4, Wiggle ends at 8 s; 80.8 s) |
| No hand for 2.5 s: `Show me your hand`, ring stays at 0, still step 1, no error | PASS (`v34-relax-nohand.png`) |
| SKIP -> next step; SKIP on step 5 -> end screen; MENU mid-way -> Start (never the lesson list) | PASS |
| No top bar / score / streak / Tutor panel in Relax; teacher rests; score unchanged; 0 word attempts | PASS |
| Inter-lesson: Lesson 1 played in full -> summary shows RELAX + NEXT LESSON -> RELAX -> finish Relax -> Lesson 2 starts automatically (MOTHER) | PASS (`v34-summary-relax.png`, `v34-relax-end-interlesson.png`; score 95 -> 95) |
| NEXT LESSON without Relax -> Lesson 2 straight away | PASS |
| MENU out of inter-lesson Relax -> Start, Lesson 2 NOT started | PASS |
| Lesson 5 summary -> RELAX -> finish -> Complete screen -> MENU -> Start | PASS |
| Regression: full Lesson 1 (demonstrate, copy, score, celebration, summary) | PASS — 10 words, score 95, 0 errors, 44 s (same as v3.3) |
| Regression: Explore virtual objects (passing ray, cup -> COFFEE, no hit, MENU) and apple -> APPLE -> teacher demo -> Replay | PASS (in the Editor Explore uses the mock camera, so `PreferCamera = false` was set for this check — the headset path) |
| Regression: Explore YOLO mock camera (apple 0.74 / cup 0.80 / bottle 0.92, hold on the apple box -> APPLE, banana -> "Not in my 50 words yet", MENU unloads the model, Learn never loads it) | PASS (5.3 fps / 176 ms this run; 7.2 fps in v3.3; same CPU, slower Editor frame) |
| Regression: MENU from the lesson list -> Start | PASS (`LessonSmokeRun` / RelaxCheck flows) |
| Regression: teacher self-test | PASS — 47 / 47 scored signs (`SignSelfTest`) |

How the checks were driven (stated plainly):
- **Relax steps:** fed through `RelaxMode.DebugSample` (simulated measurements: curls, spread, fingertip gaps, wrist x).
  These are **not real hands**.
  - What ran for real: the step logic, timers, ring, UI, buttons and return paths.
  - Separately, the real path (no test hook) read the Simulator's own simulated hands: tracked, curls 0.08–0.30, spread
    0.36–0.41. Step 1 counted the "open" half and then waited for the fist.
- **Inter-lesson summaries:**
  - Lesson 1's summary was reached by playing Lesson 1 in full (`LessonSmokeRun`, learner attempts injected as
    recogniser results, as in v3.1–v3.3).
  - For the NEXT LESSON, MENU-out and Lesson 5 cases, the summary phase was set directly after starting that lesson
    (test shortcut).
- **Learner profile:** the Tutor skips words this machine's saved profile had already mastered, so the first smoke run
  played only 1 word. For the full-lesson checks the profile was backed up, reset, and restored afterwards.
- **Editor crash:** the Editor crashed once mid-check after a script was edited during Play Mode. All checks above were
  then rerun from a clean start on the final code.

Console after the final runs: 0 errors from app code. Two entries came from test tooling:
- **ExploreCheck:** the first attempt aimed at the virtual objects while the mock camera path was on (fixed by setting
  `PreferCamera = false` for that check).
- **SignSelfTest:** it calls `Destroy` from Edit Mode (an existing editor-only tool, unchanged).

## 4. Not verified / needs a person
- **No step was tried with real webcam hands or on a Quest.** The pose thresholds (0.25 / 0.75 curl, 0.6 spread,
  0.3 x hand-size fingertip gap, 3 cm shake) are deliberately wide but untested on real hands. Fingertip-touch and shake
  detection in particular depend on tracking quality.
- The ghost hand's thumb-to-finger pose for Finger Piano is an approximation (the capsule hand bends the thumb toward the
  palm); the text line names the finger.
- Ring and ghost-hand positions were checked in the Simulator's view only, not for a seated user on a headset.

## 5. Not done / cut
Nothing was cut: both tasks and all five steps shipped. No new scene, font, package or model.
