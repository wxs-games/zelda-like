# -*- coding: utf-8 -*-
"""测量现有精灵表基准：每格内容 bbox、角色高度中位数、脚底留白，用于新素材对齐。"""
import sys, os, glob
sys.stdout.reconfigure(encoding='utf-8')
from PIL import Image

# 脚本位于 <工程根>\Tools\art-pipeline\，向上两级定位工程根（含 Assets 的目录），免配置路径
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Art', 'Art')

# 各角色待测样本: (sheet路径, cols, rows)
SAMPLES = {
    'Player': [
        ('Player/Sheets/Player_Idle_sheet.png', 5, 2),
        ('Player/Sheets/Player_Run_sheet.png', 4, 4),
        ('Player/Sheets/Player_Attack01_sheet.png', 5, 2),
        ('Player/Sheets/Player_Die_sheet.png', 5, 2),
        ('Player/Sheets/Player_Interact_sheet.png', 3, 2),
    ],
    'Enemy03': [
        ('Enemies/Enemy03/Sheets/Enemy03_Idle_sheet.png', 2, 2),
        ('Enemies/Enemy03/Sheets/Enemy03_Chase_sheet.png', 4, 2),
        ('Enemies/Enemy03/Sheets/Enemy03_Die_sheet.png', 5, 2),
    ],
    'Tower01': [
        ('Towers/Tower01/Sheets/Tower01_Idle_sheet.png', 2, 2),
        ('Towers/Tower01/Sheets/Tower01_Fire_sheet.png', 3, 2),
        ('Towers/Tower01/Sheets/Tower01_Disable_sheet.png', 3, 2),
    ],
    'Boss01': [
        ('Boss/Boss01/Sheets/Boss01_Idle_sheet.png', 3, 2),
        ('Boss/Boss01/Sheets/Boss01_Move_sheet.png', 5, 2),
        ('Boss/Boss01/Sheets/Boss01_Die_sheet.png', 4, 4),
    ],
}

def cell_stats(im, cols, rows):
    W, H = im.size
    cw, ch = W // cols, H // rows
    out = []
    a = im.split()[-1]  # alpha
    for r in range(rows):
        for c in range(cols):
            box = (c*cw, r*ch, (c+1)*cw, (r+1)*ch)
            tile = a.crop(box)
            bbox = tile.getbbox()  # 非零alpha范围
            if bbox is None:
                out.append(None); continue
            x0, y0, x1, y1 = bbox
            out.append(dict(w=x1-x0, h=y1-y0, bottom_gap=ch-1-y1,
                            cx=(x0+x1)/2/cw, top=y0/ch, bottom=y1/ch))
    return cw, ch, out

for name, sheets in SAMPLES.items():
    print(f'===== {name} =====')
    all_h, all_gap = [], []
    for rel, cols, rows in sheets:
        p = os.path.join(ROOT, rel)
        im = Image.open(p).convert('RGBA')
        cw, ch, stats = cell_stats(im, cols, rows)
        n = len([s for s in stats if s])
        hs = [s['h'] for s in stats if s]
        gs = [s['bottom_gap'] for s in stats if s]
        cxs = [s['cx'] for s in stats if s]
        tops = [s['top'] for s in stats if s]
        bots = [s['bottom'] for s in stats if s]
        all_h += hs; all_gap += gs
        med = sorted(hs)[len(hs)//2] if hs else 0
        print(f'  {os.path.basename(rel)}  cell={cw}x{ch}  非空帧={n}  身高中位={med}  '
              f'底留白中位={sorted(gs)[len(gs)//2] if gs else 0}  cx中位={sorted(cxs)[len(cxs)//2]:.3f}  '
              f'脚底线(bottom/ch)中位={sorted(bots)[len(bots)//2]:.3f}  顶线中位={sorted(tops)[len(tops)//2]:.3f}')
    mh = sorted(all_h)[len(all_h)//2]
    mg = sorted(all_gap)[len(all_gap)//2]
    print(f'  >> {name} 全局身高中位={mh}  底留白中位={mg}')
