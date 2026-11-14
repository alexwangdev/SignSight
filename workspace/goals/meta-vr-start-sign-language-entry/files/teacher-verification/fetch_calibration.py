"""
Downloads a few real human WLASL clips (test split, direct .mp4 links) for calibration (SPEC v3.1 §3.3 REVIEW rule):
if the model also misses the real human doing the sign, the model is unreliable for that word.
Clips are trimmed to the WLASL frame_start / frame_end. Stored in calib/ (git-ignored; WLASL is C-UDA, research only).

  python fetch_calibration.py [words...]
"""
import json
import os
import sys
import urllib.request

import cv2

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, os.environ.get('CLIP_DIR', 'calib'))
HOSTS = ('signstock.blob.core.windows.net', 'aslsignbank.haskins.yale.edu', 'media.spreadthesign.com',
         's3-us-west-1.amazonaws.com', 'media.asldeafined.com', 'www.signingsavvy.com', 'www.handspeak.com')
ALIAS = {'THANK-YOU': 'thank you', 'PAIN': 'hurt', 'GOODBYE': 'goodbye'}


def trim(src, dst, start, end):
    cap = cv2.VideoCapture(src)
    fps = cap.get(cv2.CAP_PROP_FPS) or 25.0
    frames = []
    while True:
        ok, f = cap.read()
        if not ok:
            break
        frames.append(f)
    cap.release()
    if not frames:
        return 0
    s = max(0, start - 1)
    e = len(frames) if end < 0 else min(len(frames), end)
    frames = frames[s:e] or frames
    h, w = frames[0].shape[:2]
    vw = cv2.VideoWriter(dst, cv2.VideoWriter_fourcc(*'mp4v'), fps, (w, h))
    for f in frames:
        vw.write(f)
    vw.release()
    return len(frames)


def main(words, per_word=1):
    data = json.load(open(os.path.join(HERE, 'data', 'WLASL_v0.3.json'), encoding='utf-8'))
    by = {e['gloss']: e for e in data[:1000]}
    os.makedirs(OUT, exist_ok=True)
    for w in words:
        g = ALIAS.get(w, w.lower())
        e = by.get(g)
        if not e:
            print(f'{w}\tnot in asl1000')
            continue
        got = 0
        for split in ('test', 'val', 'train'):
            for inst in e['instances']:
                if got >= per_word:
                    break
                u = inst['url']
                if inst['split'] != split or not u.lower().endswith('.mp4') or not any(h in u for h in HOSTS):
                    continue
                raw = os.path.join(OUT, f'raw_{w}_{inst["video_id"]}.mp4')
                dst = os.path.join(OUT, f'{w}_{inst["video_id"]}.mp4')
                try:
                    req = urllib.request.Request(u, headers={'User-Agent': 'Mozilla/5.0'})
                    with urllib.request.urlopen(req, timeout=30) as r, open(raw, 'wb') as f:
                        f.write(r.read())
                    n = trim(raw, dst, inst['frame_start'], inst['frame_end'])
                    os.remove(raw)
                    if n > 5:
                        got += 1
                        print(f'{w}\t{split}\t{n} frames\t{os.path.basename(dst)}\t{u}')
                except Exception as ex:  # dead links are common in WLASL
                    print(f'{w}\tfailed {u}: {ex}', file=sys.stderr)
            if got >= per_word:
                break
        if not got:
            print(f'{w}\tno downloadable clip')


if __name__ == '__main__':
    main(sys.argv[1:] or ['HELLO', 'COFFEE', 'MOTHER', 'WATER', 'EAT', 'HELP', 'NO', 'WHAT'],
         per_word=int(os.environ.get('CLIPS_PER_WORD', '1')))
