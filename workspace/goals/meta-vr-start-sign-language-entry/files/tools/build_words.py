"""
Builds the v3.1 word data from one table (SPEC v3.1 task A):
  Assets/Resources/templates.json        (v2 schema, 50 entries, scored flag)
  Assets/StreamingAssets/lessons.json    (5 lessons x 10 words)
  Assets/StreamingAssets/word_sources.json (Lifeprint source per word)

The 8 v3 words are copied unchanged from the current templates.json. Run from the repo root:
  py -3 -I workspace/goals/meta-vr-start-sign-language-entry/files/tools/build_words.py
"""
import json
import os
import sys

ROOT = os.getcwd()
TPL = os.path.join(ROOT, 'Assets', 'Resources', 'templates.json')
LESSONS = os.path.join(ROOT, 'Assets', 'StreamingAssets', 'lessons.json')
SOURCES = os.path.join(ROOT, 'Assets', 'StreamingAssets', 'word_sources.json')
LP = 'https://www.lifeprint.com/asl101/pages-signs/'

# ---- the 50-word list (SPEC v3.1 section 2.1, exact order) ----
LESSON_TITLES = ['Lesson 1 · Greetings', 'Lesson 2 · People', 'Lesson 3 · Food & drink',
                 'Lesson 4 · Feelings & health', 'Lesson 5 · Questions & time']
WORDS = [
    'HELLO', 'YES', 'NO', 'THANK-YOU', 'PLEASE', 'SORRY', 'GOOD', 'BAD', 'GOODBYE', 'GOOD-MORNING',
    'MOTHER', 'FATHER', 'FRIEND', 'BABY', 'MAN', 'WOMAN', 'BOY', 'GIRL', 'TEACHER', 'DOCTOR',
    'WATER', 'MILK', 'EAT', 'DRINK', 'BREAD', 'APPLE', 'HUNGRY', 'THIRSTY', 'COFFEE', 'HOT',
    'COLD', 'HELP', 'HAPPY', 'SAD', 'TIRED', 'SICK', 'PAIN', 'LOVE', 'LIKE', 'WANT',
    'NEED', 'WHAT', 'WHERE', 'WHEN', 'WHO', 'WHY', 'HOW', 'TODAY', 'TOMORROW', 'GO',
]
assert len(WORDS) == 50, len(WORDS)
assert len(set(WORDS)) == 50
V3 = ['HELLO', 'YES', 'NO', 'THANK-YOU', 'COFFEE', 'HOT', 'COLD', 'HELP']

# ---- handshapes: curl [thumb, index, middle, ring, pinky], 0 straight .. 1 curled; spread ----
H = {
    '5': ([0.0, 0.0, 0.0, 0.0, 0.0], 0.8),
    'B': ([0.25, 0.0, 0.0, 0.0, 0.0], 0.1),
    'A': ([0.2, 1.0, 1.0, 1.0, 1.0], 0.0),
    'openA': ([0.0, 1.0, 1.0, 1.0, 1.0], 0.0),
    'S': ([1.0, 1.0, 1.0, 1.0, 1.0], 0.0),
    'X': ([0.8, 0.55, 1.0, 1.0, 1.0], 0.0),
    'C': ([0.35, 0.45, 0.45, 0.45, 0.45], 0.1),
    'flatO': ([0.55, 0.45, 0.45, 0.45, 0.45], 0.0),
    'W': ([0.9, 0.0, 0.0, 0.0, 1.0], 0.6),
    'Y': ([0.0, 1.0, 1.0, 1.0, 0.0], 0.6),
    '1': ([0.9, 0.0, 1.0, 1.0, 1.0], 0.0),
    'L': ([0.0, 0.0, 1.0, 1.0, 1.0], 0.3),
    'bent': ([0.3, 0.4, 0.4, 0.4, 0.4], 0.0),
    'claw': ([0.6, 0.6, 0.6, 0.6, 0.6], 0.7),
    'open8': ([0.1, 0.0, 0.45, 0.0, 0.0], 0.6),
    '8': ([0.5, 0.0, 0.75, 0.0, 0.0], 0.6),
    'mid5': ([0.1, 0.0, 0.55, 0.0, 0.0], 0.6),
    'hook': ([0.9, 0.5, 1.0, 1.0, 1.0], 0.0),
}
P = {  # palm normal, signer space
    'Forward': (0, 0, 1), 'TowardSelf': (0, 0, -1), 'Down': (0, -1, 0), 'Up': (0, 1, 0), 'Sideways': (-1, 0, 0),
}


def v(x, y, z):
    return {'x': x, 'y': y, 'z': z}


def mv(type_, axis='', amp=0, unit='', repeats=1, dur=1.0, moving=''):
    return {'type': type_, 'axis': axis, 'amplitude': amp, 'unit': unit, 'repeats': repeats, 'durationSec': dur,
            'movingHand': moving}


def W(intent, phrase, hs, palm, anchor, contact, finger, movement, *, hands='right', end=None, left_hs=None,
      left_anchor='', left_contact='palm', left_palm=None, left_finger=None, offset=(0, 0, 0), left_offset=(0, 0, 0),
      direction=(0, 0, 0), radius=0.08, non_manual='', face=(0, 0), head=('none', 0, 0), scored=True, reason='',
      beat2=None, start_text='', end_palm=None):
    curl, spread = H[hs]
    t = {
        'intent': intent, 'phrase': phrase, 'hands': hands,
        'curlRight': curl, 'curlLeft': H[left_hs][0] if left_hs else ([] if hands == 'right' else curl),
        'spread': spread, 'palm': v(*P[palm]), 'palmLabel': palm,
        'startLocation': start_text,
        'movement': movement,
        'endCurlRight': H[end][0] if end else [],
        'head': {'type': head[0], 'amplitudeDeg': head[1], 'repeats': head[2]},
        'face': {'smile': face[0], 'jawOpen': face[1]},
        'tolerance': 0.35, 'samples': [],
        'scored': scored, 'teachOnlyReason': reason,
        'anchor': anchor, 'contact': contact, 'anchorRadius': radius, 'fingerDir': v(*finger),
        'leftAnchor': left_anchor, 'leftContact': left_contact,
        'anchorOffset': v(*offset), 'leftOffset': v(*left_offset),
        'leftPalm': v(*P[left_palm]) if left_palm else v(0, 0, 0),
        'leftFingerDir': v(*left_finger) if left_finger else v(0, 0, 0),
        'direction': v(*direction), 'endPalm': v(*P[end_palm]) if end_palm else v(0, 0, 0), 'nonManual': non_manual,
        'beat2': beat2 or {'anchor': ''},
    }
    return t


def beat(anchor, contact, hs, palm, finger, movement, *, direction=(0, 0, 0), left_anchor='', left_palm=None,
         left_finger=None, left_hs=None):
    return {'anchor': anchor, 'contact': contact, 'palm': v(*P[palm]), 'fingerDir': v(*finger),
            'curlRight': H[hs][0], 'curlLeft': H[left_hs][0] if left_hs else [], 'leftAnchor': left_anchor,
            'leftPalm': v(*P[left_palm]) if left_palm else v(0, 0, 0),
            'leftFingerDir': v(*left_finger) if left_finger else v(0, 0, 0),
            'movement': movement, 'direction': v(*direction)}


WH = 'Eyebrows: furrowed — watch the teacher (not scored)'
COMPOUND = 'Compound sign (two beats with a handshape and location change); the recogniser judges one movement.'

NEW = [
    W('PLEASE', 'Please', 'B', 'TowardSelf', 'chest', 'palm', (-0.3, 1, 0), mv('circle', 'frontal', 5, 'cm', 2, 1.3),
      start_text='flat hand on the centre of the chest'),
    W('SORRY', 'Sorry', 'A', 'TowardSelf', 'chest', 'palm', (0, 1, 0), mv('circle', 'frontal', 4, 'cm', 2, 1.3),
      start_text='A-hand on the chest'),
    W('GOOD', 'Good', 'B', 'TowardSelf', 'mouth', 'fingers', (0, 1, 0), mv('stroke', 'forward-down', 22, 'cm', 1, 0.8, 'right'),
      hands='both', left_hs='B', left_anchor='chest', left_offset=(-0.02, -0.06, 0.20), left_palm='Up', left_finger=(0.4, 0, 1),
      direction=(0, -0.55, 0.85), end_palm='Up', start_text='fingers at the lips, then into the left palm, both palms up'),
    W('BAD', 'Bad', 'B', 'TowardSelf', 'mouth', 'fingers', (0, 1, 0), mv('stroke', 'down-away', 15, 'cm', 1, 0.6),
      direction=(0, -0.8, 0.6), end_palm='Down', start_text='fingers at the lips, then down and away, ending palm down'),
    W('GOODBYE', 'Goodbye', 'B', 'Forward', 'neutral', 'palm', (0, 1, 0), mv('squeeze', 'four', 50, 'pct', 2, 1.0),
      end='bent', offset=(0.12, 0.20, -0.05), radius=0.12, start_text='open hand up at shoulder height, palm forward'),
    W('GOOD-MORNING', 'Good morning', 'B', 'TowardSelf', 'mouth', 'fingers', (0, 1, 0),
      mv('stroke', 'forward-down', 18, 'cm', 1, 0.7, 'right'), direction=(0, -0.55, 0.85), scored=False, reason=COMPOUND,
      beat2=beat('chest', 'palm', 'B', 'TowardSelf', (0, 1, 0), mv('turn', 'pitch-back', 70, 'deg', 1, 0.9),
                 left_anchor='chest', left_palm='Down', left_finger=(1, 0, 0), left_hs='B'),
      start_text='GOOD (fingers at the lips, forward) then MORNING (forearm rises like the sun)'),

    W('MOTHER', 'Mother', '5', 'Sideways', 'chin', 'thumb', (0, 1, 0), mv('tap', 'toward-chin', 5, 'cm', 2, 0.8),
      radius=0.06, start_text='thumb of the 5-hand on the chin'),
    W('FATHER', 'Father', '5', 'Sideways', 'forehead', 'thumb', (0, 1, 0), mv('tap', 'toward-forehead', 5, 'cm', 2, 0.8),
      radius=0.06, start_text='thumb of the 5-hand on the forehead'),
    W('FRIEND', 'Friend', 'hook', 'Down', 'neutral', 'index', (-1, 0, 0.2), mv('converge', 'index-to-index', 12, 'cm', 2, 1.2),
      hands='both', left_hs='hook', left_anchor='mirror', left_contact='index', left_palm='Up', left_finger=(1, 0, 0.2),
      offset=(-0.05, 0.0, 0.0), direction=(-1, 0, 0), radius=0.12, start_text='hooked index fingers interlock in front of the chest'),
    W('BABY', 'Baby', 'B', 'Up', 'chest', 'palm', (-1, 0, 0.3), mv('rock', 'side-to-side', 10, 'cm', 2, 1.6, 'both'),
      hands='both', left_hs='B', left_anchor='mirror', offset=(0.06, -0.10, 0.18), radius=0.12,
      start_text='arms cradled in front of the body, palms up'),
    W('MAN', 'Man', '5', 'Sideways', 'forehead', 'thumb', (0, 1, 0), mv('stroke', 'down', 28, 'cm', 1, 0.9),
      direction=(0, -1, 0.15), start_text='thumb of the 5-hand at the forehead, then down to the chest'),
    W('WOMAN', 'Woman', '5', 'Sideways', 'chin', 'thumb', (0, 1, 0), mv('stroke', 'down', 20, 'cm', 1, 0.8),
      direction=(0, -1, 0.15), start_text='thumb of the 5-hand at the chin, then down to the chest'),
    W('BOY', 'Boy', 'B', 'Down', 'forehead', 'fingers', (0, 0, 1), mv('squeeze', 'four', 45, 'pct', 2, 1.0),
      end='flatO', offset=(0.03, 0.02, 0.04), start_text='hand at the forehead grabbing an imaginary cap brim'),
    W('GIRL', 'Girl', 'A', 'Sideways', 'jaw', 'thumb', (0, 1, 0), mv('stroke', 'forward-down', 6, 'cm', 1, 0.6),
      direction=(0, -0.6, 0.8), radius=0.06, start_text='thumb tip on the cheek / jaw line'),
    W('TEACHER', 'Teacher', 'flatO', 'Forward', 'temple', 'fingers', (0, 1, 0.3),
      mv('stroke', 'forward', 15, 'cm', 1, 0.6, 'both'), hands='both', left_hs='flatO', left_anchor='mirror',
      direction=(0, 0, 1), scored=False, reason=COMPOUND,
      beat2=beat('upper-chest', 'palm', 'B', 'Sideways', (0, 0, 1), mv('stroke', 'down', 20, 'cm', 1, 0.6, 'both'),
                 direction=(0, -1, 0), left_anchor='mirror', left_hs='B'),
      start_text='TEACH (both hands from the head forward) then PERSON (flat hands down)'),
    W('DOCTOR', 'Doctor', 'bent', 'Down', 'left-wrist', 'fingers', (0, -0.3, 1), mv('tap', 'down-onto-wrist', 5, 'cm', 2, 0.8),
      hands='both', left_hs='B', left_anchor='chest', left_offset=(-0.04, -0.06, 0.20), left_palm='Up', left_finger=(0.6, 0, 1),
      direction=(0, 1, 0), radius=0.06, start_text='fingertips on the inside of the upturned left wrist'),

    W('WATER', 'Water', 'W', 'Sideways', 'mouth', 'index', (0, 1, 0), mv('tap', 'toward-mouth', 5, 'cm', 2, 0.8),
      radius=0.06, start_text='index finger of the W-hand at the mouth / chin'),
    W('MILK', 'Milk', 'C', 'Sideways', 'neutral', 'palm', (0, 0.4, 1), mv('squeeze', 'all', 55, 'pct', 2, 1.0),
      end='S', offset=(0.04, 0.0, -0.04), radius=0.14, start_text='C-hand in front of the body'),
    W('EAT', 'Eat', 'flatO', 'Down', 'mouth', 'fingers', (0, 0.3, -1), mv('tap', 'toward-mouth', 5, 'cm', 2, 0.8),
      radius=0.06, start_text='fingertips of the flat-O at the mouth'),
    W('DRINK', 'Drink', 'C', 'Sideways', 'chin', 'thumb', (0, 0.2, 1), mv('turn', 'pitch-back', 50, 'deg', 1, 0.8),
      radius=0.06, start_text='C-hand like a cup, thumb at the chin, then tilt toward the mouth'),
    W('BREAD', 'Bread', 'bent', 'Down', 'left-back', 'fingers', (0, -0.3, -1), mv('stroke', 'down', 7, 'cm', 2, 1.0),
      hands='both', left_hs='B', left_anchor='chest', left_offset=(-0.02, -0.02, 0.20), left_palm='TowardSelf',
      left_finger=(1, 0, 0), direction=(0, -1, 0), radius=0.06,
      start_text='left hand is the loaf (palm toward you); right fingertips slice down its back'),
    W('APPLE', 'Apple', 'X', 'Down', 'cheek', 'knuckle', (0, 0.2, 1), mv('twist', 'wrist-roll', 30, 'deg', 2, 1.0),
      radius=0.06, start_text='index knuckle of the X-hand on the cheek'),
    W('HUNGRY', 'Hungry', 'C', 'TowardSelf', 'upper-chest', 'fingers', (-1, 0.2, 0), mv('stroke', 'down', 15, 'cm', 1, 0.8),
      direction=(0, -1, 0), start_text='C-hand at the top of the chest, then down the middle of the chest'),
    W('THIRSTY', 'Thirsty', '1', 'TowardSelf', 'throat', 'index', (0, 1, 0), mv('stroke', 'down', 10, 'cm', 1, 0.7),
      direction=(0, -1, 0), radius=0.06, start_text='index fingertip at the throat, then down'),

    W('HAPPY', 'Happy', 'B', 'TowardSelf', 'chest', 'palm', (-1, 0.3, 0), mv('circle', 'sagittal', 5, 'cm', 2, 1.3),
      start_text='flat hand brushing up the chest in forward circles'),
    W('SAD', 'Sad', '5', 'TowardSelf', 'face', 'palm', (0, 1, 0), mv('stroke', 'down', 18, 'cm', 1, 1.0, 'both'),
      hands='both', left_hs='5', left_anchor='mirror', offset=(0.05, 0.0, 0.0), direction=(0, -1, 0),
      non_manual='Face: sad — watch the teacher (not scored)',
      start_text='both 5-hands in front of the face, palms in, then down'),
    W('TIRED', 'Tired', 'bent', 'Down', 'upper-chest', 'fingers', (0, 0, -1), mv('turn', 'pitch', 40, 'deg', 1, 0.9, 'both'),
      hands='both', left_hs='bent', left_anchor='mirror', offset=(0.07, 0.0, 0.0),
      start_text='bent fingertips on the chest, then the hands slump down'),
    W('SICK', 'Sick', 'mid5', 'TowardSelf', 'forehead', 'middle', (-0.3, 1, 0), mv('tap', 'inward', 5, 'cm', 1, 0.8, 'both'),
      hands='both', left_hs='mid5', left_anchor='stomach', left_contact='middle', left_palm='TowardSelf',
      left_finger=(0.6, 0.6, 0), radius=0.07, start_text='middle fingers touch the forehead and the stomach'),
    W('PAIN', 'Pain / hurt', '1', 'TowardSelf', 'neutral', 'index', (-1, 0, 0), mv('converge', 'index-jab-twist', 14, 'cm', 2, 1.0),
      hands='both', left_hs='1', left_anchor='mirror', left_contact='index', offset=(-0.05, 0.05, -0.08), direction=(-1, 0, 0),
      radius=0.12, start_text='index fingers point at each other, jab together twice'),
    W('LOVE', 'Love', 'S', 'TowardSelf', 'chest', 'palm', (-0.6, 1, 0), mv('hold', 'crossed', 0, '', 1, 0.8),
      hands='both', left_hs='S', left_anchor='chest', left_offset=(0.05, 0.0, 0.0), offset=(-0.05, 0.0, 0.0),
      start_text='fists crossed at the wrists over the chest'),
    W('LIKE', 'Like', 'open8', 'TowardSelf', 'chest', 'middle', (0, 1, 0), mv('stroke', 'forward', 15, 'cm', 1, 0.7),
      end='8', offset=(0.0, 0.0, 0.03), direction=(0, 0, 1),
      start_text='open-8 at the chest, then forward closing the middle finger and thumb'),
    W('WANT', 'Want', '5', 'Up', 'chest', 'palm', (0, 0, 1), mv('stroke', 'back', 15, 'cm', 1, 0.8, 'both'),
      hands='both', left_hs='5', left_anchor='mirror', end='claw', offset=(0.10, -0.06, 0.24), direction=(0, 0, -1),
      radius=0.12, start_text='both 5-hands palm up in front, pulled back into claws'),

    W('NEED', 'Need', 'X', 'Forward', 'neutral', 'palm', (0, 1, 0), mv('turn', 'pitch', 45, 'deg', 1, 0.6),
      radius=0.14, start_text='X-hand, palm forward, bends down from the wrist'),
    W('WHAT', 'What', '5', 'Up', 'chest', 'palm', (0, 0, 1), mv('stroke', 'outward', 10, 'cm', 1, 0.8, 'both'),
      hands='both', left_hs='5', left_anchor='mirror', offset=(0.08, -0.08, 0.22), direction=(0.7, 0, 0.5), radius=0.12,
      non_manual=WH, start_text='relaxed 5-hands, palms up, move slightly forward and apart'),
    W('WHERE', 'Where', '1', 'Forward', 'neutral', 'palm', (0, 1, 0), mv('wave', 'wrist-yaw', 20, 'deg', 2, 1.0),
      offset=(0.06, 0.10, -0.04), radius=0.14, non_manual=WH,
      start_text='index finger up in front of the shoulder, pivot side to side'),
    W('WHEN', 'When', '1', 'Down', 'left-index', 'index', (-0.3, 0.3, 1), mv('circle', 'frontal', 3, 'cm', 1, 1.0),
      hands='both', left_hs='1', left_anchor='chest', left_offset=(-0.02, 0.06, 0.22), left_palm='Sideways',
      left_finger=(0, 1, 0), radius=0.07, non_manual=WH,
      start_text='right index circles the left index tip, then touches it'),
    W('WHO', 'Who', 'L', 'Sideways', 'chin', 'thumb', (0, 1, 0), mv('squeeze', 'index', 50, 'pct', 2, 0.8),
      end='X', radius=0.06, non_manual=WH, start_text='thumb on the chin, index finger bends twice'),
    W('WHY', 'Why', 'B', 'TowardSelf', 'forehead', 'fingers', (0, 1, 0), mv('stroke', 'forward-down', 15, 'cm', 1, 0.8),
      end='Y', direction=(0, -0.5, 0.85), non_manual=WH,
      start_text='fingers at the forehead, then forward and down into a Y-hand'),
    W('HOW', 'How', 'bent', 'Down', 'chest', 'knuckle', (0, -0.5, 0.85), mv('turn', 'pitch', 140, 'deg', 1, 0.9, 'both'),
      hands='both', left_hs='bent', left_anchor='mirror', offset=(0.03, 0.0, 0.20), radius=0.12, non_manual=WH,
      start_text='curved hands, backs of the fingers together, roll forward to palms up'),
    W('TODAY', 'Today', 'Y', 'Up', 'chest', 'palm', (0, 0, 1), mv('stroke', 'down', 8, 'cm', 1, 0.5, 'both'),
      hands='both', left_hs='Y', left_anchor='mirror', offset=(0.10, -0.05, 0.22), direction=(0, -1, 0),
      scored=False, reason=COMPOUND,
      beat2=beat('left-back', 'palm', '1', 'Sideways', (0, 1, 0), mv('stroke', 'arc-across', 22, 'cm', 1, 0.9),
                 direction=(-1, -0.5, 0), left_anchor='chest', left_palm='Down', left_finger=(1, 0, 0), left_hs='B'),
      start_text='NOW (Y-hands drop) then DAY (index arm arcs across)'),
    W('TOMORROW', 'Tomorrow', 'openA', 'Sideways', 'side-head', 'thumb', (0, 1, 0), mv('stroke', 'forward', 10, 'cm', 1, 0.6),
      direction=(0, 0.25, 1), radius=0.07, start_text='open-A thumb at the side of the head / cheek, pivots forward'),
    W('GO', 'Go', '1', 'Forward', 'chest', 'index', (0, 1, 0), mv('stroke', 'forward', 20, 'cm', 1, 0.6, 'both'),
      hands='both', left_hs='1', left_anchor='mirror', offset=(0.10, 0.02, 0.18), direction=(0, 0, 1), radius=0.12,
      start_text='index fingers up, thrown forward'),
]

# ---- Lifeprint sources (handshape / location / movement / palm / non-manual / notes) ----
S = {
    'HELLO': ('h/hello.htm', 'B (flat)', 'beside the forehead', 'wave side to side', 'forward', '', 'v3 word'),
    'YES': ('y/yes.htm', 'S (fist)', 'chest height', 'nod at the wrist', 'forward', 'head nod', 'v3 word'),
    'NO': ('n/no.htm', 'index+middle with thumb', 'chest height', 'snap onto the thumb', 'forward', 'head shake', 'v3 word'),
    'THANK-YOU': ('t/thankyou.htm', 'B (flat)', 'chin / lips', 'forward and down', 'toward self', 'smile', 'v3 word'),
    'PLEASE': ('p/please.htm', 'B (flat)', 'centre of the chest', 'circle (clockwise from the observer)', 'toward self', '', ''),
    'SORRY': ('s/sorry.htm', 'A', 'chest', 'a couple of circles', 'toward self', '', 'S-hand also seen; Lifeprint recommends A'),
    'GOOD': ('g/good.htm', 'B (flat), base hand palm up', 'lips, then into the left palm', 'forward and down', 'toward self', '',
             'Lifeprint: one-handed GOOD overlaps THANK-YOU; the two-handed form is taught'),
    'BAD': ('b/bad.htm', 'B (flat)', 'lips', 'down and away, ending palm down', 'toward self', '', ''),
    'GOODBYE': ('b/bye.htm', 'open hand', 'shoulder height', 'fingers bend at the large knuckles (open-close)', 'forward', '',
                'Lifeprint page is video only; description from the video label WAVE-goodbye and the page Q&A. Needs a human check.'),
    'GOOD-MORNING': ('g/good-morning.htm', 'B then B (MORNING)', 'lips, then forearm', 'GOOD forward; MORNING forearm rises',
                     'toward self', '', 'Compound GOOD + MORNING (lifeprint m/morning.htm). Page is video only.'),
    'MOTHER': ('m/momdad.htm', '5', 'chin (thumb)', 'touch; double tap common', 'sideways', '', 'Also m/mother.htm (video). Same handshape as FATHER, different location.'),
    'FATHER': ('m/momdad.htm', '5', 'forehead (thumb)', 'touch; double tap common', 'sideways', '', 'Also f/father.htm (video).'),
    'FRIEND': ('f/friend.htm', 'X (hooked index), both', 'in front of the body', 'index fingers interlock twice', 'right down / left up', '', ''),
    'BABY': ('b/baby.htm', 'flat hands, arms cradled', 'in front of the body', 'rock the arms back and forth', 'up', '', ''),
    'MAN': ('m/man.htm', '5', 'forehead (thumb) to chest', 'from FATHER position down to the chest', 'sideways', '', 'Several versions; the FATHER-to-chest version is taught.'),
    'WOMAN': ('w/woman.htm', '5', 'chin (thumb) to chest', 'down to the chest', 'sideways', '', ''),
    'BOY': ('b/boygirl.htm', 'flat hand closing (cap brim)', 'forehead', 'grab the brim of a cap (close), repeated', 'down', '', 'b/boy.htm is images only; description from b/boygirl.htm. Needs a human check of the handshape.'),
    'GIRL': ('g/girl.htm', 'A', 'cheek / jaw (thumb)', 'slide forward and down along the cheek', 'sideways', '', ''),
    'TEACHER': ('t/teacher.htm', 'flat-O both, then flat hands', 'near the head, then chest', 'forward ~15 cm, then down (PERSON)', 'forward', '', 'Compound TEACH + PERSON'),
    'DOCTOR': ('d/doctor.htm', 'bent hand (or M / D)', 'inside of the upturned left wrist', 'tap twice', 'down', '', 'Base hand shape does not matter (Lifeprint).'),
    'WATER': ('w/water.htm', 'W', 'mouth (index finger)', 'tap twice', 'sideways', '', ''),
    'MILK': ('m/milk.htm', 'C closing to S', 'in front of the body', 'squeeze twice', 'sideways', '', ''),
    'EAT': ('e/eat.htm', 'flat-O', 'mouth', 'fingertips to the mouth, repeated', 'down', '', ''),
    'DRINK': ('d/drink.htm', 'C', 'mouth', 'short arc to the mouth (tilt)', 'sideways', '', ''),
    'BREAD': ('b/bread.htm', 'bent hand on flat base', 'back of the left hand', 'slice down a couple of times', 'down', '', ''),
    'APPLE': ('a/apple.htm', 'X', 'cheek (index knuckle)', 'pivot back and forth', 'down', '', 'A-hand version is not recommended by Lifeprint.'),
    'HUNGRY': ('h/hungry.htm', 'C', 'top of the chest', 'down the middle of the chest', 'toward self', '', ''),
    'THIRSTY': ('t/thirsty.htm', 'index finger', 'throat', 'run the fingertip down the throat', 'toward self', '', ''),
    'COFFEE': ('c/coffee.htm', 'S both', 'chest, stacked fists', 'top hand cranks', 'down', '', 'v3 word'),
    'HOT': ('h/hot.htm', 'claw', 'mouth', 'flick out and down, fast', 'toward self', '', 'v3 word'),
    'COLD': ('c/cold.htm', 'S both', 'chest', 'shiver side to side', 'sideways', '', 'v3 word'),
    'HELP': ('h/help.htm', 'A on flat palm', 'chest', 'both hands lift', 'down', '', 'v3 word'),
    'HAPPY': ('h/happy.htm', 'flat hand(s)', 'chest', 'forward circles, brushing up the chest', 'toward self', 'happy face', 'One or both hands; one is taught.'),
    'SAD': ('s/sad.htm', '5 both', 'in front of the face', 'down the length of the face', 'toward self', 'sad face, head tilts forward', ''),
    'TIRED': ('t/tired.htm', 'bent hands', 'chest', 'hands slump (change orientation, do not slide)', 'toward self', 'tired face', ''),
    'SICK': ('s/sick.htm', 'middle-finger-bent 5, both', 'forehead and stomach', 'both hands move in to touch', 'toward self', '', ''),
    'PAIN': ('h/hurt.htm', 'index fingers, both', 'in front of the body (or where it hurts)', 'tips toward each other twice, with a twist', 'toward self', 'pained face', 'Lifeprint HURT = PAIN (p/pain.htm shows the same sign).'),
    'LOVE': ('l/love.htm', 'S / fists crossed', 'chest', 'cross over the chest (hold)', 'toward self', '', ''),
    'LIKE': ('l/like.htm', 'open-8 to 8', 'just in front of the chest', 'forward while the middle finger and thumb close', 'toward self', '', ''),
    'WANT': ('w/want.htm', '5 to claw, both', 'in front of the body', 'pull back toward you', 'up', '', ''),
    'NEED': ('n/need.htm', 'X', 'in front of the body', 'bend down from the wrist', 'forward to down', '', ''),
    'WHAT': ('w/what.htm', 'relaxed 5, both', 'in front of the body', 'slightly forward and to the sides', 'up', 'WH-question eyebrows (furrowed)', 'Lifeprint: facial expression carries much of WHAT.'),
    'WHERE': ('w/where.htm', 'index finger', 'in front of the shoulder', 'pivot side to side (wrist and elbow)', 'forward', 'WH-question eyebrows', ''),
    'WHEN': ('w/when.htm', 'index fingers, both', 'in front of the body', 'right index circles the left index tip, then touches', 'down / sideways', 'WH-question eyebrows', ''),
    'WHO': ('w/who.htm', 'L (thumb on chin)', 'chin', 'index finger bends twice', 'sideways', 'WH-question eyebrows', 'Lifeprint names this the good general variation.'),
    'WHY': ('w/why.htm', 'flat hand to Y', 'forehead', 'forward and down, changing to Y', 'toward self', 'WH-question eyebrows', ''),
    'HOW': ('h/how.htm', 'curved hands, both', 'in front of the chest, knuckles together', 'roll forward to palms up', 'down', 'WH-question eyebrows', ''),
    'TODAY': ('t/today.htm', 'Y both (NOW), then index (DAY)', 'in front of the body', 'NOW drop, then DAY arc', 'up', '', 'Compound NOW + DAY (n/now.htm, d/day.htm).'),
    'TOMORROW': ('t/tomorrow.htm', 'open-A', 'side of the head / cheek (thumb)', 'forward pivot: thumb points back, ends up', 'sideways', '', 'Description from lifeprint topics/parameters-asl.htm and lessons/lesson06.htm (sign page is images only).'),
    'GO': ('g/go.htm', 'index fingers, both', 'in front of the body', 'throw the index fingers forward', 'forward', '', ''),
}


def main():
    old = json.load(open(TPL, encoding='utf-8-sig'))
    by_intent = {t['intent']: t for t in old['templates']}
    for w in V3:
        assert w in by_intent, w
    new_by = {t['intent']: t for t in NEW}
    assert set(new_by) | set(V3) == set(WORDS), set(WORDS) ^ (set(new_by) | set(V3))
    out = []
    for w in WORDS:
        if w in V3:
            t = dict(by_intent[w])
            t.setdefault('scored', True)
            t['scored'] = True
            t.setdefault('teachOnlyReason', '')
        else:
            t = new_by[w]
        out.append(t)
    # Keep per-word overrides decided after the self-test (scored / reason), if any.
    overrides_path = os.path.join(os.path.dirname(__file__), 'scored_overrides.json')
    if os.path.exists(overrides_path):
        ov = json.load(open(overrides_path, encoding='utf-8'))
        for t in out:
            if t['intent'] in ov:
                t['scored'] = ov[t['intent']]['scored']
                t['teachOnlyReason'] = ov[t['intent']].get('reason', '')
    with open(TPL, 'w', encoding='utf-8-sig', newline='\n') as f:
        f.write('{\n  "version": 2,\n  "templates": [\n')
        f.write(',\n'.join('    ' + json.dumps(t, ensure_ascii=False) for t in out))
        f.write('\n  ]\n}\n')

    old_lessons = json.load(open(LESSONS, encoding='utf-8-sig'))
    cues = {wd['intent']: wd for l in old_lessons['lessons'] for wd in l['words']}
    lessons = []
    for i in range(5):
        words = []
        for w in WORDS[i * 10:(i + 1) * 10]:
            t = new_by.get(w) or by_intent[w]
            if w in cues:
                words.append({'intent': w, 'meaning': cues[w]['meaning'], 'cue': cues[w]['cue']})
            else:
                words.append({'intent': w, 'meaning': t['phrase'], 'cue': cue_for(t)})
        lessons.append({'id': i + 1, 'title': LESSON_TITLES[i], 'words': words})
    with open(LESSONS, 'w', encoding='utf-8', newline='\n') as f:
        f.write('{\n  "lessons": [\n')
        f.write(',\n'.join('    ' + json.dumps(l, ensure_ascii=False) for l in lessons))
        f.write('\n  ]\n}\n')

    src = []
    for w in WORDS:
        url, hs, loc, mov, palm, nm, notes = S[w]
        src.append({'word': w, 'lifeprint_url': LP + url, 'handshape': hs, 'location': loc, 'movement': mov,
                    'palm': palm, 'non_manual': nm, 'notes': notes})
    with open(SOURCES, 'w', encoding='utf-8', newline='\n') as f:
        json.dump({'source': 'Lifeprint / ASL University (Dr. Bill Vicars), lifeprint.com. Not reviewed by Deaf signers yet.',
                   'words': src}, f, ensure_ascii=False, indent=1)
    scored = sum(1 for t in out if t['scored'])
    print(f'{len(out)} templates, {scored} scored, {len(out) - scored} teach-only; lessons: {[len(l["words"]) for l in lessons]}')


def cue_for(t):
    s = t['startLocation']
    s = s[0].upper() + s[1:]
    return s if len(s) <= 70 else s[:67] + '...'


if __name__ == '__main__':
    sys.exit(main())
