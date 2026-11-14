# SignSight Learn

Learn 8 useful ASL handshapes in mixed reality on Meta Quest 3 / 3S. A 3D teacher sitting opposite you shows each
sign, you copy it with your own hands, hold for 0.6 s, and get scored. When you are wrong, a ghost-hand comparison
shows which finger is off, by how much, and how to fix it.

Hands only, seated, no controllers. Built for the Meta VR Start Developer Competition 2026 (Productivity, New Experience).

## How it works

| Piece | Where |
|---|---|
| Hand features: curl per finger (bend angles between XR Hands joints, 0 = straight, 1 = curled) + palm direction | `Assets/Scripts/Hand/CurlCalculator.cs` |
| Templates (8 signs) and the recorder that tunes them | `Assets/Scripts/Hand/TemplateStore.cs`, `TemplateRecorder.cs`, `Assets/Scenes/TemplateRecorder.unity` |
| Recognition: weighted distance to templates, 5-frame smoothing, 0.6 s hold, 1 s lockout, Top1 / Top2 | `Assets/Scripts/Hand/SignRecognizer.cs` |
| Teacher: a humanoid driven directly from the same template numbers the recognizer checks | `Assets/Scripts/Learn/TeacherAvatarDriver.cs` |
| Lessons (2 × 4 words), scoring (+10 / +5 / streaks / stars), per-finger feedback | `Assets/Scripts/Learn/` |
| Lesson data | `Assets/StreamingAssets/lessons.json`, default templates `Assets/Resources/templates.json` |

No image-recognition model and no third-party sign-language API: recognition is computed from Quest's 26 hand joints.

## Third-party assets

- **Teacher character:** **Microsoft Rocketbox** avatar `Male_Adult_01` (facial version with 52 ARKit blendshapes),
  MIT License — https://github.com/microsoft/Microsoft-Rocketbox. Imported as a Unity Humanoid. We did not model it.
  Files: `Assets/Models/Rocketbox/`. `Assets/Editor/FixRocketboxMaxImport.cs` is a modified copy of the MIT import
  script from the same repository (scoped to that folder, Humanoid import; changes listed in its header).
- Meta XR All-in-One SDK v207, Unity OpenXR 1.18.0, Unity XR Hands 1.9.0, TextMeshPro.

## Honest notes

- **Static handshapes, not full ASL signs.** Several of these words are signed with movement in real ASL (for example
  COFFEE, HOT and THANK-YOU). v1 recognises one held handshape per word: COFFEE = C hand, HOT = A hand, ICED = L hand,
  YES = S fist, NO = index + middle, THANK-YOU = flat hand palm down, HELP = Y hand, HELLO = B hand. Treat them as
  handshape practice, not a complete ASL vocabulary.
- **Not tested on a real Quest.** Development and testing used Meta XR Simulator v207 with laptop-webcam hand tracking.
  Recognition thresholds (0.35 distance, 0.6 s hold) are untuned for real Quest tracking.
- Quest 3 / 3S has no face tracking, so nothing here depends on facial expressions.
- Not in v1: J / Z, R / U, M / N / T, the A–Z alphabet, continuous signing, two-way conversation.

## Running it

- Unity `6000.3.28f1`, URP, Android build target.
- Simulator: activate Meta XR Simulator (Window → Meta → Meta XR Simulator → Activate), open `Assets/Scenes/Main.unity`,
  press Play, set Simulator inputs to Hand.
- Webcam hand tracking on this dev machine: the Simulator's built-in webcam service opens camera 0, which is a
  DroidCam virtual camera here. Start the service on the real webcam instead:
  `F:\MetaXRSimulator\webcam_tracking_server\webcam_tracking_server.exe --port 33797 --camera_index 2`
  (start it after enabling webcam tracking in the Simulator).
