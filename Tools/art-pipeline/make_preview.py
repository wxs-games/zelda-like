# -*- coding: utf-8 -*-
"""Build a contact preview image for the 17 new clips, dark-green background."""
from PIL import Image, ImageDraw
from pathlib import Path

# 脚本位于 <工程根>\Tools\art-pipeline\，向上两级定位工程根（含 Assets 的目录），免配置路径
art = Path(__file__).resolve().parents[2] / "Assets" / "Art"
clips = [
    ("Art/Player", "Player_Dash"), ("Art/Player", "Player_Roll"),
    ("Art/Player", "Player_Attack02"), ("Art/Player", "Player_Attack03"),
    ("Art/Player", "Player_AttackAir"), ("Art/Player", "Player_Victory"),
    ("Art/Player", "Player_Turn"), ("Art/Player", "Player_WallSlide"),
    ("Art/Player", "Player_WallJump"), ("Art/Player", "Player_LedgeGrab"),
    ("Art/Player", "Player_Crouch"), ("Art/Player", "Player_Idle02"),
    ("Art/Player", "Player_HeavyAttack"),
    ("Art/Enemies/Enemy03", "Enemy03_Spawn"),
    ("Art/Towers/Tower01", "Tower01_Aim"),
    ("Art/Boss/Boss01", "Boss01_Special"), ("Art/Boss/Boss01", "Boss01_Enrage"),
]

FG_H = 110      # frame height in preview
PAD = 8
LABEL_W = 200
BG = (26, 51, 38)       # dark green
FG = (235, 240, 235)

rows = []
for rel, name in clips:
    sheet = Image.open(art / rel / "Sheets" / f"{name}_sheet.png").convert("RGBA")
    cols = sheet.width // (sheet.height // 2) if name == "Boss01_Special" else None
    # split by known cell: read one frame file to get cell size
    f0 = Image.open(art / rel / "Frames" / name / f"{name}_00.png").convert("RGBA")
    cw, ch = f0.size
    n = sheet.width // cw
    frames = [sheet.crop((i * cw, 0, (i + 1) * cw, ch)) for i in range(n)]
    scale = FG_H / ch
    frames = [f.resize((max(1, int(f.width * scale)), FG_H), Image.LANCZOS) for f in frames]
    rows.append((name, frames))

row_h = FG_H + PAD * 2
W = max(LABEL_W + sum(f.width + PAD for f in fs) + PAD for _, fs in rows) + PAD
H = row_h * len(rows) + PAD
img = Image.new("RGB", (W, H), BG)
d = ImageDraw.Draw(img)
for i, (name, frames) in enumerate(rows):
    y = PAD + i * row_h
    d.text((PAD + 4, y + row_h // 2 - 7), name, fill=FG)
    x = LABEL_W
    for f in frames:
        img.paste(f, (x, y), f)
        x += f.width + PAD

out = art / "_预览" / "contact_P1_new_clips.png"
img.save(out)
print(f"[OK] {out.name} {img.size[0]}x{img.size[1]}, {len(rows)} rows")
