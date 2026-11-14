"""
Builds teacher_verification_50.csv and the table part of teacher_verification_50.md (SPEC v3.1 §3.3 / §3.4) from:
  coverage (data/WLASL_v0.3.json), results_asl1000_full.json (+ results_asl1000_rerun.json for re-recorded words),
  calib_results_asl1000.json (real human WLASL clips), and the human Lifeprint check below.
"""
import csv
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..', '..'))

WORDS = [
    'HELLO', 'YES', 'NO', 'THANK-YOU', 'PLEASE', 'SORRY', 'GOOD', 'BAD', 'GOODBYE', 'GOOD-MORNING',
    'MOTHER', 'FATHER', 'FRIEND', 'BABY', 'MAN', 'WOMAN', 'BOY', 'GIRL', 'TEACHER', 'DOCTOR',
    'WATER', 'MILK', 'EAT', 'DRINK', 'BREAD', 'APPLE', 'HUNGRY', 'THIRSTY', 'COFFEE', 'HOT',
    'COLD', 'HELP', 'HAPPY', 'SAD', 'TIRED', 'SICK', 'PAIN', 'LOVE', 'LIKE', 'WANT',
    'NEED', 'WHAT', 'WHERE', 'WHEN', 'WHO', 'WHY', 'HOW', 'TODAY', 'TOMORROW', 'GO',
]
ALIASES = {'THANK-YOU': ['thank you'], 'GOODBYE': ['goodbye', 'bye'], 'GOOD-MORNING': ['good morning'],
           'PAIN': ['hurt', 'pain'], 'MOTHER': ['mother', 'mom'], 'FATHER': ['father', 'dad']}

# Human check of each template against its Lifeprint definition (word_sources.json).
# Y = handshape, location and movement as Lifeprint describes; PARTIAL = a named part is simplified.
LIFEPRINT = {w: ('Y', '') for w in WORDS}
LIFEPRINT.update({
    'FRIEND': ('PARTIAL', 'teacher links the hooked index fingers twice but does not swap which hand is on top between the links'),
    'GOOD-MORNING': ('PARTIAL', 'compound; MORNING is shown as a forearm turning up, simplified (TEACH-ONLY)'),
    'TEACHER': ('PARTIAL', 'compound TEACH + PERSON, simplified (TEACH-ONLY)'),
    'TODAY': ('PARTIAL', 'compound NOW + DAY; DAY arc simplified (TEACH-ONLY)'),
    'GOODBYE': ('Y', 'Lifeprint page is video only; checked against the page label WAVE-goodbye'),
    'BOY': ('Y', 'checked against lifeprint boygirl.htm (b/boy.htm is images only)'),
})


def names(w):
    return ALIASES.get(w, []) + [w.lower().replace('-', ' ')]


def main():
    wl = json.load(open(os.path.join(HERE, 'data', 'WLASL_v0.3.json'), encoding='utf-8'))
    g1000 = {e['gloss'] for e in wl[:1000]}
    teach = json.load(open(os.path.join(HERE, 'results_asl1000_full.json'), encoding='utf-8'))
    rerun_path = os.path.join(HERE, 'results_asl1000_rerun.json')
    if os.path.exists(rerun_path):
        teach.update(json.load(open(rerun_path, encoding='utf-8')))
    calib = {}
    for r in json.load(open(os.path.join(HERE, 'calib_results_asl1000.json'), encoding='utf-8')):
        w = os.path.basename(r['video']).rsplit('_', 1)[0]
        calib[w] = r
    tpl = json.load(open(os.path.join(ROOT, 'Assets', 'Resources', 'templates.json'), encoding='utf-8-sig'))['templates']
    scored = {t['intent']: t.get('scored', True) for t in tpl}

    rows = []
    for w in WORDS:
        ns = names(w)
        in1000 = any(n in g1000 for n in ns)
        r = teach.get(w, {})
        top5 = [g for g, _ in r.get('top5', [])]
        hit = any(g in ns for g in top5)
        lp, lp_note = LIFEPRINT[w]
        c = calib.get(w)
        chit = None if c is None else any(g in ns for g, _ in c['top5'])
        if not in1000:
            verdict, why = 'N/A', 'not in WLASL asl1000: model cannot judge'
        elif hit and lp == 'Y':
            verdict, why = 'PASS', 'expected sign in Top-5 and the template matches Lifeprint'
        elif lp != 'Y' and not hit:
            verdict, why = 'FAIL', f'template simplified vs Lifeprint: {lp_note}'
        elif hit:
            verdict, why = 'PASS*', f'in Top-5, but the template is simplified: {lp_note}'
        elif chit is False:
            verdict, why = 'REVIEW', 'model-unreliable-for-this-word: it also misses a real human signing it'
        elif chit:
            verdict, why = 'REVIEW', 'model finds it on a real human but not on the teacher; Lifeprint matches -> needs a human check of the teacher demo'
        else:
            verdict, why = 'REVIEW', 'no calibration clip'
        rows.append({
            'word': w, 'scored': 'Y' if scored.get(w, True) else 'N (TEACH-ONLY)',
            'in_asl1000': 'Y' if in1000 else 'N',
            'model_top1': top5[0] if top5 else '',
            'model_top5': ' | '.join(f'{g} {p:.2f}' for g, p in r.get('top5', [])),
            'expected_in_top5': 'Y' if hit else 'N',
            'lifeprint_match': lp,
            'human_calibration_top5_hit': '' if chit is None else ('Y' if chit else 'N'),
            'human_calibration_top5': '' if c is None else ' | '.join(f'{g} {p:.2f}' for g, p in c['top5']),
            'hand_detect_rate': r.get('hand_detect_rate', ''),
            'verdict': verdict, 'note': why,
        })
    with open(os.path.join(HERE, 'teacher_verification_50.csv'), 'w', encoding='utf-8', newline='') as f:
        wr = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        wr.writeheader()
        wr.writerows(rows)
    # Markdown table
    md = ['| word | scored | in asl1000 | model Top-1 | expected in Top-5 | Lifeprint match | human clip Top-5 hit | verdict |',
          '|---|---|---|---|---|---|---|---|']
    for r in rows:
        md.append(f"| {r['word']} | {r['scored']} | {r['in_asl1000']} | {r['model_top1']} | {r['expected_in_top5']} | "
                  f"{r['lifeprint_match']} | {r['human_calibration_top5_hit'] or '-'} | {r['verdict']} |")
    open(os.path.join(HERE, 'table.md'), 'w', encoding='utf-8').write('\n'.join(md) + '\n')
    from collections import Counter
    cnt = Counter(r['verdict'] for r in rows)
    covered = [r for r in rows if r['in_asl1000'] == 'Y']
    print('verdicts:', dict(cnt))
    print('teacher top-5 hit:', sum(r['expected_in_top5'] == 'Y' for r in covered), '/', len(covered))
    cal = [r for r in rows if r['human_calibration_top5_hit']]
    print('human calibration top-5 hit:', sum(r['human_calibration_top5_hit'] == 'Y' for r in cal), '/', len(cal))
    both = [r for r in cal if r['human_calibration_top5_hit'] == 'Y']
    print('teacher hit where the model gets the human right:', sum(r['expected_in_top5'] == 'Y' for r in both), '/', len(both))
    unreliable = [r['word'] for r in rows if 'model-unreliable' in r['note']]
    review_h = [r['word'] for r in rows if 'needs a human check' in r['note']]
    print('model-unreliable:', ', '.join(unreliable))
    print('review (model ok on human, misses teacher):', ', '.join(review_h))
    print('fail:', ', '.join(r['word'] for r in rows if r['verdict'] == 'FAIL'))
    print('pass:', ', '.join(r['word'] for r in rows if r['verdict'].startswith('PASS')))


if __name__ == '__main__':
    main()
