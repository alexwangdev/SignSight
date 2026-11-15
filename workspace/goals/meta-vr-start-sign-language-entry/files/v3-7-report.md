# SignSight Learn v3.7 — Relax visual polish (2026-11-15)

Started from `43c27de` (v3.6). Visuals only: no threshold, timeout, step order, `Progress`, `Status`, `StepEndings`
or recognition logic changed in `RelaxMode.cs`. `RelaxMode.cs` only gained read-only accessors (`LastSample`,
`PianoFinger`, `ShakeCount`, `Holding`), and `MeanFingerCurl` was made public. No package, model, font, audio or
post-processing; still silent; passthrough on; no score, stars or Tutor in Relax; the safety line is on every Relax
screen. Checked in Editor Play Mode + Meta XR Simulator. Screenshots and logs: `v37-screens/`.

## Task 1 — one living object per step: `Assets/Scripts/Relax/RelaxVisual.cs` (one file)
- **What it reads:** only what RelaxMode already measures — the last sample (curls, spread, fingertip gaps), the piano
  target finger, the shake direction-change count, `Progress`. It adds no detection.
- **Placement:** left of the ghost hand at (-0.80, 0.12, 0.72), scale 1.4. Clear of the ghost hand, ring, MENU, word
  card and buttons.
- **Rendering:** each visual is **one procedural mesh** with vertex colours, drawn with a copy of the existing Confetti
  (URP Particles/Unlit) material, culling off.

| Step | Visual | Driven by |
|---|---|---|
| Open & Close | flower: 5 pink petals + yellow centre + stem; petals fan open / fold up | `1 - MeanFingerCurl(LastSample.Curl)` (smoothed) |
| Spread Wide | sun: 10 gold rays, slowly turning | ray length from `max(Spread, Progress)` |
| Finger Piano | 4 dots (index, middle, ring, pinky) | target finger (`PianoFinger`) amber with a pulsing glow; green once `TipGap[f] < PianoTouch` (the existing test) and for finished fingers; others grey |
| Shake It Out | a resting leaf + small leaves that drift off and fade (1.2 s) | one leaf per increase of `ShakeCount`, capped at 8 alive |
| Wiggle & Rest | the bubbles come to the front (4, brighter, faster) | looping |
| all steps | the same 4 bubbles drift slowly upward in the background | tint per step: OpenClose (0.62, 0.92, 0.62), Spread (1, 0.82, 0.45), Piano (0.6, 0.75, 1), Shake (0.75, 1, 0.72), Wiggle (0.72, 0.88, 1) |

**Draw calls:** 5 renderers in all (Flower, Sun, PianoDots, Leaves, Bubbles). At most **2 are visible at once** (the
step visual + the bubbles), so 2 draw calls. All are hidden on the end screen and outside Relax.

## Task 2 — the ghost hand
- **Colour:** the Relax ghost is warm (1.0, 0.9, 0.75, 0.6), semi-transparent. The Learn feedback ghost is unchanged.
- **Hold tint:**
  - While a hold counts (`_hold > 0`, Open & Close and Spread Wide), the involved fingers fade toward green
    (0.55, 0.95, 0.6, 0.6) with `GhostHand.SetFingerColor`. The share follows `_hold / hold seconds`, in 10 % steps.
  - The involved fingers are index–pinky, plus the thumb in Open & Close. Otherwise they stay warm.
  - No "wrong" colour.
- **Pop:** `SizeMultiplier` 0.8 -> 0.9 -> 0.8 over 0.4 s (`CelebrationFX.PopSeconds`). It runs on **every** `EndStep`
  (target or timeout — the no-fail "Nice — moving on") and on `Finish()`.
- **Sparkle:**
  - The existing `RelaxSparkle` (`Play(null, null, 0, anchor)`) now fires on each **target** ending and on `Finish()`,
    with a burst of **18** particles (was 12). Timeout endings get none.
  - Colours: OpenClose green (0.45, 0.9, 0.5), Spread gold (1, 0.82, 0.3), Piano blue (0.5, 0.7, 1), Shake green
    (0.6, 0.95, 0.55), Wiggle blue (0.6, 0.85, 1). Finish uses the Wiggle blue.
  - Wiggle & Rest always ends at its 8 s, which RelaxMode records as its target, so it sparkles.

## Task 3 — progress you can see (`LessonUI.RefreshRelax`)
- **Dots line:** under `Step n of 5` — green ● for finished steps, amber ● for the current one, blue ○ for the rest
  (step 3: ● ● ● ○ ○). These are **text glyphs U+25CF / U+25CB**; they render through the project font's fallback
  (checked with `HasCharacter` and in the screenshots), so no code-drawn dots were needed. The fallback atlas change
  this caused at runtime was reverted before commit.
- **Breathing line:** `Breathe in… out…`, small, as the second line of the Relax instruction. The why line and the
  safety line stay.
- **Hold ring:** not moved or resized.

## Task 4 — teacher present (done)
- **Smile:** `SetActive(true)`: after the existing `Rest()`, `teacher.Face.Expression(30, 0)`. The hands stay at rest.
  The face reached smile weight 54 in the check (FaceDriver scales it).
- **Back to neutral:** `SetActive(false)` calls `teacher.Face.Neutral()` (when no demonstration starts); the smile
  faded to 0.
- No conflict with `Rest()` was seen.

## Checks (0 errors)
Log: `v37-relax-visual-check.txt` (`Assets/Scripts/DevTools/V37Check.cs`, simulated measurements via
`RelaxMode.DebugSample` — not real hands).

| Check | Result |
|---|---|
| Teacher soft smile in Relax, hands at rest; neutral after leaving | PASS (54 -> 0) |
| Each step shows only its visual (+ bubbles); dots line 1 -> 5; breathing + safety lines | PASS for all 5 steps |
| Hold tint while Spread's hold counts | PASS |
| Target path: pop on 5 steps + Finish, sparkle on 5 target endings + Finish | PASS (6 / 6) |
| Timeout path: pop on each ending, sparkle only on Wiggle's 8 s end + Finish | PASS (6 pops / 2 sparkles) |
| End screen: step visuals hidden | first read FAIL — the check read the renderers in the same frame the state changed, before `LateUpdate`; re-read one frame later: PASS (logged as RECHECK) |
| MENU -> Start; classroom / café props hidden and unmoved | PASS |
| Unchanged `RelaxCheck` Task A (standalone, 18) + Task B (inter-lesson: RELAX -> DONE -> Lesson 2, MENU -> Start, last lesson -> Complete, 10) | PASS |
| Regression: Lesson 1 full run | PASS — 10 words, score 95, 0 errors (inside RelaxCheck B) |
| Regression: Explore apple -> APPLE -> demo -> Replay | PASS |
| Regression: teacher self-test | PASS — 54 / 54 |

**Screenshots:**
- `v37-relax-open.png` (bloomed flower), `v37-relax-spread.png` (sun), `v37-relax-piano.png` (index green, middle
  amber), `v37-relax-shake.png`, `v37-relax-wiggle.png`.
- `v37-relax-dots.png` (the piano screen, dots ● ● ● ○ ○).

**Not verified:** none of this was tried with real hands (webcam or Quest); the visuals follow simulated
measurements.
