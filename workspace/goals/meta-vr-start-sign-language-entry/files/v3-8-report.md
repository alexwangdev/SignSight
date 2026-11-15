# SignSight Learn v3.8 — full virtual classroom + café, Explore stays passthrough

Started from `e846e87` (v3.7). Branch `v3.8-virtual-rooms`. No APK, no module install, no Unity / URP / Meta SDK / XR Hands
change, no package, no font, no new model, no network at runtime. Protected files untouched (scoring, recognition, Tutor,
data files, `ExploreMode.cs`, `YoloDetector.cs`, `RelaxMode.cs`, `RelaxVisual.cs`, `CurlCalculator.cs`,
`SignRecognizer.cs`, `TrajectoryTracker.cs`, `TutorAgent.cs`, `ScoreManager.cs`, `ExamMode.cs`).
Checked in Editor Play Mode + Meta XR Simulator. **The real-headset passthrough toggle is UNVALIDATED** (no Quest owned).
Screenshots and logs: `v38-screens/`.

## Task 0 — passthrough per mode
- **File:** `Assets/Scripts/UI/EnvironmentMode.cs` (one file).
- **Hook:** subscribes to `LessonManager.ModeChanged` only. No toggle calls in ExamMode, RelaxMode or ExploreMode.
  Attached to the `LessonManager` object by `SignSightModesSetup.Apply()` (`AddComponent` if missing).
- **Behaviour:** `Learn` and `Exam` are virtual rooms: passthrough off, camera solid warm background
  `(0.35, 0.27, 0.22, 1)`. `Start`, `Explore` and `Relax` keep passthrough on, with a transparent background.
  Learn ↔ Exam changes the state too, and Explore ↔ Relax does not (no change, so no fade).
- **No hard cut:** a black quad on `CenterEyeAnchor` (URP Particles/Unlit copy of `UI/Confetti`, culling off) fades to
  black over 0.3 s; the passthrough switch happens at black; then it fades back over 0.3 s. The quad is inactive when
  not fading.
- **Note:** the fade is on the eye camera, not on `LearnUI`, because `LearnUI` is a world-space panel and would not cover
  the view.

## Task A — virtual café shell (Exam only, inside `CafeProps`)
- **Shell:** `RoomShell` child of `CafeProps`, primitive cubes, no colliders: floor, back wall, left and right walls,
  `RoomLight` (directional, 0.9, warm). Walls `CafeWall (0.86, 0.74, 0.58)`, floor `CafeFloor (0.45, 0.30, 0.20)`.
- **Plinth:** `CounterPlinth` (0.80 × 0.64 × 0.25 m). The KayKit counter's base is at y −1.03 (model bounds, from v3.6),
  so it floated 0.64 m above the floor. The plinth fills that gap from the floor up to the counter base.
- **Floor:** y **−1.67**, the teacher's feet and the café table and chair bases.
- **Geometry:** floor 5.4 × 0.1 × 3.0 m, centred z 1.5; back wall at z 3.05 (top y 1.60); side walls at x ±2.75.
  No ceiling. The back wall is behind every café prop (furthest is the table at z 2.85).
- **Props:** not moved, replaced or re-imported. `ExamMode.cs` unchanged.

## Task B — virtual classroom shell (Learn only, inside `ClassroomProps`)
- **Shell:** `RoomShell` child of `ClassroomProps`, same construction and light. Walls `ClassWall (0.84, 0.88, 0.76)`,
  floor `ClassFloor (0.60, 0.45, 0.32)`.
- **Shelf base:** `ShelfBase` (0.70 × 1.22 × 0.35 m) from the floor up to the shelf's bottom. The shelf's bottom is at
  y −0.45 (v3.6 placement), so it floated 1.2 m above the floor.
- **Floor:** y **−1.67**, the teacher's feet, the teacher's desk, the student desks and chairs.
- **Back wall** at z 3.05, behind the blackboard (z 2.60), and behind the student desks (furthest z 2.93).
- **Props:** not moved. Blackboard still at (−2.05, 0.70, 2.60).
- **Note:** the v3.6 report's Shelf coordinate (−1.55, −0.45, 2.30) is the shelf's **bottom** point, not its pivot.

## Budgets (measured with `EnvCheck`, `GetIndexCount` — the KayKit meshes are not readable)

| Room | Shell | Shell draw calls | Whole room (shell + props, no teacher, no UI) | Whole-room draw calls | Limit |
|---|---|---|---|---|---|
| Café (Exam) | 60 tris | 5 | 1,584 tris | 13 | shell < 2k / < 6; room < 22k / < 26 — PASS |
| Classroom (Learn) | 60 tris | 5 | 1,616 tris | 13 | shell < 2k / < 6; room < 22k / < 26 — PASS |

(The v3.6 props alone were 1,498 / 1,530 tris; the shell adds 60 each, and the measured totals are slightly higher.)

## Mode-switch matrix (`EnvCheck`, Play Mode + Simulator) — all PASS, 0 errors
Log: `v38-mode-matrix.txt`.

| Transition | Passthrough | Room | Fade at 0.1 s (no hard cut) |
|---|---|---|---|
| boot → Start | on | real | — |
| Start → Learn | off | classroom | yes |
| Learn → Start | on | real | yes |
| Start → Exam | off | café | yes |
| Exam → Start | on | real | yes |
| Start → Explore | on | real | no fade (passthrough stays on) |
| Explore → Learn | off | classroom | yes |
| Learn → Relax | on | real | yes |
| Relax → Learn | off | classroom | yes |
| Learn → Start (exit) | on | real | yes |

Each transition also checks the camera background alpha (1 virtual, 0 real), that `EnvironmentMode.VirtualRoom` matches,
and that only the right room is active. No mode showed the wrong room or passthrough through a virtual room.

## Other checks (Editor Play Mode + Simulator)

| Check | Result | Log |
|---|---|---|
| Exam happy path, retry, hint, timeout, choice turns, MENU → Start, FINAL EXAM from Lesson 6 summary | PASS (`ExamCheck`, 0 errors) | `v38-exam-check.txt` |
| Lesson 1 full run | PASS — 10 words, score 95, 0 errors | `v38-lesson1-smoke.txt` |
| Lesson 6 full run | PASS (0 errors) — **13 words, score 90** (see note) | `v38-lesson6-smoke.txt` |
| Explore virtual objects + apple → APPLE → demo → Replay | PASS | `v38-explore-check.txt` |
| Relax standalone (Task A, 18) and inter-lesson (Task B, 10) | PASS, 0 errors | `v38-relax-taskA.txt`, `v38-relax-taskB.txt` |
| Teacher self-test | **54 / 54 scored PASS** (8 teach-only rows show FAIL, as in v3.6) | `v38-selftest.txt` |

**Note on Lesson 6:** the v3.6 run was 12 words / 80. This run used a reset profile and the Tutor picked one review word
(YES, "last practised 6 lessons ago"), which made it 13 words / 90. The lesson flow and scoring are unchanged in code;
the difference comes from the Tutor's review pick on a reset profile. Lesson 6 finished with 0 errors.

## Screenshots (`v38-screens/`)
- `v38-classroom-fullview.png`, `v38-cafe-fullview.png`: the full eye view from the camera capture (900 × 945).
- `v38-classroom.png`, `v38-cafe.png`: game-view captures (a cropped strip of the eye view).
- `v38-start-passthrough*.png`, `v38-explore-passthrough*.png`: the real-room screens. **The Simulator's passthrough is not
  rendered into camera captures, so these are black** — the toggle is verified by the camera background and the
  passthrough flag, not by a visible real room.
- `v38-classroom-game.png`: the game-view capture of the classroom.

## Issues found and handled
- **Café floor:** the counter floated (base at y −1.03, floor at −1.67) → plinth under the counter.
- **Classroom shelf:** the shelf floated (bottom at −0.45) → base under the shelf.
- **Teacher's shoes in the café:** the teacher's feet sit inside the counter footprint (z 1.16 is the feet's front edge),
  so the shoes show below the counter in the café view. This comes from v3.6 placement; the brief says not to move the
  teacher, so it is recorded here and not changed.
- **Font atlas drift:** saving changed `LiberationSans SDF - Fallback.asset` at runtime; reverted before the commit (as in v3.4).
- **Unity Pipeline server** dropped out several times during the session; checks were re-run after reconnecting.
- **Logs:** the first EnvCheck run failed the error count because it read unreadable mesh triangles. Fixed with
  `GetIndexCount` and re-run; the matrix results were the same both times.

## Not verified
- The real-headset passthrough toggle (fade, background, passthrough layer) — **unvalidated**.
- Room lighting and wall colours on a real headset or a seated user's real view.
- Teacher's shoes in the café: visible in the Simulator capture; no change made.
- The unused v3.5 material `Assets/Materials/CafeCounter.mat` is still in place.
