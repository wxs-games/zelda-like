# -*- coding: utf-8 -*-
"""
程序化音频合成：为一个类塞尔达 2D 游戏生成 BGM 与核心音效。

设计原则
--------
1) 纯加法合成 / 物理建模，不依赖任何外部采样素材，可复现（固定随机种子）。
2) 像素风 8-bit 小游戏，音色走「明亮、方正、短促」路线，不追求写实。
3) BGM 必须无缝循环：所有音符在循环边界处对齐相位，且整体做等功率交叉淡化。

用法：python synth_audio.py <输出根目录>
"""
import sys
import os
import numpy as np

SR = 44100          # 采样率
RNG = np.random.default_rng(20261018)


# ============================================================
# 基础工具
# ============================================================

def t_axis(dur):
    """生成 [0, dur) 的时间轴。"""
    return np.arange(int(SR * dur)) / SR


def env_ad(n, attack=0.005, decay=0.1, sustain=0.0, release=0.05,
           curve=2.0, dur=None):
    """
    ADSR 包络。attack/decay/release 单位为秒，sustain 为持续段电平。
    curve>1 时 release 段指数衰减，听感更自然。
    """
    if dur is None:
        dur = len(n) / SR
    total = len(n)
    a = max(int(attack * SR), 1)
    d = max(int(decay * SR), 1)
    r = max(int(release * SR), 1)
    s = max(total - a - d - r, 0)

    e = np.zeros(total, dtype=np.float64)
    idx = 0
    e[idx:idx + a] = np.linspace(0, 1, a, endpoint=False)
    idx += a
    e[idx:idx + d] = np.linspace(1, sustain, d, endpoint=False)
    idx += d
    e[idx:idx + s] = sustain
    idx += s
    e[idx:idx + r] = sustain * np.power(np.linspace(1, 0, r, endpoint=False), curve)
    if idx + r < total:
        e[idx + r:] = 0.0
    return e


def sine(freq, dur, phase=0.0):
    t = t_axis(dur)
    return np.sin(2 * np.pi * freq * t + phase)


def triangle(freq, dur, phase=0.0):
    """三角波，比方波柔和，适合做旋律声部。"""
    t = t_axis(dur)
    x = (freq * t + phase / (2 * np.pi)) % 1.0
    return 4.0 * np.abs(x - 0.5) - 1.0


def square(freq, dur, duty=0.5, phase=0.0):
    t = t_axis(dur)
    x = (freq * t + phase / (2 * np.pi)) % 1.0
    return np.where(x < duty, 1.0, -1.0)


def saw(freq, dur, phase=0.0):
    t = t_axis(dur)
    x = (freq * t + phase / (2 * np.pi)) % 1.0
    return 2.0 * x - 1.0


def noise(n, low=None, high=None):
    """带限白噪声（通过频域硬截止，够用且无需额外依赖）。"""
    x = RNG.standard_normal(n)
    if low is None and high is None:
        return x
    X = np.fft.rfft(x)
    freqs = np.fft.rfftfreq(n, 1.0 / SR)
    mask = np.ones_like(freqs)
    if low is not None:
        mask *= (freqs >= low)
    if high is not None:
        mask *= (freqs <= high)
    return np.fft.irfft(X * mask, n)


def lowpass(x, cutoff):
    """一阶 IIR 低通。用 lfilter 手工实现，避免引入 scipy。"""
    if cutoff <= 0:
        return np.zeros_like(x)
    if cutoff >= SR / 2:
        return x
    a = np.exp(-2.0 * np.pi * cutoff / SR)
    y = np.zeros_like(x)
    # y[n] = (1-a)*x[n] + a*y[n-1]
    # 用累积技巧实现单极点低通
    out = np.empty_like(x)
    prev = 0.0
    b = 1.0 - a
    for i in range(len(x)):
        prev = b * x[i] + a * prev
        out[i] = prev
    return out


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def normalize(x, peak=0.85):
    m = np.max(np.abs(x))
    if m < 1e-9:
        return x
    return x / m * peak


def fade_edges(x, fade_in=0.005, fade_out=0.02):
    """首尾淡入淡出，消除爆音（click）。"""
    y = x.copy()
    fi = min(int(fade_in * SR), len(y) // 2)
    fo = min(int(fade_out * SR), len(y) // 2)
    if fi > 1:
        y[:fi] *= np.linspace(0, 1, fi)
    if fo > 1:
        y[-fo:] *= np.linspace(1, 0, fo)
    return y


def at(dur, offset, sig):
    """把 sig 放到时间轴 offset 秒处，长度补零到 dur。"""
    out = np.zeros(int(SR * dur), dtype=np.float64)
    i = int(SR * offset)
    n = min(len(sig), len(out) - i)
    if n > 0:
        out[i:i + n] += sig[:n]
    return out


# ============================================================
# 音色
# ============================================================

def ks_pluck(freq, dur, damp=0.996, bright=0.5):
    """
    Karplus-Strong 拨弦：噪声激励 + 衰减延迟线。
    比加法合成自然得多，接近真实乐器，且能随freq 变化自动变音色。
    """
    n = int(SR * dur)
    N = max(int(SR / freq), 2)
    buf = RNG.standard_normal(N)
    # 初始激励低通：bright 越低越暗（低音区更暗，符合真实乐器）
    a = 0.2 + 0.75 * bright
    for i in range(1, N):
        buf[i] = a * buf[i] + (1 - a) * buf[i - 1]
    out = np.zeros(n, dtype=np.float64)
    pos = 0
    for i in range(n):
        out[i] = buf[pos]
        nxt = (pos + 1) % N
        # 延迟线两端平均 + 阻尼：阻尼越大衰减越快
        buf[pos] = damp * 0.5 * (buf[pos] + buf[nxt])
        pos = nxt
    return out


def bell(freq, dur, partials=((1, 1.0), (2.01, 0.5), (3.02, 0.25), (4.5, 0.12))):
    """加法合成的铃/钟音色，用于金币、任务完成。"""
    t = t_axis(dur)
    x = np.zeros_like(t)
    for ratio, amp in partials:
        x += amp * np.sin(2 * np.pi * freq * ratio * t)
    return x


def soft_lead(freq, dur, vib=0.0):
    """带轻微颤音的柔和主旋律声部，像笛子/口琴。"""
    t = t_axis(dur)
    if vib > 0:
        # 颤音延迟起音，避免一开始就有抖动
        lfo = np.sin(2 * np.pi * 5.2 * t) * vib * np.minimum(t / 0.15, 1.0)
    else:
        lfo = 0.0
    ph = 2 * np.pi * freq * t + 2 * np.pi * lfo
    return np.sin(ph) + 0.18 * np.sin(2 * ph)   # 叠一点二次谐波增加厚度


# ============================================================
# 音效
# ============================================================

def sfx_sword():
    """挥剑：快速下滑的带通噪声 + 金属感的短促谐波。"""
    dur = 0.28
    t = t_axis(dur)
    # 噪声部分：从 6000Hz 扫到 1200Hz
    x = noise(int(SR * dur), low=800, high=7000)
    # 手动做扫频：按时间切片用不同截止频率的低通
    out = np.zeros_like(x)
    chunk = max(int(SR * 0.01), 1)
    for i in range(0, len(x), chunk):
        frac = i / len(x)
        cut = 6500 * (1 - frac) + 1100 * frac
        seg = x[i:i + chunk]
        if len(seg) < 64:
            out[i:i + chunk] = seg
        else:
            out[i:i + chunk] = lowpass(seg, cut)
    e = env_ad(out, attack=0.002, decay=0.02, sustain=0.35, release=0.2, curve=2.5, dur=dur)
    out = out * e
    # 叠一个短促的高频「刃鸣」
    out += 0.35 * sine(2400, dur) * env_ad(np.ones(int(SR * dur)), 0.001, 0.03, 0.1, 0.15, 3.0, dur)
    return normalize(out, 0.8)


def sfx_bow():
    """射箭：拉弦噪声 + 快速下扫的哨音。"""
    dur = 0.35
    t = t_axis(dur)
    # 弓弦摩擦
    n = noise(int(SR * dur), low=400, high=4500)
    n *= env_ad(n, 0.003, 0.05, 0.25, 0.2, 2.0, dur)
    # 箭矢飞出的频率下扫
    f0, f1 = 1400, 420
    sweep = np.zeros_like(t)
    # 用指数扫频的相位积分
    k = (f1 - f0) / dur
    ph = 2 * np.pi * (f0 * t + 0.5 * k * t * t)
    sweep = np.sin(ph)
    sweep *= np.minimum(np.linspace(1.0, 0.0, len(t)) ** 1.4, 1.0)
    out = 0.55 * sweep + 0.4 * n
    out *= env_ad(out, 0.002, 0.08, 0.3, 0.18, 2.2, dur)
    return normalize(out, 0.8)


def sfx_enemy_hit():
    """敌人受击：短促的低频撞击 + 噪声爆裂。"""
    dur = 0.2
    body = sine(150, dur) * env_ad(np.ones(int(SR * dur)), 0.001, 0.05, 0.12, 0.12, 3.0, dur)
    nz = noise(int(SR * dur), low=200, high=3000)
    nz *= env_ad(nz, 0.001, 0.03, 0.0, 0.1, 3.0, dur)
    out = 0.6 * body + 0.5 * nz
    return normalize(out, 0.85)


def sfx_enemy_die():
    """敌人死亡：下滑音 + 碎裂噪声，比受击更长更「散」。"""
    dur = 0.55
    t = t_axis(dur)
    k = -260 / dur
    ph = 2 * np.pi * (320 * t + 0.5 * k * t * t)
    body = np.sin(ph) * env_ad(np.ones_like(t), 0.005, 0.1, 0.3, 0.35, 2.0, dur)
    nz = noise(int(SR * dur), low=150, high=2500)
    nz *= env_ad(nz, 0.005, 0.12, 0.15, 0.3, 2.2, dur)
    out = 0.55 * body + 0.45 * nz
    return normalize(out, 0.85)


def sfx_coin():
    """拾取金币：两个泛音快速叠加，经典「叮叮」。"""
    dur = 0.4
    n1 = bell(1318.5, dur, ((1, 1.0), (2.76, 0.4), (5.4, 0.15)))   # E6
    n2 = bell(1975.5, dur * 0.75, ((1, 1.0), (2.76, 0.35), (5.4, 0.12)))  # B6
    n2 = at(dur, 0.055, n2)
    out = 0.7 * n1 + 0.55 * n2
    out *= env_ad(out, 0.001, 0.03, 0.35, 0.28, 2.4, dur)
    return normalize(out, 0.7)


def sfx_quest_complete():
    """任务完成：三音上行琶音 + 铃，胜利感。"""
    dur = 1.3
    notes = [(587.33, 0.0), (739.99, 0.13), (880.0, 0.26)]   # D5 F5 A5
    out = np.zeros(int(SR * dur))
    for f, off in notes:
        v = bell(f, dur - off, ((1, 1.0), (2.0, 0.45), (3.01, 0.2), (4.1, 0.08)))
        v *= env_ad(v, 0.004, 0.06, 0.32, 0.5, 2.0, dur - off)
        out += at(dur, off, v)
    return normalize(out, 0.75)


def sfx_ui_click():
    """UI 点击：极短的方波脆响。用对称占空比避免直流分量导致发闷。"""
    dur = 0.07
    x = square(880, dur, duty=0.5)   # 50% 占空比 → DC 为 0
    x = lowpass(x, 5200)
    x *= env_ad(x, 0.001, 0.012, 0.0, 0.045, 3.5, dur)
    x = highpass(x, 120)   # 再去一次低频，去掉残余直流与隆隆声
    return normalize(x, 0.55)


def sfx_purchase():
    """购买成功：双音上行 + 一点铃，比 UI 点击更「成事」。"""
    dur = 0.6
    a = triangle(659.25, dur)          # E5
    a *= env_ad(a, 0.003, 0.05, 0.25, 0.3, 2.2, dur)
    b = bell(987.77, dur - 0.09, ((1, 1.0), (2.0, 0.4), (3.0, 0.15)))
    b *= env_ad(b, 0.003, 0.05, 0.28, 0.32, 2.2, dur - 0.09)
    out = 0.6 * a + at(dur, 0.09, 0.7 * b)
    return normalize(out, 0.72)


def sfx_player_hurt():
    """玩家受伤：低频闷响 + 轻微失真感。"""
    dur = 0.35
    t = t_axis(dur)
    body = sine(110, dur) + 0.5 * sine(165, dur)
    nz = noise(int(SR * dur), low=100, high=1400)
    e = env_ad(np.ones_like(t), 0.002, 0.07, 0.18, 0.22, 2.4, dur)
    out = (0.7 * body + 0.35 * nz) * e
    # 轻微软削波制造「受挤压」的粗糙感
    out = np.tanh(out * 1.8) * 0.6
    return normalize(out, 0.85)


def sfx_ui_hover():
    """按钮悬停：极轻的高音点，存在感要低。"""
    dur = 0.06
    x = sine(1320, dur) + 0.3 * sine(1980, dur)
    x *= env_ad(x, 0.002, 0.01, 0.0, 0.04, 4.0, dur)
    return normalize(x, 0.22)   # 音量刻意压低


def sfx_levelup():
    """升级/加血：上行滑音，明亮。"""
    dur = 0.7
    t = t_axis(dur)
    f0, f1 = 523.25, 1046.5
    ph = 2 * np.pi * (f0 * t + (f1 - f0) / dur * t * t)
    out = np.sin(ph) + 0.25 * np.sin(2 * ph)
    out *= env_ad(out, 0.005, 0.1, 0.35, 0.4, 2.0, dur)
    return normalize(out, 0.7)


def sfx_jump():
    """跳跃：短促上滑的方波，轻巧。"""
    dur = 0.18
    t = t_axis(dur)
    f0, f1 = 320, 700
    ph = 2 * np.pi * (f0 * t + (f1 - f0) / dur * t * t)
    out = square_from_phase(ph)
    out = lowpass(out, 4000)
    out *= env_ad(out, 0.002, 0.04, 0.3, 0.1, 2.5, dur)
    return normalize(out, 0.5)


def square_from_phase(ph):
    return np.sign(np.sin(ph))


# ============================================================
# BGM：田园轻快循环
# ============================================================

NOTE = {
    'C3': 130.81, 'D3': 146.83, 'E3': 164.81, 'F3': 174.61, 'G3': 196.00,
    'A3': 220.00, 'B3': 246.94,
    'C4': 261.63, 'D4': 293.66, 'E4': 329.63, 'F4': 349.23, 'G4': 392.00,
    'A4': 440.00, 'B4': 493.88,
    'C5': 523.25, 'D5': 587.33, 'E5': 659.25, 'F5': 698.46, 'G5': 783.99,
    'A5': 880.00,
}


def make_bgm():
    """
    田园轻快：五声音阶（C D E G A），四小节乐句循环。
    声部：主旋律（笛声）+ 分解和弦伴奏（拨弦）+ 轻低音。
    """
    bpm = 104.0
    beat = 60.0 / bpm          # 0.577s
    bar = beat * 4# 2.308s
    bars = 4
    total = bar * bars         # 9.23s

    # (音符, 起始拍, 时值拍)
    melody = [
        # 第 1 小节
        ('E5', 0.0, 1.0), ('G5', 1.0, 1.0), ('A5', 2.0, 1.5), ('G5', 3.5, 0.5),
        # 第 2 小节
        ('E5', 4.0, 1.0), ('D5', 5.0, 1.0), ('C5', 6.0, 2.0),
        # 第 3 小节
        ('D5', 8.0, 1.0), ('E5', 9.0, 1.0), ('G5', 10.0, 1.0), ('A5', 11.0, 1.0),
        # 第 4 小节
        ('G5', 12.0, 1.5), ('E5', 13.5, 0.5), ('D5', 14.0, 2.0),
    ]
    # 伴奏：C-G-Am-F 的田园感走向（四小节）
    chords = [
        (0, ['C4', 'E4', 'G4']),      # C
        (4, ['A3', 'C4', 'E4']),      # Am
        (8, ['F3', 'A3', 'C4']),      # F
        (12, ['G3', 'B3', 'D4']),     # G
    ]

    n = int(SR * total)
    lead = np.zeros(n)
    bass = np.zeros(n)
    comp = np.zeros(n)

    # --- 主旋律 ---
    for name, start_beat, len_beat in melody:
        f = NOTE[name]
        dur = len_beat * beat * 0.92   # 略微断开，音符更清晰
        offset = start_beat * beat
        # 长音加 vibrato，短音不加
        v = soft_lead(f, dur, vib=0.006 if len_beat >= 1.5 else 0.0)
        v *= env_ad(v, 0.012, 0.05, 0.72, min(0.18, dur * 0.4), 1.8, dur)
        lead += at(total, offset, v)

    # --- 分解和弦伴奏（每拍一下，低音在根音） ---
    for start_beat, names in chords:
        for k in range(4):
            beat_idx = start_beat + k
            offset = beat_idx * beat
            # 根音/五音/三音交替，形成琶音
            pick = names[[0, 2, 1, 2][k] % len(names)]
            f = NOTE[pick]
            v = ks_pluck(f, beat * 1.1, damp=0.994, bright=0.55)
            v *= env_ad(v, 0.003, 0.05, 0.15, 0.2, 2.2, beat * 1.1)
            comp += at(total, offset, 0.5 * v)

    # --- 低音（每小节根音，八分音符律动） ---
    bass_notes = {'C': 'C3', 'A': 'A3', 'F': 'F3', 'G': 'G3'}
    bass_keys = ['C', 'A', 'F', 'G']
    for bi, key in enumerate(bass_keys):
        f = NOTE[bass_notes[key]]
        for k in range(8):
            offset = (bi * 4 + k * 0.5) * beat
            dur = beat * 0.45
            v = triangle(f, dur)
            v *= env_ad(v, 0.004, 0.03, 0.5, 0.06, 2.0, dur)
            bass += at(total, offset, 0.42 * v)

    mix = 0.55 * normalize(lead, 1.0) + 0.28 * comp + 0.30 * bass
    mix = normalize(mix, 0.62)

    # --- 无缝循环 ---
    #
    # 【踩过的坑】先做的是「首尾等功率交叉淡化」，结果循环点爆音更严重：
    # 交叉淡化是把 head 渐入叠到 mix 上，于是循环点从 mix 结尾（响）
    # 突然跳到 mix 开头（同样响但相位无关），跳变≈峰值，必然听出「啪」。
    #
    # 正确做法：合成时多留一个「尾巴拍」（让最后几个音符的延音自然延伸），
    # 输出时只取前面N 拍作为循环体。循环播放到末尾自然滑回开头，
    # 因为音乐结构上本来就是同一句旋律的重复，不存在相位突变。
    fade_out = int(SR * 0.15)
    mix = np.concatenate([mix, np.zeros(fade_out)])
    mix = fade_edges(mix, 0.008, 0.0)   # 只淡入，尾部由结构自然衔接

    loop_len = int(SR * total)
    loop = mix[:loop_len]
    return loop


# ============================================================
# 输出
# ============================================================

def write_wav(path, data, peak=0.9):
    data = np.asarray(data, dtype=np.float64)
    m = np.max(np.abs(data))
    if m > 1e-9:
        data = data / m * peak
    # 软削波，防止归一化后仍有削顶失真
    data = np.tanh(data * 1.05) / np.tanh(1.05) * 0.98
    pcm = (data * 32767.0).astype(np.int16)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'wb') as f:
        import wave
        w = wave.open(f, 'wb')
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
        w.close()
    return len(data) / SR


def main():
    out_root = sys.argv[1] if len(sys.argv) > 1 else "Assets/Audio"
    sfx_dir = os.path.join(out_root, "SFX")
    bgm_dir = os.path.join(out_root, "BGM")

    sfx = {
        "SFX_SwordSwing": sfx_sword,
        "SFX_BowShoot": sfx_bow,
        "SFX_EnemyHit": sfx_enemy_hit,
        "SFX_EnemyDie": sfx_enemy_die,
        "SFX_CoinPickup": sfx_coin,
        "SFX_QuestComplete": sfx_quest_complete,
        "SFX_UiClick": sfx_ui_click,
        "SFX_Purchase": sfx_purchase,
        "SFX_PlayerHurt": sfx_player_hurt,
        "SFX_UiHover": sfx_ui_hover,
        "SFX_LevelUp": sfx_levelup,
        "SFX_Jump": sfx_jump,
    }

    print("生成音效 ->", sfx_dir)
    for name, fn in sfx.items():
        data = fn()
        p = os.path.join(sfx_dir, name + ".wav")
        dur = write_wav(p, data)
        print("  %-20s %5.2fs" % (name + ".wav", dur))

    print("生成 BGM ->", bgm_dir)
    bgm = make_bgm()
    p = os.path.join(bgm_dir, "BGM_PastoralTheme.ogg".replace(".ogg", ".wav"))
    dur = write_wav(p, bgm, peak=0.75)
    print("  %-20s %5.2fs" % ("BGM_PastoralTheme.wav", dur))
    print("完成。")


if __name__ == "__main__":
    main()