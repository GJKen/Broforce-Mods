# -*- coding: utf-8 -*-
"""从 Broforce 的 Unity asset bundle 里导出官方贴图(角色动作图 / 枪图 / 头像 / 图标)。

原理
----
Broforce 的官方美术全在:
    <游戏>\\Broforce_beta_Data\\StreamingAssets\\*.assetbundle
其中 sharedtextures.assetbundle(约 29MB)装的正是全部角色与道具贴图。
它是标准 Unity 资源包,用 UnityPy 就能遍历里面的 Texture2D 并还原成 PNG。

角色主贴图规格(实测):
    body     1024x512   32 列 x 16 行、每帧 32x32、共 512 帧
    gun      1024x64    32 列 x  2 行、每格 32x32、共  64 格
    avatar   128x64     4 格 32x64,内容落在每格的 row 27~63
    cutscene 256x256
    icon     16x16      特技图标

安装
----
    python -m pip install UnityPy

用法
----
    python ExportGameSprites.py                      # 默认导出 sharedtextures
    python ExportGameSprites.py --bundle <path>      # 指定别的 bundle
    python ExportGameSprites.py --all                # 不筛尺寸,全导出(含 4K 视差图,很占地方)

注意
----
导出的官方素材**只能自用**(做参考、改色、做 mod 的底图)。
不要把导出的官方贴图打包进对外分发的 mod —— 那是别人的版权素材。
"""
import argparse
import csv
import os
import re

import UnityPy

DEFAULT_BUNDLE = (r'E:\SteamLibrary\steamapps\common\Broforce'
                  r'\Broforce_beta_Data\StreamingAssets\sharedtextures.assetbundle')
# 默认落到仓库里的 Broforce_src\GameAssets\
_HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_OUT = os.path.normpath(os.path.join(_HERE, '..', '..', 'Broforce_src', 'GameAssets'))


def classify(w, h, keep_all):
    """按尺寸给贴图分类。尺寸相同但用途不同的(如 128x64 里混着锯片/栅栏)
    不在这里区分,靠 index.csv 里的名字筛。"""
    if keep_all:
        return 'all'
    if w == 1024 and h in (512, 256, 128, 64, 32):
        return 'hero'          # 角色动作图 / 枪图
    if (w, h) == (128, 64):
        return 'avatar'
    if (w, h) == (256, 256):
        return 'cutscene'
    if (w, h) == (16, 16):
        return 'icon'
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--bundle', default=DEFAULT_BUNDLE)
    ap.add_argument('--out', default=DEFAULT_OUT)
    ap.add_argument('--all', action='store_true', help='不筛尺寸,全部导出')
    args = ap.parse_args()

    if not os.path.exists(args.bundle):
        raise SystemExit('找不到 bundle: %s' % args.bundle)

    env = UnityPy.load(args.bundle)
    rows, ok, fail = [], 0, 0
    used = {}                    # 路径 -> 已用过几次(同名同尺寸的贴图会撞车)
    for obj in env.objects:
        if obj.type.name != 'Texture2D':
            continue
        d = obj.read()
        cls = classify(d.m_Width, d.m_Height, args.all)
        if not cls:
            continue
        # 名字里有空格/特殊字符,落盘前先洗一遍
        safe = re.sub(r'[^A-Za-z0-9_.-]', '_', d.m_Name).strip('_') or 'unnamed'
        sub = os.path.join(args.out, cls)
        os.makedirs(sub, exist_ok=True)
        stem = '%s_%dx%d' % (safe, d.m_Width, d.m_Height)
        # 包里存在**同名同尺寸但内容不同**的贴图(实测量级:icon 丢 10 张、
        # cutscene 丢 4 张)。不加后缀就会静默互相覆盖,所以这里强制去重。
        n = used.get(stem, 0)
        used[stem] = n + 1
        name = stem if n == 0 else '%s__dup%d' % (stem, n)
        path = os.path.join(sub, name + '.png')
        try:
            d.image.save(path)
            rows.append((cls, d.m_Name, d.m_Width, d.m_Height, path))
            ok += 1
        except Exception as ex:                      # 少数贴图格式 UnityPy 解不了
            fail += 1
            print('  跳过 %s: %s' % (d.m_Name, ex))

    with open(os.path.join(args.out, 'index.csv'), 'w', newline='', encoding='utf-8') as f:
        wtr = csv.writer(f)
        wtr.writerow(['class', 'name', 'width', 'height', 'path'])
        wtr.writerows(rows)

    print('导出 %d 张,跳过 %d 张 -> %s' % (ok, fail, os.path.abspath(args.out)))
    from collections import Counter
    for k, v in sorted(Counter(r[0] for r in rows).items()):
        print('  %-9s %d' % (k, v))


if __name__ == '__main__':
    main()
