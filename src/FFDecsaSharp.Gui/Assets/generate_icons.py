#!/usr/bin/env python3
"""
从 icon.svg 一键生成所有图标格式:
  - app-icon.png  (1024×1024, 窗口/托盘图标)
  - app-icon.ico  (多尺寸, Windows exe 图标)
  - app-icon.icns (macOS .app 图标)

渲染引擎: rsvg-convert (pango + fontconfig, 正确使用系统字体)
依赖: brew install librsvg / pip install pillow
macOS ICNS 需要系统自带的 iconutil 命令。
"""
from __future__ import annotations

import shutil
import subprocess
import sys
import io
import tempfile
from pathlib import Path


def find_rsvg() -> str | None:
    """Find rsvg-convert binary."""
    path = shutil.which("rsvg-convert")
    if path:
        return path
    # Check common brew locations
    for p in ["/opt/homebrew/bin/rsvg-convert", "/usr/local/bin/rsvg-convert"]:
        if p.exists():
            return str(p)
    return None


def rsvg_render(svg: Path, output: Path, size: int) -> None:
    """Render SVG to PNG at given size using rsvg-convert."""
    subprocess.run(
        ["rsvg-convert", "-w", str(size), "-h", str(size),
         str(svg), "-o", str(output)],
        check=True, capture_output=True,
    )


def rsvg_render_bytes(svg: Path, size: int) -> bytes:
    """Render SVG to PNG bytes at given size."""
    r = subprocess.run(
        ["rsvg-convert", "-w", str(size), "-h", str(size), str(svg)],
        check=True, capture_output=True,
    )
    return r.stdout


def main() -> None:
    assets = Path(__file__).resolve().parent
    svg = assets / "icon.svg"

    if not svg.exists():
        sys.exit(f"错误: 找不到 {svg}")

    rsvg = find_rsvg()
    if not rsvg:
        sys.exit("错误: 未找到 rsvg-convert，请安装:  brew install librsvg")

    try:
        from PIL import Image
    except ImportError:
        sys.exit("错误: 缺少 pillow，请运行:  pip install pillow")

    png_size = 1024

    # ── 1. PNG ──────────────────────────────────────────────
    png_path = assets / "app-icon.png"
    rsvg_render(svg, png_path, png_size)
    print(f"  app-icon.png  ({png_size}×{png_size})")

    # ── 2. ICO ──────────────────────────────────────────────
    ico_path = assets / "app-icon.ico"
    data = rsvg_render_bytes(svg, png_size)
    img = Image.open(io.BytesIO(data))
    img.save(str(ico_path), format="ICO",
             sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    print(f"  app-icon.ico  (16–256)")

    # ── 3. ICNS (macOS) ────────────────────────────────────
    icns_path = assets / "app-icon.icns"
    iconset = assets / "app-icon.iconset"

    icns_sizes = {
        "icon_16x16.png":        16,
        "icon_16x16@2x.png":     32,
        "icon_32x32.png":        32,
        "icon_32x32@2x.png":     64,
        "icon_128x128.png":     128,
        "icon_128x128@2x.png":  256,
        "icon_256x256.png":     256,
        "icon_256x256@2x.png":  512,
        "icon_512x512.png":     512,
        "icon_512x512@2x.png": 1024,
    }

    if shutil.which("iconutil"):
        iconset.mkdir(exist_ok=True)
        for name, size in icns_sizes.items():
            rsvg_render(svg, iconset / name, size)
        r = subprocess.run(
            ["iconutil", "-c", "icns", str(iconset), "-o", str(icns_path)],
            capture_output=True, text=True,
        )
        shutil.rmtree(iconset, ignore_errors=True)
        if r.returncode != 0:
            print(f"  ⚠ iconutil 失败: {r.stderr.strip()}", file=sys.stderr)
        else:
            print(f"  app-icon.icns (10 sizes)")
    else:
        print("  ⚠ 跳过 app-icon.icns (未找到 iconutil，仅 macOS 可用)")

    # ── 汇总 ───────────────────────────────────────────────
    print("\n  生成完毕:")
    for f in sorted(assets.glob("app-icon.*")):
        kb = f.stat().st_size / 1024
        print(f"    {f.name:20s}  {kb:7.1f} KB")


if __name__ == "__main__":
    main()
