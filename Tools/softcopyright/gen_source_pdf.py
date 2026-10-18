# -*- coding: utf-8 -*-
"""
生成软著申请所需的「源程序 PDF」——零第三方依赖实现。

为什么不用 reportlab / fpdf：
    本机 pip 通道不可用，而 PDF 内置了标准 14 字体（Courier 等），
    中文字体也可用 Adobe-GB1 字符集的标准 CJK 字体（STSong-Light）直接引用、无需嵌文件。
    因此手写 PDF 语法即可完成，脚本在任意 Python 3 环境都能跑，复现性最好。

版权保护中心对鉴别材料的硬性要求：
  - 源程序：前 30 页 + 后 30 页，共 60 页；每页不少于 50 行；不足 60 页全部提交。
  - 页眉必须标注软件名称 + 版本号，且与申请表、说明书三处完全一致，否则驳回。
  - 必须是可复制的代码文本，不能是截图。
  - 不得出现与申请人无关的第三方版权声明或署名。

代码选取策略：
  只取 Assets/Scripts 下的运行时脚本，排除 Editor/ 目录。
  理由：Editor/ 是构建期工具（数据库生成、出包脚本），不随 APP 发布，
  放进鉴别材料容易被质疑「这些代码不会出现在成品里」。
"""
import glob
import os

PROJECT = r"E:\desktop\UnityXiangMu\zeldaLike\zeldaLike"
OUT = os.path.join(PROJECT, "Docs", "softcopyright", "源程序.pdf")

# —— 以下三项必须与申请表、说明书完全一致，改一处就要同步三处 ——
SOFTWARE_NAME = "塞尔达风格动作冒险游戏软件"
VERSION = "V1.0"
COPYRIGHT_LINE = "Copyright (c) 2026 StarEcoStudio"
HEADER_TEXT = f"{SOFTWARE_NAME} {VERSION}"

LINES_PER_PAGE = 50
PAGES_BEFORE = 30
PAGES_AFTER = 30

PAGE_W, PAGE_H = 595.27, 841.89          # A4
MARGIN_L = 56.0
MARGIN_R = 42.0
HEADER_Y = PAGE_H - 42.0
FOOTER_Y = 34.0
BODY_TOP = PAGE_H - 56.0
BODY_BOTTOM = 52.0
FONT_SIZE = 7.6
LINE_HEIGHT = (BODY_TOP - BODY_BOTTOM) / LINES_PER_PAGE
MAX_CHARS = 112

# Courier：PDF 标准 14 字体，中文用 Adobe-GB1 标准 CJK 字体
FONT_COURIER = "/F1"
FONT_CJK = "/F2"


def collect_runtime_scripts():
    """按系统主干优先排序，保证代码连续且体现架构。"""
    files = glob.glob(os.path.join(PROJECT, "Assets", "Scripts", "**", "*.cs"), recursive=True)
    runtime = []
    for f in files:
        rel = os.path.relpath(f, PROJECT).replace(os.sep, "/")
        if rel.startswith("Assets/Scripts/Editor/"):
            continue
        runtime.append(f)

    def key(f):
        rel = os.path.relpath(f, PROJECT).replace(os.sep, "/")
        if "/Core/" in rel:
            tier = 0          # 系统主干：持久化、输入、数据库
        elif rel.count("/") >= 3:
            tier = 1          # 各功能模块
        else:
            tier = 2
        return (tier, rel)

    return sorted(runtime, key=key)


def read_code_lines(files):
    out = []
    for f in files:
        rel = os.path.relpath(f, PROJECT).replace(os.sep, "/")
        try:
            text = open(f, encoding="utf-8-sig", errors="ignore").read()
        except Exception as exc:
            print(f"  跳过无法读取的文件 {rel}: {exc}")
            continue
        out.append(f"// ===== File: {rel} =====")
        out.extend(text.splitlines())
        out.append("")
    return out


def is_substantive(line):
    """有效行：空行与纯注释行不计入——版权中心核验的是有效代码行数。"""
    s = line.strip()
    if not s:
        return False
    if s.startswith("//") or s.startswith("/*") or s.startswith("*"):
        return False
    return True


def paginate(all_lines):
    pages, cur = [], []
    for line in all_lines:
        if not is_substantive(line):
            if len(cur) < LINES_PER_PAGE:
                cur.append(line)     # 噪声行保留，但不占有效行额度
            continue
        cur.append(line)
        if len(cur) >= LINES_PER_PAGE:
            pages.append(cur)
            cur = []
    if cur:
        pages.append(cur)
    return pages


def esc_ascii(s):
    """Courier 用：剔除非 ASCII（WinAnsi 无对应字形，保留会变豆腐块）。"""
    safe = "".join(ch if 32 <= ord(ch) < 127 else " " for ch in s)
    safe = safe.replace("\t", "    ")[:MAX_CHARS]
    return safe.replace("\\", r"\\").replace("(", r"\(").replace(")", r"\)")


def hex_utf16(s):
    """CJK 字体用：编码为 UniGB-UCS2-H 的 UTF-16BE 十六进制串。"""
    data = s.encode("utf-16-be")
    return "<" + data.hex().upper() + ">"


def draw_text(font, size, x, y, text, gray=0.0):
    if font == FONT_CJK:
        body = hex_utf16(text)
    else:
        body = "(" + esc_ascii(text) + ")"
    return f"BT {font} {size} Tf {gray} g {x:.2f} {y:.2f} Td {body} Tj ET"


def build_pdf(pages):
    objs = []
    objs.append(b"<< /Type /Catalog /Pages 2 0 R >>")
    objs.append(None)                 # 2 = Pages，末尾回填
    kids = []

    total = len(pages)
    for idx, lines in enumerate(pages):
        page_no = idx + 1
        parts = [
            draw_text(FONT_CJK, 8, MARGIN_L, HEADER_Y, HEADER_TEXT, 0.45),
            draw_text(FONT_CJK, 8, PAGE_W - MARGIN_R - 46, HEADER_Y,
                      f"第 {page_no} 页 / 共 {total} 页", 0.45),
            draw_text(FONT_COURIER, 7, MARGIN_L, FOOTER_Y, COPYRIGHT_LINE, 0.45),
        ]
        y = BODY_TOP
        for ln in lines:
            # 含中文的行整体走 CJK 字体（ASCII 也能正常显示），其余用 Courier 等宽对齐
            font = FONT_CJK if any(ord(ch) > 127 for ch in ln) else FONT_COURIER
            parts.append(draw_text(font, FONT_SIZE, MARGIN_L, y, ln, 0.0))
            y -= LINE_HEIGHT

        stream = "\n".join(parts).encode("latin-1", "replace")

        page_obj = len(objs) + 1
        content_obj = len(objs) + 2
        # 字体对象在所有页面对象之后追加：Courier = 3 + total*2，CJK 再 +1。
        # 这个编号必须写对，写错会让中文页眉与中文注释整段渲染不出来（而不是报错，静默失败）。
        courier_obj = 3 + total * 2
        cjk_obj = courier_obj + 1
        objs.append(
            f"<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 {courier_obj} 0 R "
            f"/F2 {cjk_obj} 0 R >> >> "
            f"/MediaBox [0 0 {PAGE_W:.2f} {PAGE_H:.2f}] /Contents {content_obj} 0 R >>".encode()
        )
        objs.append(b"<< /Length " + str(len(stream)).encode() + b" >>\nstream\n" + stream + b"\nendstream")
        kids.append(page_obj)

    objs.append(b"<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>")

    # CJK 标准字体：直接引用 Adobe-GB1 集合，无需嵌入字体文件
    objs.append(
        ("<< /Type /Font /Subtype /Type0 /BaseFont /STSong-Light /Encoding /UniGB-UCS2-H "
         "/DescendantFonts [<< /Type /Font /Subtype /CIDFontType0 /BaseFont /STSong-Light "
         "/CIDSystemInfo << /Registry (Adobe) /Ordering (GB1) /Supplement 2 >> "
         "/FontDescriptor << /Type /FontDescriptor /FontName /STSong-Light /Flags 4 "
         "/FontBBox [-25 -254 1000 880] /ItalicAngle 0 /Ascent 880 /Descent -254 "
         "/CapHeight 731 /StemV 58 >> /DW 1000 >>] >>").encode()
    )

    objs[1] = (
        b"<< /Type /Pages /Kids [" + b" ".join(f"{i} 0 R".encode() for i in kids)
        + b"] /Count " + str(total).encode() + b" >>"
    )

    out = bytearray(b"%PDF-1.4\n%\xe2\xe3\xcf\xd3\n")
    offsets = [0] * (len(objs) + 1)
    for i, body in enumerate(objs, start=1):
        offsets[i] = len(out)
        out += f"{i} 0 obj\n".encode() + body + b"\nendobj\n"

    xref = len(out)
    n = len(objs) + 1
    out += f"xref\n0 {n}\n".encode()
    out += b"0000000000 65535 f \n"
    for i in range(1, n):
        out += f"{offsets[i]:010d} 00000 n \n".encode()
    out += f"trailer\n<< /Size {n} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n".encode()
    return bytes(out)


def main():
    files = collect_runtime_scripts()
    print(f"运行时脚本：{len(files)} 个")

    all_lines = read_code_lines(files)
    print(f"总行数（含注释空行）：{len(all_lines)}")

    pages = paginate(all_lines)
    print(f"可排版页数：{len(pages)}")

    need = PAGES_BEFORE + PAGES_AFTER
    if len(pages) < need:
        print(f"注意：仅 {len(pages)} 页 < 要求的 {need} 页，将提交全部页面")
        chosen = pages
    else:
        chosen = pages[:PAGES_BEFORE] + pages[-PAGES_AFTER:]
    print(f"实际输出：{len(chosen)} 页（前 {PAGES_BEFORE} + 后 {PAGES_AFTER}）")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "wb") as fh:
        fh.write(build_pdf(chosen))
    print(f"已生成：{OUT}（{os.path.getsize(OUT) / 1024:.0f} KB）")


if __name__ == "__main__":
    main()