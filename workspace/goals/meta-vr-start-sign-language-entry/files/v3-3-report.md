# SignSight Learn v3.3 — Start screen with two modes (Learn & Test, Explore) (2026-11-14)

Started from `8c416e7` (verified with `git log --oneline -1` after fetching; local == origin/main). No APK was built, no
Android module was installed, no Unity / URP / Meta SDK / XR setting was changed. Checked in Unity Editor Play Mode with
the Meta XR Simulator (XR active). Screenshots and the timed check log: `v33-screens/`.

## 1. Task A — done

### Flow and mode state
- **Mode state lives in `LessonManager`** (`enum Mode { Start, Learn, Explore }`, `CurrentMode`, `ModeChanged`),
  separate from `Phase`. Chosen over a new `AppMode.cs` because the existing buttons already call LessonManager and only
  need to become mode-aware. `Phase.Menu` keeps meaning "pick a lesson" in Learn mode.
- The app opens on **Start** (after lessons load). `StartLearnMode()` -> lesson list; `StartExploreMode()` -> Explore.
- **MENU / back:** the Next button is MENU outside a lesson (lesson list, Explore) and at the end of a run; all three
  land on Start (`GoStart()`). Inside a lesson Next behaves as before.
- In Explore the existing buttons are reused: Replay replays the chosen word, Try Again is labelled **BACK**
  (back to pointing), Next is **MENU**. Speed works in Explore; FACE appears in Explore for words with a face.
- Score, streak, top bar and Tutor panel are shown in Learn only; Explore shows no score (not even 0), no stars, no
  Tutor, no "wrong" state. Start view: `LessonUI.RefreshStartOrExplore()` (extended `LessonUI.cs`, no new StartScreen.cs).

### Start screen layout (`Main.unity`, built by `Assets/Editor/SignSightModesSetup.cs`)
- **Teacher stands left of centre** (x = -0.32, as in v3), so both cards go **side by side on the right half**:
  Learn & Test at (0.045, 0.05, 0.90), Explore at (0.36, 0.05, 0.90). Each card: pinch disc 0.20 m (1.54x the
  0.13 m lesson disc), code-drawn icon (book / eye, `ButtonIcon` + `UIStyle`), title 0.24, rounded panel 0.28 x 0.36 m
  with subtitle and small line (`50 words · 5 lessons` / `3 objects to try`).
- Title = the word card (scaled 0.7) at (0.30, 0.36, 1.35): `SignSight Learn` / `Learn 50 signs · Watch the face,
  copy the hands`. Instruction `Pinch a mode to start` under the cards at (0.155, -0.17, 0.70).
- What overlapped and was moved (Simulator view):
  1. first try, cards at z 0.62 either side of centre: far too large, the left card covered half the teacher, subtitles
     cut off, the title sat on the instruction line;
  2. cards at z 0.90, x = ±0.34: the left card still covered the teacher's face (he stands left of centre);
  3. final: both cards on the right half, subtitles moved below the discs, instruction moved under the cards.
  The teacher's face is unobstructed (`v33-start.png`).
- **Lesson list:** five lesson buttons stay in the row at y = -0.12 (unchanged); a sixth button (MENU) did not fit the
  view, so MENU sits on its own at the top left, (-0.42, 0.10, 0.75), left of the teacher (`v33-lessonlist.png`).
- Button row height y = -0.12 unchanged.

### Explore (Task A: virtual objects)
- Table (thin cube, no collider) at (0, -0.34, 0.80); objects on it, **0.28 m apart**, primitives only, no labels:
  Apple (red sphere + stem) at x = -0.28 -> `apple` -> APPLE; Cup (cylinder) at x = 0 -> `cup` -> COFFEE, card
  "COFFEE / Cup — COFFEE"; Bottle (cylinder + cap) at x = 0.28 -> `bottle` -> WATER, card "WATER / Bottle — WATER".
  Positions are relative to `LearnUI` (the seated head anchor). In the Simulator's narrow view the objects sit at the
  bottom edge (the learner looks down at the table).
- `Assets/Scripts/Explore/ExploreMode.cs`: pointing = index straight (curl <= 0.35), middle / ring / pinky curled
  (>= 0.5) from the existing `XRHandsProvider` + `CurlCalculator`; ray from the index tip along proximal -> tip;
  `Physics.Raycast` to `ExploreObject` colliders; **hold 0.6 s on the same object** (a change of target restarts the
  hold); the v3 hold ring fills while holding; a thin pointing ray is drawn. On select: word card (word, meaning, cue from
  lessons.json, face line if any) and `TeacherAvatarDriver.Demonstrate(template)` — the same call as Learn mode.
- `Assets/Scripts/Explore/CocoMapping.cs` is the only label -> sign table (apple, cup, bottle). Any other label shows
  `<Label> — Not in my 50 words yet` and the teacher does nothing (checked with "banana", `v33-explore-unmapped.png`).
- None of the three Explore words has a teacher face (APPLE, COFFEE, WATER are neutral), so the FACE button never shows
  in Task A Explore; the code path is in place for mapped words with a face.

### A4 checks (Play Mode + Simulator)
| Check | Result |
|---|---|
| Start screen is the entry point; both cards visible, non-overlapping, teacher's face unobstructed | PASS (`v33-start.png`) |
| Learn & Test -> lesson list (5 buttons) -> full Lesson 1 (demonstrate, copy, score, celebration, summary) | PASS — 10 words, score 95, 9 celebrations, 0 errors (`LessonSmokeRun`) |
| MENU from the lesson list -> Start; MENU at the end of a run -> Start | PASS |
| Explore -> point + hold on the apple -> APPLE card -> teacher demonstrates APPLE -> Replay | PASS (`v33-explore-apple.png`, `v33-explore-replay.png`) |
| A ray passing over the objects (0.3 s each) selects nothing | PASS (`v33-explore-check.txt`) |
| Hold on the cup -> COFFEE "Cup — COFFEE", teacher demo 2.7 s | PASS |
| No hit for 3 s shows nothing; MENU from Explore -> Start | PASS |
| Explore shows no score / stars / Tutor / "wrong" | PASS (screenshots) |
| Regression: teacher self-test 47/47, Tutor Agent checks | PASS |

The pointing in these checks used a test ray (`ExploreMode.DebugRay`, editor only) aimed at the objects, because no
hands were in front of the webcam; the hold, selection, demo and UI logic all ran for real. **Pointing with real
webcam hands in the Simulator has not been tried yet.**

## 2. Task B — done in the Editor (mock camera); not validated on a Quest
Task B was started only after Task A passed. Explore now uses a camera + YOLO path **in the Editor only**; on a device
(and whenever the model or a mock source is missing) Explore falls back to the Task A virtual objects, so the Nov 18
build path is unchanged.

### Model
- **YOLOv9-t** (80 COCO classes) from Meta's `Unity-PassthroughCameraApiSamples` (MultiObjectDetection sample). Licence:
  MIT, per that repo's README (the weights come from MultimediaTechLab/YOLO, MIT). Labels: `coco-classes.txt` from the
  same sample.
- `Assets/StreamingAssets/yolov9-t.sentis`, **4.2 MB** (4,360,248 bytes, < 10 MB). Rebuilt with
  `Assets/Editor/ExploreModelBuilder.cs` from Meta's `yolov9onnx.onnx` with Meta's own output graph (box centres ->
  corners, max class score, class id) but **Float16 weights instead of Meta's UInt8 quantisation**: with Meta's
  `.sentis` the cup on the mock frame scored 0.12; the Float16 build gives the same scores as the ONNX (cup 0.80).
  The ONNX and Meta's original files stay outside Git (`files/explore-ref/`, git-ignored).
- Unity Inference Engine 2.6.1 (already installed), **CPU backend** as in Meta's sample. Letterbox preprocessing
  (aspect kept, grey 114 bars) instead of squashing: apple 0.26 -> 0.74.
- Loaded only when Explore uses the camera (`YoloDetector.Load`), released on MENU (`Unload`). Learn never loads it.
  No network, no cloud, no API key.

### Code (`Assets/Scripts/Explore/`)
- `ICameraFrameProvider` — `Frame`, `IsAvailable`, `Source`. The detector reads frames only through it.
- `MockCameraProvider` (Editor only) — first of: `StreamingAssets/explore-mock.mp4`, `explore-mock.png`, a webcam.
  Copies frames at 8 fps. Used here: `explore-mock.png` (1280x720).
- `QuestCameraProvider` — **stub**: `IsAvailable` false, throws `NotSupportedException`; never called. TODO: Passthrough
  Camera API (Horizon OS v74+, camera permission, a headset).
- `YoloDetector` — 8 fps target, score threshold **0.4**, IoU 0.6, NMS copied from Meta's
  `SentisInferenceRunManager.NonMaxSuppression`.
- `ExploreCameraView` — the frame on a 0.48 m panel at (0.38, 0, 0.95) (clear of MENU, the instruction and the
  teacher's face; checked with a viewport overlap scan), thin one-colour boxes + `label score`. Pointing at a box
  (hold 0.6 s, same ray and hold as Task A) selects its label; the panel hides while the teacher demonstrates.
- `CocoMapping` stays the only label -> sign table. Unmapped labels show `<Label> — Not in my 50 words yet` and the
  teacher does nothing.
- Start card small line becomes `Real objects (mock camera)` when the camera path is available.

### Mock frame
`files/explore-mock/compose_mock.py` puts four CC0 photos from Wikimedia Commons on a grey 1280x720 frame:
`Red_apple_on_a_plate_2017_A.jpg`, `Cup_of_coffee_with_beans.jpg`, `Bottle_of_Water.jpg`,
`Bunch_of_bananas_on_sale.jpg` (the banana is the unmapped test object). Output: `StreamingAssets/explore-mock.png`
(385 KB). Source photos are git-ignored (`explore-mock/src/`).

### B5 checks (Play Mode + Simulator, `ExploreCameraCheck`, log `v33-screens/v33-explore-camera-check.txt`)
| Check | Result |
|---|---|
| Mock frames reach the detector | PASS — `explore-mock.png` 1280x720 |
| Detection rate | 7.2 fps (target 8), 122 ms per inference on CPU; editor 22 fps with YOLO running (34 fps without). The first CPU run after load takes ~44 s in the Editor |
| apple / cup / bottle >= 0.4 | PASS — apple 0.74, cup 0.80, bottle 0.92 |
| Hold 0.6 s on the apple box -> APPLE card + teacher demo | PASS (demo 2.5 s) |
| Pointing at an empty part of the panel selects nothing | PASS |
| Unmapped label -> "Banana — Not in my 50 words yet", teacher idle | PASS, **with the threshold lowered to 0.3 for this check only** (banana scores 0.37 < 0.4 on this frame) |
| MENU -> Start and the model is unloaded; Learn runs with no YOLO loaded | PASS |
| Quest provider is never called | PASS (stub) |

Screenshot: `v33-screens/v33-explore-camera.png`. The pointing again used the test ray aimed at box centres on the
panel.

### Not validated
- **Quest camera alignment is unvalidated.** The panel mapping (2D box on a flat panel -> pointing ray) exists only for
  the mock. On a Quest a box must become a 3D ray from the camera pose and intrinsics plus a depth / environment
  raycast; none of that was run (no headset).
- Real webcam frames and real-hand pointing at the panel were not tried.
- The banana is below 0.4 at the spec threshold on this frame.

## 3. Not done
- QuestCameraProvider is a stub (no headset); Explore on a device uses the Task A virtual objects.
- No mode or card was cut. No new scene, font or package was added; one model file (yolov9-t.sentis, 4.2 MB) was added.

## 4. Needs a real Quest 3 / 3S before Explore can be claimed on-device
- Pointing with tracked hands (pose thresholds, ray stability, 0.6 s hold) — only the logic was checked in the Editor.
- Table height and object positions for a seated user (they sit low in the Simulator's narrow view).
- Task B: camera permission, Passthrough Camera API frames, 2D box -> 3D ray alignment, depth raycasting,
  thresholds at 0.5 / 1 / 2 m and front / 45° / side angles.
