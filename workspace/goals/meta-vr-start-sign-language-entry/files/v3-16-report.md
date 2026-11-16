# SignSight Learn v3.16 — elbow limits (the 15 words that did not look human)

Branch `v3.16-elbow-limits`, committed, **not merged**. Clamp OFF, self-test 54 / 54 (five runs). No template edited.
13 of the 15 reported words now look human in the close-ups. **BOY and APPLE are still wrong** (reason below).

## Verified: you were right, and it was one problem
I looked at the v3.15 close-ups you listed. In almost all of them the hand is fine and the **elbow is somewhere a
shoulder cannot put it**: behind the head (APPLE), above the head (BOY, YOU), behind the back (SORRY, WANT, HERE,
TODAY, HOW-MUCH, COLD), or the forearm comes out of the neck or chest (ME, GOOD, DOCTOR, HOT, THANK-YOU).

## Root cause
1. **The elbow search only minimised the wrist bend.** For each hand pose the driver tries 36 elbow positions on a
   circle and keeps the one where the wrist bends least. Nothing said where an elbow may be. An elbow behind the head
   often gives a perfectly straight wrist, so it won.
2. **My v3.15 keep / revert rule made it worse.** It kept the old arm for 37 words because their wrist bend number was
   lower. For most of them the number was lower *because* the elbow was in an impossible place. 12 of your 15 words
   were on that list. The elbow guard I added in v3.15 only ran on the other 25 words.
3. **THIRSTY was a different fault.** Its hand target (a stroke down from the throat) lies inside this avatar's
   chest, so the whole hand and forearm disappeared into the body.

## Fix (`TeacherAvatarDriver.cs`)
- **Elbow limits for every word, at every moment.** The elbow must stay on its own side (at least 0.11 m from the
  mid-line), outside the torso, at most 0.05 m above the shoulder joint and at most 0.12 m behind it. Breaking a
  limit costs 30° of wrist bend per centimetre, so the wrist bend is only compared between elbows a person could have.
- **One arm for all 62 words.** The per-word revert list is empty.
- **Wrist kept out of the torso.** A wrist target inside the chest is moved straight forward to the chest surface.
  Only two words move: THIRSTY 4.9 cm, LOVE 1.0 cm.

## Result for the 15 words (close-ups, judged by me)

| Word | Before (v3.15) | Now | Wrist bend before → now |
|---|---|---|---|
| SORRY | forearm from behind the back | PASS | 55.5° → 58.9° |
| GOOD | forearm out of the chest | PASS | 29.9° → 42.8° |
| THANK-YOU | upper arm hidden, forearm from the chest | PASS | 18.3° → 31.9° |
| HOT | elbow in front of the chest | PASS | 11.2° → 28.3° |
| COLD | elbows behind the back | PASS | 50.4° → 50.9° |
| DOCTOR | forearm from the neck | PASS | 82.6° → 86.5° |
| ME | forearm pointing down from the face | PASS | 128.5° → 129.4° |
| YOU | elbow above the head | PASS | 65.4° → 67.8° |
| WANT | no arms visible | PASS | 24.2° → 47.3° |
| HERE | arms behind the body | PASS | 4.2° → 23.6° |
| TODAY | arms behind the body | PASS | 11.2° → 27.6° |
| HOW-MUCH | arm behind the body | PASS | 29.4° → 50.5° |
| THIRSTY | hand inside the chest | PASS | 64.2° → 67.4° |
| APPLE | elbow behind the head | **still wrong**: forearm now comes up in front, but the wrist bends about 130° | 14.7° → 131.7° |
| BOY | elbow above the head | **still wrong**: elbow still above the head (0.12 m over the limit) | 37.7° → 127.7° |

The wrist bend went **up** for most of these. That is the honest trade: the old low numbers came from impossible
elbows. ME and DOCTOR still measure high but read as human in the picture; I would still check them yourself.

**Why BOY and APPLE cannot be fixed in the arm.** Their templates put the hand where no arm reaches it. BOY: fingers
on the forehead pointing forward, so the wrist is above the top of the head. APPLE: knuckle on the cheek with the
fingers pointing forward, so the wrist is behind the cheek, beside the ear. Both need a template change (finger
direction), like HOW in v3.14. I did not edit templates.

## Other words I checked (all 62 hold close-ups, 11 sheets)
- No other word shows an elbow behind the head or back, or a hand inside the body.
- Borderline: **EAT** (forearm crosses in front of the face; the template points the fingers at the mouth from the
  front), **SICK** (hand covers half the face, as the sign does), **BREAD**, **WHEN**.
- 36 of 62 words changed by more than 0.05° at the hold.

## Pictures
- Hold, one per word: `v316-screens/all-words-hold/v316-<WORD>-hold-closeup.png`
- Review sheets, 6 words each: `v316-screens/all-words-hold/sheets/` (`index.txt` names the words on each sheet)
- Move / mid / hold strip, one per word: `v316-screens/travel/strips/v316-<WORD>-strip.png`
  (I reviewed the strips for SORRY, ME, WANT, YOU, THIRSTY and DOCTOR; the other 56 strips are saved, not reviewed.)
- Numbers: `v316-screens/v316-measure.txt` (bend, elbow position and limit violation per word)

## Verification

| Check | Result |
|---|---|
| Self-test, clamp OFF, from rest, five runs | 54 / 54 each (`v316-selftest-five-runs.txt`) |
| Elbow inside its limits at the hold | 60 of 62 words (BOY 0.12 m over, APPLE 0.01 m over) |
| Exam happy path, Start card, MENU | 19 / 19 PASS, 70 / 70, 0 errors |
| Lesson 1 full run | PASS: 10 words, score 95, 0 errors |
| Lesson 6 full run | PASS: 12 words, score 80, 0 errors |
| Explore, Relax A + B | PASS, 0 errors |
| Fade check | PASS (classroom at alpha 0.22, café at 0.98) |

## Words the video must not show
**BOY, APPLE.** Check EAT yourself before using it.

## Not done
- Not merged.
- BOY and APPLE template fix (needs your OK: it is a sign-definition change).
- Real-headset passthrough toggle: UNVALIDATED (no Quest owned).
- The motion trace (velocity jumps) was not re-run for this change.
