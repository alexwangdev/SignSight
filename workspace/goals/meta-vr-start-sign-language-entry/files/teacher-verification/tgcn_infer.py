"""
Offline teacher verification (SPEC v3.1 §3.3), dev machine only — nothing here ships in the app.

  video -> MediaPipe Holistic (tasks API) -> 55 OpenPose-layout keypoints per frame -> 50 frames -> TGCN (WLASL)

Keypoint layout matches WLASL code/TGCN/sign_dataset.py read_pose_file(): OpenPose BODY_25 minus the 12 lower-body
points (13 left: nose, neck, R shoulder/elbow/wrist, L shoulder/elbow/wrist, mid-hip, R eye, L eye, R ear, L ear),
then the left hand (21) and the right hand (21); x, y in a 256 x 256 frame, mapped to 2 * (v / 256 - 0.5).
Missing parts are 0 (as OpenPose writes them). Frames: 50, sampled uniformly over the clip.
Class index = position in sorted(first N glosses of WLASL_v0.3.json) (sklearn LabelEncoder in the WLASL code).

Usage:
  python tgcn_infer.py --model models/tgcn/asl1000 --wlasl data/WLASL_v0.3.json video1.mp4 [video2.mp4 ...]
"""
import argparse
import configparser
import json
import os
import sys

import cv2
import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'third_party', 'wlasl_tgcn'))
from tgcn_model import GCN_muti_att  # noqa: E402

import mediapipe as mp  # noqa: E402
from mediapipe.tasks.python import vision  # noqa: E402
from mediapipe.tasks.python.core.base_options import BaseOptions  # noqa: E402

NUM_SAMPLES = 50
SIZE = 256.0
FRAME_MODE = os.environ.get('TGCN_FRAME', 'crop')  # crop (used for all reported results) | squash
FILL_GAPS = os.environ.get('TGCN_FILL', '1') == '1'

# MediaPipe pose index for each kept OpenPose BODY_25 point (None = computed).
# OpenPose kept order: 0 nose, 1 neck, 2 RShoulder, 3 RElbow, 4 RWrist, 5 LShoulder, 6 LElbow, 7 LWrist, 8 MidHip,
#                      15 REye, 16 LEye, 17 REar, 18 LEar
MP_NOSE, MP_LSH, MP_RSH, MP_LEL, MP_REL, MP_LWR, MP_RWR, MP_LHIP, MP_RHIP = 0, 11, 12, 13, 14, 15, 16, 23, 24
MP_LEYE, MP_REYE, MP_LEAR, MP_REAR = 2, 5, 7, 8


def load_model(model_dir, num_class):
    cfg = configparser.ConfigParser()
    cfg.read(os.path.join(model_dir, 'config.ini'))
    hidden = cfg.getint('GCN', 'HIDDEN_SIZE')
    stages = cfg.getint('GCN', 'NUM_STAGES')
    drop = cfg.getfloat('TRAIN', 'DROP_P')
    model = GCN_muti_att(input_feature=NUM_SAMPLES * 2, hidden_feature=hidden, num_class=num_class,
                         p_dropout=drop, num_stage=stages)
    ck = torch.load(os.path.join(model_dir, 'pytorch_model.bin'), map_location='cpu')
    sd = ck.get('state_dict', ck) if isinstance(ck, dict) else ck
    missing, unexpected = model.load_state_dict(sd, strict=False)
    if missing or unexpected:
        print(f'[warn] state_dict: {len(missing)} missing, {len(unexpected)} unexpected keys', file=sys.stderr)
    model.eval()
    return model


def class_names(wlasl_path, n):
    data = json.load(open(wlasl_path, encoding='utf-8'))
    return sorted(e['gloss'] for e in data[:n])


def make_landmarker(models_dir):
    opts = vision.HolisticLandmarkerOptions(
        base_options=BaseOptions(model_asset_path=os.path.join(models_dir, 'holistic_landmarker.task')),
        running_mode=vision.RunningMode.VIDEO)
    return vision.HolisticLandmarker.create_from_options(opts)


def frame_keypoints(res, w, h, crop):
    """55 x 2 array in the 256 frame (pixel units), zeros where missing."""
    out = np.zeros((55, 2), np.float32)

    def px(lm):
        if crop is None:
            return (lm.x * SIZE, lm.y * SIZE)
        x0, y0, side = crop
        return ((lm.x * w - x0) / side * SIZE, (lm.y * h - y0) / side * SIZE)

    pose = res.pose_landmarks
    if pose:
        p = [px(l) for l in pose]
        mid = lambda a, b: ((p[a][0] + p[b][0]) / 2, (p[a][1] + p[b][1]) / 2)  # noqa: E731
        body = [p[MP_NOSE], mid(MP_LSH, MP_RSH), p[MP_RSH], p[MP_REL], p[MP_RWR], p[MP_LSH], p[MP_LEL], p[MP_LWR],
                mid(MP_LHIP, MP_RHIP), p[MP_REYE], p[MP_LEYE], p[MP_REAR], p[MP_LEAR]]
        out[:13] = body
    if res.left_hand_landmarks:
        out[13:34] = [px(l) for l in res.left_hand_landmarks]
    if res.right_hand_landmarks:
        out[34:55] = [px(l) for l in res.right_hand_landmarks]
    return out


def extract(video_path, models_dir):
    cap = cv2.VideoCapture(video_path)
    fps = cap.get(cv2.CAP_PROP_FPS) or 30.0
    frames = []
    while True:
        ok, img = cap.read()
        if not ok:
            break
        frames.append(cv2.cvtColor(img, cv2.COLOR_BGR2RGB))
    cap.release()
    if not frames:
        raise RuntimeError(f'no frames in {video_path}')
    h, w = frames[0].shape[:2]
    if FRAME_MODE == 'squash':
        crop = None  # whole frame resized to 256 x 256 (aspect not kept)
    else:
        side = min(w, h)
        crop = ((w - side) / 2.0, (h - side) / 2.0, float(side))  # centred square
    lm = make_landmarker(models_dir)
    kps, hands = [], 0
    for i, f in enumerate(frames):
        res = lm.detect_for_video(mp.Image(image_format=mp.ImageFormat.SRGB, data=np.ascontiguousarray(f)),
                                  int(i * 1000.0 / fps))
        k = frame_keypoints(res, w, h, crop)
        hands += int(bool(res.left_hand_landmarks) or bool(res.right_hand_landmarks))
        kps.append(k)
    lm.close()
    return np.stack(kps), hands / len(frames)


def fill_gaps(kps):
    """OpenPose always outputs both hands; MediaPipe drops them in fast frames. Fill a missing hand from the nearest
    frames where it was seen (or at its wrist if never seen), so missing detections are not read as a pose."""
    kps = kps.copy()
    n = len(kps)
    for lo, hi, wrist in ((13, 34, 7), (34, 55, 4)):  # left hand <- L wrist (body 7), right hand <- R wrist (body 4)
        seen = [i for i in range(n) if np.any(kps[i, lo:hi] != 0)]
        for i in range(n):
            if i in seen:
                continue
            if seen:
                j = min(seen, key=lambda s: abs(s - i))
                kps[i, lo:hi] = kps[j, lo:hi] - kps[j, wrist] + kps[i, wrist] if np.any(kps[i, wrist]) and np.any(kps[j, wrist]) else kps[j, lo:hi]
            elif np.any(kps[i, wrist]):
                kps[i, lo:hi] = kps[i, wrist]
    return kps


def sample(kps):
    if FILL_GAPS:
        kps = fill_gaps(kps)
    n = len(kps)
    idx = np.linspace(0, n - 1, NUM_SAMPLES).round().astype(int) if n >= NUM_SAMPLES else \
        np.concatenate([np.arange(n), np.full(NUM_SAMPLES - n, n - 1)])
    s = kps[idx]                                   # 50 x 55 x 2
    s = 2.0 * (s / SIZE - 0.5)
    s[np.all(kps[idx] == 0, axis=2)] = -1.0        # OpenPose zeros -> 2 * (0 / 256 - 0.5) = -1
    x = np.transpose(s, (1, 0, 2)).reshape(55, NUM_SAMPLES * 2)  # per keypoint: x0, y0, x1, y1, ...
    return torch.from_numpy(x).float().unsqueeze(0)


def predict(model, names, kps, k=5):
    with torch.no_grad():
        p = torch.softmax(model(sample(kps)), dim=1)[0]
    top = torch.topk(p, k)
    return [(names[i], float(v)) for v, i in zip(top.values, top.indices)]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--model', default=os.path.join(HERE, 'models', 'tgcn', 'asl1000'))
    ap.add_argument('--classes', type=int, default=1000)
    ap.add_argument('--wlasl', default=os.path.join(HERE, 'data', 'WLASL_v0.3.json'))
    ap.add_argument('--mp', default=os.path.join(HERE, 'models'))
    ap.add_argument('--json', help='write results here')
    ap.add_argument('videos', nargs='+')
    a = ap.parse_args()
    model = load_model(a.model, a.classes)
    names = class_names(a.wlasl, a.classes)
    results = []
    for v in a.videos:
        kps, hand_rate = extract(v, a.mp)
        top = predict(model, names, kps)
        results.append({'video': v, 'frames': len(kps), 'hand_detect_rate': round(hand_rate, 2), 'top5': top})
        print(f'{os.path.basename(v)}\tframes {len(kps)}\thands {hand_rate:.2f}\t' +
              ', '.join(f'{g} {p:.2f}' for g, p in top))
    if a.json:
        json.dump(results, open(a.json, 'w', encoding='utf-8'), indent=1)


if __name__ == '__main__':
    main()
