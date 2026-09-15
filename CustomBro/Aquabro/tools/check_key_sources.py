import csv
import json
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

root = Path(sys.argv[1])
body = Image.open(root / 'sprite.png').convert('RGBA')
gun = Image.open(root / 'gunSprite.png').convert('RGBA')


def same(a, b, label):
    # Fully transparent pixels may retain RGB values in edited Aseprite cels.
    difference = ImageChops.difference(a.convert('RGBa'), b.convert('RGBa'))
    assert a.size == b.size and not difference.getbbox(alpha_only=False), label


def cell(image, number):
    x, y = number % 32 * 32, number // 32 * 32
    return image.crop((x, y, x + 32, y + 32))


same(body, Image.open(root / 'body-reopened.png').convert('RGBA'), 'Body Aseprite round trip')
same(gun, Image.open(root / 'gun-reopened.png').convert('RGBA'), 'Weapon Aseprite round trip')
manifest = list(csv.DictReader((root / 'traversal-manifest.csv').open(encoding='utf-8')))
traversal_cells = set()
for entry in manifest:
    frame, body_cell = int(entry['frame']), int(entry['body_cell'])
    traversal_cells.add(body_cell)
    combined = cell(body, body_cell)
    if body_cell >= 512:
        combined = Image.alpha_composite(combined, cell(gun, 595 + (body_cell - 512) * 9))
    same(combined, Image.open(root / f'source-{frame}.png').convert('RGBA'), f'Traversal frame {frame}')

before_body = Image.open(root / 'movement-body.png').convert('RGBA')
before_gun = Image.open(root / 'movement-gun.png').convert('RGBA')
for number in range(1024):
    if number not in traversal_cells:
        same(cell(body, number), cell(before_body, number), f'Preserved body {number}')
    if number < 73 or number > 756:
        same(cell(gun, number), cell(before_gun, number), f'Preserved weapon {number}')
    elif number < 595:
        assert cell(gun, number).getbbox() is None, f'Unarmed weapon cell {number}'

run = Image.open(root / 'run-v3.png').convert('RGBA')
for index in range(8):
    weapon = 9 + (index + 1) // 7 * 16 + (index + 1) % 7
    for body_cell in (32 + index, 96 + index):
        same(Image.alpha_composite(cell(body, body_cell), cell(gun, weapon)),
             run.crop((index * 32, 0, (index + 1) * 32, 32)), f'Run-v3 body {body_cell}')
same(Image.alpha_composite(cell(body, 50), cell(gun, 59)), run.crop((0, 0, 32, 32)), 'Landing to run')

preview = Image.new('RGB', (8 * 192, 2 * 224), '#40454b')
draw = ImageDraw.Draw(preview)
for index in range(8):
    im = run.crop((index * 32, 0, (index + 1) * 32, 32)).resize((192, 192), Image.Resampling.NEAREST)
    preview.paste(im, (index * 192, 24), im)
    draw.text((index * 192 + 4, 4), f'run-v3 {index + 1}', fill='white')
for index in range(6):
    im = Image.open(root / f'source-{81 + index}.png').convert('RGBA').resize((192, 192), Image.Resampling.NEAREST)
    preview.paste(im, (index * 192, 248), im)
    draw.text((index * 192 + 4, 228), f'zipline {13 + index}', fill='white')
preview.save(root / 'rebuild-preview.png')
frames = []
for index in range(8):
    im = run.crop((index * 32, 0, (index + 1) * 32, 32)).resize((256, 256), Image.Resampling.NEAREST)
    frame = Image.new('RGB', im.size, '#40454b')
    frame.paste(im, mask=im.getchannel('A'))
    frames.append(frame)
frames[0].save(root / 'run-v3.gif', save_all=True, append_images=frames[1:], duration=80, loop=0)
result = {'movement_body_cells': 44, 'movement_weapon_cells': 36, 'attack_poses': 36,
          'traversal_source_frames': len(manifest), 'traversal_body_cells': len(traversal_cells),
          'run_source': '01_动作主稿/haiwang_trident_run-v3.aseprite',
          'landing_to_run_v3': True, 'aseprite_png_match': True, 'gameplay_verified': False}
(root / 'checks.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps(result, ensure_ascii=False))
