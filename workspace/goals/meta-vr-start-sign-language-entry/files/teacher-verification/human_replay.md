# Real human video replay through the recogniser (2026-11-14)

Goal: test the v3.1 movement detectors on real human signing before the live webcam test.
Not a substitute for it (see limits).

## Method
- 143 real human clips from WLASL (direct-link sources, trimmed to WLASL frame_start/end), up to 3 per word, 49 words
  (GOOD-MORNING has none). `fetch_calibration.py` with `CLIP_DIR=human_clips CLIPS_PER_WORD=3`.
- `export_hands.py`: MediaPipe Holistic world landmarks -> both hands' 21 joints in the recogniser's signer frame
  (origin between the eyes, x = signer's right, y = up, z = toward the viewer, metres). Hands placed at the pose
  wrists; gaps <= 6 frames filled; 11 left-handed signers mirrored. Median shoulder width 0.32 m (scale is plausible).
- `Assets/Editor/HumanReplayTest.cs`: joints -> XR Hands layout (same as the teacher self-test) -> TrajectoryTracker ->
  every scored template. **Fires like the app**: at the first completed movement of any sign + 0.5 s settle, judges only
  what was signed until then; correct = own sign Top-1, movement completed, total <= 0.35 (ScoreManager rule).
  Spec (device) values; `Logs`: `v31-human-replay.txt` (fire like app), `v31-human-replay-wholeclip.txt`.

## Results
- **Recognised (fire like the app): 20 / 137 scored clips (15 %).** Words with >= 1 clip recognised: 14 / 47.
- Movement detected anywhere in the clip (detector recall), by type:

| type | completed | | type | completed |
|---|---|---|---|---|
| circle | 9/12 | | snap (v3) | 3/3 |
| squeeze | 7/10 | | wrist-nod (v3) | 2/3 |
| turn | 8/12 | | flick-out (v3) | 2/3 |
| twist | 3/3 | | shake (v3) | 1/3 |
| rock | 3/3 | | lift (v3) | 1/3 |
| hold | 2/3 | | wave (v3) | 1/5 |
| converge | 3/5 | | crank (v3) | 0/3 |
| tap | 6/18 | | push-forward (v3) | 0/3 |
| stroke | 11/45 | | | |

## Detector fixes made from this test (v3.1 detectors only; v3 detectors untouched)
| Problem seen on real signers | Fix |
|---|---|
| HUNGRY's downward stroke fired inside circular signs (PLEASE, SORRY, HAPPY, HOW) | a stroke must be a roughly straight path (path <= 1.3 x straight-line travel; a half circle is 1.57) |
| strokes never started: real signers flow into the sign, they do not hold still | start = a pause (local speed minimum, < 0.6 m/s) at the anchor, not a full stop |
| taps never counted: contact lands 2-5 cm off the anchor point | a tap = the distance dips below 6 cm and lifts off by >= 1.5 cm (was: < 3 cm then > 4.5 cm) |
| DRINK / NEED / TIRED ("turn") fired while the hand was being raised into place | a turn must start from a pause at its anchor, with the palm moving < 10 cm |

After the fixes the teacher self-test is still **47 / 47** (all 8 v3 words included).

## Why most failures are the video, not the detectors (limits)
- **Depth:** the hand's position is right to 1-5 cm across the image but 8-20 cm off in depth (single-camera estimate,
  `HumanReplayTest.Trace`). Location and tap / stroke starts are 3D, so they fail. Quest hand tracking measures depth;
  the Simulator's webcam has the same weakness as video (the editor already down-weights depth for it).
- **v3 push-forward (THANK-YOU) needs 12 cm forward travel** and **crank (COFFEE) needs the bottom hand still within 4 cm**:
  video depth noise makes both fail. Not changed (v3).
- **Handshape:** MediaPipe finger curls from small dictionary videos are rough: handshape distance 0.2-0.35 even on
  correctly recognised clips (teacher: ~0.1).
- **Variants:** WLASL signers do not always use the Lifeprint variant (e.g. HELLO as a salute; one-handed vs two-handed),
  and clips are cut tightly, so the pause before a stroke is sometimes missing.
- So 15 % is a lower bound for this pipeline, not a prediction for Quest. The webcam test (Simulator) and a real
  headset remain the tests that count.
