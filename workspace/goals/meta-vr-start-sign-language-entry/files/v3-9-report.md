# SignSight Learn v3.9 — café shoes fixed, Start in classroom, 62-word text, dead material cleanup

Started from `f5fc69a` (v3.8, on main). Branch `v3.9-cafe-start-room`. No APK, no module install, no Unity / URP / Meta
SDK / XR Hands change, no package, no font, no new model, no network at runtime. Scoring, recognition, Tutor, data files,
`CurlCalculator.cs`, `SignRecognizer.cs`, `TrajectoryTracker.cs`, `TutorAgent.cs`, `ScoreManager.cs`, `YoloDetector.cs`,
`RelaxMode.cs`, `RelaxVisual.cs` untouched. Checked in Editor Play Mode + Meta XR Simulator.
**The real-headset passthrough toggle is still UNVALIDATED** (no Quest owned). Screenshots and logs: `v39-screens/`.

## Task A — café shoes: fix (a), teacher moved back
- **Picked:** (a) — `ExamMode.TeacherCafePosition` z **1.60 → 1.85** (+0.25 m). x and y unchanged (−0.30, −1.67).
- **Why:** the teacher's feet front edge was at z 1.16, inside the counter footprint (z 1.125–1.375). At z 1.41 the feet
  sit behind the counter's back face, and the line of sight from the camera to the feet passes through the plinth.
- **Not changed:** counter position, cup (`CafeCupPos`), menu board, counter size.
- **Result (Simulator camera capture `v39-cafe-shoes-fixed-fullview.png`):** shoes hidden; teacher's face, word card and
  menu board clear; the counter still reads as the barrier between teacher and learner. The cup appears after turn 3
  as before (checked by `ExamCheck`).

## Task B — Start screen in the virtual classroom
- **Table now:** Start = classroom, passthrough off; Learn = classroom, off; Exam = café, off; Explore = none, on;
  Relax = none, on.
- **`EnvironmentMode.cs`:** the on/off boolean is replaced by a room: `enum Room { None, Classroom, Cafe }`,
  `RoomFor(mode)`, and `CurrentRoom`. Passthrough = (room == None). At Start the room is applied immediately, with no
  fade (boot). Then a fade runs **only when the room changes**:

| Transition | Room change | Fade | Passthrough during |
|---|---|---|---|
| boot → Start | — | none (applied at boot) | off |
| Start → Learn | classroom → classroom | **no** | off |
| Learn → Start | classroom → classroom | **no** | off |
| Start → Exam | classroom → café | **yes** | off |
| Exam → Start | café → classroom | **yes** | off |
| Start → Explore | classroom → none | yes | on |
| Explore → Learn | none → classroom | yes | off |
| Learn → Relax | classroom → none | yes | on |
| Relax → Learn | none → classroom | yes | off |
| Learn → Start (exit) | classroom → classroom | **no** | off |

- **`LessonUI.cs`:** one line — `ClassroomProps` is shown for `Learn` **or** `Start` (same `phase != Loading` guard).
  `CafeProps` stays Exam-only. No other visibility line changed.
- **Overlap check:** in the Start capture the right student desk's edge touched the Final Exam card. I moved the **prop**
  (both student desks and chairs +0.15 m in x: desks 1.55/2.20 → 1.70/2.35; chairs likewise). Cards and teacher not moved.
- **Contrast check:** on the pale cream classroom wall the card title and subtitle were hard to read. I darkened the
  classroom **shell** wall (`ClassWall` (0.84, 0.88, 0.76) → (0.56, 0.62, 0.52)). The props and cards are unchanged.

## Task C — "62 words"
- `CocoMapping.NotInWords` → `"Not in my 62 words yet"`.
- `ExploreMode.cs` comments updated at line 11 and line 245 (as asked), **and at line 233** (its summary said "one of the
  50 words", which was the same stale text; I changed it too, comment only). No Explore behaviour changed.
- Check: `v33-explore-camera-check.txt` — "Banana — Not in my 62 words yet", teacher idle: PASS.

## Task D — dead material
- **Reference:** `SignSightModesSetup.cs` line ~126, `Mat("CafeCounter", …)` — only in the branch that creates a
  *primitive* counter. That counter is destroyed by the same Apply run (the v3.6 "remove primitive counter" step), so the
  material was never visible. On this tree `CafeProps` already exists, so the branch does not run.
- **Also checked:** no GUID reference to `CafeCounter.mat` anywhere in `Assets/` (scenes, prefabs, scripts).
- **Done:** removed the `sharedMaterial = Mat("CafeCounter")` line, then deleted `Assets/Materials/CafeCounter.mat` and
  its `.meta`. Re-applied the setup: 0 errors; the café still uses `kitchencounter_straight_A` (KayKit) with `restaurant` material.

## Matrix (Editor Play Mode + Simulator) — all PASS, 0 errors
Log: `v39-mode-matrix.txt`. Boot = classroom (no fade). The ten transitions above, each checked for passthrough state,
background alpha, the active room, and no hard cut (fade running 0.1 s after the switch when expected; nothing moved when not).
Budgets unchanged: café 1,584 tris / 13 draw calls; classroom 1,616 tris / 13 draw calls.

## Other checks (0 errors each)

| Check | Result | Log |
|---|---|---|
| Exam (happy path, retry, hint, timeout, choice turns, MENU → Start, FINAL EXAM from Lesson 6 summary) | PASS | `v35-exam-check.txt` |
| Lesson 1 full run | PASS — 10 words, score 95, 0 errors | `v35-lesson1-smoke.txt` |
| Lesson 6 full run | PASS, 0 errors — 13 words, score 90 (Tutor review pick; same as the v3.8 rerun) | `v31-lesson-smoke.txt` → `v39-lesson6-smoke.txt` |
| Explore (virtual objects, apple, unmapped object shows "62 words", YOLO mock camera) | PASS | `v33-explore-check.txt`, `v33-explore-camera-check.txt` |
| Relax standalone (Task A, 18) and inter-lesson (Task B, 10) | PASS, 0 errors | `v34-relax-check-taskA.txt`, `v34-relax-check-taskB.txt` |
| Teacher self-test | **54 / 54 scored PASS** (8 teach-only rows FAIL, as before) | `v39-selftest.txt` |

**Notes:**
- Lesson 1 and Lesson 6 log the `XR` flag as False in this session. The Simulator was not reporting an active XR device
  when the lesson runs started; the lesson flow itself was unchanged.
- The learner profile was backed up and restored around the Lesson 6 run and the self-test.

## Screenshots (`v39-screens/`)
- `v39-start-classroom.png` / `v39-start-classroom-fullview.png` — Start in the classroom: teacher and all four cards clear; no prop over a card.
- `v39-learn-classroom.png` / `v39-learn-classroom-fullview.png` — Learn in the classroom.
- `v39-cafe-shoes-fixed.png` / `v39-cafe-shoes-fixed-fullview.png` — Exam in the café: shoes hidden.
- `v39-explore-passthrough.png`, `v39-mode-matrix.txt`, plus the logs listed above.

## Issues and what was done
- **Editor restarted** during the session (it auto-shut-down after inactivity, then the pipeline was unreachable). Reopened the project; the saved scene had all v3.9 changes.
- **Font atlas drift** on save (`LiberationSans SDF - Fallback.asset`) — reverted before the commit.
- **Known, not changed:** when a room changes, its props are hidden or shown at the moment the fade starts (the
  `LessonUI` visibility update runs in the same mode event). For the first frames of the 0.3 s fade the old room's contents
  are gone while the screen is still clear. Fixing this means changing the visibility timing in `LessonUI`, which the brief
  did not allow. Recorded for a later version.

## Not verified
- Real-headset passthrough toggle and fade — **unvalidated**.
- Readability of the Start cards on a headset; the wall colour was judged in the Simulator capture only.
