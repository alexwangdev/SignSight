# SignSight Learn

Learn 8 **real ASL signs** in mixed reality on Meta Quest 3 / 3S. A 3D teacher sitting opposite you performs each
sign — handshape, location, movement, head and face — you copy it with your own hands within 3 seconds, and get
scored on handshape, start location and movement. When you are wrong, ghost hands show the target next to yours,
replay the movement at half speed, mark the fingers that are off, and say what to change in the movement.

Hands only, seated, no controllers. Built for the Meta VR Start Developer Competition 2026 (Productivity, New Experience).

## The 8 signs (definitions from Lifeprint / ASL University)

| Lesson | Sign | How it is made |
|---|---|---|
| 1 | HELLO | Flat hand by the forehead, wave side to side |
| 1 | YES | S-fist nodding at the wrist, with a head nod |
| 1 | NO | Index + middle snap down onto the thumb, with a head shake |
| 1 | THANK-YOU | Flat hand from the chin, pushed forward, with a smile |
| 2 | COFFEE | Two S-fists, top hand cranks a grinder, bottom hand still |
| 2 | HOT | Claw hand at the mouth, flicked out and down, fast |
| 2 | COLD | Two S-fists shivering side to side |
| 2 | HELP | Fist resting on the flat palm, both hands lift together |

ICED is not taught: in ASL iced coffee is fingerspelled I-C-E + COFFEE, so COLD replaces it. Definitions follow
Lifeprint (lifeprint.com, Dr. Bill Vicars); they have not been reviewed by Deaf signers yet — corrections welcome.

## How it works

| Piece | Where |
|---|---|
| Hand features: curl per finger (bend angles between XR Hands joints) + palm direction | `Assets/Scripts/Hand/CurlCalculator.cs` |
| Both hands buffered in the signer's body frame; one movement detector per sign (wave, wrist nod, snap, push, crank, flick, shake, lift) | `Assets/Scripts/Hand/TrajectoryTracker.cs` |
| Recognition: 0.4 handshape + 0.2 start location + 0.4 movement, 3 s attempt window, settle, 1 s lockout, Top1 / Top2 | `Assets/Scripts/Hand/SignRecognizer.cs` |
| Sign definitions (v2 schema: hands, curls, palm, movement, head, face) and the recorder that tunes them from your own hands | `Assets/Resources/templates.json`, `TemplateStore.cs`, `TemplateRecorder.cs`, `Assets/Scenes/TemplateRecorder.unity` |
| Teacher: both arms, fingers, head nod / shake and face (smile, blink) driven from the same definitions | `Assets/Scripts/Learn/TeacherAvatarDriver.cs`, `SignMotion.cs`, `FaceDriver.cs` |
| Lessons (2 × 4 signs), scoring (+10 / +5 / streaks / stars), ghost-hand + movement feedback | `Assets/Scripts/Learn/` |
| Lesson data | `Assets/StreamingAssets/lessons.json` |

No image-recognition model and no third-party sign-language API: recognition is computed from Quest's hand joints.

## Third-party assets

- **Teacher character:** **Microsoft Rocketbox** avatar `Male_Adult_01` (facial version with 52 ARKit blendshapes),
  MIT License — https://github.com/microsoft/Microsoft-Rocketbox. Imported as a Unity Humanoid. We did not model it.
  Files: `Assets/Models/Rocketbox/`. `Assets/Editor/FixRocketboxMaxImport.cs` is a modified copy of the MIT import
  script from the same repository (scoped to that folder, Humanoid import; changes listed in its header).
- Meta XR All-in-One SDK v207, Unity OpenXR 1.18.0, Unity XR Hands 1.9.0, TextMeshPro.

## Honest notes

- **Not tested on a real Quest.** Development and testing used Meta XR Simulator v207 with laptop-webcam hand tracking.
  Thresholds (0.35 total distance, 3 s window, per-movement thresholds) are tuned in the simulator only. Two-handed
  signs suffer most from a single webcam (touching fists merge into one hand; ~30 cm depth jitter), so the editor uses
  a jitter allowance for COFFEE's "bottom hand still" check; device builds use the spec values.
- Quest 3 / 3S has no face tracking: the learner's face is never scored. The teacher's face is display only.
- Not in this version: J / Z, R / U, M / N / T, the A–Z alphabet, continuous signing, two-way conversation.

## Running it

- Unity `6000.3.28f1`, URP, Android build target. Build: menu **SignSight → Build Quest APK** (`Builds/SignSightLearn.apk`).
- Simulator: activate Meta XR Simulator (Window → Meta → Meta XR Simulator → Activate), open `Assets/Scenes/Main.unity`,
  press Play, set Simulator inputs to Hand.
- Webcam hand tracking on this dev machine: the Simulator's built-in webcam service opens camera 0, which is a
  DroidCam virtual camera here. Start the service on the real webcam instead:
  `F:\MetaXRSimulator\webcam_tracking_server\webcam_tracking_server.exe --port 33797 --camera_index 2`
  (start it after enabling webcam tracking in the Simulator).
