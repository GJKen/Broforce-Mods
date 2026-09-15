"""UpscaleGameHeroSprites.py — 批量放大 GameAssets/hero 下的全部贴图(最近邻,倍数可调)。

用途:
    做自定义 bro 前想高清预览/参考英雄整版图、或其他需要大尺寸贴图素材的场合,
    把官方导出的 hero 贴图整体放大(默认 2 倍),输出到独立文件夹,不动原文件。

用法:
    python CustomBro-Knowledge/templates/UpscaleGameHeroSprites.py [scale [--out <dir>]]
    例: 3 倍放大并输出到默认目录 ->  python .../UpscaleGameHeroSprites.py 3

默认:
    scale = 2 (最近邻;传 2/3/4... 即可换倍数)
    输入  = <repo>/Broforce_src/GameAssets/hero
    输出  = <repo>/Broforce_src/GameAssets/hero_<scale>x   (不存在则创建,输出目录随倍数走)

命名约定:
    原文件形如 `00Bro7_anim_1024x512.png`,文件名末尾 `_宽x高` 是导出时的尺寸后缀。
    放大后按同样的约定更新为新尺寸,如 `_1024x512` -> `_2048x1024`,方便后续按尺寸识别。
"""

import re
import sys
from pathlib import Path

from PIL import Image

REPO_ROOT = Path(__file__).resolve().parents[2]  # <repo>/CustomBro-Knowledge/templates -> <repo>
SRC_DIR = REPO_ROOT / "Broforce_src" / "GameAssets" / "hero"


def _default_out_dir(scale: int) -> Path:
    """输出目录随倍数走: hero_2x / hero_3x / ..."""
    return REPO_ROOT / "Broforce_src" / "GameAssets" / f"hero_{scale}x"

# 文件名末尾的尺寸后缀,如 1024x512 / 64x64
_DIM_RE = re.compile(r"_(\d{2,5})x(\d{2,5})(?=\.png$)", re.IGNORECASE)


def new_filename(name: str, scale: int) -> str:
    """按项目约定同步新的尺寸后缀: Name_1024x512.png -> Name_2048x1024.png """
    m = _DIM_RE.search(name)
    if m:
        w, h = int(m.group(1)), int(m.group(2))
        return name[: m.start()] + f"_{w * scale}x{h * scale}" + name[m.end():]
    # 没有尺寸后缀的也没关系,保持原名
    return name


def main(argv) -> int:
    scale = int(argv[0]) if len(argv) > 0 and argv[0].isdigit() else 2
    out_dir = Path(argv[argv.index("--out") + 1]) if "--out" in argv else _default_out_dir(scale)
    out_dir.mkdir(parents=True, exist_ok=True)

    pngs = sorted(SRC_DIR.glob("*.png"))
    if not pngs:
        print(f"[error] 输入目录没有 png: {SRC_DIR}")
        return 1

    done, skipped, failed = 0, 0, []
    for src in pngs:
        try:
            with Image.open(src) as im:
                im = im.convert("RGBA") if im.mode not in ("RGBA", "P", "LA") else im
                out = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
                dst = out_dir / new_filename(src.name, scale)
                out.save(dst, "PNG", optimize=False)
            done += 1
        except Exception as exc:  # noqa: BLE001
            failed.append((src.name, str(exc)))

    print(f"输入目录 : {SRC_DIR}")
    print(f"输出目录 : {out_dir}")
    print(f"放大倍数 : {scale}x (最近邻)")
    print(f"成功 {done} / 失败 {len(failed)} / 跳过 {skipped}")
    for name, err in failed:
        print(f"  FAIL {name}: {err}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))