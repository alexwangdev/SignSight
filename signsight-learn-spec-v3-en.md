# SignSight Learn — Quest Sign-Language Teaching · English SPEC v3.0 (for Claude to run)

> **Instructions for Claude (read this first, then build)**
> 1. This is v3.0, the only valid SPEC. v2.0 (`signsight-learn-spec-zh.md` / `-en.md`) is archived and void, and v1.0 two-way conversation is void even earlier. Do not build v2 again. Do not bring back the Mixamo teacher, the static curl templates, or ICED.
> 2. v3 is a **targeted correction of v2, not a rewrite from scratch**. The repo `github.com/alexwangdev/SignSight` (single commit `e9cb60e`) is already built to v2: Unity `6000.3.28f1`, URP, Meta v207, OpenXR 1.18, XR Hands 1.9, `Assets/Scripts/Hand/` (`CurlCalculator` / `TemplateStore` / `TemplateRecorder` / `SignRecognizer`), `Assets/Scripts/Learn/` (`LessonManager` / `TeacherAvatarDriver` / `ScoreManager` / `FeedbackPanel` / `GhostHand`), `Assets/StreamingAssets/lessons.json`, `Assets/Resources/templates.json` (v1 schema). v3 changes only what §2 lists. Keep other files, class names and scene names as they are.
> 3. You are the only executor of this SPEC. Follow §12 in phase order, one phase at a time, run the §13 checks at the end of each phase, and only move on when they pass. Do not skip phases. Do not build everything at once.
> 4. Do not change scope. Anything marked "not in v3" / "do not do" must not be added on your own. If a change is needed, explain why in your reply and wait for the user to confirm.
> 5. All numbers, thresholds, file names and package versions in this file win over anything else. Do not substitute versions from your training data. On conflict, this file wins. **The motion numbers in §4 are initial values, not final values**: they come from Lifeprint's text descriptions, and the recorder (§4.5) overwrites them with the user's own recordings. Do not hard-code them as unchangeable constants.
> 6. Code comments and UI copy in English; explanations to the user in Chinese. Strings in code are English.
> 7. The user has no Quest. All laptop verification uses Meta XR Simulator (§10). Do not assume a real headset is available.
> 8. **The project already exists. Do not create a new one.** The repo is the project at `D:\Unity\Projects\SignSightVR` (Unity `6000.3.28f1`, Universal 3D / URP). P0 is to open that project, swap the teacher model, and verify it.
> 9. Why v3 (for Claude, do not overturn): v2's 8 words were **static handshape practice, not real ASL**. The repo README admits this. COFFEE's C-hand was invented (real COFFEE is two S-fists grinding coffee), HOT's A-fist was wrong (real HOT is a claw hand flicked out from the mouth), THANK-YOU had no forward movement, YES / NO had no head nod / shake, HELP as a one-handed Y-hand was wrong (real HELP is two-handed lifting), and ICED is not a standalone sign at all. v3 replaces all 8 with real ASL actions from Lifeprint and swaps the teacher from Mixamo to Rocketbox, because only Rocketbox's facial FBX has 52 ARKit blendshapes, which THANK-YOU's smile and YES / NO's head movements need. Teaching a wrong sign is worse than not teaching it; a judge who knows ASL will spot it immediately.

---

## 1. One sentence: what this is

**SignSight Learn v3**: on Quest 3 / 3S, learn 8 **real ASL** signs face-to-face with a 3D teacher.

- The teacher shows each sign first: handshape + location + movement + head / face, all of it.
- You copy it, completing the movement within 3 seconds. Scoring checks handshape, location and movement.
- Correct: points. Wrong: a side-by-side "target hand" vs "your hand" view, per-finger red / yellow / green, plus a movement hint (e.g. "keep the bottom hand still — only the top hand cranks").

**Not** an A–Z alphabet app, no fingerspelling words, no continuous signing. Just 8 immediately useful words — but they must be real.

### 1.1 Competition (fixed, do not change)

- Competition: Meta VR Start Developer Competition 2026
- Deadline: **2026-11-18 15:00 ET**
- Track: **Productivity**, Division: **New Experience**
- Prizes targeted: Productivity New 1st $70,000 / 2nd $35,000; special award **Best Accessibility Forward $25,000** primary, plus **Best First Five Minutes $25,000** and **Best Reason to Come Back $25,000**
- Hard requirements (all mandatory):
  - **Hands-first end-to-end**: no controllers, no controller pairing needed.
  - **Seated-optimized**: 2-foot / airplane-seat test, finish a lesson sitting down.
  - **≤10 min satisfying moment**: from putting the headset on to finishing 8 signs and seeing the total score, under 10 minutes.
  - **Original, not a thin wrapper**: swapping the recognition model out must still leave a working app (teacher driver, scoring, per-finger feedback, lesson progress are ours).
- Deliverables: playable APK (new "Competition" release channel + Invite URL) + **<3 min public YouTube / Vimeo video** (judges are not guaranteed to watch beyond 3 min) + Devpost form (track, division, hand-interaction notes). Must be a Start program member before submitting.
- Judges test on **Quest 3 or Quest 3S**. Video may be simulator footage, but the APK must run on real hardware. Official FAQ: no headset is required to compete; video may be real-headset footage, XR Simulator, or an equivalent emulator.

---

## 2. What v3 changes vs v2 (Claude's change list — read this first)

| # | v2 (void) | v3 (do this) | Files to change |
|---|---|---|---|
| 1 | Teacher: Mixamo Ch33, `Assets/Models/Teacher.fbx`, Standard Skeleton 65 bones, no facial blendshapes | Teacher: **Microsoft Rocketbox Male_Adult_01**, using **`Export/Male_Adult_01_facial.fbx` (1.9 MB)**, see §3 | Delete `Assets/Models/Teacher.fbx`; add `Assets/Models/Rocketbox/Male_Adult_01_facial.fbx` + textures; add `Assets/Editor/FixRocketboxMaxImport.cs` (ships in the Rocketbox repo, see §3.3); rebuild `Teacher.prefab` with the new model |
| 2 | 8 words as static curl templates: COFFEE = C-hand `[0.5×5]`, HOT = A-fist, THANK-YOU = flat hand held still, HELP = one-handed Y-hand, ICED = L-hand | 8 words as **real ASL action definitions**: handshape + start location + movement trajectory + head / face, see §4; **ICED replaced by COLD** (why in §4.8) | `Assets/StreamingAssets/lessons.json` (ICED → COLD); `Assets/Resources/templates.json` upgraded to **v2 schema** (§4.5); `TemplateStore.cs` gains fields |
| 3 | `TeacherAvatarDriver` drives only the right hand, curl interpolation only: 0.5s in, 1.5s hold, 0.5s out | `TeacherAvatarDriver` v3: **both hands + movement sequence + head + face**, see §3.4; new `FaceDriver.cs` (ARKit blendshapes) and head nod / shake | Rewrite core methods of `Assets/Scripts/Learn/TeacherAvatarDriver.cs`; add `Assets/Scripts/Learn/FaceDriver.cs`; change the `LessonManager.cs` call from `teacher.Demonstrate(curl, palm)` to `teacher.Demonstrate(template)` |
| 4 | `SignRecognizer` recognises only a static hold: 0.6s hold + 1s lockout, one hand | `SignRecognizer` v3: **trajectory recognition**, see §4.6: handshape (0.4) + location (0.2) + movement (0.4); track both hands; dynamic signs fire on movement completion, no 0.6s static hold required | `Assets/Scripts/Hand/SignRecognizer.cs`; new `Assets/Scripts/Hand/TrajectoryTracker.cs` (1.5s ring buffer); `CurlCalculator.cs` unchanged (handshape algorithm is fixed) |
| 5 | `GhostHand` is one right hand, static pose side-by-side | `GhostHand` v3: **left + right hands + movement playback**, see §8; feedback adds a movement line (e.g. "move only the top hand", "flick faster") | `Assets/Scripts/Learn/GhostHand.cs`; `FeedbackPanel.cs` gains a movement hint line |
| 6 | `TemplateRecorder` records only a static 1s hold | `TemplateRecorder` v2: record the start pose (hold 1s), then perform the full movement once (trajectory auto-extracts amplitude / repeats), see §4.5 | `Assets/Scripts/Hand/TemplateRecorder.cs`; `TemplateRecorder.unity` gains a "perform the movement once" step |
| 7 | Scoring, lessons, UI, scoring rules | **Unchanged**: +10 / +5, streaks, stars, two-lesson order, Replay / Next buttons, see §7 | `ScoreManager.cs`, `LessonManager.cs` (except the call change in #3), `LessonUI.cs` unchanged |

**Unchanged**: tech stack (§5), scene structure, Passthrough, seated play, 72 FPS target, simulator testing (§10), not-doing list (§11, plus one addition: "static handshapes passed off as real ASL" is now explicitly not-doing).

---

## 3. Teacher: Microsoft Rocketbox Male_Adult_01, facial version. Not Mixamo

### 3.1 Source (fixed)

- **Source**: `github.com/microsoft/Microsoft-Rocketbox`, MIT License (switched to MIT in Dec 2020). A library of 115 characters, free for research and commercial projects. Cite the source and license in the README / submission. Do not claim the model is ours.
- **Exact model**: under `Assets/Avatars/Adults/Male_Adult_01/Export/` there are two FBX files:
  - `Male_Adult_01.fbx` (510 KB): body + Bip01 rig, **no facial blendshapes**. Do not use it as the teacher.
  - **`Male_Adult_01_facial.fbx` (1.9 MB): use this one.** Same body plus facial blendshapes. After import, confirm the `SkinnedMeshRenderer` has blendshapes; the count should be 52.
- **Do not use Ready Player Me.** Acquired by Netflix in Dec 2025; its public avatar creator, PlayerZero and developer APIs were shut down on 2026-01-31.
- **Do not use Mixamo any more.** Mixamo strips facial morphs, so it cannot do THANK-YOU's smile. Delete v2's `Assets/Models/Teacher.fbx` (Ch33) in P0. Do not keep two teacher models in the project (APK size + confusion).

### 3.2 What this model actually contains (verified 2026-11-12 by reading the FBX strings, not from memory)

- **Fingers**: Bip01 rig, 15 finger bones per hand: `Bip01 L/R Finger0` (thumb), `Finger1` (index), `Finger2` (middle), `Finger3` (ring), `Finger4` (pinky), 3 segments each (e.g. `Finger0 / Finger01 / Finger02`). Face bones include `Bip01 REye / LEye`, `Bip01 MJaw`, `Bip01 LMouthBottom / RMouthBottom`, `Bip01 RMouthCorner / LMouthCorner`, `Bip01 RCheek / LCheek`, `Bip01 RInnerEyebrow / LInnerEyebrow`, `Bip01 MMiddleEyebrow`, `Bip01 ROuterEyebrow / LOuterEyebrow`, `Bip01 REyeBlinkTop / Bottom`, and others.
- **Facial blendshapes (facial version, 52 ARKit, `AK_01`–`AK_52`, each name verified)**: `AK_01 BrowDownLeft`, `AK_02 BrowDownRight`, `AK_03 BrowInnerUp`, `AK_04 BrowOuterUpLeft`, `AK_05 BrowOuterUpRight`, `AK_06 CheekPuff`, `AK_07/08 CheekSquint`, `AK_09/10 EyeBlinkLeft/Right`, `AK_11–18 EyeLook*`, `AK_19/20 EyeSquint`, `AK_21/22 EyeWide`, `AK_23 JawForward`, `AK_24 JawLeft`, `AK_25 JawOpen`, `AK_26 JawRight`, `AK_27 MouthClose`, `AK_28/29 MouthDimple`, `AK_30/31 MouthFrown`, `AK_32 MouthFunnel`, `AK_33 MouthLeft`, `AK_34/35 MouthLowerDown`, `AK_36/37 MouthPress`, `AK_38 MouthPucker`, `AK_39 MouthRight`, `AK_40 MouthRollLower`, `AK_41 MouthRollUpper`, `AK_42/43 MouthShrug`, `AK_44 MouthSmileLeft`, `AK_45 MouthSmileRight`, `AK_46/47 MouthStretch`, `AK_48/49 MouthUpperUp`, `AK_50/51 NoseSneer`, `AK_52 TongueOut`.
- **v3 uses only 4 groups** (see §3.5): `MouthSmileLeft/Right` (THANK-YOU), `JawOpen` (HOT, THANK-YOU slight), `EyeBlinkLeft/Right` (idle blink), `BrowInnerUp / BrowOuterUp*` (reserved, not scored in v3). Do not touch the other 40+ in v3.
- **LOD**: Rocketbox ships hipoly / midpoly / lowpoly / ultralowpoly. Import at **lowpoly** (Quest target: memory <500 MB, draw calls <100). Do not use hipoly.

### 3.3 Import steps (in order; stop and report the error if a step fails — do not guess)

1. Copy `Assets/Editor/FixRocketboxMaxImport.cs` (ships in the Rocketbox repo under `Assets/Editor/`) into this project's `Assets/Editor/`. It fixes 3ds Max materials on Unity import (diffuse colour doubling, glass-like hair) and sets the avatar to T-pose and reorganises bones for the Unity Humanoid rig. Without it, colours and pose will be wrong.
2. Copy `Male_Adult_01_facial.fbx` + its textures (`Male_Adult_01.png`, etc.) into `Assets/Models/Rocketbox/`.
3. In Unity, select the FBX → Rig → Animation Type = **Humanoid** → Apply → Configure Avatar. Check the mapping: 15 finger bones per hand, head, upper/lower arms, legs — **all green**. Bip01 finger names are `Finger0`–`Finger4`; Unity may not auto-map them to `HumanBodyBones.RightThumbProximal` etc. **If finger mapping is red / missing, do not force it**: use the §3.4 fallback (drive bones directly by name) and state in your reply exactly which bones failed to map.
4. Select the model's `SkinnedMeshRenderer` and confirm the BlendShapes list has 52 entries named `AK_*`. Fewer than 52 means the wrong file was imported (probably the 510 KB non-facial version) — re-import the facial version.
5. Delete `Assets/Models/Teacher.fbx` (Mixamo Ch33) and the old model reference in `Teacher.prefab`; rebuild `Teacher.prefab` with Rocketbox. Update the README "Third-party assets" section: Mixamo → Rocketbox + MIT + link. Do not leave two source statements.
6. Place the teacher ~1.5 m opposite the learner, seated (thighs forward 90°, shins down 90°, reusing v2 `TeacherAvatarDriver.PoseRestBody`), about life-size when seated.

### 3.4 How the teacher performs a sign (key: a movement sequence, not curl interpolation)

- **Do not** search for, buy, or hand-keyframe 8 ASL animation clips. Free ready-made ASL animation basically does not exist, and hand-keyframing 8 clips is not doable in 5 days.
- v2's `TeacherAvatarDriver.Demonstrate(curl, palm)` (0.5s in, 1.5s hold, 0.5s out, right hand only) is **void**. Replace it with `Demonstrate(SignTemplate template)`, taking the full action definition from §4.5 and playing a **keyframe sequence**:
  ```
  0.0–0.4s  Move to start: hand(s) to the §4 start location, fingers interpolate to start curl, palm to start orientation
  0.4–(0.4+duration)s  Perform the movement (§4): crank / flick / nod / shake / push / lift, repeats times
  final 0.8s  Hold the end pose (so the learner can see the endpoint)
  0.4s  Return to neutral rest pose
  ```
  One demonstration lasts ~2–2.5s total, about the same as v2. Do not stretch it past 4s.
- **Both hands**: `TeacherAvatarDriver` must drive both arms / hands. v2 only implemented analytic two-bone IK for the right arm (`SolveTwoBone`); mirror it for the left arm. Do not leave the left hand hanging for COFFEE, COLD and HELP — they are two-handed signs, a still left hand is wrong.
- **Finger driving reuses v2's method**: fetch bones via the Humanoid avatar (`HumanBodyBones.RightThumbProximal` etc.), convert curl to angles with each finger's bend axis (thumb 45°/45°/55° per segment, other fingers 80°/100°/70°, the `fingerMaxDegrees / thumbMaxDegrees` in v2's `TeacherAvatarDriver` — unchanged). **Fallback**: if Humanoid finger mapping is missing, fetch the same bones directly by name (`Bip01 R Finger1`, etc.) and drive them the same way. Only the bone lookup changes. Do not let a mapping problem block P1.
- **Head**: nod / shake by rotating the head bone, not by blendshape. YES: head pitch ±10°, 2 times, synced with the wrist nod. NO: head yaw ±12°, 2 times, synced with the finger snap. Other signs: head still.
- **Face**: see §3.5. Only THANK-YOU's smile and idle blinking — do not add an expression to every word.

### 3.5 Face: only smile and blink. Not a full expression set

- Add `FaceDriver.cs`: input is an ARKit blendshape name + target weight (0–100), smooth in/out over 0.3s. Implementation is `SkinnedMeshRenderer.SetBlendShapeWeight(index, weight)`, looking the index up by name — do not hard-code numeric indices (import order can change).
- **THANK-YOU**: `MouthSmileLeft = 60`, `MouthSmileRight = 60`, `JawOpen = 10`, fading in at the start of the movement and out at the end. Lifeprint requires the smile: "Smile (so they'll know you mean it)."
- **HOT**: `JawOpen = 20` (hot breath), nothing else. No frown, no wide eyes.
- **Idle**: blink every 4–6s, `EyeBlinkLeft/Right = 100` for 0.2s. Do not keep the eyes wide open.
- **Other signs: face still.** Do not use `BrowDown / BrowInnerUp` as grammar markers; none of the 8 v3 words is a question.
- **Hard boundary**: the teacher's face is **display only, never scored**. The learner's face is **not tracked and not scored** — Quest 3 / 3S has no face tracking. Do not write any code that reads the learner's face for scoring.

---

## 4. The 8 words: real ASL action definitions (Lifeprint is the authority; every entry is verifiable)

### 4.1 Principles (fixed)

- **Single source**: definitions follow **Lifeprint / ASL University (Dr. Bill Vicars)** text descriptions; the source for each word is cited below. Do not use AI-generated infographics or random web images (in Aug 2026 a 20-sign infographic was widely corrected by Deaf users and CODAs — most of it was wrong).
- **One sign = handshape + location + movement + head / face.** Missing any one of these is wrong. Using handshape alone was the root error in v2.
- **Only these 8.** No A–Z, no sentences, no continuous signing.
- **Learner scoring looks only at hands and arms**, not face or head (head is teacher-only in the demo; the learner's nod / shake is a nice-to-have hint, never a scoring gate — see §4.6).
- **Dominant hand**: right hand below, unless stated otherwise. A "I'm left-handed" switch in the recorder mirrors the templates; v3 does not do automatic left/right detection.

### 4.2 Lessons: 8 words, two lessons (use this for the demo, do not change it)

| Lesson | Words | Order | Note |
|---|---|---|---|
| Lesson 1 — Greetings | HELLO, YES, NO, THANK-YOU | In table order | All one-handed, but all have movement / head |
| Lesson 2 — Café | COFFEE, HOT, COLD, HELP | In table order | COFFEE, COLD, HELP are two-handed; HOT is one-handed + fast flick |

- Both lessons are unlocked directly in the menu. No gating.
- 4 words per lesson, a lesson summary after each, total score and stars after all 8.

### 4.3 Action definition table (Claude writes `templates.json` v2 from this; numbers are initial values)

Curl notation: `[thumb, index, middle, ring, pinky]`, 0 = straight, 1 = fully curled. Spread: 0 = together, 1 = fully spread. Locations are relative to the signer's own body, never absolute world coordinates.

| # | Word / meaning | Hands | Start handshape curl | Start location | Palm | Movement (the core) | Head / face | Lesson |
|---|---|---|---|---|---|---|---|---|
| 1 | HELLO "Hello" | Right, one hand | B / flat hand `[0.2, 0.0, 0.0, 0.0, 0.0]`, spread 0.1 | Beside the temple / forehead, <20 cm from the head | Forward | **Wave**: wrist yaw ±25°, 2 times, 1.2s | Head still, face neutral | L1 |
| 2 | YES "Yes" | Right, one hand | S fist `[1.0, 1.0, 1.0, 1.0, 1.0]` | Chest height | Forward | **Wrist nod**: wrist pitch ±20°, 2 times, 1.0s | **Head nod** ±10°, 2 times (teacher only; not required of the learner) | L1 |
| 3 | NO "No" | Right, one hand | Index + middle extended, thumb underneath: `[0.3, 0.0, 0.0, 1.0, 1.0]` | Chest height | Forward | **Snap**: index + middle snap down onto the thumb pad, <0.4s, 1 time; fingertip-to-thumb distance shrinks ≥40% | **Head shake** ±12°, 2 times (teacher only; not required of the learner) | L1 |
| 4 | THANK-YOU "Thank you" | Right, one hand | Flat hand `[0.0, 0.0, 0.0, 0.0, 0.0]`, spread 0.1 | **At the chin / lips**, <15 cm from the chin | Toward self (palm in / down) | **Push forward**: hand moves forward + slightly down 15–20 cm, 0.8s | **Face smile**: MouthSmile L/R 60, JawOpen 10 | L1 |
| 5 | COFFEE "Coffee, please" | **Both** | Both S fists `[1.0, 1.0, 1.0, 1.0, 1.0]` ×2 | Chest height, dominant hand on top of non-dominant, hands <12 cm apart | Both down / inward | **Grind coffee**: **only the top hand turns**, wrist roll ≥30°, 2 times, 1.2s; bottom hand stays still (moves <4 cm) | Head still, face neutral | L2 |
| 6 | HOT "Hot, thanks" | Right, one hand | **Claw hand** `[0.6, 0.6, 0.6, 0.6, 0.6]`, spread 0.7 | **At the mouth**, <12 cm from the mouth | Toward self | **Flick out**: hand moves outward + downward ≥15 cm, <0.7s; faster = hotter | Face: JawOpen 20 (teacher) | L2 |
| 7 | COLD "Cold / iced, thanks" | **Both** | Both S fists `[1.0, 1.0, 1.0, 1.0, 1.0]` ×2 | Chest height, hands ~15 cm apart | Both inward | **Shiver**: both hands shake side to side together, amplitude 3–5 cm, 4–6 Hz, 1.0s | Head still, face neutral | L2 |
| 8 | HELP "I need help" | **Both** | Dominant: loose A-hand / S-hand `[0.9, 1.0, 1.0, 1.0, 1.0]` (thumb loose, out); non-dominant: flat hand palm up `[0.0, 0.0, 0.0, 0.0, 0.0]` | Dominant hand resting on the non-dominant palm, chest height | Dominant down, non-dominant up | **Lift**: both hands rise together 10–12 cm, 0.8s, with a slight forward arc | Head still, face neutral | L2 |

### 4.4 Per-word sources (read this before writing code; put the video links in the submission references too)

- **HELLO**: Lifeprint: "Just use the same signs that you would in the 'hearing world.' … in general you wave hello with a side to side movement."; "'hi' is more of a sideways sweep and 'hey' is an up and down flutter." — a wave, not a static B-hand. Ref: `lifeprint.com/asl101/pages-signs/h/hello.htm`
- **YES**: "Make an S handshape, a closed fist. Bend it up and down at the wrist, like a head nodding. At the same time, nod your head." — S-fist + wrist nod + head nod, all three required.
- **NO**: "Hold your index and middle fingers out together, with your thumb out under them. Snap the two fingers down to touch the thumb. At the same time, shake your head."; Lifeprint lexicalised fingerspelling entry: "#NO (straightened index and middle fingers raised and lowered onto pad of thumb)". — v2's static `[1,0,0,1,1]` got only half of it; the snap and the head shake were missing.
- **THANK-YOU**: "The sign for 'thank you' is made by starting with the fingers of your dominant hand near your lips. Your hand should be a 'flat hand.' Move your hand forward and a bit down in the direction of the person you are thanking. Smile (so they'll know you mean it)." — starts at the lips, not the chest; pushes forward; smiles. Ref: `lifeprint.com/asl101/pages-signs/t/thankyou.htm`
- **COFFEE**: "COFFEE: The sign coffee looks like you are turning an old fashioned coffee grinder with the top hand while the bottom hand stays still. The movement is a cranking movement, not a twisting movement."; "Think of the movement of an old coffee grinder. The bottom hand stays still while the top hand turns the crank." — **two S-fists, top cranks, bottom still, crank not twist**. v2's C-hand `[0.5×5]` was wrong; `MAKE` is the one where both hands twist — do not confuse them. Ref: `lifeprint.com/asl101/pages-signs/c/coffee.htm`
- **HOT**: "Use a 'claw' handshape. Move it outward and downward from your mouth. Do it quickly. The stronger and faster the movement, the hotter the temperature." — claw hand + flick out and down from the mouth + fast. v2's A-fist `[0.7,0.9,0.9,0.9,0.9]` was wrong. Ref: `lifeprint.com/asl101/pages-signs/h/hot.htm`
- **COLD** (see §4.8): "Make the sign for 'cold' by forming both hands into the letter 's.' Hold both hands in front of you and shake them as if shivering from the cold." — two S-fists + shiver. Ref: `lifeprint.com/asl101/pages-signs/h/hotcold.htm`
- **HELP**: "The sign for 'help' is made by forming a loose-thumb-A hand (or even an 'S' hand) and lifting it with your other hand."; directional: "If you start it near yourself and move it toward someone else in and up, forward, then down arch it means 'I will help you'…" — two-handed lift, not a one-handed Y-hand. v2's Y-hand `[0,1,1,1,0]` was wrong. Ref: `lifeprint.com/asl101/pages-signs/h/help.htm`

### 4.5 Template data model v2 (`templates.json`, replaces the v1 schema)

v1's `SignTemplate` had only `curl[5] + palm + tolerance`. v3 must add movement, both hands, and head / face. Change `TemplateStore.cs` as below, bump the version to `version = 2`, and auto-migrate v1 files on load (old `curl` becomes `curlRight`, `movement = none`, log one migration line — do not silently drop data):

```json
{
  "version": 2,
  "templates": [
    {
      "intent": "COFFEE",
      "phrase": "Coffee, please",
      "hands": "both",
      "curlRight": [1.0, 1.0, 1.0, 1.0, 1.0],
      "curlLeft":  [1.0, 1.0, 1.0, 1.0, 1.0],
      "palm": { "x": 0.0, "y": -1.0, "z": 0.0 },
      "palmLabel": "Down",
      "startLocation": "chest, dominant hand on top of non-dominant, hands <12cm apart",
      "movement": { "type": "crank", "axis": "wrist-roll", "amplitude": 30, "unit": "deg", "repeats": 2, "durationSec": 1.2, "movingHand": "dominant-only" },
      "endCurlRight": [1.0, 1.0, 1.0, 1.0, 1.0],
      "head": { "type": "none", "amplitudeDeg": 0, "repeats": 0 },
      "face": { "smile": 0, "jawOpen": 0 },
      "tolerance": 0.35,
      "samples": []
    }
  ]
}
```

Field notes:
- `hands`: `"right"` or `"both"`. When `both`, `curlLeft` is required; when `right`, it may be omitted.
- `movement.type`: enum, only `wave | wrist-nod | snap | push-forward | crank | flick-out | shake | lift` — one per word, do not invent a ninth. `amplitude` uses `unit` (`deg` or `cm`), `repeats` is a count, `durationSec` is total time, `movingHand` is only used for `crank` (`"dominant-only"`).
- `head.type`: `none | nod | shake`, teacher demo only, not required of the learner (§4.6).
- `face`: `smile` / `jawOpen` are 0–100 blendshape weights, teacher demo only, never scored.
- `tolerance`: initial 0.35 (total distance, see §4.6), tunable in the recorder.

### 4.6 Recognition v3: trajectory recognition (replaces v2's static hold)

v2's flow (per-frame curl distance, Top1 stable ≥0.6s fires) is **not enough** in v3: YES (S-fist) and COFFEE (two S-fists) share a handshape and differ only in movement; HOT (claw) and COLD (two S-fists shaking) are not separable by handshape alone; THANK-YOU and HELLO are both flat / B-hands and differ in start location and trajectory. Trajectory is mandatory.

Add `TrajectoryTracker.cs`: a 1.5s ring buffer per hand (60 Hz, ~90 frames), storing per frame: wrist position, wrist orientation (pitch / yaw / roll), index tip, thumb tip, palm normal; head position / orientation too (for demo sync display only, never for scoring).

Each frame (only in the Copy phase, while `listening`):
1. `CurlCalculator` (unchanged) computes each hand's `curl[5] + palm + spread + pinch`, 5-frame moving average.
2. `TrajectoryTracker` extracts the movement feature this word needs from the buffer:
   - `wave`: wrist yaw crosses zero ≥2 times in 1.2s, amplitude ≥15°.
   - `wrist-nod`: wrist pitch crosses zero ≥2 times in 1.0s, amplitude ≥15°.
   - `snap`: index + middle tip to thumb tip distance shrinks ≥40% within 0.4s.
   - `push-forward`: hand displaces ≥12 cm toward the teacher, starting <15 cm from the chin.
   - `crank`: dominant wrist roll accumulates ≥30° × 2, non-dominant hand moves <4 cm, hands <12 cm apart.
   - `flick-out`: hand starts at the mouth (<12 cm) and moves outward + downward ≥15 cm in <0.7s.
   - `shake`: both hands oscillate ≥3 times in 1.0s, amplitude ≥2.5 cm.
   - `lift`: dominant + non-dominant hands rise together ≥8 cm, dominant hand staying <8 cm above the non-dominant palm.
3. Total distance = `0.4 × handshape distance + 0.2 × location distance + 0.4 × movement distance`, each normalised to 0–1. Handshape distance reuses v1 `TemplateStore.Distance` (curl + palm). Location distance = start-position error / 20 cm (capped at 1). Movement distance = 1 − (measured amplitude / template amplitude, capped at 1); no movement at all scores 1.
4. **Fire condition**: the learner completes the movement once inside a 3s window and total distance ≤ `tolerance` (initial 0.35) → fire one recognition event `{ Top1, Top2, confidence, userCurl, userPalm, movementError }`, then a 1s lockout (as in v2, against double-firing). No completion within 3s → no fire; show only the closest candidate in small preview text, never scored.
5. **Head / face are not scored**: YES's nod, NO's shake and THANK-YOU's smile happen only in the teacher demo. If the learner does them, the UI may say "nice nod", but doing them is never required. Do not write any code that scores the learner's head / face — Quest 3 / 3S has no face tracking, and head-nod detection from headset orientation must not be a scoring dependency in v3.

### 4.7 How scoring changes (only the fire condition changes; points do not)

- Scoring itself is unchanged (§7): first try correct +10, second try correct +5, second try wrong +0, streaks and stars unchanged.
- What changes is the definition of "correct": v2 was "Top1 is the target word and confidence ≥0.7 and distance ≤0.35"; v3 is "movement completed inside 3s and total distance ≤0.35 (§4.6 step 3)". Everything else is unchanged.
- Wrong movements must be explainable (§8): `movementError` goes back to `FeedbackPanel`, e.g. COFFEE: "bottom hand moved too (9 cm, should be <4 cm)" or "only cranked 10°, need 30°"; COLD: "shaking too slowly (2 Hz, need 4–6 Hz)".

### 4.8 ICED → COLD (mandatory, not optional)

- **ICED is not a standalone ASL sign.** Lifeprint's coffee page states: "Iced coffee is 'I-C-E' + 'coffee.'" — iced coffee is fingerspelled I-C-E, then COFFEE. v2's L-hand `[0,0,1,1,1]` was invented. Teaching it would be wrong.
- **Replace it with COLD**: two S-fists shivering (§4.3 #7), a real sign, and the natural pair to HOT (hot vs cold). Lesson 2's café logic is unchanged ("hot or cold?"), and the lesson order is unchanged (COFFEE, HOT, COLD, HELP).
- Change points: `lessons.json` `ICED / Iced, thanks` → `COLD / Cold, thanks`; `templates.json` delete the ICED template and add COLD; remove every ICED string from `LessonManager` / UI; do not put ICED in video captions either.
- In the submission form, do not write "we teach ICED". Write "we teach COLD (cold / cold drink)".

### 4.9 Explicitly excluded (v3 does not do these; list them in the form's Future Work — do not add them quietly)

- J, Z: need trajectory fingerspelling; not in v3.
- R / U, M / N / T: differ only in thumb position or finger crossing; Quest hand tracking cannot separate them.
- The full A–Z alphabet: do not expand to 26 letters; 8 words is the whole set.
- Fingerspelling I-C-E: ICED is replaced by COLD; do not add fingerspelling back.
- Any meaning distinguished only by eyebrows / mouth shape (Quest 3 / 3S has no eye or face tracking; the teacher's face is display only).
- Continuous signing, sentence-level grammar, two-way conversation (v1.0 direction, archived).

---

## 5. Tech stack (fixed, do not change; only the teacher-model row changes)

### 5.1 Locked versions

| Item | Version | Note |
|---|---|---|
| Unity editor | **Unity 6.3 LTS, `6000.3.x`** (current `6000.3.28f1`) | Not 2022.3 (cannot install v207), not 6.6, not Unity 7 alpha |
| Rendering | **URP (Universal 3D template)** | The existing project is this; not Built-in |
| Target platform | **Android, Meta Quest Build Profile** | Build Settings → Android, Quest Profile |
| Meta XR All-in-One SDK | **v207.0** | Minimum Unity 6000.0.66f2; 6.3 satisfies it |
| OpenXR Plugin | **1.18.0** | Via Unity Package Manager |
| XR Hands | **1.9.0** | Do not use the deprecated Oculus XR Plugin |
| Meta XR Interaction SDK | Ships with v207 | Use Building Blocks / ISDK pinch; do not write your own ray |
| Meta XR Simulator | Ships with v207 | Laptop verification, see §10 |
| Teacher model | **Microsoft Rocketbox Male_Adult_01_facial.fbx, lowpoly, MIT** | See §3; not Mixamo, not Ready Player Me |
| Scripts | **C#** | No Visual Scripting for core logic |

Install modules (Hub or CLI): Android Build Support + OpenJDK + Android SDK & NDK Tools. Android is not installed yet; install in P0 with the CLI: `unity install-modules -e 6000.3.28f1 -m android --cm --accept-eula --yes`; on failure paste the full terminal error. Do not retry silently in the Hub.

The project already exists: `D:\Unity\Projects\SignSightVR`, repo `github.com/alexwangdev/SignSight`. Use it; do not create a new project.

### 5.2 Key APIs (Claude: check the official docs, do not invent from memory)

- Hand tracking: `UnityEngine.XR.Hands`, `XRHandTrackingSubsystem`, `XRHandJointID` (26 joints). **Read both hands** — v2 read only the right hand (`signingHand = Right`); v3 must read per the template's `hands` field.
- Gestures (optional accelerator): `XRHandShape` / `XRHandPose` / `StaticHandGesture`. If used as a helper, keep the template recorder; StaticHandGesture cannot replace custom templates and cannot do dynamic movement.
- Hand interaction: Meta Interaction SDK `HandGrab` / `Poke` / `Pinch`; do not write your own collision.
- Scene: `OVRCameraRig` + `Passthrough` (MR). Not a black VR background — judges must see the real room + the teacher opposite.
- Character: Unity Humanoid Avatar + `SkinnedMeshRenderer.SetBlendShapeWeight` (face); finger bones driven per §3.4, never a ready-made animation clip passed off as ASL.

### 5.3 Project structure (★ = v3 change; the rest follows v2 / the repo as it stands)

```
Assets/
  Scenes/
    Main.unity                 # Main scene: teaching, Passthrough + teacher + UI
    TemplateRecorder.unity     # Recorder scene (v2: add a "perform the movement once" step)
  Models/
    Rocketbox/                 # ★ new
      Male_Adult_01_facial.fbx # ★ teacher, lowpoly, 52 ARKit blendshapes
      Male_Adult_01.png        # ★ texture (plus other textures in the same folder)
    # ★ deleted: Models/Teacher.fbx (Mixamo Ch33)
  Editor/
    FixRocketboxMaxImport.cs   # ★ new, ships in the Rocketbox repo
  Scripts/
    Hand/
      CurlCalculator.cs        # unchanged
      TemplateStore.cs         # ★ v2 schema (§4.5) + migration
      TemplateRecorder.cs      # ★ v2: start pose + movement trajectory
      SignRecognizer.cs        # ★ trajectory recognition (§4.6)
      TrajectoryTracker.cs     # ★ new: 1.5s ring buffer + movement feature extraction
      IHandProvider.cs         # unchanged (QuestProvider / SimulatorProvider share it)
    Learn/
      LessonManager.cs         # call becomes teacher.Demonstrate(template); rest unchanged
      TeacherAvatarDriver.cs   # ★ both hands + movement sequence + head (§3.4)
      FaceDriver.cs            # ★ new: ARKit blendshape driver
      ScoreManager.cs          # unchanged
      FeedbackPanel.cs         # ★ movement hint line (§8)
      GhostHand.cs             # ★ two hands + movement playback (§8)
    UI/
      LessonUI.cs              # unchanged
      WordCard.cs              # unchanged
  Prefabs/
    Teacher.prefab             # ★ rebuilt with the Rocketbox model
    GhostHand.prefab           # ★ able to spawn left + right hands
    WordCard.prefab
    FeedbackPanel.prefab
  Resources/
    templates.json             # ★ v2 schema, 8 real action definitions
  StreamingAssets/
    lessons.json               # ★ ICED → COLD
```

**Do not build these (already cut in v2 / v1):** `Conversation/`, `Speech/`, TTS, STT, ConfirmGate, DialogueHistory, CaptionManager, café dialogue scripts.

---

## 6. Teaching UI: big text, big buttons, no small print

- Word card: the current word in ≥48pt, e.g. `COFFEE`, one line below in smaller English `Coffee, please`, and one more line with the movement cue, e.g. `Both fists — only the top hand cranks` (do not show only the word; show the movement cue).
- Progress: top shows `Lesson 2 · 2 / 4` and total `6 / 8`, score `Score 40`, streak `Streak ×3`.
- Buttons: Replay, Try Again, Next — all pinch-triggered, each ≥10 cm wide, centred low in the field of view. No gaze dwell.
- During the teacher demo, only Replay is shown; after scoring, the buttons become Try Again / Next.
- Colours: high contrast, dark translucent background + white text, green for correct, red for wrong, a blue progress ring while performing (fills over the 3s window).
- All UI sits inside the seated field of view (horizontal ±30°, vertical −10° to +20°). Never make the user look up for a button.

---

## 7. Scoring: first try correct +10, second try correct +5, no deductions (points unchanged; only the fire condition changes)

```
Movement completed inside the 3s window, total distance ≤ 0.35 (§4.6):
  → correct
Otherwise:
  → wrong, go to §8 feedback
```

- First try correct: **+10**, green `Correct! +10`, streak +1.
- Second try correct (after feedback): **+5**, green `Correct! +5`, streak kept.
- Second try still wrong: **+0**, streak reset, show the correct answer, move to the next word. Never block the user on one word for 5 minutes.
- Streak bonus: 3 consecutive first-try corrects add **+5**, shown as `Streak ×3 +5`. Streaks count first-try corrects only.
- Total: 8 words, 80 points max (excluding streak bonus; max 80 + 10 = 90 — display the total and the stars, not the arithmetic).
- Stars: ≥70 = 3 stars, ≥50 = 2 stars, ≥30 = 1 star, <30 = 0 stars. The completion screen shows stars + a "practice the words you missed" button.
- No deductions, no countdown pressure, no leaderboard. A teaching app must let people fail safely.

---

## 8. Feedback: ghost hand + per-finger comparison + movement hint (the core design — do not change it; v3 adds the movement line)

This is the most valuable design in the teaching version and the moment to stage in the video. Never show only "wrong, try again".

### 8.1 How the ghost hand is posed (v3: two hands, and it moves)

- On screen, side by side: the **target hand(s) (ghost hand)** — semi-transparent, posed from the current target template — and **your hand**: your real hand in Passthrough, plus a virtual hand posed from your `userCurl[5]` at the moment of the attempt, so the two can be compared side by side.
- **New in v3**: the ghost hand is not static. It **replays the target movement once, at 0.5× speed** (e.g. COFFEE's two cranks, COLD's shake), so the learner sees the trajectory, not just a still hand. One loop, then stop.
- Both hands about the same size, same palm orientation. Never one facing forward and the other backward.
- `GhostHand.cs` is driven by the template (including movement), the same numbers the teacher uses. `GhostHand` must be able to spawn left + right hands (v2 had only a right hand); mirror the right hand for the left — do not write two implementations.

### 8.2 Per-finger marking (unchanged)

Per finger, `diff = |userCurl[i] - targetCurl[i]|`:

| diff | Colour | Meaning |
|---|---|---|
| < 0.15 | Green | This finger is right |
| 0.15 – 0.30 | Yellow | Close |
| > 0.30 | Red | This finger is wrong — fix this one |

- Mark only the worst 1–2 fingers. Do not turn all 5 red and scare the learner.
- One concrete hint per worst finger, in this form:
  - Index diff large and userCurl > targetCurl → "Index: straighten more"
  - Index diff large and userCurl < targetCurl → "Index: curl more"
  - Thumb / middle / ring / pinky follow the same pattern.
- If Top1 is another word (e.g. target COFFEE, learner did HOT), add one more line: `Looks more like HOT — check your index finger`. Do not just say wrong; say what it looked like.

### 8.3 Movement hint (new in v3 — mandatory)

Beyond the per-finger line, add one movement hint line from `movementError`. Never write empty advice:

| Word | How to phrase a movement error |
|---|---|
| COFFEE | Bottom hand moved too → "Bottom hand: keep it still"; only cranked 10° → "Crank more — 30° × 2" |
| COLD | Too slow → "Shake faster — 4–6 times per second"; too small → "Shake bigger — 3–5 cm" |
| HOT | Too slow / too short → "Flick faster, from your mouth outward"; did not start at the mouth → "Start at your mouth" |
| THANK-YOU | No forward push → "Push forward from your chin"; did not start at the chin → "Start at your chin" |
| HELLO / YES | Handshape only, no movement → "Wave it — side to side, twice" / "Nod your wrist — up and down, twice" |
| NO | No snap → "Snap your fingers down onto your thumb" |
| HELP | No lift → "Lift both hands up together" |

### 8.4 The video must stage this once

- Stage one deliberate mistake: learning COFFEE, first do it with one hand only (or crank both hands). The ghost hand appears, the hint says "keep the bottom hand still — only the top hand cranks", then the learner does it correctly, +5.
- Then stage one correct one: THANK-YOU, teacher smiles + hand pushes forward from the chin, learner copies it, +10.
- Video caption, one line: `wrong finger, not just wrong answer`, plus one more line: `real ASL — not handshapes`.
- Do not hide the feedback. That is what the judges need to see.

---

## 9. Scene and art: MR Passthrough, not a black background

- Main scene is Passthrough: the user sees the real room; the teacher is virtual, sitting ~1.5 m opposite.
- Art style: clean, high contrast, no clutter. UI: dark translucent background + white text + green / red scoring colours.
- A small virtual coffee cup on the table as a visual anchor is fine. Do not build a full 3D café.
- Performance target: 72 FPS on Quest 3, draw calls <100, memory <500 MB. Use Rocketbox **lowpoly**, no real-time ray tracing. The facial FBX is 1.9 MB; that is fine at lowpoly — if the import comes in as hipoly, fix the LOD in `FixRocketboxMaxImport.cs` immediately, do not discover it in P4.
- Seated test: all UI inside the seated field of view (horizontal ±30°, vertical −10° to +20°).

---

## 10. No real headset — how to test (the user has no Quest now)

### 10.1 Main path: Meta XR Simulator v207 + laptop webcam

- Meta XR Simulator can track real hands from a laptop webcam (v207 feature), not only fake mouse hands.
  - Enable it: Simulator Inputs panel → Left / Right Input = Hand → enable Camera tracking → allow camera permission, status Off → Starting → Streaming.
  - It tracks two hands, 21 keypoints per hand, mapped to OpenXR's 26 joints. v207.1 added grab / pinch for webcam hand tracking.
- **Be honest about its limits:**
  - The laptop has one ordinary RGB camera; Quest has 4 IR cameras + depth. Occlusion, thumb crossing and fast movements will be worse in the simulator. **v3's two-handed signs (COFFEE, COLD, HELP) are the most exposed**: with hands stacked, the simulator may track only one. State in the acceptance notes that "two-handed signs may lose one hand in the simulator; re-tune on real hardware".
  - Light, distance and a cluttered background cause jitter. Fast movements (HOT's flick, NO's snap) may drop frames in the simulator — leave margin in the thresholds.
  - The head can rotate but not move. Passthrough is synthetic, not a real camera feed.
  - Performance is desktop GPU / CPU performance, not Quest's XR2 Gen 2.
- So simulator verification means: **logic works, thresholds are roughly right, UI is readable, video is recordable**. Not "thresholds are already tuned".
- The teacher demo needs no real hands and is fully visible in the simulator: both hands doing the movement sequence, face smiling, head nodding — all recordable.

### 10.2 Fallback: MediaPipe bridge (only if the simulator webcam is not enough)

- Laptop webcam → OpenCV VideoCapture 30 FPS → MediaPipe Hands (21 points) → curl / pinch → UDP / WebSocket to Unity → the same recognition code (`IHandProvider` interface, QuestProvider and MediaPipeProvider share it).
- Try the simulator webcam for 30 minutes first; only then spend half a day on the bridge. v3 has even less time — avoid this path if possible.

### 10.3 Real headset (if one can be borrowed / bought)

- Reserve **1 day** before the deadline for real-headset threshold tuning. The simulator-tuned 0.35 distance threshold and 3s window will very likely need changes on real hardware, especially for two-handed signs and fast movements.
- Options: borrow from a colleague, a Toronto VR group, or buy a Quest 3S 128GB (new ~$479.99 CAD, second-hand ~$250–$350 CAD), tune, then decide whether to keep it. Do not leave the first real-headset run to the last day.
- If no headset can be borrowed, do not block submission: the official FAQ says no headset is required to compete and video may be simulator footage. The APK must run on real hardware means the judges' hardware, not that the submitter must have tested on one — but if it was never tested on real hardware, say so honestly in the form. Do not claim it was tested.

---

## 11. Not-doing list (v3 explicitly does not do these — Claude, do not add them)

- No TTS, no STT, no Deepgram, no dual modes, no ConfirmGate, no café dialogue, no captions.
- No full ASL translation, no continuous signing, no sentence-level grammar, no A–Z alphabet.
- No J / Z, no R / U, M / N / T.
- No fingerspelling I-C-E (ICED is replaced by COLD).
- **No static handshapes passed off as real ASL**: do not bring back v2's C-hand COFFEE, A-fist HOT, Y-hand HELP or L-hand ICED. Every word's definition follows §4.
- No learner face / eyebrow recognition, no Quest Pro-only features (Face Tracking may only be an optional enhancement, never required). The teacher's face is display only, never scored.
- No multiplayer, no leaderboard, no cloud LLM, no Token Factory / Nemotron.
- No controller support, no standing / walking mode.
- No SPECS / Lens Studio version (the user decided not to enter that competition — do not raise it again).
- No v1.0 two-way conversation (archived; one line in Future Work is fine, do not implement it in v3).
- No text-to-pose generative model (Progressive Transformers / SignLLM, etc.): v3 is a hard-coded action dictionary, see §4.5. Generative models are post-competition research — do not put them in the APK.

---

## 12. Execution phases (Claude: follow this order; ~5 days total; the repo already has the v2 base — do not start from zero)

| Phase | What to do | Acceptance (must be demonstrable) | Estimate |
|---|---|---|---|
| **P0 Swap the teacher** | Delete Mixamo `Teacher.fbx`; import the Rocketbox facial version + `FixRocketboxMaxImport.cs` per §3.3, lowpoly; Humanoid mapping all green, 52 blendshapes; rebuild `Teacher.prefab`; update the README source | In the Simulator, the Rocketbox teacher is visible sitting down, fingers move, and the mouth corners rise on THANK-YOU (MouthSmile 60) | 0.5 day |
| **P1 Teacher movement** | `TeacherAvatarDriver` v3: both-hands IK + movement sequences (§3.4) + head nod / shake; `FaceDriver.cs`; `lessons.json` ICED → COLD; `templates.json` v2 schema (§4.5) | In the Simulator, watch the teacher do COFFEE once (two S-fists, top cranks, bottom still) and THANK-YOU once (flat hand pushes forward from the chin + smile), matching §4.3 | 1 day |
| **P2 Trajectory recognition** | `TrajectoryTracker.cs` + `SignRecognizer` v3 (§4.6) + `TemplateRecorder` v2 (start pose + movement trajectory); both-hands tracking | In Simulator webcam mode, record COFFEE, then perform COFFEE and have it recognised; cranking both hands must NOT fire (or must report "keep the bottom hand still"); 3 consecutive correct performances, no double-firing | 1.5 days |
| **P3 Scoring + feedback** | `GhostHand` two hands + movement playback + movement hints (§8.3); `ScoreManager` unchanged; `LessonManager` call becomes `Demonstrate(template)` | Deliberately perform COFFEE wrong once: ghost hand replays the grind, hint says "keep the bottom hand still", second try scores +5; THANK-YOU correct shows the teacher smiling | 1 day |
| **P4 Polish + video + submission** | UI size / contrast, performance to 72 FPS, (if a real headset is available) one threshold pass, record a <3 min video, write the Devpost form, upload the APK to the Competition channel | Video contains one full wrong-COFFEE → feedback → correct sequence and one THANK-YOU smile sequence; the form states the hand interaction and the "real ASL" sources; APK is on the Competition channel and the Invite URL opens | 1 day |

Total ~5 days, deadline 11-18 15:00 ET. P4 video and submission must not be eaten by P0–P3; video must be recordable by the evening of November 17.

### 12.1 Cut line (if P2 slips, cut in this order — do not just push on)

1. First cut **movement scoring for HOT / THANK-YOU**: score them on handshape + start location only (claw hand at the mouth, flat hand at the chin counts as correct); movement stays in the teacher demo only. **Do not cut** movement scoring for COFFEE / COLD / YES / NO / HELLO — those five cannot be separated by handshape alone; cutting them returns to v2's error.
2. Then cut **face**: keep only THANK-YOU's smile; cut HOT's JawOpen and idle blinking. Do not cut the smile — the video relies on it.
3. Last, cut **ghost-hand movement playback**: ghost hands go back to static pose comparison; movement hints stay as text only. Do not cut per-finger red / yellow / green.
4. Trigger: if trajectory recognition is not working by the end of P2 (evening of Nov 15), cut immediately in the order above and state in your reply exactly what was cut. Do not wait until P4 to admit it cannot be done.

---

## 13. Acceptance checklist per phase (Claude: tick each item at the end of the phase)

### P0
- [ ] Mixamo `Teacher.fbx` deleted; only one teacher model in the project
- [ ] Rocketbox facial version imported, lowpoly, Humanoid mapping all green (finger gaps handled per the §3.4 fallback and stated explicitly)
- [ ] `SkinnedMeshRenderer` shows 52 `AK_` blendshapes
- [ ] `FixRocketboxMaxImport.cs` in `Assets/Editor/`, no import errors
- [ ] Rocketbox teacher visible seated in the Simulator, Passthrough working
- [ ] README model source changed to Rocketbox + MIT

### P1
- [ ] Teacher performs all 8 movements per §4.3; two-handed signs move both hands, one-handed signs keep the other hand still
- [ ] COFFEE: top cranks, bottom still — visibly a crank, not a twist
- [ ] THANK-YOU: hand pushes forward from the chin while smiling (MouthSmile 60)
- [ ] YES / NO: head nods / shakes with the hands
- [ ] `lessons.json` has no ICED; `templates.json` is v2 schema (version 2)

### P2
- [ ] Recorder records a start pose + one full movement; JSON contains a movement field
- [ ] After recording, performing the sign recognises Top1; firing only inside the 3s window, never on timeout
- [ ] Cranking both hands for COFFEE does not fire (or reports the movement error); cranking only the top hand fires
- [ ] No double-firing inside the 1s lockout
- [ ] Top2 is reported, confidence in 0–1

### P3
- [ ] First-try correct +10, second-try correct +5, second-try wrong +0 and moves on
- [ ] 3-streak bonus +5, stars computed per §7
- [ ] On a wrong attempt the ghost hand(s) appear (two for two-handed signs), per-finger red / yellow / green correct, worst finger has a concrete hint, movement hint line correct
- [ ] Try Again at most twice; never blocks

### P4
- [ ] Video <3 min, contains one full wrong-COFFEE → feedback → correct sequence and one THANK-YOU smile sequence
- [ ] Video labels which parts are simulator footage and which are real-headset footage (if any)
- [ ] APK on the Competition channel, Invite URL opens
- [ ] Devpost form submitted, screenshot kept
- [ ] (If never tested on real hardware) the form honestly states "not tested on real hardware; thresholds tuned in the simulator"

---

## 14. Submission form copy (Claude drafts this for the user — do not submit it directly)

- **Track:** Productivity
- **Division:** New Experience
- **One-liner:** Learn 8 real ASL signs in MR — a 3D teacher shows the sign, you copy it, and per-finger + per-movement feedback tells you exactly what to fix.
- **Hand interaction:** Hands-only, seated. Watch the teacher, copy the sign (handshape + location + movement), complete it within 3s, pinch Replay / Next. No controllers. Two-handed signs (COFFEE, COLD, HELP) use both hands.
- **Real-ASL note (write this prominently, do not hide it):** the 8 definitions follow Lifeprint / ASL University (Dr. Bill Vicars): COFFEE is two S-fists grinding coffee (top cranks, bottom still), HOT is a claw hand flicked out from the mouth, THANK-YOU is a flat hand pushed forward from the chin + a smile, HELP is a two-handed lift, and ICED was replaced by COLD (iced coffee in ASL is fingerspelled I-C-E + COFFEE, not a standalone sign).
- **Teacher model:** Microsoft Rocketbox Male_Adult_01 (facial version, MIT License), low-poly + 52 ARKit facial blendshapes, fingers driven from templates, no ready-made ASL animation.
- **Special awards:** Best Accessibility Forward (primary), Best First Five Minutes, Best Reason to Come Back
- **Future Work:** more signs, A–Z alphabet, continuous signing, two-way conversation (v1.0 direction, deferred), facial grammar (needs Quest Pro or an external camera), text-to-pose generative models (post-competition research, not in v3).

---

## 15. References: projects not to copy (Claude: do not clone them)

- VRSignify (Unity Quest 3, sign → text, learning): one-way recognition + text only, no teacher demo, no per-finger feedback.
- XR-SignQuest (Quest 3, YOLOv8 learning): learning + confidence only, no ghost-hand comparison.
- ASL-Passthrough (Quest 2, casting ICE): its pose-saving tool may be referenced, but do not copy the whole project.
- Sign Pose VR (on the Quest Store, learning): a shipped competitor — do not copy its lesson structure.
- Handset (browser + Twilio calls): two-way loop concept is close, but it is not Quest MR, and it is the v1.0 direction, archived.
- Hand Wave (smart-glasses fingerspelling): fingerspelling only, one-way, no on-screen captions back.
- **sign.mt** (web two-way translation): speech → text → gloss → pose lookup + skeleton / 3D avatar, self-reported accuracy only 10–40%, translation not teaching. Its lexicon / pose-data approach may be referenced; **do not port its web app to Quest** — that would violate the New Experience originality requirement.
- **Lifeprint / ASL University**: the source for v3's action definitions, not a code reference. Read §4.4 before writing code; do not invent movements from memory.

Reference ideas only; never copy a whole project. Copying a whole project violates the New Experience originality requirement. The teaching space is crowded — differentiate on §8's per-finger + movement ghost-hand feedback and on the real-ASL definitions, not on another "right / wrong" learning app.

---

## 16. Risks and honest statements (put these in the submission form — do not hide them)

- The user has no Quest now; thresholds were tuned in the simulator + laptop webcam and will likely need re-tuning on real hardware. Two-handed signs may lose one hand in the simulator. Only 1 day is reserved for real hardware in P4; if none can be borrowed, submission is not blocked.
- Quest 3 / 3S has no face tracking; the learner's face is not scored. The teacher's face is display only (smile / blink), not grammar.
- The teacher is a free Microsoft Rocketbox character (MIT); cite the source — it is not our own model. The Rocketbox repo is archived (no updates since 2022), but downloads and the license remain valid. Do not use Ready Player Me — it is discontinued.
- The 8 definitions follow Lifeprint, but **they have not been validated by the Deaf community** — do not write "validated by Deaf users" in the form. Write "definitions sourced from Lifeprint / ASL University; corrections from Deaf users welcome". Teaching a wrong sign is worse than not teaching it; v3 exists to fix v2's errors.
- Teaching apps already exist. This project's differentiation is per-finger + movement ghost-hand feedback and real ASL action definitions, not "another alphabet app".

---

*SPEC version: v3.0 teaching version, 2026-11-12. Authors: user + Muse. Executor: Claude. Deadline: 2026-11-18 15:00 ET.*
*This file: `workspace/goals/meta-vr-start-sign-language-entry/files/signsight-learn-spec-v3-en.md` — the only valid SPEC. v2.0 (`signsight-learn-spec-zh.md` / `-en.md`, Mixamo + static templates + ICED) is archived and void; v1.0 two-way conversation is void even earlier. Do not build from them. The SPECS / Lens Studio version is cancelled — do not mix it in.*
*Repo state: `github.com/alexwangdev/SignSight`, single commit `e9cb60e`, already built to v2 with all scripts, scenes, lessons.json and templates.json v1. v3 is a targeted correction of that repo — see §2.*
