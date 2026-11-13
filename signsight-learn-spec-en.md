# SignSight Learn — Quest Sign-Language Teaching · English SPEC v2.0 (for Claude)

> **Execution instructions for Claude — read this first, then start work**
> 1. This is v2.0, the teaching version, and the only valid SPEC now. The v1.0 two-way conversation SPEC (`signsight-quest-spec-zh.md` / `signsight-quest-spec-en.md`) is archived and superseded. Do not follow it. Do not bring back TTS, STT, dual modes, ConfirmGate, or the coffee-shop dialogue.
> 2. You are the sole executor of this SPEC. Follow the phases in §12 in order, one phase at a time. Run the §13 acceptance checks at the end of each phase, and only move to the next phase after they pass. Do not skip phases. Do not write all features at once.
> 3. Do not change the scope. Anything marked "not in v1" / "do not do" must not be added as a favour. If a change is needed, explain why in your reply first and wait for the user to confirm.
> 4. All numbers, thresholds, file names and package versions in this file prevail over your training data. If there is a conflict, this file wins.
> 5. Code comments and UI strings in English; explanations to the user in English. This is the English SPEC.
> 6. The user does not own a Quest. All laptop verification goes through Meta XR Simulator (§10). Do not assume a real device is available.
> 7. **The project already exists — DO NOT create a new project.** The user has already created it: `D:\Unity\Projects\SignSightVR`, Unity `6000.3.28f1` LTS, Universal 3D (URP) template. P0 means: open that existing project, verify it, and add the packages / Quest Profile to it. Do not create a second project, do not re-run the Hub New Project wizard.
> 8. **Current setup status (2026-11-12):** `claude mcp list` already shows `unity-editor-mcp` — the Unity CLI MCP connection is working. **Android Build Support is NOT installed yet.** In P0, install it with the Unity CLI first: `unity install-modules -e 6000.3.28f1 -m android --cm --accept-eula --yes`, and paste the full terminal error if it fails. Do not retry silently in the Hub GUI — the Hub hides the failure reason, the CLI prints it.
> 9. Why the pivot (for Claude — do not overturn it): on 2026-11-12 the user pivoted from two-way conversation to teaching because only ~6 days remained before the deadline. The teaching version drops the hardest parts — TTS / STT / dual-mode echo prevention / ambiguity confirmation — and replaces them with a shippable loop: "teacher demonstrates → you copy it → score → per-finger correction". Teaching apps already exist (see §15). This project's differentiator is not "another learn-the-alphabet app" — it is the per-finger ghost-hand feedback in §8: when you are wrong, it shows which finger is wrong, by how much, and how to fix it.

---

## 1. One line: what this is

**SignSight Learn**: wearing a Quest 3 / 3S, learn 8 useful ASL signs from a 3D teacher sitting opposite you.

- The teacher demonstrates a sign first, so you can see it clearly.
- You copy it, hold for 0.6 s, and the system scores you.
- Correct: you earn points. Wrong: the screen shows the target hand and your hand side by side, with each finger coloured red / green, telling you exactly which finger to fix.

**This is NOT** a learn-A-Z alphabet app. It does not spell words and does not do continuous signing. It teaches 8 immediately useful words.

### 1.1 Competition positioning (decided — do not change)

- Competition: Meta VR Start Developer Competition 2026
- Deadline: **2026-11-18 15:00 ET**
- Track: **Productivity**, Division: **New Experience**
- Target prizes: Productivity New 1st $70,000 / 2nd $35,000; special awards **Best Accessibility Forward $25,000** (primary), plus **Best First Five Minutes $25,000** and **Best Reason to Come Back $25,000**
- Hard requirements (all mandatory):
  - **Hands-first end-to-end**: the whole experience works with no controllers, no controller pairing needed.
  - **Seated**: passes the 2-foot / airplane-seat test — one full lesson can be completed while seated.
  - **A complete experience in ≤10 minutes**: from putting the headset on to learning all 8 signs and seeing the final score, under 10 minutes.
  - **Original, not a wrapper**: if you swap the recognition model out, the project must still work (teacher driving, scoring, per-finger feedback and lesson progress are all built by us).
- Deliverables: a playable APK (Competition release channel + Invite URL) + a **<3-minute public YouTube / Vimeo video** (judges are not required to watch past 3 minutes) + the Devpost form (track, division, hand-interaction description). You must be a Start program member before submitting.
- Judges test on a real **Quest 3 or Quest 3S**. Video may use simulator footage, but the APK must run on a real device. The official FAQ is explicit: competing does not require owning a headset, and the video may show the project on a Quest device, via XR Simulator, or via an equivalent emulator.

### 1.2 Why teaching (do not overturn this in the SPEC)

- The v1.0 two-way version needed TTS out loud, STT captions, dual-mode echo prevention and ambiguity confirmation all working at once. Its 11-day plan had only ~6 days left, so the risk of not shipping was too high.
- The teaching version keeps the hardest and most valuable piece: hard-coded joint recognition (§4). It drops the surrounding speech pipeline, not the core.
- Teaching apps have competitors, but most of them only give "correct / wrong" or a confidence number. This project differentiates with §8's ghost hand + per-finger comparison: when you are wrong, you can see which finger, by how much.

---

## 2. Users and scenario

### 2.1 Who uses it

| Role | Device | Does |
|---|---|---|
| Learner (primary user) | Wears Quest 3 / 3S, seated | Watches the teacher demonstrate; copies the sign; pinches Replay / Next |
| 3D teacher | Virtual character, sitting opposite at ~1.5 m | Demonstrates signs; does not speak, uses hands + large text only |

- Primary-user assumption: a beginner, or someone who knows only a few signs, and can read large English text.
- Solo throughout. No second person, no second headset. Judges also test alone.

### 2.2 Lessons: 8 words, 2 lessons (the demo uses this — do not change it)

| Lesson | Words | Order |
|---|---|---|
| Lesson 1 — Greetings | HELLO, YES, NO, THANK-YOU | In table order |
| Lesson 2 — Café | COFFEE, HOT, ICED, HELP | In table order |

- Both lessons are unlocked directly from the menu. Do not add a gating rule. Judges have only a few minutes — they must not have to finish Lesson 1 to see Lesson 2.
- Each lesson has 4 words. Finishing a lesson shows a short lesson summary, then the next lesson. Finishing all 8 shows the total score and star rating.
- Seated throughout, quiet indoor room. No walking, no noisy street.

### 2.3 Main loop: Demonstrate → Copy → Score → Correct (do not change the order)

1. **Teacher demonstrates**: the teacher makes the target sign, holds it for 1.5 s, with large text beside it, e.g. `COFFEE — Coffee, please`. Pinch Replay to watch it again.
2. **You copy it**: you see your own real hands in passthrough, make the same sign, and hold for 0.6 s to lock.
3. **Score**: see §7. Correct earns points and moves on; wrong does not skip ahead — it goes to correction.
4. **Correct**: see §8. After studying the ghost-hand comparison, pinch Try Again to retry the same word. At most two attempts per word; if the second attempt is still wrong, move on anyway. Never get stuck.

---

## 3. Teacher: a free 3D character from Mixamo — do NOT use Ready Player Me

### 3.1 Model source (decided)

- **Preferred: Mixamo, free.** Use a free Mixamo character, e.g. Ybot / Xbot. Download FBX for Unity, With Skin, T-pose.
- **Skeleton LOD must be Standard Skeleton (65 bones)** — the option with every finger jointed. Do not pick No Fingers / 2 Chain / 3 Chain, or the fingers cannot move.
- Per Adobe, Mixamo characters and animations are royalty-free for commercial projects, including video games. Record the model file and its source in the README / submission notes; do not claim the model was built by us.
- **Do not use Ready Player Me.** Netflix acquired the company in December 2025, and on January 31, 2026 its public avatar creator, PlayerZero and developer APIs went offline.
- Fallback (only if Mixamo fails): Genies Avatar SDK, free on the Unity Asset Store, supports URP and Unity 6. Do not bring in two character systems at once.

### 3.2 How the teacher moves (key point — do not look for ready-made ASL animations)

- **Do not** search for, buy, or hand-keyframe 8 ASL animation clips. Free ready-made ASL animations essentially do not exist, and hand-keyframing 8 of them cannot be done in 6 days.
- **Drive the teacher's hands directly from the §4.3 curl templates.** If the template for COFFEE is `[0.5, 0.5, 0.5, 0.5, 0.5]`, bend the teacher model's 5 fingers to those values. Recognition uses the same numbers, so the teacher and the checker agree by construction — the teacher cannot demonstrate one shape while the system recognises another.
- Implementation: `TeacherAvatarDriver.cs`, input target `curl[5] + palm`, interpolate from a neutral hand to the target in 0.5 s, hold for 1.5 s, then return to neutral. Raise the arm to chest height and set the palm orientation from the template's palm value — do not move only the fingers while the arm stays still.
- The teacher's face and body may stay simple: standing / sitting still, only the arm and hand move. No facial expressions, no lip sync — Quest 3 / 3S has no face tracking anyway, and the teacher does not need it.
- Place the teacher opposite at ~1.5 m, roughly life-size for a seated person. Do not make a giant or a miniature. In passthrough the user sees the real room, with the virtual teacher sitting behind a real table.

---

## 4. Sign recognition: hard-coded joints, no image model (carried over unchanged from v1.0)

### 4.1 Principles (decided)

- **No image-recognition model, no third-party sign-translation API.** Quest already gives 26 joints per hand — compute from the joints. This also avoids wrapper and licensing problems.
- Do not hard-code absolute positions. Use finger curl + palm orientation, normalised to 0–1.
- Only **static signs**. A sign only counts when it is held. No continuous signing, no dynamic J / Z.

### 4.2 Features: 5 numbers per hand + orientation

For each finger, compute curl `curl ∈ [0, 1]`, where 0 = fully extended, 1 = fully curled:

- `curl = average of FullCurl / BaseCurl / TipCurl`, or convert directly from `XRHandJoint` angles via XR Hands (see §5.2 — Claude picks one method and stays consistent).
- Also compute: `pinch` (thumb–index distance, normalised), `spread` (how open the fingers are), `palmNormal` (palm facing up / forward / toward self).

One template = one `float[5] curl` + `palm` + `tolerance`.

### 4.3 v1 sign set: 8 signs, all static, as visually distinct as possible

> The curl values below are initial values, not final values. Values recorded by the user with the recorder (§4.4) override them. Claude must not bake these numbers in as unchangeable constants. The teacher demonstration uses the same values (§3.2).

| # | Intent (English) | Meaning | Hand shape | Curl reference [thumb, index, middle, ring, pinky] | Palm | Lesson |
|---|---|---|---|---|---|---|
| 1 | HELLO | "Hello" | B hand: four fingers together and extended, thumb against palm | [0.2, 0.0, 0.0, 0.0, 0.0] | Forward | L1 |
| 2 | COFFEE | "Coffee, please" | C hand: half-closed into a C shape | [0.5, 0.5, 0.5, 0.5, 0.5] | Sideways | L2 |
| 3 | HOT | "Hot, thanks" | A hand: fist, thumb in front of the fist | [0.7, 0.9, 0.9, 0.9, 0.9] | Forward | L2 |
| 4 | ICED | "Iced, thanks" | L hand: index + thumb form an L, others curled | [0.0, 0.0, 1.0, 1.0, 1.0] | Forward | L2 |
| 5 | YES | "Yes" | S fist: fully closed fist | [1.0, 1.0, 1.0, 1.0, 1.0] | Forward | L1 |
| 6 | NO | "No" | Index + middle extended together, others curled | [1.0, 0.0, 0.0, 1.0, 1.0] | Forward | L1 |
| 7 | THANK-YOU | "Thank you" | Flat hand: five fingers extended together, palm down | [0.0, 0.0, 0.0, 0.0, 0.0] | Down | L1 |
| 8 | HELP | "I need help" | Y hand: thumb + pinky extended, others curled | [0.0, 1.0, 1.0, 1.0, 0.0] | Forward | L2 |

**Explicitly excluded (not in v1 — list in the submission form's Future Work, do not add them quietly):**

- J, Z: they need motion trajectories; static templates cannot do them.
- R / U, M / N / T: they differ only by thumb position or finger crossing, which Quest hand tracking cannot separate.
- The full A–Z alphabet: do not expand to 26 letters. The 8 words are the whole set.
- Any meaning distinguished only by eyebrows, mouth shape or facial expression (Quest 3 / 3S has no eye tracking and no face tracking).

### 4.4 Template Recorder (mandatory — this is the only way to tune thresholds)

Build a `Template Recorder` scene (in Settings, not in the main demo):

1. Select an intent (e.g. COFFEE).
2. On-screen prompt: "Make this sign and hold it for 1 second".
3. After holding for 1 second, save the current `curl[5] + palm` as that intent's template, stored in `Application.persistentDataPath/templates.json`.
4. Allow 3 recordings per intent; use the average as the template.
5. After recording, test immediately: make the sign and see the recognition result and confidence.

Without the recorder, thresholds cannot be tuned on a real device. Do not cut this feature.

### 4.5 Recognition flow: only counts after holding for 0.6 s

```
Every frame:
  Read XR Hands 26 joints → compute curl[5] + palm
  Compute distance (Euclidean, weighted: index/middle higher, thumb lower) to the 8 templates
  Take Top1, Top2 + confidence (distance mapped to 0–1, closer = higher)
  If Top1 stays stable for ≥ 0.6 s (~10–15 frames at 60 Hz):
      → Fire one recognition event, output { Top1, Top2, confidence, userCurl[5] }
      → 1 s lockout, during which no further event fires (prevents jitter re-triggering)
  Otherwise: do not fire; show the closest candidate in the UI as small preview text only, do not score
```

- Smoothing: apply a 5-frame moving average to curl values; do not compare raw jittery values directly.
- Tolerance: initial `distanceThreshold = 0.35` (normalised distance), adjustable in the recorder. Prefer a miss over a false positive.
- If the hand leaves the field of view / tracking is lost for > 0.5 s, clear the current candidate.
- In the teaching version, scoring only checks whether Top1 is the current target word (§7). Top2 is only used in correction to say "you looked more like another word" — do not build the v1.0 ConfirmGate.

---

## 5. Tech stack (decided — do not change)

### 5.1 Version lock

| Item | Version | Notes |
|---|---|---|
| Unity Editor | **Unity 6.3 LTS, `6000.3.x`, latest patch in Hub** (currently `6000.3.28f1`) | Do not use 2022.3 (cannot install v207), do not use 6.6 (only for VR Glasses; judges use Quest 3/3S), do not use Unity 7 alpha |
| Rendering | **URP (Universal 3D template)** | The existing project already uses this; do not use Built-in |
| Target platform | **Android, Meta Quest Build Profile** | Switch to Android in Build Settings, select the Quest Profile |
| Meta XR All-in-One SDK | **v207.0** | Minimum Unity 6000.0.66f2; 6.3 satisfies it |
| OpenXR Plugin | **1.18.0** | Install via Unity Package Manager |
| XR Hands | **1.9.0** | Minimum Unity 6000.0; works on 6.3. Do not use the old Oculus XR Plugin (deprecated) |
| Meta XR Interaction SDK | Included in v207 | Use Building Blocks / ISDK pinch; do not write your own raycasting |
| Meta XR Simulator | Included in v207 | For laptop verification, see §10 |
| Teacher model | **Mixamo free character, Standard Skeleton (65 bones)** | FBX for Unity, With Skin, T-pose, Humanoid. Do not use Ready Player Me |
| Scripting | **C#** | Do not use Visual Scripting for core logic |

Hub modules to install (via Hub or CLI): Android Build Support + OpenJDK + Android SDK & NDK Tools. Android is not installed yet — install it in P0 via CLI, see instruction 8 at the top.

The project already exists: `D:\Unity\Projects\SignSightVR`, Unity `6000.3.28f1`, Universal 3D template. Use that project. Do not create a new one. Do not upgrade an old 2022.3 project in place.

### 5.2 Key APIs (Claude must check the official docs — do not invent them from memory)

- Hand tracking: `UnityEngine.XR.Hands`, `XRHandTrackingSubsystem`, `XRHandJointID` (26 joints).
- Gestures (optional accelerator): `XRHandShape` / `XRHandPose` / `StaticHandGesture`. If used as an aid, the Template Recorder must still be kept — StaticHandGesture does not replace custom templates.
- Hand interaction: Meta Interaction SDK `HandGrab` / `Poke` / `Pinch`; do not write your own collision handling.
- Scene: `OVRCameraRig` + `Passthrough` (MR passthrough). Do not build a black-background pure-VR scene — judges must see the real room + the teacher opposite.
- Character: Unity Humanoid Avatar, with finger bones driven from curl values per §3.2. Do not pretend a ready-made animation clip is the teacher.

### 5.3 Project structure (Claude must build it this way — do not invent another layout)

```
Assets/
  Scenes/
    Main.unity                 # Main scene: teaching, Passthrough + teacher + UI
    TemplateRecorder.unity     # Recorder scene
  Models/
    Teacher.fbx                # Mixamo character, Standard Skeleton (65)
  Scripts/
    Hand/
      CurlCalculator.cs        # Joints → curl[5] + pinch + spread + palm
      TemplateStore.cs         # templates.json read/write
      TemplateRecorder.cs      # Recording flow
      SignRecognizer.cs        # Distance matching + 0.6 s hold + lockout + Top1/Top2 output
      IHandProvider.cs         # Interface: QuestProvider / SimulatorProvider (same code)
    Learn/
      LessonManager.cs         # 2 lessons, 8 words, order, progress, completion
      TeacherAvatarDriver.cs   # curl[5] → teacher finger bones, 0.5 s interpolate + 1.5 s hold
      ScoreManager.cs          # Scoring, streak, stars (§7)
      FeedbackPanel.cs         # Ghost hand + per-finger comparison (§8)
      GhostHand.cs             # Semi-transparent target hand, posed from template curl
    UI/
      LessonUI.cs              # Word card, progress 1/8, score, Replay / Next / Try Again
      WordCard.cs              # Large word card
  Prefabs/
    Teacher.prefab
    GhostHand.prefab
    WordCard.prefab
    FeedbackPanel.prefab
  Resources/
    templates.json             # Default 8 templates (§4.3 initial values)
  StreamingAssets/
    lessons.json               # 2 lessons, 8 words, order and English meanings
```

**Do NOT build these (cut from v1.0):** `Conversation/`, `Speech/`, TTS, STT, ConfirmGate, DialogueHistory, CaptionManager, coffee-shop dialogue script.

---

## 6. Teaching UI: large text, large buttons, no small text

- Word card: the current word in large text ≥ 48 pt, e.g. `COFFEE`, with a smaller English line below, `Coffee, please`. A Chinese hint line is only for simulator debugging — do not stack Chinese and English in the shipped UI.
- Progress: top bar shows `Lesson 2 · 2 / 4` and total progress `6 / 8`, score `Score 40`, streak `Streak ×3`.
- Buttons: Replay, Try Again, Next — all pinch-triggered, each ≥ 10 cm wide, lower-centre of the field of view, never gaze-dwell.
- During the teacher demonstration, only Replay is shown. After scoring, the buttons become Try Again / Next.
- Colours: high contrast, dark translucent panels + white text, green for correct, red for wrong, a blue progress ring while holding (fills over 0.6 s).
- All UI must sit inside the seated field of view (horizontal ±30°, vertical -10° to +20°). Do not make the user look up to find a button.

---

## 7. Scoring: +10 on the first try, +5 on the second try, no penalties

```
On a recognition event, if Top1 is the current target word:
  If confidence ≥ 0.7 AND distance ≤ 0.35:
      → Correct
  Otherwise:
      → Wrong, go to §8 correction
```

- Correct on the first try: **+10 points**, green text `Correct! +10`, streak +1.
- Correct after correction on the second try: **+5 points**, green text `Correct! +5`, streak is not reset.
- Still wrong on the second try: **+0 points**, streak reset to 0, show the correct answer, move to the next word. Never get stuck — do not let the user spend 5 minutes on one word.
- Streak bonus: 3 consecutive first-try correct answers earn an extra **+5 points**, shown as `Streak ×3 +5`. Streaks only count first-try correct answers.
- Total: 8 words, 80 points maximum before streak bonuses (80 + up to 10 streak bonus = 90 max). Show the total and the star rating, not a confusing breakdown.
- Stars: total ≥ 70 = 3 stars, ≥ 50 = 2 stars, ≥ 30 = 1 star, < 30 = 0 stars. The completion screen shows the stars and a "Practice the words you missed" button.
- No point deductions, no countdown timer pressuring the user, no leaderboard. The teaching version must make it safe to try and be wrong.

---

## 8. Correction: ghost hand + per-finger comparison (core design — do not change)

This is the most valuable design in the teaching version, and the video must deliberately stage it once. Do not just show "Wrong, try again".

### 8.1 How the ghost hand is posed

- The screen shows two hands side by side. On the left is the **target hand (ghost hand)**: semi-transparent, posed from the current target word's template curl. On the right is **your hand**: your real hand seen in passthrough, plus a virtual hand overlaid on it posed from your just-recorded `userCurl[5]`, so the two can be compared side by side.
- Both hands are roughly the same size and the same palm orientation. Do not show one facing forward and the other facing backward.
- The ghost hand is driven by `GhostHand.cs`, using the same template curl as the teacher.

### 8.2 How each finger is marked

For each of the 5 fingers, compute `diff = |userCurl[i] - targetCurl[i]|`:

| diff | Colour | Meaning |
|---|---|---|
| < 0.15 | Green | This finger is correct |
| 0.15 – 0.30 | Yellow | Close |
| > 0.30 | Red | This finger is wrong — fix this one |

- Mark only the 1–2 worst fingers. Do not turn all 5 red and scare the user.
- Below it, give one concrete hint for the worst finger. Do not write vague encouragement:
  - Index diff large and userCurl > targetCurl → "Index: straighten more"
  - Index diff large and userCurl < targetCurl → "Index: curl more"
  - Same pattern for thumb / middle / ring / pinky.
- If Top1 was a different word (e.g. the target was COFFEE but you signed HOT), add one small line: `Looks more like HOT — check your index finger`. Do not just say wrong — say what it looked like.

### 8.3 The video must stage it once

- Deliberately stage a mistake: while learning COFFEE, first sign HOT, show the ghost hand with the index finger in red and the hint "straighten your index finger more", then sign it correctly on the second try and earn +5.
- The video caption reads: `wrong finger, not just wrong answer`.
- Do not hide the correction. That is what the judges need to see.

---

## 9. Scene and art: MR passthrough, no black background

- Main scene is Passthrough: the user sees the real room, with the virtual teacher sitting opposite at ~1.5 m.
- Art style: clean, high contrast, no clutter. UI uses dark translucent panels + white text + green / red scoring colours.
- A small virtual coffee cup may sit on the table as a visual anchor. Do not build a complex 3D coffee shop.
- Performance target: 72 FPS on Quest 3, draw calls < 100, memory < 500 MB. Use the Mixamo low-poly character; no high-poly models, no real-time ray tracing.
- Seated test: all UI must sit inside the seated field of view (horizontal ±30°, vertical -10° to +20°). Do not make the user look up to find a button.

---

## 10. No real device — how to test (the user does not own a Quest yet)

### 10.1 Main path: Meta XR Simulator v207 + laptop camera

- Meta XR Simulator can track real hands through a laptop camera (a v207 feature), not just a mouse-driven fake hand.
  - Enable it: Simulator Inputs panel → Left / Right Input set to Hand → enable Camera tracking → allow camera permission; status goes Off → Starting → Streaming.
  - It tracks up to two hands, 21 keypoints each, mapped to OpenXR's 26 joints. v207.1 also adds grab / pinch from camera hand tracking.
- **State its limits honestly:**
  - The laptop has 1 ordinary RGB camera; Quest has 4 infrared cameras + depth. Occlusion, thumb crossing and fast motion are worse on the simulator than on a real device.
  - Lighting, distance and a cluttered background cause jitter. Crossed-finger signs such as M / N / R are also unreliable on the simulator (and on a real device too, which is why v1 does not include them).
  - The head can only rotate, not move position. Passthrough is synthetic, not a real camera feed.
  - Performance is desktop GPU / CPU performance, not Quest's XR2 Gen 2.
- So the goal of simulator verification is: **logic works, thresholds are roughly right, UI is readable, video can be recorded**. It is not "thresholds are already tuned".
- The teacher demonstration needs no real hands — in the simulator you can already watch the teacher's fingers interpolate from neutral, hold, and return.

### 10.2 Fallback: MediaPipe bridge (only if the simulator camera is not enough)

- Laptop camera → OpenCV VideoCapture 30 FPS → MediaPipe Hands (21 points) → compute curl / pinch → UDP / WebSocket to Unity → the same recognition code (`IHandProvider` interface, QuestProvider and MediaPipeProvider shared).
- Try the simulator camera for 30 minutes first; only spend half a day on the bridge if it fails. Do not start with the bridge. The teaching version has even less time, so skip this path if at all possible.

### 10.3 A real device (if one can be borrowed / bought)

- Reserve **1 day** before the deadline for real-device threshold tuning (v1.0 reserved 2 days; the teaching version compresses this to 1 day). The 0.35 distance threshold and 0.6 s hold time tuned on the simulator will very likely need changing on a real device.
- Options: borrow from a colleague, borrow from a Toronto VR group, or buy a Quest 3S 128 GB (new ~$479.99 CAD, second-hand ~$250–$350 CAD), tune, then decide whether to keep it. Do not leave the first real-device run to the last day.
- If no device can be borrowed, do not block submission: the official FAQ is explicit that competing does not require owning a headset, and the video may use simulator footage. The APK must run on a real device in the judges' hands — but if it has never been run on a real device by the submitter, say so honestly in the submission form. Do not claim it was tested.

---

## 11. Non-goals (explicitly NOT in v2 — Claude must not add them)

- No TTS, no STT, no Deepgram, no dual modes, no ConfirmGate, no coffee-shop dialogue, no captions.
- No full ASL translation, no continuous signing, no sentence-level grammar, no full A–Z alphabet.
- No J / Z, no R / U, M / N / T.
- No face / eyebrow recognition; no Quest Pro-only features (face tracking may only be an optional enhancement, never a requirement).
- No multiplayer, no leaderboard, no cloud LLM, no Token Factory / Nemotron.
- No controller support, no standing / walking mode.
- No SPECS / Lens Studio version (the user has decided not to enter that competition — do not mention it again).
- No v1.0 two-way conversation (archived; it may be mentioned in Future Work, but do not implement it in v2).

---

## 12. Execution phases (Claude follows this order — total ~5.5 days)

| Phase | What to do | Acceptance (must be demonstrable) | Estimate |
|---|---|---|---|
| **P0 Project setup** | **Use the existing project** `D:\Unity\Projects\SignSightVR` (Unity 6000.3.28f1, Universal 3D / URP — already created, do NOT create a new one); install Android via CLI first (see instruction 8); switch to the Quest Profile; install Meta v207 + OpenXR 1.18 + XR Hands 1.9; OVRCameraRig + Passthrough running; Simulator can enter the scene | In the Simulator, see the passthrough scene + one simulated hand | 0.5 day |
| **P1 Hand tracking + Recorder + Recognizer** | CurlCalculator + TemplateStore + TemplateRecorder + SignRecognizer: record 8 templates, save as JSON, distance matching + 0.6 s hold + 1 s lockout + Top1/Top2 output + smoothing | In Simulator camera mode, record a COFFEE, then sign COFFEE and have it recognised; sign it 3 times in a row, all 3 recognised correctly, no duplicate triggers | 1.5 days |
| **P2 Teacher + lesson loop** | Import the Mixamo character (Standard Skeleton 65), TeacherAvatarDriver drives fingers from curl, LessonManager runs both lessons / 8 words, word card + Replay / Next buttons work | In the Simulator, watch the teacher sign COFFEE once — fingers interpolate from neutral to a C shape and hold for 1.5 s; walk through all 8 words in order | 1.5 days |
| **P3 Scoring + correction** | ScoreManager (+10 / +5 / streak / stars) + FeedbackPanel + GhostHand: per-finger red / yellow / green, concrete hint, Try Again with at most two attempts | Deliberately sign COFFEE wrong once: see the ghost hand, the index finger in red, the hint "straighten your index finger more", then sign it correctly and earn +5 | 1 day |
| **P4 Polish + video + submission** | Tune UI size / contrast, performance to 72 FPS, (if a real device is borrowed) tune thresholds once, record a <3-minute video, write the Devpost form, upload the APK to the Competition channel | Video includes one full wrong → correction → correct sequence; the form describes the hand interaction clearly; APK is in the Competition channel and the Invite URL opens | 1 day |

Total ~5.5 days, deadline 2026-11-18 15:00 ET. Do not let P0–P3 eat P4's video and submission time — the video must be recordable on the evening of November 17.

---

## 13. Acceptance checklist per phase (Claude ticks each item at the end of every phase)

### P0
- [ ] Unity 6000.3.28f1 + Meta v207 opens without errors
- [ ] Android Build Support is installed (CLI output saved; if the Hub install failed, the CLI error is reported as-is)
- [ ] Passthrough scene is visible in the Simulator
- [ ] Switched to Android + Quest Profile

### P1
- [ ] The recorder can record 8 templates; the JSON contains curl values
- [ ] After recording, signing is recognised as Top1
- [ ] Holding for 0.6 s triggers; holding for less than 0.6 s does not trigger
- [ ] No duplicate trigger inside the 1 s lockout
- [ ] Top2 is output, confidence is in 0–1

### P2
- [ ] The Mixamo character is imported; all finger bones are green in the Humanoid Avatar mapping
- [ ] The teacher can demonstrate all 8 signs from template curl, 0.5 s interpolate + 1.5 s hold
- [ ] Both lessons / 8 words can be walked through in order; Replay replays the demonstration
- [ ] Word card, progress and score display correctly

### P3
- [ ] Correct on the first try earns +10, on the second try +5, still wrong on the second try earns +0 and moves on
- [ ] 3 consecutive first-try correct answers earn an extra +5; stars follow §7
- [ ] On a wrong answer, the ghost hand appears, per-finger red / yellow / green is correct, and the worst finger has a concrete hint
- [ ] Try Again allows at most two attempts and never gets stuck

### P4
- [ ] Video is <3 minutes and includes one full wrong → correction → correct sequence
- [ ] Video labels which parts are simulator footage and which (if any) are real-device footage
- [ ] APK is in the Competition channel and the Invite URL opens
- [ ] Devpost form is submitted, screenshot saved
- [ ] (If never run on a real device) the form honestly states "not tested on a real device; thresholds were tuned on the simulator"

---

## 14. Submission form draft (Claude drafts it for the user — do not submit it directly)

- **Track:** Productivity
- **Division:** New Experience
- **One-liner:** Learn 8 useful ASL signs in MR — a 3D teacher shows the sign, you copy it, and per-finger feedback tells you exactly what to fix.
- **Hand-interaction description:** Hands-only, seated. Watch the teacher, copy the sign, hold 0.6s to lock, pinch Replay / Next. No controllers.
- **Special awards:** Best Accessibility Forward (primary), Best First Five Minutes, Best Reason to Come Back
- **Future Work:** More signs, A–Z alphabet, continuous signing, two-way conversation (the v1.0 direction, deferred), facial markers (needs Quest Pro or an external camera).

---

## 15. References: projects not to clone (Claude must not clone them)

- VRSignify (Unity Quest 3, sign → text, for learning): one-way recognition + text only, no teacher demonstration, no per-finger correction.
- XR-SignQuest (Quest 3, YOLOv8 learning): learning + confidence only, no ghost-hand comparison.
- ASL-Passthrough (Quest 2, spelling ICE to cast a spell): the pose-saving tool may be referenced, but do not copy the whole project.
- Sign Pose VR (shipped on the Quest Store, learning category): a shipped competitor — do not copy its lesson structure.
- Handset (browser + Twilio phone call): two-way loop is conceptually close, but it is not Quest MR, and it is the v1.0 direction, archived.
- Hand Wave (smart-glasses fingerspelling): fingerspelling only, one-way only, no screen for captions.

Use them for ideas only. Do not copy code directly. Cloning a whole project violates the New Experience originality requirement. Teaching apps are crowded, so the differentiator must be §8's per-finger feedback — do not ship a learning app that only says "correct / wrong".

---

## 16. Risks and honest disclosure (include in the submission form — do not hide them)

- The user does not own a Quest yet. Thresholds were tuned on the simulator + laptop camera and may need re-tuning on a real device. P4 reserves only 1 day for a real device; if none can be borrowed, submission is not blocked.
- Quest 3 / 3S has no face tracking. The teaching version needs no facial markers, and the 8 signs are static.
- The teacher model is a free Mixamo character — state its source clearly; it was not modelled by us. Do not use Ready Player Me — it has shut down.
- The 8 signs are static only. Continuous signing, J / Z, R / U, M / N / T and the full A–Z alphabet are not in v2.
- Teaching apps already have competitors. This project's differentiator is per-finger ghost-hand correction, not "another learn-the-alphabet app".

---

*SPEC version: v2.0 teaching version, 2026-11-12. Author: user + Muse. Executor: Claude. Deadline: 2026-11-18 15:00 ET.*
*File: `workspace/goals/meta-vr-start-sign-language-entry/files/signsight-learn-spec-en.md`. This is the only valid SPEC now. The Chinese SPEC is `signsight-learn-spec-zh.md` in the same folder. The v1.0 two-way conversation SPECs (`signsight-quest-spec-zh.md` / `signsight-quest-spec-en.md`) are archived and superseded — do not follow them. The SPECS / Lens Studio version is cancelled — do not mix them.*
