# -*- coding: utf-8 -*-
"""Flip Player_Victory frame 0 to face right, then rebuild the sheet (4x2)."""
from PIL import Image
from pathlib import Path

# 脚本位于 <工程根>\Tools\art-pipeline\，向上两级定位工程根（含 Assets 的目录），免配置路径
art = Path(__file__).resolve().parents[2] / "Assets" / "Art" / "Art" / "Player"
frames_dir = art / "Frames" / "Player_Victory"
sheet_path = art / "Sheets" / "Player_Victory_sheet.png"

files = sorted(frames_dir.glob("Player_Victory_*.png"))
assert len(files) == 8, f"expect 8 frames, got {len(files)}"
imgs = [Image.open(f).convert("RGBA") for f in files]

imgs[0] = imgs[0].transpose(Image.FLIP_LEFT_RIGHT)
imgs[0].save(files[0])

cw, ch = imgs[0].size
sheet = Image.new("RGBA", (cw * 4, ch * 2), (0, 0, 0, 0))
for i, im in enumerate(imgs):
    r, c = divmod(i, 4)
    sheet.paste(im, (c * cw, r * ch))
sheet.save(sheet_path)
print(f"[OK] Victory frame0 flipped, sheet rebuilt {cw}x{ch} cell, 4x2")
