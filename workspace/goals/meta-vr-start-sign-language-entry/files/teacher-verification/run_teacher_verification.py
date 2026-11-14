"""
Teacher verification (SPEC v3.1 §3.3): PNG frames recorded in Unity -> MP4 (30 fps) -> MediaPipe Holistic -> TGCN.
Writes teacher_videos/<WORD>.mp4 and results_<model>.json; build_report.py turns them into the table.

  python run_teacher_verification.py [--model models/tgcn/asl1000 --classes 1000]
"""
import argparse
import glob
import json
import os

import cv2

import tgcn_infer as T

HERE = os.path.dirname(os.path.abspath(__file__))


def frames_to_mp4(frame_dir, out_path, fps=30):
    files = sorted(glob.glob(os.path.join(frame_dir, '*.png')))
    if not files:
        return 0
    first = cv2.imread(files[0])
    h, w = first.shape[:2]
    vw = cv2.VideoWriter(out_path, cv2.VideoWriter_fourcc(*'mp4v'), fps, (w, h))
    for f in files:
        vw.write(cv2.imread(f))
    vw.release()
    return len(files)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--model', default=os.path.join(HERE, 'models', 'tgcn', 'asl1000'))
    ap.add_argument('--classes', type=int, default=1000)
    ap.add_argument('--frames', default=os.path.join(HERE, 'teacher_frames'))
    ap.add_argument('--videos', default=os.path.join(HERE, 'teacher_videos'))
    ap.add_argument('--out', default=None)
    a = ap.parse_args()
    os.makedirs(a.videos, exist_ok=True)
    model = T.load_model(a.model, a.classes)
    names = T.class_names(os.path.join(HERE, 'data', 'WLASL_v0.3.json'), a.classes)
    results = {}
    for d in sorted(os.listdir(a.frames)):
        fd = os.path.join(a.frames, d)
        if not os.path.isdir(fd):
            continue
        mp4 = os.path.join(a.videos, d + '.mp4')
        n = frames_to_mp4(fd, mp4)
        kps, hand_rate = T.extract(mp4, os.path.join(HERE, 'models'))
        top = T.predict(model, names, kps)
        results[d] = {'frames': n, 'hand_detect_rate': round(hand_rate, 2), 'top5': top}
        print(f'{d}\t{n}\thands {hand_rate:.2f}\t' + ', '.join(f'{g} {p:.2f}' for g, p in top), flush=True)
    out = a.out or os.path.join(HERE, f'results_asl{a.classes}.json')
    json.dump(results, open(out, 'w', encoding='utf-8'), indent=1)


if __name__ == '__main__':
    main()
