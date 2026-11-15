# SignSight Learn v3.6 — real café + classroom models (no-login sources) (2026-11-15)

Started from `133ca96` (v3.5). No APK, no module install, no version or settings change, no package, no font, no GLTF
importer, no light, no network at runtime. Protected files untouched (scoring, recognition, Tutor, data files,
`ExploreMode.cs`, `YoloDetector.cs`, `RelaxMode.cs`). Checked in Editor Play Mode + Meta XR Simulator.
Screenshots and logs: `v36-screens/`.

## Sources
| Task | Source | Result |
|---|---|---|
| A — café | KayKit Restaurant Bits 1.0, CC0 — `git clone --depth 1 https://github.com/KayKit-Game-Assets/KayKit-Restaurant-Bits-1.0` | used |
| B — classroom, source 1 | Low Poly School Pack (reodesuxz, itch.io) | **source 1: login/blocked, fallback taken** — the page returns HTML; the file is behind itch.io's "Download Now" JavaScript flow, not a direct file URL. Nothing was downloaded from it, no account. |
| B — classroom, fallback | KayKit Furniture Bits 1.0, CC0 — `git clone --depth 1 https://github.com/KayKit-Game-Assets/KayKit-Furniture-Bits-1.0` | used |

- **Clones:** both went to a temp folder outside the project and were deleted after copying.
- **Not opened:** Sketchfab, Asset Store, Fab, Meshy, CGTrader.
- **Licences:** each pack's `LICENSE.txt` (CC0, Kay Lousberg) sits next to its FBX files. README → Third-party assets
  updated, and the old "built from Unity primitives — no third-party model" line was removed.

## Files imported (only these)
- `Assets/Models/Cafe/`:
  - `kitchencounter_straight_A.fbx`, `table_round_A.fbx`, `chair_A.fbx`
  - `restaurantbits_texture.png`, `LICENSE.txt`
- `Assets/Models/Classroom/`:
  - `table_medium.fbx`, `chair_A.fbx`, `shelf_A_small.fbx`, `book_set.fbx`
  - `furniturebits_texture.png`, `LICENSE.txt`
- Unity imported the materials as URP Lit with the atlas, so no material was authored.
- Each model is an empty, unrotated holder (keeping the object names) with the FBX instance as its child. The holder is
  scaled to a size in metres taken from the mesh bounds, so the FBX keeps its own import rotation and unit scale.
  The first try set the rotation on the FBX root and laid the desk on its side; that was fixed this way.
- No colliders, no shadows.

## Café (Exam) — `CafeProps`
Object names kept: `Counter`, `MenuBoard`, `MenuText`, `CafeCup`. The primitive counter was removed.

| Prop | Model | Position (LearnUI local, bottom-centre) | Size / scale |
|---|---|---|---|
| Counter | kitchencounter_straight_A | (-0.30, -1.03, 1.25) — same footprint, top at y -0.53 | 0.80 x 0.50 x 0.25 m |
| CafeTable | table_round_A | (1.50, -1.67, 2.45) | 0.80 x 0.72 x 0.80 m |
| CafeChair1 / 2 | chair_A | (1.09 / 1.91, -1.67, 2.45), facing the table | 0.85 m tall |
| MenuBoard + MenuText | (kept, panel) | (-0.95, -0.22, 1.45) | 0.34 x 0.30 m |
| CafeCup | (kept, primitive; the pack has no cup) | (-0.08, -0.53, 1.16), after turn 3 | — |

- **Teacher:** stays at `ExamMode.TeacherCafePosition` (-0.30, -1.67, 1.60), behind the counter.
- **Budget:** **8 renderers = 8 draw calls, 1,498 triangles** (counter 168, table 312, chairs 2 x 428, the rest is the
  kept panel / cup).

## Classroom (Learn) — `ClassroomProps`
- **Visibility:** shown only in `Mode.Learn` (`LessonUI.Refresh`), hidden in Start / Explore / Relax / Exam.
- **Placement:** all props are low, behind or beside the teacher.

| Prop | Model | Position (LearnUI local, bottom-centre) | Size |
|---|---|---|---|
| TeacherDesk | table_medium | (-0.95, -1.67, 2.05) | 1.10 x 0.75 x 0.55 m |
| Books | book_set | on the desk, (-1.15, -0.92, 2.05) | 0.30 m wide |
| Shelf | shelf_A_small | (-1.55, -0.45, 2.30) | 0.70 m wide |
| StudentDesk1 / 2 | table_medium | (1.55 / 2.20, -1.67, 2.70) | 0.60 x 0.72 x 0.45 m |
| StudentChair1 / 2 | chair_A | (1.55 / 2.20, -1.67, 2.35) | 0.85 m tall |
| Blackboard | rounded panel (not a model) | (-2.05, 0.70, 2.60), above the Tutor panel | 1.00 x 0.55 m |

- **Models:** 4 model types (the brief allows at most 5) — table_medium (teacher desk + 2 student desks), chair_A,
  shelf_A_small, book_set — plus the blackboard panel.
- **Budget:** **8 renderers = 8 draw calls, 1,530 triangles**.
- **Placement fixes:** the student desks were moved back once (at first they sat beside the learner's right hand), and
  the blackboard was raised once (at first it sat behind the Tutor panel).

## Checks (0 errors)

| Check | Result |
|---|---|
| Classroom only in Learn (lesson list and inside a lesson); café only in Exam; both hidden in Start / Explore / Relax | PASS (`v36-visibility-check.txt`) |
| Exam happy path 70 / 70 with the new counter, cup after turn 3, MENU -> teacher home + `CafeProps` hidden (+ the 16 other exam checks) | PASS 19 / 19 (`v36-exam-check.txt`) |
| Lesson 1 full run | PASS — 10 words, score 95, 0 errors (unchanged) |
| Lesson 6 full run | PASS — 12 words, score 80, 0 errors |
| Explore virtual objects + apple -> APPLE -> demo -> Replay; YOLO mock camera | PASS |
| Relax standalone (18) + inter-lesson (10) | PASS |
| Teacher self-test | PASS — 54 / 54 (run last, the scene not saved after it) |
| Teacher's face, word card, buttons clear | checked in the screenshots (`v36-cafe-fullview.png`, `v36-classroom.png`, `v36-classroom-fullview.png`) |

**Screenshots:**
- `v36-cafe.png` / `v36-classroom.png`: the game view, a cropped strip of the eye view. In the café one the counter is
  below the frame.
- `*-fullview.png`: the full eye view (camera capture).

**Not verified:** the props were checked in the Simulator view only, not on a headset or for a seated user's real
room. The unused v3.5 material `Assets/Materials/CafeCounter.mat` is left in place.
