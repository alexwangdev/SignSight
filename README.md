# SignSight Learn

Learn 50 **real ASL signs** for everyday life in mixed reality on Meta Quest 3 / 3S — greetings, people, food and
drink, feelings and health, questions and time. A 3D teacher sitting opposite you performs each sign — handshape,
location, movement, head and face — at normal or half speed; you copy it with your own hands within 3 seconds, and get
scored on handshape, start location and movement. When you are wrong, ghost hands show the target next to yours,
replay the movement at half speed, mark the fingers that are off, and say what to change.

An on-device **Tutor Agent** decides what you practise next — a word you missed twice comes back at once, a finger you
keep getting wrong is the only one highlighted, words you have not seen for two lessons come back for review, words you
get right first time twice are shown less often — and shows its reason on screen ("Next: YES — because you missed it
twice / Focus: your index finger"). Rule-based, no network, no account.

The teacher also shows the face, because ASL is not hands-only: 22 of the 50 signs have a facial expression (WH-question
furrowed brows with a head tilt, SAD, HAPPY, PAIN, THANK-YOU's smile, ...), sourced from Lifeprint where it describes
one. The word card names what the brows, eyes and mouth do, and a FACE button replays just the face at half speed.
**The learner's face is never read or scored** — Quest 3 / 3S has no face tracking; every face line says
"Not scored — watch and copy". Correct answers get a silent confetti burst and stars; wrong answers get the ghost-hand
correction only.

Visual only, by design: the learner may be Deaf or hard of hearing. Every instruction is a caption; nothing depends on
sound. Hands only, seated, no controllers. Built for the Meta VR Start Developer Competition 2026 (Productivity, New
Experience).

## The 50 signs (definitions from Lifeprint / ASL University)

| Lesson | Signs |
|---|---|
| 1 Greetings | HELLO, YES, NO, THANK-YOU, PLEASE, SORRY, GOOD, BAD, GOODBYE, GOOD-MORNING* |
| 2 People | MOTHER, FATHER, FRIEND, BABY, MAN, WOMAN, BOY, GIRL, TEACHER*, DOCTOR |
| 3 Food & drink | WATER, MILK, EAT, DRINK, BREAD, APPLE, HUNGRY, THIRSTY, COFFEE, HOT |
| 4 Feelings & health | COLD, HELP, HAPPY, SAD, TIRED, SICK, PAIN, LOVE, LIKE, WANT |
| 5 Questions & time | NEED, WHAT, WHERE, WHEN, WHO, WHY, HOW, TODAY*, TOMORROW, GO |

\* Practice only — not scored: compound signs (two beats with a handshape / location change). 47 signs are scored.
Every sign's Lifeprint source, handshape, location, movement and non-manual markers are in
`Assets/StreamingAssets/word_sources.json`. WH-question words show "Eyebrows: furrowed — watch the teacher (not scored)":
Quest 3 / 3S has no face tracking, so the face is never scored. ICED is not taught (fingerspelled in ASL).
Definitions follow Lifeprint (lifeprint.com, Dr. Bill Vicars); they have not been reviewed by Deaf signers yet —
corrections welcome.

## How it works

| Piece | Where |
|---|---|
| Hand features: curl per finger (bend angles between XR Hands joints) + palm direction | `Assets/Scripts/Hand/CurlCalculator.cs` |
| Both hands buffered in the signer's body frame; movement detectors: wave, wrist nod, snap, push, crank, flick, shake, lift (v3) and tap, stroke, circle, twist, turn, squeeze, converge, hold, rock (v3.1); start location = contact point (e.g. thumb tip) to a named body anchor (chin, forehead, chest, ...) | `Assets/Scripts/Hand/TrajectoryTracker.cs`, `SignScorerV31.cs`, `SignAnchors.cs` |
| Recognition: 0.4 handshape + 0.2 start location + 0.4 movement, 3 s attempt window, settle, 1 s lockout, Top1 / Top2 | `Assets/Scripts/Hand/SignRecognizer.cs` |
| Sign definitions (v2 schema: hands, curls, palm, movement, head, face) and the recorder that tunes them from your own hands | `Assets/Resources/templates.json`, `TemplateStore.cs`, `TemplateRecorder.cs`, `Assets/Scenes/TemplateRecorder.unity` |
| Teacher: both arms, fingers, head nod / shake and face (smile, blink) driven from the same definitions | `Assets/Scripts/Learn/TeacherAvatarDriver.cs`, `SignMotion.cs`, `FaceDriver.cs` |
| Lessons (5 × 10 signs), scoring (+10 / +5 / streaks / stars), ghost-hand + movement feedback | `Assets/Scripts/Learn/` |
| Tutor Agent: perceive each attempt, remember per-word history on the headset (JSON), decide the next word, act with a specific hint and a visible reason | `Assets/Scripts/Learn/TutorAgent.cs`, `LearnerProfile.cs` |
| Lesson data, Lifeprint sources | `Assets/StreamingAssets/lessons.json`, `word_sources.json` |
| Offline teacher check against a sign-recognition model (dev machine only, nothing ships) | `workspace/goals/meta-vr-start-sign-language-entry/files/teacher-verification/` |

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
- **The 42 signs added in v3.1 have not been tried with real hands yet.** They pass a self-test (the teacher's own hands
  through the recogniser: 47 / 47 scored signs recognised, all 8 v3 signs included), which shows the teacher and the
  definitions agree — not that a learner's hands will be judged well.
- **Replay of real human videos** (143 WLASL clips -> MediaPipe 3D hands -> the recogniser): 15 % of clips recognised.
  Video depth is 8-20 cm off and finger curls are rough, so this is a lower bound, not a headset prediction; it was used
  to fix four detector problems. Details: `teacher-verification/human_replay.md`.
- **Teacher check against a sign-recognition model (WLASL Pose-TGCN, asl1000, Top-5):** the teacher's sign was in the
  model's Top-5 for 11 of the 49 signs the model knows; the same model found 26 of 41 real human signers. For 13 signs the
  model also misses real humans (model unreliable for them); 22 signs need a human check of the teacher's demo; FRIEND's
  demo is simplified (the hands do not swap between the two links). Lifeprint is the reference; no definition was changed
  only because the model disagreed. Details: `teacher-verification/teacher_verification_50.md`.
- Not in this version: J / Z, R / U, M / N / T, the A–Z alphabet, continuous signing, two-way conversation.

## Running it

- Unity `6000.3.28f1`, URP, Android build target. Build: menu **SignSight → Build Quest APK** (`Builds/SignSightLearn.apk`).
- Simulator: activate Meta XR Simulator (Window → Meta → Meta XR Simulator → Activate), open `Assets/Scenes/Main.unity`,
  press Play, set Simulator inputs to Hand.
- Webcam hand tracking on this dev machine: the Simulator's built-in webcam service opens camera 0, which is a
  DroidCam virtual camera here. Start the service on the real webcam instead:
  `F:\MetaXRSimulator\webcam_tracking_server\webcam_tracking_server.exe --port 33797 --camera_index 2`
  (start it after enabling webcam tracking in the Simulator).
