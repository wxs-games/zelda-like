# -*- coding: utf-8 -*-
"""Generate the three-table starter workbook (schedule xlsx) for a new game project.

Usage:
  python make_tables.py --project <dir> --manifest <list.json>

manifest JSON schema (produced by the agent after the user confirms the clip list):
{
  "game": "游戏名或类型描述",
  "sheets": [
    {
      "role": "主角 Player",               # 角色分组名
      "cell": "692x833",                  # 该角色画布尺寸（可先写待定）
      "clips": [
        {"name": "Player_Idle", "action": "待机呼吸", "frames": 10,
         "samples": 12, "loop": true, "events": "帧3 呼吸顶点",
         "use": "全关卡待机", "priority": "P0"}
      ]
    }
  ]
}
Output: <project>/Assets/Art/美术资源排期表.xlsx with 3 sheets:
  进度看板 / 资源清单 / 资源规范
"""
import argparse
import json
import sys
from pathlib import Path

import openpyxl
from openpyxl.styles import Alignment, Font, PatternFill
from openpyxl.utils import get_column_letter

HEAD_FILL = PatternFill("solid", fgColor="DDE8DD")
HEAD_FONT = Font(bold=True)
WRAP = Alignment(wrap_text=True, vertical="top")

LIST_COLS = ["角色", "序号", "优先级", "Clip 命名", "动作", "帧数", "Samples",
             "循环", "时长(s)", "动画事件帧", "用途 / 对应关卡", "精灵表路径",
             "Clip 路径", "画布尺寸", "制作状态", "验收状态", "备注"]

SPEC_ROWS = [
    ["单帧画布", "各角色内部统一 cell 尺寸（以定妆图测得为准），同一角色所有动作一致"],
    ["锚点与基线", "Pivot 统一 Bottom Center，脚底留白 7px，水平居中"],
    ["统一朝向", "全部朝右绘制，朝左用 SpriteRenderer.flipX"],
    ["帧命名", "Clip名_两位序号：Player_Run_00.png"],
    ["精灵表", "一个动作一张横排 Sheet，等大网格切图；另存 raw 白底原图留档"],
    ["目录结构", "Assets/Art/<角色>/Sheets（+raw 留档）、Assets/Art/<角色>/Frames/<动作>/"],
    ["循环设置", "Idle / Run / Fall 等循环动作勾 Loop Time，攻击/受击/死亡不循环"],
    ["Samples 与帧率", "循环动作 10-24，爆发动作 15-30；时长 = 帧数 / Samples"],
    ["攻击帧结构", "前摇（可取消）→ 判定 1-3 帧（HitOn/HitOff 事件）→ 后摇"],
    ["Animator 参数", "Speed(float)、IsGrounded(bool)、Attack/Hurt/Dash(Trigger)、IsDead(bool)"],
    ["打断优先级", "Die > Hurt > Attack/Interact > Dash > Jump > Run/Idle"],
    ["位移同步", "关闭 Root Motion，Rigidbody2D 驱动位移；Run 步长 = 时长 x moveSpeed 防打滑"],
    ["纹理导入", "Sprite Mode Multiple，Filter Bilinear，Compression BC7(PC)/ASTC6x6(移动)，关 Mipmap"],
    ["特效独立", "挥砍轨迹/尘土/火花/毒气做独立 FX 精灵表，挂动画事件帧"],
    ["状态流转", "制作状态：未开始 → 制作中 → 已完成；验收状态：待验收 → 通过"],
]


def build(manifest_path: Path, project_dir: Path) -> Path:
    data = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    wb = openpyxl.Workbook()

    # ---- 资源清单 ----
    ws = wb.active
    ws.title = "资源清单"
    ws.append(LIST_COLS)
    for c in ws[1]:
        c.fill, c.font, c.alignment = HEAD_FILL, HEAD_FONT, WRAP
    r = 2
    for group in data["sheets"]:
        for i, clip in enumerate(group["clips"], start=1):
            ws.append([
                group["role"], i, clip.get("priority", "P0"), clip["name"],
                clip.get("action", ""), clip.get("frames", ""), clip.get("samples", ""),
                "是" if clip.get("loop") else "否",
                f"=IF(OR(F{r}=\"\",G{r}=\"\"),\"\",ROUND(F{r}/G{r},2))",
                clip.get("events", ""), clip.get("use", ""),
                f"Assets/Art/{group['role']}/Sheets/{clip['name']}_sheet.png",
                f"Assets/Animations/{group['role']}/{clip['name']}.anim",
                group.get("cell", "待定"), "未开始", "待验收", clip.get("note", ""),
            ])
            r += 1
    widths = [16, 6, 8, 20, 12, 8, 10, 7, 10, 30, 22, 40, 34, 12, 10, 10, 18]
    for idx, w in enumerate(widths, start=1):
        ws.column_dimensions[get_column_letter(idx)].width = w
    for row in ws.iter_rows(min_row=2):
        for c in row:
            c.alignment = WRAP
    ws.freeze_panes = "A2"

    # ---- 进度看板 ----
    dash = wb.create_sheet("进度看板")
    dash["A1"] = "动画资源进度看板（统计公式随清单自动更新）"
    dash["A1"].font = Font(bold=True, size=13)
    dash.append([])
    dash.append(["对象", "动作条数", "总帧数", "已完成条数", "完成率"])
    for c in dash[3]:
        c.fill, c.font = HEAD_FILL, HEAD_FONT
    last = r - 1
    dash.append(["全部", f"=COUNTA(资源清单!D2:D{last})",
                 f"=SUM(资源清单!F2:F{last})",
                 f"=COUNTIF(资源清单!O2:O{last},\"已完成\")",
                 f"=IF(C4=0,\"\",D4/C4)"])
    for idx, w in enumerate([18, 12, 12, 14, 10], start=1):
        dash.column_dimensions[get_column_letter(idx)].width = w

    # ---- 资源规范 ----
    spec = wb.create_sheet("资源规范")
    spec["A1"] = "资源制作规范（帧动画路线）"
    spec["A1"].font = Font(bold=True, size=13)
    spec.append([])
    spec.append(["分类", "规范"])
    for c in spec[3]:
        c.fill, c.font = HEAD_FILL, HEAD_FONT
    for row in SPEC_ROWS:
        spec.append(row)
    spec.column_dimensions["A"].width = 16
    spec.column_dimensions["B"].width = 90
    for row in spec.iter_rows(min_row=4):
        for c in row:
            c.alignment = WRAP

    out = project_dir / "Assets" / "Art"
    out.mkdir(parents=True, exist_ok=True)
    path = out / "美术资源排期表.xlsx"
    wb.save(path)
    return path


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--project", required=True)
    ap.add_argument("--manifest", required=True)
    args = ap.parse_args()
    path = build(Path(args.manifest), Path(args.project))
    print(f"[OK] {path}")


if __name__ == "__main__":
    sys.exit(main())
