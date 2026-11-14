# Teacher verification — 50 words (SPEC v3.1 §3)

Date: 2026-11-14. Dev machine only; nothing here ships in the app (no model weights, no network at runtime).

## What was run

| Step | What | Result |
|---|---|---|
| Coverage | `check_coverage.py` on `WLASL_v0.3.json` | asl100 18/50, asl300 36/50, **asl1000 49/50**, asl2000 49/50 (`coverage_result_2026-11-14.txt`). GOOD-MORNING is in no WLASL subset: model cannot judge it. |
| Teacher videos | `Assets/Scripts/DevTools/TeacherRecorder.cs` (Editor Play Mode only), front camera, plain grey background, 512×512, **30 fps, teacher speed 1x**, each clip = rest 0.3 s + full demonstration (to start, movement, hold, return) + 0.2 s; 75–131 frames | 50 clips. MediaPipe found hands in ≥ 0.93 of frames for 48/50 clips (TIRED 0.36 and HOW 0.61: hands seen edge-on). 1x was kept because hands were not missed on fast movements. |
| Model | `tgcn_infer.py`: MediaPipe Holistic (tasks API 1.2.3) → 55 keypoints in the WLASL Pose-TGCN layout (13 upper-body + 2 × 21 hands, 256-px frame) → 50 frames → **TGCN asl1000** (`sharonn18/tgcn-wlasl`, `checkpoints/asl1000`). Judged on Top-5. | Teacher: expected sign in Top-5 for **11 / 49** covered words (`results_asl1000_full.json`). |
| Calibration | `fetch_calibration.py`: one real human WLASL clip per word (direct-link sources, trimmed to WLASL frame_start/end), same pipeline | Model gets the human right (Top-5) for **26 / 41** calibrated words (`calib_results_asl1000.json`). |
| SignRep | Searched GitHub / Hugging Face / arXiv for 30 min | **SignRep: not runnable in 30 min — limitation, not a pass.** The official repo `ryanwongsa/SignRep` publishes only a self-supervised feature extractor (`ckpt.pt`, release v0.0.1, CC BY-NC-SA 4.0). The ASL Citizen classifier behind the paper's Top-1 / Top-5 numbers is not released; training one needs the ASL Citizen dataset. No SignRep result is claimed. |

Pipeline notes (found while calibrating, before any teacher video was scored):
- The model card says "55 MediaPipe pose keypoints", "Top-1 > 85 %" and has template placeholders ("Your Name"); these claims are unverified. The checkpoint shapes and `config.ini` match the original WLASL Pose-TGCN, so the WLASL keypoint layout (`code/TGCN/sign_dataset.py`) was used.
- With missing hands written as OpenPose zeros the model collapsed to a few classes on real humans (0/10). Filling a missed hand from the nearest frame where it was seen (OpenPose always outputs both hands) fixed this: 6/10 on the first calibration set. All results below use that setting (`TGCN_FILL=1`, centred square crop).

## Verdicts

Rules (SPEC v3.1 §3.3): PASS = expected sign in Top-5 and template matches Lifeprint. REVIEW = not in Top-5 but the template matches Lifeprint; calibrated on a real human clip: if the model also misses the human → *model-unreliable-for-this-word*. FAIL = not in Top-5 and the template differs from Lifeprint. **Lifeprint is the ground truth; no template was changed only because the model disagreed.**

| Verdict | Count | Words |
|---|---|---|
| PASS | 11 | NO, BAD, FATHER (as WLASL "dad"), WATER, HUNGRY, SAD, SICK, LOVE, WANT, NEED, WHAT |
| REVIEW — model-unreliable-for-this-word (misses the real human too) | 13 | YES, GOODBYE, BABY, WOMAN, BOY, GIRL, MILK, EAT, DRINK, APPLE, PAIN, WHO, WHY |
| REVIEW — model gets the human, not the teacher; Lifeprint matches → needs a human check of the demo | 22 | HELLO, THANK-YOU, PLEASE, SORRY, GOOD, MOTHER, MAN, DOCTOR, BREAD, THIRSTY, COFFEE, HOT, COLD, HELP, HAPPY, TIRED, LIKE, WHERE, WHEN, HOW, TOMORROW, GO |
| FAIL (template simplified vs Lifeprint) | 3 | FRIEND (scored), TEACHER, TODAY (both TEACH-ONLY compounds) |
| N/A — not in asl1000 | 1 | GOOD-MORNING (TEACH-ONLY) |

### FAIL words — what was done
- **FRIEND**: Lifeprint links the hooked index fingers, swaps which hand is on top, and links again. The teacher links twice without the swap. A swap was implemented, but on the Rocketbox rig it pulled the hooked fingers apart and broke the self-test, so it was reverted. **Not fixed** — needs rig work or a human-made animation; listed for human review.
- **PAIN** (found in the same Lifeprint pass, verdict REVIEW): the teacher's jab had no twist; Lifeprint says the index fingers twist as they come together. **Fixed** (`index-jab-twist`), re-recorded, re-run: still not in Top-5 — and the model also misses the real human PAIN clip, so the model is unreliable for it.
- **TEACHER, TODAY** (and GOOD-MORNING): compounds, simplified second beat. They are TEACH-ONLY (not scored) for that reason. Not fixed.

### What the calibration says about the model check itself
- The model recognised the real human for 26 words, but the teacher for only **1** of those 26 (WATER). The other 10 PASS words are ones without a human clip, or where the model also misses humans (NO, WHAT).
- A model error on 25 separate templates whose definitions all match Lifeprint is less likely than a **systematic gap between the avatar video and the human videos the model was trained on** (rendered low-poly avatar, idealised constant-speed motion, no body sway, clip timing).
- Sensitivity check: trimming the rest and return frames (`results_asl1000_trimmed.json`) gives 13/49 instead of 11/49, with a different set. Not a fix.
- **Conclusion:** with Pose-TGCN asl1000 the model check is weak evidence either way. It confirmed 11 words, flagged no template that Lifeprint contradicts, and it cannot replace a human check. The REVIEW list is the list a Deaf signer / ASL teacher should look at first.

### Words the model cannot judge (scored: false because of the model?)
None. `scored` was decided by the recogniser (teacher self-test, below), not by this model. TEACH-ONLY words are GOOD-MORNING, TEACHER, TODAY (compounds). GOOD-MORNING is also not in WLASL.

## Recogniser self-test (separate from the model check)

`Assets/Editor/SignSelfTest.cs` — the teacher performs each sign frame by frame, its own hands go through the recogniser, and every scored template is evaluated: **47 / 47 scored words are Top-1 and complete their movement** (`v31-selftest.txt`); the 3 TEACH-ONLY compounds are not candidates. This shows the teacher, the definitions and the recogniser agree; it is not a test with real learners' hands.

## Full table

Per-word Top-5 lists, human-clip Top-5 and hand-detection rates: `teacher_verification_50.csv`.

| word | scored | in asl1000 | model Top-1 | expected in Top-5 | Lifeprint match | human clip Top-5 hit | verdict |
|---|---|---|---|---|---|---|---|
| HELLO | Y | Y | cat | N | Y | Y | REVIEW |
| YES | Y | Y | off | N | Y | N | REVIEW |
| NO | Y | Y | off | Y | Y | N | PASS |
| THANK-YOU | Y | Y | color | N | Y | Y | REVIEW |
| PLEASE | Y | Y | color | N | Y | Y | REVIEW |
| SORRY | Y | Y | pull | N | Y | Y | REVIEW |
| GOOD | Y | Y | cheap | N | Y | Y | REVIEW |
| BAD | Y | Y | pull | Y | Y | - | PASS |
| GOODBYE | Y | Y | cat | N | Y | N | REVIEW |
| GOOD-MORNING | N (TEACH-ONLY) | N | birthday | N | PARTIAL | - | N/A |
| MOTHER | Y | Y | wine | N | Y | Y | REVIEW |
| FATHER | Y | Y | lettuce | Y | Y | - | PASS |
| FRIEND | Y | Y | necklace | N | PARTIAL | Y | FAIL |
| BABY | Y | Y | bring | N | Y | N | REVIEW |
| MAN | Y | Y | cafeteria | N | Y | Y | REVIEW |
| WOMAN | Y | Y | cafeteria | N | Y | N | REVIEW |
| BOY | Y | Y | dumb | N | Y | N | REVIEW |
| GIRL | Y | Y | carrot | N | Y | N | REVIEW |
| TEACHER | N (TEACH-ONLY) | Y | together | N | PARTIAL | Y | FAIL |
| DOCTOR | Y | Y | spider | N | Y | Y | REVIEW |
| WATER | Y | Y | cafeteria | Y | Y | Y | PASS |
| MILK | Y | Y | dog | N | Y | N | REVIEW |
| EAT | Y | Y | silly | N | Y | N | REVIEW |
| DRINK | Y | Y | silly | N | Y | N | REVIEW |
| BREAD | Y | Y | turtle | N | Y | Y | REVIEW |
| APPLE | Y | Y | chicken | N | Y | N | REVIEW |
| HUNGRY | Y | Y | complain | Y | Y | - | PASS |
| THIRSTY | Y | Y | secretary | N | Y | Y | REVIEW |
| COFFEE | Y | Y | surgery | N | Y | Y | REVIEW |
| HOT | Y | Y | light | N | Y | Y | REVIEW |
| COLD | Y | Y | move | N | Y | Y | REVIEW |
| HELP | Y | Y | change | N | Y | Y | REVIEW |
| HAPPY | Y | Y | shirt | N | Y | Y | REVIEW |
| SAD | Y | Y | beard | Y | Y | - | PASS |
| TIRED | Y | Y | more | N | Y | Y | REVIEW |
| SICK | Y | Y | stink | Y | Y | - | PASS |
| PAIN | Y | Y | retire | N | Y | N | REVIEW |
| LOVE | Y | Y | rest | Y | Y | - | PASS |
| LIKE | Y | Y | pull | N | Y | Y | REVIEW |
| WANT | Y | Y | want | Y | Y | - | PASS |
| NEED | Y | Y | need | Y | Y | - | PASS |
| WHAT | Y | Y | what | Y | Y | N | PASS |
| WHERE | Y | Y | eight | N | Y | Y | REVIEW |
| WHEN | Y | Y | dumb | N | Y | Y | REVIEW |
| WHO | Y | Y | snake | N | Y | N | REVIEW |
| WHY | Y | Y | warm | N | Y | N | REVIEW |
| HOW | Y | Y | doctor | N | Y | Y | REVIEW |
| TODAY | N (TEACH-ONLY) | Y | meet | N | PARTIAL | Y | FAIL |
| TOMORROW | Y | Y | bar | N | Y | Y | REVIEW |
| GO | Y | Y | ready | N | Y | Y | REVIEW |

## Reproduce (dev machine; large and third-party files are git-ignored)

```
py -3.13 -m venv verify-venv ; verify-venv\Scripts\pip install mediapipe opencv-python numpy torch --index-url https://download.pytorch.org/whl/cpu --extra-index-url https://pypi.org/simple
data/WLASL_v0.3.json         <- https://raw.githubusercontent.com/dxli94/WLASL/master/start_kit/WLASL_v0.3.json
third_party/wlasl_tgcn/*.py  <- https://github.com/dxli94/WLASL/tree/master/code/TGCN (tgcn_model.py, configs.py, sign_dataset.py, utils.py)
models/tgcn/asl1000/         <- https://huggingface.co/sharonn18/tgcn-wlasl/tree/main/checkpoints/asl1000
models/holistic_landmarker.task <- https://storage.googleapis.com/mediapipe-models/holistic_landmarker/holistic_landmarker/float16/latest/holistic_landmarker.task
Unity Play Mode: SignSight.DevTools.TeacherRecorder.Begin(<all intents>, 1f)   -> teacher_frames/
python check_coverage.py data/WLASL_v0.3.json
python run_teacher_verification.py --out results_asl1000_full.json   (TGCN_FRAME=crop, TGCN_FILL=1 are the defaults used)
python fetch_calibration.py <WORDS> ; python tgcn_infer.py --json calib_results_asl1000.json calib/*.mp4
python build_report.py
```
