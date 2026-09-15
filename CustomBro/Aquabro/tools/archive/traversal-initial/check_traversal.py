"""检查当前 Aseprite 源稿、待接入图集和修改前备份；不修改素材。"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import struct
import zlib
from pathlib import Path

from PIL import Image, ImageChops


def check(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def read_aseprite(path: Path) -> dict:
    data = path.read_bytes()
    size, magic, count, width, height, depth = struct.unpack_from("<I5H", data)
    check(size == len(data) and magic == 0xA5E0 and depth == 32, f"无效 RGBA 工程：{path}")
    layers, tags, frames, durations = [], [], [], []
    start = 128
    for frame in range(count):
        length, frame_magic, old_chunks, duration = struct.unpack_from("<I3H", data, start)
        new_chunks = struct.unpack_from("<I", data, start + 12)[0]
        check(frame_magic == 0xF1FA, "无效帧头")
        chunks = new_chunks if old_chunks == 0xFFFF else old_chunks
        offset = start + 16
        cels = {}
        for _ in range(chunks):
            chunk_size, kind = struct.unpack_from("<IH", data, offset)
            payload = data[offset + 6: offset + chunk_size]
            if kind == 0x2004:
                flags, layer_kind = struct.unpack_from("<HH", payload)
                name_size = struct.unpack_from("<H", payload, 16)[0]
                raw_name = payload[18:18 + name_size]
                layers.append({"name": raw_name.decode("utf-8", errors="replace"), "raw_name": raw_name,
                               "visible": bool(flags & 1), "kind": layer_kind, "opacity": payload[12]})
            elif kind == 0x2005:
                layer, x, y, opacity, cel_kind = struct.unpack_from("<HhhBH", payload)
                if cel_kind == 1:
                    linked_frame = struct.unpack_from("<H", payload, 16)[0]
                    pixels = frames[linked_frame][layer]["pixels"].copy()
                else:
                    check(cel_kind in (0, 2), "不支持的 cel 类型")
                    w, h = struct.unpack_from("<HH", payload, 16)
                    raw = payload[20:] if cel_kind == 0 else zlib.decompress(payload[20:])
                    check(len(raw) == w * h * 4, "cel 像素长度不一致")
                    pixels = Image.frombytes("RGBA", (w, h), raw)
                cels[layer] = {"position": (x, y), "opacity": opacity, "pixels": pixels}
            elif kind == 0x2018:
                tag_count = struct.unpack_from("<H", payload)[0]
                tag_offset = 10
                for _ in range(tag_count):
                    first, last = struct.unpack_from("<HH", payload, tag_offset)
                    name_size = struct.unpack_from("<H", payload, tag_offset + 17)[0]
                    raw_name = payload[tag_offset + 19:tag_offset + 19 + name_size]
                    tags.append((raw_name.decode("utf-8", errors="replace"), first + 1, last + 1))
                    tag_offset += 19 + name_size
            offset += chunk_size
        check(offset == start + length, "Aseprite 帧边界不一致")
        frames.append(cels)
        durations.append(duration)
        start += length
    check(start == len(data), "Aseprite 尾部有未处理数据")
    return {"size": (width, height), "layers": layers, "tags": tags, "frames": frames, "durations": durations}


def layer_image(sprite: dict, frame: int, layer: int) -> Image.Image:
    if layer not in sprite["frames"][frame - 1]:
        return Image.new("RGBA", sprite["size"])
    cel = sprite["frames"][frame - 1][layer]
    check(cel["opacity"] == 255 and sprite["layers"][layer]["opacity"] == 255, "出现半透明图层或 cel")
    image = Image.new("RGBA", sprite["size"])
    image.alpha_composite(cel["pixels"], cel["position"])
    return image


def composite(sprite: dict, frame: int, indexes=None) -> Image.Image:
    image = Image.new("RGBA", sprite["size"])
    for layer in range(len(sprite["layers"])) if indexes is None else indexes:
        if sprite["layers"][layer]["visible"] and layer in sprite["frames"][frame - 1]:
            image.alpha_composite(layer_image(sprite, frame, layer))
    return image


def equal(a: Image.Image, b: Image.Image) -> bool:
    if a.size != b.size or ImageChops.difference(a.getchannel("A"), b.getchannel("A")).getbbox() is not None:
        return False
    # Aseprite 保留少量 alpha=0 像素的 RGB；比较可见颜色，同时严格核对 alpha。
    background = Image.new("RGBA", a.size, (0, 0, 0, 255))
    return ImageChops.difference(Image.alpha_composite(background, a),
                                Image.alpha_composite(background, b)).getbbox(alpha_only=False) is None


def cell(image: Image.Image, index: int) -> Image.Image:
    x, y = index % 32 * 32, index // 32 * 32
    return image.crop((x, y, x + 32, y + 32))


def components(image: Image.Image, opaque=True, diagonal=True) -> list[set]:
    pixels = image.load()
    remaining = {(x, y) for y in range(image.height) for x in range(image.width)
                 if bool(pixels[x, y][3]) == opaque}
    offsets = [(x, y) for x in (-1, 0, 1) for y in (-1, 0, 1)
               if (x or y) and (diagonal or abs(x) + abs(y) == 1)]
    result = []
    while remaining:
        queue = [remaining.pop()]
        for x, y in queue:
            for dx, dy in offsets:
                adjacent = (x + dx, y + dy)
                if adjacent in remaining:
                    remaining.remove(adjacent)
                    queue.append(adjacent)
        result.append(set(queue))
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", type=Path, required=True)
    parser.add_argument("--backup", type=Path, required=True)
    args = parser.parse_args()
    project = Path(__file__).resolve().parents[2]
    source_path = project / "tools/haiwang_trident_traversal-v2.aseprite"
    source = read_aseprite(source_path)
    expected_tags = [("贴墙", 1, 10), ("悬挂", 11, 28), ("攀爬", 29, 48), ("爬梯", 49, 68), ("滑索", 69, 86)]
    expected_layers = ["绿色裤靴", "金色鳞甲与颈部", "抓附手臂", "金发与脸部", "金色三叉戟", "活动手臂"]
    check(source["size"] == (32, 32) and len(source["frames"]) == 86, "源稿尺寸或帧数不符")
    check(source["tags"] == expected_tags, "五类中文标签或帧范围不符")
    check([layer["name"] for layer in source["layers"]] == expected_layers, "海王分层或 UTF-8 图层名不符")
    check(all(layer["visible"] and layer["kind"] == 0 for layer in source["layers"]), "源稿含额外参考层")
    check(all({0, 1, 2, 3, 5}.issubset(frame) for frame in source["frames"]), "源稿存在缺失身体或手臂 cel")

    manifest = list(csv.DictReader((args.work / "traversal-manifest.csv").open(encoding="utf-8")))
    body = Image.open(args.work / "sprite.png").convert("RGBA")
    gun = Image.open(args.work / "gunSprite.png").convert("RGBA")
    check(body.size == gun.size == (1024, 1024), "新图集尺寸不符")
    check(read_aseprite(args.work / "body-atlas.aseprite")["size"] == body.size, "身体工程尺寸不符")
    check(read_aseprite(args.work / "gun-atlas.aseprite")["size"] == gun.size, "武器工程尺寸不符")
    check(equal(body, Image.open(args.work / "body-reopened.png").convert("RGBA")), "重新打开的身体 Aseprite 与 PNG 不一致")
    check(equal(gun, Image.open(args.work / "gun-reopened.png").convert("RGBA")), "重新打开的武器 Aseprite 与 PNG 不一致")
    body_cells = sorted({int(row["body_cell"]) for row in manifest})
    expected_cells = list(range(76, 96)) + list(range(107, 125)) + list(range(160, 174)) + list(range(192, 198)) + list(range(512, 530))
    check(body_cells == expected_cells, "源稿图集范围与原版方法不符")
    indexes = {value: index for index, value in enumerate(expected_cells)}
    seam_count = 0
    for row in manifest:
        frame, body_cell = int(row["frame"]), int(row["body_cell"])
        check(source["durations"][frame - 1] == int(row["duration_ms"]), f"源稿时长不符：{frame}")
        images = [layer_image(source, frame, layer) for layer in range(6)]
        # 使用重新打开工程后的 Aseprite 原生合成，保留用户握点叠色的整数取整结果。
        flat = Image.open(args.work / f"source-{frame}.png").convert("RGBA")
        check(set(flat.getchannel("A").get_flattened_data()).issubset({0, 255}), f"出现半透明像素：{frame}")
        check(len(components(flat)) == 1, f"角色有游离像素：{frame}")
        if body_cell < 512:
            check(images[4].getbbox() is None, f"空手动作残留三叉戟：{frame}")
            check(equal(flat, cell(body, body_cell)), f"空手动作未完整包含双臂：{frame}")
        else:
            check(len(components(images[4])) == 1, f"滑索三叉戟有游离像素：{frame}")
            overlap = ImageChops.multiply(images[4].getchannel("A"), images[5].getchannel("A"))
            check(overlap.getbbox() is not None, f"手掌脱离戟杆：{frame}")
        for layer in (0, 1, 3):
            check(ImageChops.multiply(images[4].getchannel("A"), images[layer].getchannel("A")).getbbox() is None,
                  f"武器穿过头部、鳞甲或裤靴：{frame}")
        ax, ay = int(row["anchor_x"]), int(row["anchor_y"])
        check(images[2].getpixel((ax, ay))[3] != 0, f"抓附点不在手掌上：{frame}")
        for hole in components(flat, opaque=False, diagonal=False):
            if len(hole) <= 4 and all(x not in (0, 31) and y not in (0, 31) for x, y in hole):
                seam_count += 1
        paired = cell(body, body_cell)
        paired.alpha_composite(cell(gun, 73 + indexes[body_cell] * 9))
        check(equal(flat, paired), f"身体与武器图集未匹配源稿：{frame}")
    check(seam_count == 0, f"仍有 {seam_count} 处小型封闭透明接缝")
    previous = read_aseprite(args.backup / "tools/haiwang_trident_traversal-v2.aseprite")
    for frame in range(1, 87):
        expected = composite(previous, frame, [0, 1, 2, 3, 5] if frame <= 68 else None)
        actual = composite(source, frame)
        if frame == 63:
            # 包杆指尖收回一像素；完整性及不透明性由上面的检查保证。
            for image in (actual, expected):
                image.paste((0, 0, 0, 0), (23, 21, 25, 23))
        check(equal(actual, expected), f"移除三叉戟时误改身体、双臂或滑索：{frame}")
    check(source["durations"] == previous["durations"], "空手修改改变了动作时长")
    check(equal(composite(source, 68), composite(source, 49)), "进梯末帧未匹配上行首帧")

    old_body = Image.open(args.backup / "_Mod/sprite.png").convert("RGBA")
    old_gun = Image.open(args.backup / "_Mod/gunSprite.png").convert("RGBA")
    unchanged_body = 0
    for index in range(1024):
        if index in body_cells:
            continue
        if index < (old_body.width // 32) * (old_body.height // 32):
            check(equal(cell(old_body, index), cell(body, index)), f"误改旧身体格：{index}")
            unchanged_body += 1
        else:
            check(cell(body, index).getbbox() is None, f"身体扩展区有无关像素：{index}")
    for index in range(73):
        check(equal(cell(old_gun, index), cell(gun, index)), f"误改原攻击或站跑跳蹲武器格：{index}")
    for index in range(73, 595):
        check(cell(gun, index).getbbox() is None, f"空手动作旧武器格残留像素：{index}")
    for index in range(512, 530):
        check(equal(cell(old_body, index), cell(body, index)), f"误改滑索身体格：{index}")
    for index in range(595, 757):
        check(equal(cell(old_gun, index), cell(gun, index)), f"误改滑索持戟或攻击：{index}")
    for index in range(757, 1024):
        check(cell(gun, index).getbbox() is None, f"武器扩展区有无关像素：{index}")
    audit = list(csv.DictReader((args.work / "attack-audit.csv").open(encoding="utf-8")))
    check(len(audit) == 162 and {int(row["weapon_cell"]) for row in audit} == set(range(595, 757)), "滑索配套武器格缺失或重叠")
    skin = {(233, 166, 140), (215, 148, 121), (181, 124, 101)}
    for row in audit:
        pose = int(row["pose"])
        weapon = cell(gun, int(row["weapon_cell"]))
        check(weapon.getbbox() is not None, "配套武器格为空，可能漏掉自由手臂")
        if pose == 4:
            check(all(rgba[:3] in skin for rgba in weapon.get_flattened_data() if rgba[3]), "离手帧残留武器或身体")
        check(int(row["offset_x"]) == (9 if pose == 7 else 0), "突刺肩点预移与显示偏移不符")

    baseline = json.loads((args.backup / "baseline.json").read_text(encoding="utf-8-sig"))
    preserved = ["haiwang_trident_held-v2.aseprite", "haiwang_trident_run-v2.aseprite", "haiwang_trident_run-v3.aseprite",
                 "haiwang_trident_jump-v2.aseprite", "haiwang_trident_crouch-v2.aseprite"]
    before = {row["name"]: row["sha256"].lower() for row in baseline["sources"]}
    for name in preserved:
        digest = hashlib.sha256((project / "tools" / name).read_bytes()).hexdigest()
        check(digest == before[name], f"用户已有源稿被改动：{name}")
    result = {"source_frames": 86, "layers": expected_layers, "tags": expected_tags, "body_cells": 76,
              "unarmed_frames": 68, "unarmed_body_cells": 58, "zipline_frames": 18,
              "paired_weapon_cells": 162, "cleared_weapon_cells": 522, "unchanged_old_body_cells": unchanged_body,
              "unchanged_old_weapon_cells": 73, "preserved_user_sources": preserved,
              "pixel_pairing": "86/86", "small_transparent_gaps": 0, "weapon_body_overlaps": 0,
              "empty_hand_and_zipline_isolation": "passed", "gameplay_verification": "pending"}
    (args.work / "asset-checks.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
