local root = app.params['root'] or 'E:/Study/C#/Broforce-Mods/'
local work = app.params['work'] or (root .. 'CustomBro/Aquabro/tools/gibs-v2/')
local sourcePath = root .. 'Broforce_src/GameAssets/hero/RAMBO_anim_1024x512.png'
local atlasPath = root .. 'CustomBro/Aquabro/tools/haiwang_trident_body_atlas.aseprite'
local pngPath = root .. 'CustomBro/Aquabro/_Mod/sprite.png'
local outSource = work .. 'haiwang_trident_death_gibs-v1.aseprite'
local outBefore = work .. 'atlas-before.png'
local outAfter = work .. 'atlas-after.png'
local outReference = work .. 'rambro-reference.png'
local outPreview = work .. 'rambro-aquabro-compare.png'
local outAqua = work .. 'aquabro-gibs.png'
local outChecks = work .. 'checks.json'

local pc = app.pixelColor
local function color(r, g, b, a) return pc.rgba(r, g, b, a or 255) end
local C = {
  hair = color(255, 221, 94),
  hairMid = color(232, 175, 51),
  hairDark = color(167, 116, 42),
  armor = color(247, 154, 60),
  armorMid = color(224, 122, 24),
  armorDark = color(168, 86, 15),
  pants = color(47, 138, 42),
  pantsMid = color(28, 90, 24),
  boot = color(18, 63, 20),
  black = color(0, 0, 0),
}

local function blank(w, h)
  return Image(w, h, ColorMode.RGB)
end

local function alpha(p)
  return pc.rgbaA(p)
end

local function channels(p)
  return pc.rgbaR(p), pc.rgbaG(p), pc.rgbaB(p)
end

local function is(r, g, b, p)
  local pr, pg, pb = channels(p)
  return alpha(p) > 0 and pr == r and pg == g and pb == b
end

local function isBlack(p) return is(0, 0, 0, p) end

local function isRed(p)
  local r, g, b = channels(p)
  return alpha(p) > 0 and r > 0 and g == 0 and b == 0
end

local function borderPixel(im, x, y)
  for _, d in ipairs({ {-1, 0}, {1, 0}, {0, -1}, {0, 1} }) do
    local xx, yy = x + d[1], y + d[2]
    if xx < 0 or yy < 0 or xx >= im.width or yy >= im.height or alpha(im:getPixel(xx, yy)) == 0 then
      return true
    end
  end
  return false
end

local function recolor(p, kind, x, y, source)
  if alpha(p) == 0 then return 0 end
  if isRed(p) then return p end
  if is(39, 70, 32, p) then return C.armor end
  if is(30, 54, 25, p) then return C.armorDark end
  if is(111, 64, 27, p) then return C.pants end
  if is(76, 39, 9, p) then return C.pantsMid end
  if isBlack(p) and kind == 'head' then
    local phase = (x + y) % 3
    return phase == 0 and C.hair or (phase == 1 and C.hairMid or C.hairDark)
  end
  if isBlack(p) and kind == 'leg' then return C.boot end
  return p
end

local native = assert(Image{ fromFile = sourcePath })
assert(native.width == 1024 and native.height == 512, 'Unexpected Rambro source dimensions')

local pieces = {
  { name = 'head', label = '头部碎块', x = 320, y = 1, w = 16, h = 16, ox = 0, oy = 0 },
  { name = 'torso', label = '躯干碎块', x = 320, y = 14, w = 16, h = 16, ox = 0, oy = 13 },
  { name = 'arm', label = '双臂碎块', x = 341, y = 5, w = 8, h = 8, ox = 21, oy = 4 },
  { name = 'leg', label = '双腿碎块', x = 341, y = 19, w = 8, h = 8, ox = 21, oy = 18 },
}

local reference = blank(30, 30)
local mapped = blank(30, 30)
local mappedByPiece = {}
local counts = {}

for _, piece in ipairs(pieces) do
  local image = blank(30, 30)
  local originalPixels, mappedPixels, changedPixels, semiTransparent = 0, 0, 0, 0
  for yy = 0, piece.h - 1 do
    for xx = 0, piece.w - 1 do
      local sourcePixel = native:getPixel(piece.x + xx, piece.y + yy)
      local mappedPixel = recolor(sourcePixel, piece.name, piece.x + xx, piece.y + yy, native)
      image:putPixel(piece.ox + xx, piece.oy + yy, mappedPixel)
      if alpha(sourcePixel) > 0 then originalPixels = originalPixels + 1 end
      if alpha(mappedPixel) > 0 then mappedPixels = mappedPixels + 1 end
      if sourcePixel ~= mappedPixel then changedPixels = changedPixels + 1 end
      if alpha(sourcePixel) > 0 and alpha(sourcePixel) < 255 then semiTransparent = semiTransparent + 1 end
    end
  end
  reference:drawImage(image, Point(0, 0))
  mappedByPiece[piece.name] = image
  mapped:drawImage(image, Point(0, 0))
  counts[piece.name] = {
    originalPixels = originalPixels,
    mappedPixels = mappedPixels,
    changedPixels = changedPixels,
    semiTransparent = semiTransparent,
  }
end

-- The reference image must contain the untouched Rambro pixels, not the recolored result.
reference:clear(0)
for _, piece in ipairs(pieces) do
  for yy = 0, piece.h - 1 do
    for xx = 0, piece.w - 1 do
      reference:putPixel(piece.ox + xx, piece.oy + yy, native:getPixel(piece.x + xx, piece.y + yy))
    end
  end
end

local source = Sprite(30, 30, ColorMode.RGB)
local referenceLayer = source.layers[1]
referenceLayer.name = '原版兰博碎块参考（隐藏）'
referenceLayer.isVisible = false
source:newCel(referenceLayer, 1, reference, Point(0, 0)).data =
  '参考角色=Rambro；取样区=身体图集实体碎块；源稿帧=1；图集格号=非动画帧'

local function addSourceLayer(name, image, data)
  local layer = source:newLayer()
  layer.name = name
  layer.isVisible = true
  local cel = source:newCel(layer, 1, image, Point(0, 0))
  cel.data = data
end

local headLayer = blank(30, 30)
headLayer:drawImage(mappedByPiece.head, Point(0, 0))
addSourceLayer('金发与脸部（头部碎块）', headLayer,
  '动作=实体破裂；源稿帧=1；图集取样=(320,17),16×16；PNG区域=(320,1),16×16；原版轮廓逐像素保留')

local torsoLayer = blank(30, 30)
torsoLayer:drawImage(mappedByPiece.torso, Point(0, 0))
addSourceLayer('金色鳞甲与颈部（躯干碎块）', torsoLayer,
  '动作=实体破裂；源稿帧=1；图集取样=(320,30),16×16；PNG区域=(320,14),16×16；原版轮廓逐像素保留')

local armLayer = blank(30, 30)
armLayer:drawImage(mappedByPiece.arm, Point(0, 0))
addSourceLayer('前侧手臂（双臂共用碎块）', armLayer,
  '动作=实体破裂；源稿帧=1；图集取样=(341,13),8×8；PNG区域=(341,5),8×8；左右实体共用此采样区')
addSourceLayer('后侧手臂（双臂共用碎块）', blank(30, 30),
  '动作=实体破裂；源稿帧=1；与前侧手臂共用图集采样=(341,13),8×8；本层保留为空以避免重复绘制')

local legLayer = blank(30, 30)
legLayer:drawImage(mappedByPiece.leg, Point(0, 0))
addSourceLayer('绿色裤靴（双腿碎块）', legLayer,
  '动作=实体破裂；源稿帧=1；图集取样=(341,27),8×8；PNG区域=(341,19),8×8；原版轮廓逐像素保留')
addSourceLayer('金色三叉戟（空手留空）', blank(30, 30),
  '动作=实体破裂；源稿帧=1；普通死亡碎块不显示武器')

local tag = source:newTag(1, 1)
tag.name = '实体破裂'
tag.aniDir = AniDir.FORWARD
source.frames[1].duration = 1.0
source:saveAs(outSource)

local atlas = assert(app.open(atlasPath))
assert(atlas.width == 1024 and atlas.height == 1024, 'Unexpected atlas dimensions')
local before = blank(1024, 1024)
before:drawSprite(atlas, 1)
before:saveAs(outBefore)

local rects = {
  { x = 320, y = 1, w = 16, h = 16 },
  { x = 320, y = 14, w = 16, h = 16 },
  { x = 341, y = 5, w = 8, h = 8 },
  { x = 341, y = 19, w = 8, h = 8 },
}

local function clearRect(image, rect, position)
  for yy = 0, rect.h - 1 do
    for xx = 0, rect.w - 1 do
      local x, y = rect.x - position.x + xx, rect.y - position.y + yy
      if x >= 0 and y >= 0 and x < image.width and y < image.height then image:putPixel(x, y, 0) end
    end
  end
end

local function clearVisibleLayers(layerList)
  for _, layer in ipairs(layerList) do
    if layer.isGroup then
      clearVisibleLayers(layer.layers)
    elseif layer.isVisible then
      local cel = layer:cel(1)
      if cel then
        local image = Image(cel.image)
        for _, rect in ipairs(rects) do clearRect(image, rect, cel.position) end
        cel.image = image
      end
    end
  end
end
clearVisibleLayers(atlas.layers)

local function atlasLayer(name, image, piece, data)
  local layer = atlas:newLayer()
  layer.name = name
  layer.isVisible = true
  local cel = atlas:newCel(layer, 1, image, Point(0, 0))
  cel.data = data
end

local function atlasPiece(piece, image)
  local out = blank(1024, 1024)
  for yy = 0, piece.h - 1 do
    for xx = 0, piece.w - 1 do
      out:putPixel(piece.x + xx, piece.y + yy, image:getPixel(piece.ox + xx, piece.oy + yy))
    end
  end
  return out
end

atlasLayer('金发与脸部（实体破裂头部）', atlasPiece(pieces[1], mappedByPiece.head), pieces[1],
  '动作=实体破裂；源稿帧=1；图集取样=(320,17),16×16；原版透明位置已清空')
atlasLayer('金色鳞甲与颈部（实体破裂躯干）', atlasPiece(pieces[2], mappedByPiece.torso), pieces[2],
  '动作=实体破裂；源稿帧=1；图集取样=(320,30),16×16；原版透明位置已清空')
atlasLayer('前侧手臂（实体破裂双臂）', atlasPiece(pieces[3], mappedByPiece.arm), pieces[3],
  '动作=实体破裂；源稿帧=1；图集取样=(341,13),8×8；左右实体共用此采样区')
atlasLayer('绿色裤靴（实体破裂双腿）', atlasPiece(pieces[4], mappedByPiece.leg), pieces[4],
  '动作=实体破裂；源稿帧=1；图集取样=(341,27),8×8；原版透明位置已清空')

local atlasReference = blank(1024, 1024)
for _, piece in ipairs(pieces) do
  for yy = 0, piece.h - 1 do
    for xx = 0, piece.w - 1 do
      atlasReference:putPixel(piece.x + xx, piece.y + yy, native:getPixel(piece.x + xx, piece.y + yy))
    end
  end
end
local hidden = atlas:newLayer()
hidden.name = '原版兰博碎块参考（隐藏）'
hidden.isVisible = false
local hiddenCel = atlas:newCel(hidden, 1, atlasReference, Point(0, 0))
hiddenCel.data = '只读参考；不参与游戏显示'

atlas:saveAs(atlasPath)
atlas:close()

local reopened = assert(app.open(atlasPath))
local after = blank(1024, 1024)
after:drawSprite(reopened, 1)
after:saveAs(outAfter)
after:saveAs(pngPath)
reopened:close()

reference:saveAs(outReference)
mapped:saveAs(outAqua)
local compare = blank(60, 30)
compare:drawImage(reference, Point(0, 0))
compare:drawImage(mapped, Point(30, 0))
compare:saveAs(outPreview)

local function inRect(x, y)
  for _, rect in ipairs(rects) do
    if x >= rect.x and x < rect.x + rect.w and y >= rect.y and y < rect.y + rect.h then return true end
  end
  return false
end

local changedPixels, outsideChanges = 0, 0
local afterSemiTransparent, sourceSemiTransparent = 0, 0
local addedOutsideNative, removedNative, changedSkin, changedBlood = 0, 0, 0, 0
local skinColors = {
  [0xE9A68C] = true, [0xD79479] = true, [0xCF8C71] = true,
  [0xB57C65] = true, [0xAA735D] = true, [0xC3866D] = true,
}
local function rgbKey(p)
  local r, g, b = channels(p)
  return r * 0x10000 + g * 0x100 + b
end
for y = 0, after.height - 1 do
  for x = 0, after.width - 1 do
    local a, b = before:getPixel(x, y), after:getPixel(x, y)
    if a ~= b then
      changedPixels = changedPixels + 1
      if not inRect(x, y) then outsideChanges = outsideChanges + 1 end
    end
  end
end
for _, piece in ipairs(pieces) do
  for yy = 0, piece.h - 1 do
    for xx = 0, piece.w - 1 do
      local x, y = piece.x + xx, piece.y + yy
      local nativePixel, afterPixel = native:getPixel(x, y), after:getPixel(x, y)
      local nativeAlpha, afterAlpha = alpha(nativePixel), alpha(afterPixel)
      if nativeAlpha > 0 and nativeAlpha < 255 then sourceSemiTransparent = sourceSemiTransparent + 1 end
      if afterAlpha > 0 and afterAlpha < 255 then afterSemiTransparent = afterSemiTransparent + 1 end
      if nativeAlpha == 0 and afterAlpha > 0 then addedOutsideNative = addedOutsideNative + 1 end
      if nativeAlpha > 0 and afterAlpha == 0 then removedNative = removedNative + 1 end
      if skinColors[rgbKey(nativePixel)] and nativePixel ~= afterPixel then changedSkin = changedSkin + 1 end
      if isRed(nativePixel) and nativePixel ~= afterPixel then changedBlood = changedBlood + 1 end
    end
  end
end
local f = assert(io.open(outChecks, 'w'))
f:write('{\n')
f:write('  "action": "实体破裂",\n')
f:write('  "sourceFrame": 1,\n')
f:write('  "reference": "Rambro body atlas",\n')
f:write('  "sourcePath": "Broforce_src/GameAssets/hero/RAMBO_anim_1024x512.png",\n')
f:write('  "atlas": "CustomBro/Aquabro/tools/haiwang_trident_body_atlas.aseprite",\n')
f:write('  "samples": [\n')
for i, piece in ipairs(pieces) do
  local c = counts[piece.name]
  f:write(string.format('    {"name":"%s","lowerLeftPixel":[%d,%d],"pixelDimensions":[%d,%d],"pngRect":[%d,%d,%d,%d],"originalPixels":%d,"mappedPixels":%d,"changedPixels":%d,"semiTransparent":%d}%s\n',
    piece.name, piece.x, piece.y + piece.h, piece.w, piece.h, piece.x, piece.y, piece.w, piece.h,
    c.originalPixels, c.mappedPixels, c.changedPixels, c.semiTransparent, i == #pieces and '' or ','))
end
f:write('  ],\n')
f:write(string.format('  "geometryChanged": false,\n  "atlasChangedPixels": %d,\n  "outsideSamplePixels": %d,\n  "sourceSemiTransparentPixels": %d,\n  "afterSemiTransparentPixels": %d,\n  "addedOutsideNative": %d,\n  "removedNative": %d,\n  "changedSkinPixels": %d,\n  "changedBloodPixels": %d,\n',
  changedPixels, outsideChanges, sourceSemiTransparent, afterSemiTransparent, addedOutsideNative, removedNative, changedSkin, changedBlood))
f:write('  "preview": {"normal":"normal.gif","slow":"slow.gif","compare":"rambro-aquabro-compare-annotated.png"}\n')
f:write('}\n')
f:close()

print('Created entity-breakup source, atlas overlay, PNG, comparison, and checks.')
