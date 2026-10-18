# -*- coding: utf-8 -*-
"""音频质量检查：削顶、静音段、峰值、循环接缝。"""
import sys
import os
import wave
import numpy as np

SR = 44100


def load(path):
    with wave.open(path, 'rb') as w:
        assert w.getnchannels() == 1, "期望单声道"
        assert w.getsampwidth() == 2
        assert w.getframerate() == SR
        n = w.getnframes()
        data = np.frombuffer(w.readframes(n), dtype=np.int16).astype(np.float64) / 32768.0
    return data


def check(path):
    x = load(path)
    name = os.path.basename(path)
    peak = np.max(np.abs(x))
    rms = np.sqrt(np.mean(x ** 2))
    # 削顶检测：连续满幅样本
    clip = np.sum(np.abs(x) >= 0.999)
    # 静音检测：最长连续近零段（10ms 为阈值）
    frame = int(SR * 0.01)
    frames = x[:len(x) // frame * frame].reshape(-1, frame)
    quiet = np.max(np.abs(frames), axis=1) < 0.005
    max_quiet = 0
    run = 0
    for q in quiet:
        run = run + 1 if q else 0
        max_quiet = max(max_quiet, run)
    # DC 偏移
    dc = np.mean(x)
    return {
        "name": name,
        "dur": len(x) / SR,
        "peak": peak,
        "rms": rms,
        "clip": clip,
        "maxQuietMs": max_quiet * 10,
        "dc": dc,
    }


def seam_check(path):
    """
    BGM 循环接缝：模拟真实循环播放（把音频首尾相接播放两遍），
    在拼接点附近取窗口，检验是否有瞬时跳变（爆音）。

    注意不能直接比较「最后一帧 vs 第一帧」——音乐的首尾相位本来就不同，
    直接相减必然得到接近峰值的差值，那是误判。真实判据是：拼接点附近的
    信号是否平滑，即相邻样本的跳变量是否与音频内部典型跳变量同量级。
    """
    x = load(path)
    looped = np.concatenate([x, x])          # 真实循环播放的样子
    n = len(x)
    win = int(SR * 0.05)

    # 拼接点位于索引 n。取 [n-win, n+win] 区间
    seg = looped[n - win: n + win]
    seam_jump = np.max(np.abs(np.diff(seg)))  # 拼接点处的最大逐样本跳变

    # 对比：音频内部的典型逐样本跳变（取几个随机位置的最大值作基准）
    rng = np.random.default_rng(1)
    typical = []
    for _ in range(200):
        i = rng.integers(win, n - win)
        seg2 = looped[i:i + 2 * win]
        typical.append(np.max(np.abs(np.diff(seg2))))
    typical_max = float(np.max(typical))
    typical_med = float(np.median(typical))

    return seam_jump, typical_max, typical_med


def main():
    root = sys.argv[1]
    print("%-24s %7s %8s %8s %6s %9s %8s" % ("文件", "时长", "峰值", "RMS", "削顶", "最长静音", "DC偏移"))
    print("-" * 80)
    problems = []
    for sub in ("SFX", "BGM"):
        d = os.path.join(root, sub)
        if not os.path.isdir(d):
            continue
        for f in sorted(os.listdir(d)):
            if not f.lower().endswith(".wav"):
                continue
            p = os.path.join(d, f)
            r = check(p)
            flag = ""
            if r["peak"] >= 0.999:
                flag += " 峰值触顶"
            if r["clip"] > 3:
                flag += " 削顶%d样本" % r["clip"]
            if r["rms"] < 0.02:
                flag += " 几乎无声"
            if abs(r["dc"]) > 0.02:
                flag += " DC偏移大"
            print("%-24s %6.2fs %8.4f %8.4f %6d %7dms %8.4f%s" % (
                r["name"], r["dur"], r["peak"], r["rms"], r["clip"],
                r["maxQuietMs"], r["dc"], flag))
            if flag:
                problems.append(r["name"] + flag)

    bgm = os.path.join(root, "BGM", "BGM_PastoralTheme.wav")
    if os.path.exists(bgm):
        seam, tmax, tmed = seam_check(bgm)
        print("\n[BGM 循环接缝] 模拟真实循环播放：")
        print("  拼接点最大逐样本跳变 = %.5f" % seam)
        print("  音频内部最大跳变     = %.5f  (基准上限)" % tmax)
        print("  音频内部中位跳变     = %.5f  (典型值)" % tmed)
        # 判据：拼接点跳变不应显著超过音频内部已有的最大跳变
        if seam <= tmax * 1.05:
            print("  结论：接缝在正常范围内，不会听出爆音")
        else:
            print("  结论：接缝跳变超出音频内部范围，可能听出爆音")
            problems.append("BGM 接缝跳变 %.5f > 内部上限 %.5f" % (seam, tmax))

    print("\n结论：%s" % ("全部通过" if not problems else "有问题 -> " + "; ".join(problems)))


if __name__ == "__main__":
    main()