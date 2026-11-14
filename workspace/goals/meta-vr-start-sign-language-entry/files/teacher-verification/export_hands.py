"""
Real human clips -> 3D hand joints in the recogniser's signer frame, for replay in Unity (SignSight.EditorTools.HumanReplayTest).

MediaPipe Holistic (tasks API) gives pose world landmarks (metres, origin between the hips) and hand world landmarks
(metres, origin at the hand's centre). Each hand is placed at its pose wrist. Axes: MediaPipe x = image right,
y = down, z = away from the camera; the signer faces the camera, so the signer frame (x = signer's right, y = up,
z = toward the viewer) is (-x, -y, -z). Origin = between the eyes (pose landmarks 2 and 5), like the headset.

Left-handed signers (left wrist travels further) are mirrored (x -> -x, hands swapped) so the dominant hand is "right",
as the app assumes. A hand missing for <= 6 frames is filled from the nearest frame it was seen, moved to the current
wrist; longer gaps stay untracked (as on a headset when the hand leaves view).

  python export_hands.py human_clips/*.mp4   -> human_hands/<clip>.json
"""
import json
import os
import sys

import cv2
import numpy as np

import tgcn_infer as T
import mediapipe as mp

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'human_hands')
MAX_GAP = 6


def conv(lm):
    return np.array([[-p.x, -p.y, -p.z] for p in lm], np.float32)


def export(path, models_dir):
    cap = cv2.VideoCapture(path)
    fps = cap.get(cv2.CAP_PROP_FPS) or 25.0
    imgs = []
    while True:
        ok, img = cap.read()
        if not ok:
            break
        imgs.append(cv2.cvtColor(img, cv2.COLOR_BGR2RGB))
    cap.release()
    lm = T.make_landmarker(models_dir)
    frames = []
    for i, f in enumerate(imgs):
        res = lm.detect_for_video(mp.Image(image_format=mp.ImageFormat.SRGB, data=np.ascontiguousarray(f)), int(i * 1000.0 / fps))
        fr = {'pose': None, 'R': None, 'L': None}
        if res.pose_world_landmarks:
            pose = conv(res.pose_world_landmarks)
            eyes = (pose[2] + pose[5]) / 2.0
            fr['pose'] = pose - eyes
            fr['shoulders'] = float(np.linalg.norm(pose[11] - pose[12]))
            for side, hw, wi in (('L', res.left_hand_world_landmarks, 15), ('R', res.right_hand_world_landmarks, 16)):
                if hw:
                    h = conv(hw)
                    fr[side] = h - h[0] + fr['pose'][wi]
        frames.append(fr)
    lm.close()

    # Fill short gaps from the nearest frame where the hand was seen, moved to this frame's wrist.
    for side, wi in (('L', 15), ('R', 16)):
        seen = [i for i, f in enumerate(frames) if f[side] is not None]
        for i, f in enumerate(frames):
            if f[side] is not None or f['pose'] is None or not seen:
                continue
            j = min(seen, key=lambda s: abs(s - i))
            if abs(j - i) <= MAX_GAP:
                f[side] = frames[j][side] - frames[j][side][0] + f['pose'][wi]

    # Dominant hand = the wrist that travels further; mirror left-handed signers.
    def travel(wi):
        pts = [f['pose'][wi] for f in frames if f['pose'] is not None]
        return float(sum(np.linalg.norm(b - a) for a, b in zip(pts, pts[1:])))
    left_handed = travel(15) > travel(16) * 1.15
    if left_handed:
        m = np.array([-1, 1, 1], np.float32)
        for f in frames:
            r, l = f['R'], f['L']
            f['R'] = None if l is None else l * m
            f['L'] = None if r is None else r * m

    sh = [f['shoulders'] for f in frames if f.get('shoulders')]
    out = {
        'clip': os.path.basename(path), 'fps': fps, 'left_handed_mirrored': left_handed,
        'shoulder_width_m': round(float(np.median(sh)), 3) if sh else None,
        'tracked_R': round(sum(f['R'] is not None for f in frames) / max(1, len(frames)), 2),
        'tracked_L': round(sum(f['L'] is not None for f in frames) / max(1, len(frames)), 2),
        'frames': [{'R': None if f['R'] is None else f['R'].round(4).tolist(),
                    'L': None if f['L'] is None else f['L'].round(4).tolist()} for f in frames],
    }
    return out


def main(paths):
    os.makedirs(OUT, exist_ok=True)
    for p in paths:
        o = export(p, os.path.join(HERE, 'models'))
        json.dump(o, open(os.path.join(OUT, os.path.splitext(o['clip'])[0] + '.json'), 'w'))
        print(f"{o['clip']}\tframes {len(o['frames'])}\tR {o['tracked_R']}\tL {o['tracked_L']}\t"
              f"shoulders {o['shoulder_width_m']}\tmirrored {o['left_handed_mirrored']}", flush=True)


if __name__ == '__main__':
    main(sys.argv[1:])
