"""
Builds the Explore mock camera frame (v3.3 Task B, Editor only): four CC0 photos from Wikimedia Commons side by side
on a 1280 x 720 grey "table" frame -> Assets/StreamingAssets/explore-mock.png. Sources (all CC0):
  apple   https://commons.wikimedia.org/wiki/File:Red_apple_on_a_plate_2017_A.jpg
  cup     https://commons.wikimedia.org/wiki/File:Cup_of_coffee_with_beans.jpg
  bottle  https://commons.wikimedia.org/wiki/File:Bottle_of_Water.jpg
  banana  https://commons.wikimedia.org/wiki/File:Bunch_of_bananas_on_sale.jpg   (not one of the 50 words)
  python compose_mock.py <repo root>
"""
import os
import sys

import cv2
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ORDER = ['Red_apple_on_a_plate_2017_A.jpg', 'Cup_of_coffee_with_beans.jpg', 'Bottle_of_Water.jpg',
         'Bunch_of_bananas_on_sale.jpg']
W, H, TILE_W, TILE_H = 1280, 720, 300, 520


def main(root):
    canvas = np.full((H, W, 3), 140, np.uint8)
    for i, name in enumerate(ORDER):
        img = cv2.imread(os.path.join(HERE, 'src', name))
        s = min(TILE_W / img.shape[1], TILE_H / img.shape[0])
        img = cv2.resize(img, (int(img.shape[1] * s), int(img.shape[0] * s)), interpolation=cv2.INTER_AREA)
        cx = int((i + 0.5) * W / len(ORDER))
        x0, y0 = cx - img.shape[1] // 2, (H - img.shape[0]) // 2
        canvas[y0:y0 + img.shape[0], x0:x0 + img.shape[1]] = img
    out = os.path.join(root, 'Assets', 'StreamingAssets', 'explore-mock.png')
    cv2.imwrite(out, canvas)
    print(out, os.path.getsize(out))


if __name__ == '__main__':
    main(sys.argv[1])
