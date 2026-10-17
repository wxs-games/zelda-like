# -*- coding: utf-8 -*-
"""透明底(RGBA) trim 不规则图(5+3) -> alpha 直通 -> 连通域自动分帧 -> 重拼规则 4x2 透明底网格
-> raw\Player_Run_raw.png

alpha-first: 入口即为透明底，全程以 alpha 为准，不再合成白底，raw 自身就是透明底 RGBA。
(此前版本误用 convert("RGB") 把透明像素下的深灰 RGB 当成黑底，绕了一圈「透明→白底→再猜回透明」)
"""
from PIL import Image
from pathlib import Path
import numpy as np
from scipy import ndimage

# 脚本位于 <工程根>\Tools\art-pipeline\，向上两级定位工程根（含 Assets 的目录），免配置路径
PROJ = Path(__file__).resolve().parents[2]
SRC = r"C:\Users\WSX.LAPTOP-5HQ2JJQQ\Pictures\林柯-run-trimmed.png"   # 外部原始素材，按实际来源改这里
DST = PROJ / "Assets" / "Art" / "Art" / "Player" / "Sheets" / "raw" / "Player_Run_raw.png"
N, COLS, ROWS = 8, 4, 2
ALPHA_MIN = 8

im = Image.open(SRC).convert("RGBA")
W, H = im.size
arr = np.asarray(im)
alpha = arr[..., 3]
print(f"src={W}x{H} mode=RGBA")
assert (alpha <= ALPHA_MIN).mean() > 0.02, "源图不是透明底，请先在入口抠图"

fg = alpha > ALPHA_MIN
print(f"fg ratio: {fg.mean():.4f}")
lbl, n = ndimage.label(fg)
if n < N:
    raise RuntimeError(f"只找到 {n} 个连通域, 少于 {N}")
sizes = ndimage.sum(fg, lbl, range(1, n + 1))
order = np.argsort(sizes)[::-1][:N]       # 取 8 个最大域(人物), 悬空小配件不会入选
boxes = []
for idx in order:
    ys, xs = np.where(lbl == idx + 1)
    boxes.append((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))

def cy(b):
    return (b[1] + b[3]) / 2

# 按 (行, 列) 排序: y 分两类行, 行内按 x
boxes.sort(key=cy)
row1 = sorted([b for b in boxes if cy(b) < H / 2], key=lambda b: b[0])
row2 = sorted([b for b in boxes if cy(b) >= H / 2], key=lambda b: b[0])
seq = row1 + row2
print(f"row1={len(row1)} row2={len(row2)}")

# 颜色外溢: 透明区 RGB 用最近不透明像素颜色填充, 避免下游缩放时渗色出晕圈
empty = ~fg
if empty.any():
    idx = ndimage.distance_transform_edt(empty, return_distances=False, return_indices=True)
    rgb = arr[..., :3][idx[0], idx[1]]
else:
    rgb = arr[..., :3]
master = np.dstack([rgb, np.where(fg, alpha, 0).astype(np.uint8)])

# 重拼等分透明网格 (格子 576x704, 内容居中)
CW, CH = 576, 704
sheet = Image.new("RGBA", (CW * COLS, CH * ROWS), (0, 0, 0, 0))
for i, b in enumerate(seq):
    crop = master[b[1]:b[3], b[0]:b[2]]
    img = Image.fromarray(crop, 'RGBA')
    r, c = divmod(i, COLS)
    px = c * CW + (CW - img.width) // 2
    py = r * CH + (CH - img.height) // 2
    sheet.paste(img, (px, py))       # 目标全透明, 直贴保留 straight alpha
    print(f"frame{i}: bbox={b} size={img.size}")
sheet.save(DST)
print(f"OK -> {DST} ({sheet.size[0]}x{sheet.size[1]}, cell {CW}x{CH}, mode={sheet.mode})")
