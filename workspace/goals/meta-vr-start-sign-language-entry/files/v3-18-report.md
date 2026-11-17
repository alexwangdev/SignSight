# SignSight Learn v3.18 — BOY travel (move to start)

Branch `v3.18-travel-arc` (on top of v3.16 and v3.17), committed, **not merged**. Clamp OFF, self-test 54 / 54 (five runs).

## What was wrong
`v317-BOY-move-closeup.png`: on the way from rest to the forehead, the elbow swung far out to the side and the arm
stuck out horizontally. Measured: elbow 0.42–0.45 m out from the mid-line (the shoulder joint is at 0.21).

## Root cause (three parts, found by tracing the elbow every frame of the 0.4 s travel)
1. **The wrist travelled in a straight line** from the hanging hand to the forehead. That line passes right beside the
   shoulder, so half-way the arm had to fold up tight.
2. **The elbow used the hold rule during the travel.** For a hand at head height that rule points the elbow out and
   back; the tight "not behind the shoulder" limit then pushed it further out sideways.
3. **The elbow followed too slowly.** Its damping (8 / s) is right for a hold but too slow for a 0.4 s travel: the
   elbow was still on its way out when the hand was already near the head.

## Fix (`TeacherAvatarDriver.cs`, travel only; holds and the movement itself are unchanged)
- The wrist keeps at least 65 % of its reach from the shoulder, so it travels on an arc in front of the body
  (`travelReachShare` 0.65).
- During the travel the elbow hangs under the shoulder (down, a little out and back) and may go up to 0.24 m behind
  the shoulder; the hold rule takes over as the hand arrives (`travelPoleWeight` 1, `travelElbowBehindShoulder` 0.24).
- Elbow damping during the travel: 40 / s (`travelPoleDamping`); 8 / s at the hold, as before.

Result: BOY's elbow now stays at 0.24–0.32 m from the mid-line during the travel (was up to 0.45 m). Per-frame elbow
traces for BOY, HELLO, THANK-YOU, COFFEE, FATHER and SORRY: `v318-screens/v318-elbow-trace.txt`.

## Pictures
- BOY: `v318-screens/v318-BOY-move-closeup.png`, `-mid-`, `-hold-` (the frame you flagged).
- Mid-travel frame of **all 62 words**, 12 per sheet: `v318-screens/move-sheets/` (`index.txt` names the words).
- Move / mid / hold strip per word: `v318-screens/strips/v318-<WORD>-strip.png`.

I reviewed all five move sheets. BOY now raises the forearm from the side with the elbow down. No word shows an arm
sticking out sideways or an elbow behind the head. A few hands still look stiff mid-travel (fingers hanging, as a
relaxed hand does); I did not change that.

## Verification

| Check | Result |
|---|---|
| Self-test, clamp OFF, from rest, five runs | 54 / 54 each |
| Exam happy path, Start card, MENU | 19 / 19 PASS, 70 / 70, 0 errors |
| Lesson 1 / Lesson 6 full runs | 95, 0 errors / 80, 0 errors |
| Explore, Relax A + B | PASS, 0 errors |
| Fade check | PASS (classroom at alpha 0.21, café at 0.98) |

## Not done
- Not merged (v3.16, v3.17 and v3.18 are all on this branch line).
- Real-headset passthrough toggle: UNVALIDATED (no Quest owned).
