"""
Coverage of the 50 SignSight Learn v3.1 words in the WLASL subsets (asl100 / asl300 / asl1000 / asl2000).
WLASL subsets are the first N glosses of WLASL_v0.3.json (in file order, as in the WLASL paper / start_kit).

  py -3 -I check_coverage.py data/WLASL_v0.3.json > coverage_result_<date>.txt

A word "is covered" when its gloss, or one of its aliases (same sign, different English label in WLASL), is in the
subset. Aliases are listed below and reported, so nothing is matched silently.
"""
import json
import sys

WORDS = [
    'HELLO', 'YES', 'NO', 'THANK-YOU', 'PLEASE', 'SORRY', 'GOOD', 'BAD', 'GOODBYE', 'GOOD-MORNING',
    'MOTHER', 'FATHER', 'FRIEND', 'BABY', 'MAN', 'WOMAN', 'BOY', 'GIRL', 'TEACHER', 'DOCTOR',
    'WATER', 'MILK', 'EAT', 'DRINK', 'BREAD', 'APPLE', 'HUNGRY', 'THIRSTY', 'COFFEE', 'HOT',
    'COLD', 'HELP', 'HAPPY', 'SAD', 'TIRED', 'SICK', 'PAIN', 'LOVE', 'LIKE', 'WANT',
    'NEED', 'WHAT', 'WHERE', 'WHEN', 'WHO', 'WHY', 'HOW', 'TODAY', 'TOMORROW', 'GO',
]
assert len(WORDS) == 50

# Same sign under another English gloss in WLASL.
ALIASES = {
    'THANK-YOU': ['thank you', 'thanks'],
    'GOODBYE': ['bye', 'goodbye'],
    'GOOD-MORNING': ['good morning'],
    'PAIN': ['hurt', 'pain'],
    'MOTHER': ['mother', 'mom'],
    'FATHER': ['father', 'dad'],
}
SUBSETS = [100, 300, 1000, 2000]


def norm(g):
    return g.strip().lower().replace('-', ' ').replace('_', ' ')


def main(path):
    data = json.load(open(path, encoding='utf-8'))
    glosses = [norm(e['gloss']) for e in data]
    rank = {}
    for i, g in enumerate(glosses):
        rank.setdefault(g, i)
    print(f'WLASL file: {path} ({len(glosses)} glosses)')
    print('word\tmatched_gloss\tclass_index\t' + '\t'.join(f'asl{n}' for n in SUBSETS))
    totals = {n: 0 for n in SUBSETS}
    for w in WORDS:
        cands = ALIASES.get(w, []) + [norm(w)]
        hits = [(rank[c], c) for c in cands if c in rank]
        if hits:
            idx, g = min(hits)
        else:
            idx, g = None, '-'
        cols = []
        for n in SUBSETS:
            ok = idx is not None and idx < n
            totals[n] += ok
            cols.append('Y' if ok else 'N')
        print(f'{w}\t{g}\t{idx if idx is not None else "-"}\t' + '\t'.join(cols))
    print('TOTAL\t\t\t' + '\t'.join(f'{totals[n]}/50' for n in SUBSETS))
    print('Aliases used: ' + '; '.join(f'{k} -> {v}' for k, v in ALIASES.items()))


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else 'data/WLASL_v0.3.json')
