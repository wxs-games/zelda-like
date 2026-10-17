# -*- coding: utf-8 -*-
"""
精灵图集流水线（alpha-first，全程透明底）：
  入口归一化（透明底直通 / 白底·黑底 -> 先抠图 -> 统一 RGBA master）
  -> 切格 -> 连通域清理（去碎影 / 可只留最大域）
  -> 条内身高中位数统一缩放 -> 脚底基线对齐（底留白 7px）+ 水平居中
  -> 输出 Frames 单帧 + Sheets 等大透明精灵表

说明：全程以 alpha 为准，不再经过白底中间图。非透明底来源在入口先抠成 alpha；
      阈值抠图的边缘质量天然低于原生 alpha，故只对这类来源额外做 1px 收缩去白晕。
"""
import sys, os, glob, re
sys.stdout.reconfigure(encoding='utf-8')
import numpy as np
from PIL import Image, ImageFilter
from collections import deque

try:
    from scipy import ndimage
    HAVE_SCIPY = True
except ImportError:
    HAVE_SCIPY = False

# 脚本位于 <工程根>\Tools\art-pipeline\，向上两级定位工程根（含 Assets 的目录），免配置路径
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Art', 'Art')
BOT_GAP = 7          # 与既有素材一致的脚底留白
WHITE_THR = 232      # 近白阈值
BLACK_THR = 60       # 近黑阈值
KEEP_RATIO = 0.12    # 小于最大连通域该比例的碎影丢弃
ALPHA_MIN = 8        # 低于该 alpha 视为完全透明

# ---- 18 条规格 ----
SPECS = [
    # name, cols, rows, n, cell(w,h), target_h
    dict(name='Player_Run',        cols=4, rows=2, n=8,  cell=(692,833), target=760, largest=True),
    dict(name='Player_Dash',       cols=5, rows=1, n=5,  cell=(692,833), target=760),
    dict(name='Player_Roll',       cols=4, rows=2, n=8,  cell=(692,833), target=430),
    dict(name='Player_Attack01',   cols=5, rows=2, n=10, cell=(692,833), target=760),
    dict(name='Player_Attack02',   cols=4, rows=2, n=8,  cell=(692,833), target=760),
    dict(name='Player_Attack03',   cols=3, rows=3, n=9,  cell=(692,833), target=760),
    dict(name='Player_AttackAir',  cols=3, rows=2, n=6,  cell=(692,833), target=760),
    dict(name='Player_Victory',    cols=4, rows=2, n=8,  cell=(692,833), target=760),
    dict(name='Player_Turn',       cols=1, rows=1, n=1,  cell=(692,833), target=760),
    dict(name='Player_WallSlide',  cols=3, rows=1, n=3,  cell=(692,833), target=760),
    dict(name='Player_WallJump',   cols=3, rows=1, n=3,  cell=(692,833), target=760),
    dict(name='Player_LedgeGrab',  cols=3, rows=1, n=3,  cell=(692,833), target=760),
    dict(name='Player_Crouch',     cols=2, rows=1, n=2,  cell=(692,833), target=500),
    dict(name='Player_Idle02',     cols=4, rows=2, n=8,  cell=(692,833), target=760),
    dict(name='Player_HeavyAttack',cols=6, rows=3, n=18, cell=(692,833), target=760),
    dict(name='Player_Die',        cols=7, rows=1, n=7,  cell=(692,833), target=760),
    dict(name='Enemy03_Spawn',     cols=2, rows=2, n=4,  cell=(993,1124), target=900),
    dict(name='Tower01_Aim',       cols=2, rows=2, n=4,  cell=(942,979), target=890),
    dict(name='Boss01_Special',    cols=5, rows=3, n=14, cell=(831,798), target=760),
    dict(name='Boss01_Enrage',     cols=3, rows=2, n=6,  cell=(831,798), target=760),
]

def subdir_for(name):
    if name.startswith('Player'):  return 'Player'
    if name.startswith('Enemy03'): return os.path.join('Enemies','Enemy03')
    if name.startswith('Tower01'): return os.path.join('Towers','Tower01')
    if name.startswith('Boss01'):  return os.path.join('Boss','Boss01')
    raise ValueError(name)

def find_raw(name):
    d = os.path.join(ROOT, subdir_for(name), 'Sheets', 'raw')
    hits = [p for p in glob.glob(os.path.join(d, name + '_raw.*')) if not p.endswith('.meta')]
    if not hits: raise FileNotFoundError(name + '_raw.*')
    return hits[0]

# ===== 入口归一化：任意来源 -> 统一 RGBA master =====

def _bg_from_border(rgb):
    """用图像外框像素的中位亮度判定背景类型：亮=白底，暗=黑底。"""
    b = np.concatenate([rgb[0, :], rgb[-1, :], rgb[:, 0], rgb[:, -1]])
    return 'white' if np.median(b.mean(axis=1)) >= 128 else 'black'

def _border_connected(mask):
    """返回与图像边缘连通的区域（scipy 缺失时用 BFS 兜底）。"""
    h, w = mask.shape
    if HAVE_SCIPY:
        lbl, _ = ndimage.label(mask)
        border = set(lbl[0, :]) | set(lbl[-1, :]) | set(lbl[:, 0]) | set(lbl[:, -1])
        border.discard(0)
        return np.isin(lbl, list(border)) if border else np.zeros((h, w), bool)
    seen = np.zeros((h, w), bool); dq = deque()
    for x in range(w):
        for y in (0, h - 1):
            if mask[y, x] and not seen[y, x]: seen[y, x] = True; dq.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if mask[y, x] and not seen[y, x]: seen[y, x] = True; dq.append((y, x))
    while dq:
        y, x = dq.popleft()
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                seen[ny, nx] = True; dq.append((ny, nx))
    return seen

def matte_background(rgb, bg):
    """非透明底 -> alpha：只去掉与边缘连通的背景色，保留内部同色区（白 T 恤 / 眼白 / 暗部细节）。"""
    target = (rgb >= WHITE_THR).all(axis=2) if bg == 'white' else (rgb <= BLACK_THR).all(axis=2)
    return ((~_border_connected(target)) * 255).astype(np.uint8)

def bleed_colors(rgb, alpha):
    """颜色外溢（alpha bleeding）：用最近的不透明像素颜色填满透明区，
    避免下游 LANCZOS 缩放时透明区的黑/白 RGB 渗进边缘产生晕圈。"""
    if not HAVE_SCIPY:
        return rgb
    empty = alpha <= ALPHA_MIN
    if not empty.any() or empty.all():
        return rgb
    idx = ndimage.distance_transform_edt(empty, return_distances=False, return_indices=True)
    return rgb[idx[0], idx[1]]

def clean_mask(mask, largest_only):
    """连通域清理：去碎影；largest_only=True 时只保留最大域（去除悬空配件/水印）。"""
    if not HAVE_SCIPY:
        return mask
    lbl, n = ndimage.label(mask)
    if n <= 1:
        return mask
    areas = ndimage.sum(mask, lbl, range(1, n + 1))
    keep = np.zeros(n + 1, bool)
    if largest_only:
        keep[int(np.argmax(areas)) + 1] = True
    else:
        amax = areas.max()
        for i, a in enumerate(areas, start=1):
            if a >= amax * KEEP_RATIO: keep[i] = True
    return keep[lbl]

def load_master(path):
    """入口归一化：任意来源 -> (RGBA ndarray, 是否来自阈值抠图)。
    透明底直通；白底/黑底先抠图变透明。返回前统一做颜色外溢，保证缩放安全。"""
    im = Image.open(path)
    has_alpha = im.mode in ('RGBA', 'LA', 'PA') or 'transparency' in im.info
    if has_alpha:
        rgba = np.asarray(im.convert('RGBA'))
        a = rgba[..., 3]
        if (a <= ALPHA_MIN).mean() > 0.02:      # 确有透明区域 -> 直通
            return np.dstack([bleed_colors(rgba[..., :3], a), a]), False
    rgb = np.asarray(im.convert('RGB'))
    bg = _bg_from_border(rgb)
    a = matte_background(rgb, bg)
    print(f'  [entry] 非透明底（{bg}） -> 抠图成 alpha')
    return np.dstack([bleed_colors(rgb, a), a]), True

def content_rgba(cell_rgb, alpha):
    bbox = Image.fromarray(alpha).getbbox()
    if bbox is None: return None, None
    rgba = np.dstack([cell_rgb, alpha])
    return Image.fromarray(rgba, 'RGBA').crop(bbox), bbox

def process(spec):
    name = spec['name']; cols=spec['cols']; rows=spec['rows']; n=spec['n']
    cw, ch = spec['cell']; target = spec['target']
    raw_p = find_raw(name)
    if not raw_p.endswith('.png'):
        png = raw_p.rsplit('.', 1)[0] + '.png'
        Image.open(raw_p).save(png); os.remove(raw_p); raw_p = png
    master, matted = load_master(raw_p)
    H, W = master.shape[:2]
    cellw, cellh = W//cols, H//rows
    frames = []
    for i in range(n):
        r, c = divmod(i, cols)
        tile = master[r*cellh:(r+1)*cellh, c*cellw:(c+1)*cellw]
        a = tile[..., 3]
        gate = clean_mask(a > ALPHA_MIN, spec.get('largest', False))
        alpha = np.where(gate, a, 0).astype(np.uint8)
        f, _ = content_rgba(tile[..., :3], alpha)
        frames.append(f)
    # 坏帧替换: replace={目标帧: 来源帧}, 如末帧与水印重叠被切时用首帧替补
    for dst, src in (spec.get('replace') or {}).items():
        frames[dst] = frames[src]
    ok = [f for f in frames if f is not None]
    if len(ok) < max(1, n-2):
        raise RuntimeError(f'{name}: 有效帧过少 {len(ok)}/{n}')
    # ref: 基准身高取条内最高帧（max）。若按中位数，最高帧缩放后会超出画格被裁，
    # 死亡这类「站立帧少、躺平帧多」的双峰分布尤其明显（max 亦即站立帧高度）
    ref_mode = spec.get('ref', 'max')
    heights = sorted(f.height for f in ok)
    ref_h = max(heights) if ref_mode == 'max' else heights[len(heights)//2]
    scale = target / ref_h
    # 画格宽度同样以条内最宽帧为基准：固定 692 会把挥砍等横向大动作左右裁掉
    cw_out = max(cw, max(round(f.width*scale) for f in ok))
    sub = os.path.join(ROOT, subdir_for(name))
    fdir = os.path.join(sub, 'Frames', name); os.makedirs(fdir, exist_ok=True)
    sheet = Image.new('RGBA', (cw_out*cols, ch*rows), (0,0,0,0))
    for i, f in enumerate(frames):
        out = Image.new('RGBA', (cw_out, ch), (0,0,0,0))
        if f is not None:
            nw, nh = max(1,round(f.width*scale)), max(1,round(f.height*scale))
            f = f.resize((nw, nh), Image.LANCZOS)
            if matted:      # 阈值抠图来源: 额外 1px 收缩+轻羽化去白晕（原生 alpha 来源不做）
                a = f.split()[-1].filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.6))
                f.putalpha(a)
            # 溢出防裁：宽/高超格时 clamp 到 0，让溢出端从右/下裁掉，保住 bbox 左上（人物主体）
            x = max(0, (cw_out - nw)//2)
            y = max(0, ch - BOT_GAP - nh)
            out.paste(f, (x, y))        # 目标全透明, 直贴保留 straight alpha, 避免 mask 混合压暗边缘
        out.save(os.path.join(fdir, f'{name}_{i:02d}.png'))
        sheet.paste(out, ((i%cols)*cw_out, (i//cols)*ch))
    sp = os.path.join(sub, 'Sheets', f'{name}_sheet.png')
    sheet.save(sp)
    src_kind = '阈值抠图' if matted else '原生 alpha'
    wide = f'  画格宽={cw_out}(基准{cw}+自适应)' if cw_out != cw else ''
    print(f'[OK] {name}: {n}帧  来源={src_kind}  raw格={cellw}x{cellh}{wide}  '
          f'基准身高[{ref_mode}]={ref_h}  scale={scale:.3f}  -> {os.path.relpath(sp, ROOT)}')

if __name__ == '__main__':
    only = sys.argv[1:] if len(sys.argv) > 1 else None
    for s in SPECS:
        if only and s['name'] not in only: continue
        try:
            process(s)
        except Exception as e:
            print(f'[FAIL] {s["name"]}: {e}')
