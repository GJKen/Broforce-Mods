local root = 'E:/Study/C#/Broforce-Mods/'
local sourcePath = root .. 'CustomBro/Aquabro/tools/海王_Aseprite关键文件/04_死亡碎块/haiwang_trident_death_gibs-v1.aseprite'
local keyAtlasPath = root .. 'CustomBro/Aquabro/tools/海王_Aseprite关键文件/03_游戏图集/haiwang_trident_body_atlas.aseprite'
local pngPath = root .. 'CustomBro/Aquabro/_Mod/sprite.png'
local report = root .. 'CustomBro/Aquabro/tools/archive/gibs-v2/'
local outBefore = report .. 'atlas-before.png'
local outAfter = report .. 'atlas-after.png'
local outReference = report .. 'rambro-reference.png'
local outPreview = report .. 'rambro-aquabro-compare.png'
local outAqua = report .. 'aquabro-gibs.png'
local outChecks = report .. 'checks.json'

local pc = app.pixelColor
local function alpha(p) return pc.rgbaA(p) end
local function blank(w, h)
  local image = Image(w, h, ColorMode.RGB)
  image:clear(0)
  return image
end

local source = assert(app.open(sourcePath))
assert(source.width == 30 and source.height == 30, 'Unexpected gibs source dimensions')

local function findLayer(layers, name)
  for _, layer in ipairs(layers) do
    if layer.name == name then return layer end
    if layer.isGroup then
      local found = findLayer(layer.layers, name)
      if found then return found end
    end
  end
  return nil
end

local function layerPixel(layer, x, y)
  local cel = layer:cel(1)
  if not cel then return 0 end
  local sx, sy = x - cel.position.x, y - cel.position.y
  if sx < 0 or sy < 0 or sx >= cel.image.width or sy >= cel.image.height then return 0 end
  return cel.image:getPixel(sx, sy)
end

local pieces = {
  { name = 'head', layer = '金发与脸部（头部碎块）', x = 320, y = 1, w = 16, h = 16, sx = 0, sy = 0, atlasLayer = '金发与脸部（实体破裂头部）' },
  { name = 'torso', layer = '金色鳞甲与颈部（躯干碎块）', x = 320, y = 14, w = 16, h = 16, sx = 0, sy = 13, atlasLayer = '金色鳞甲与颈部（实体破裂躯干）' },
  { name = 'arm', layer = '前侧手臂（双臂共用碎块）', x = 341, y = 5, w = 8, h = 8, sx = 21, sy = 4, atlasLayer = '前侧手臂（实体破裂双臂）' },
  { name = 'leg', layer = '绿色裤靴（双腿碎块）', x = 341, y = 19, w = 8, h = 8, sx = 21, sy = 18, atlasLayer = '绿色裤靴（实体破裂双腿）' },
}

for _, piece in ipairs(pieces) do
  piece.sourceLayer = assert(findLayer(source.layers, piece.layer), 'Missing source layer: ' .. piece.layer)
end

local reference = blank(30, 30)
local mapped = blank(30, 30)
local mappedByPiece = {}
local counts = {}
local native = assert(Image{ fromFile = root .. 'Broforce_src/GameAssets/hero/RAMBO_anim_1024x512.png' })

for _, piece in ipairs(pieces) do
  local overlay = blank(1024, 1024)
  local originalPixels, mappedPixels, changedPixels, semiTransparent = 0, 0, 0, 0
  for yy = 0, piece.h - 1 do
    for xx = 0, piece.w - 1 do
      local nativePixel = native:getPixel(piece.x + xx, piece.y + yy)
      local mappedPixel = layerPixel(piece.sourceLayer, piece.sx + xx, piece.sy + yy)
      overlay:putPixel(piece.x + xx, piece.y + yy, mappedPixel)
      reference:putPixel(piece.sx + xx, piece.sy + yy, nativePixel)
      mapped:putPixel(piece.sx + xx, piece.sy + yy, mappedPixel)
      if alpha(nativePixel) > 0 then originalPixels = originalPixels + 1 end
      if alpha(mappedPixel) > 0 then mappedPixels = mappedPixels + 1 end
      if nativePixel ~= mappedPixel then changedPixels = changedPixels + 1 end
      if alpha(nativePixel) > 0 and alpha(nativePixel) < 255 then semiTransparent = semiTransparent + 1 end
    end
  end
  mappedByPiece[piece.name] = overlay
  counts[piece.name] = {
    originalPixels = originalPixels,
    mappedPixels = mappedPixels,
    changedPixels = changedPixels,
    semiTransparent = semiTransparent,
  }
end

local rects = {}
for _, piece in ipairs(pieces) do
  rects[#rects + 1] = { x = piece.x, y = piece.y, w = piece.w, h = piece.h }
end

local function clearVisibleLayers(layerList, parentVisible)
  parentVisible = parentVisible ~= false
  for _, layer in ipairs(layerList) do
    local visible = parentVisible and layer.isVisible
    if layer.isGroup then
      clearVisibleLayers(layer.layers, visible)
    elseif visible then
      local cel = layer:cel(1)
      if cel then
        local image = Image(cel.image)
        for _, rect in ipairs(rects) do
          for yy = 0, rect.h - 1 do
            for xx = 0, rect.w - 1 do
              local x = rect.x - cel.position.x + xx
              local y = rect.y - cel.position.y + yy
              if x >= 0 and y >= 0 and x < image.width and y < image.height then image:putPixel(x, y, 0) end
            end
          end
        end
        cel.image = image
      end
    end
  end
end

local function findExistingLayer(layers, name)
  for _, layer in ipairs(layers) do
    if layer.name == name then return layer end
    if layer.isGroup then
      local found = findExistingLayer(layer.layers, name)
      if found then return found end
    end
  end
  return nil
end

local function writeOverlay(atlas, piece, overlay)
  local layer = findExistingLayer(atlas.layers, piece.atlasLayer)
  if not layer then
    layer = atlas:newLayer()
    layer.name = piece.atlasLayer
  end
  layer.isVisible = true
  local cel = layer:cel(1)
  if cel then
    cel.image = overlay
  else
    cel = atlas:newCel(layer, 1, overlay, Point(0, 0))
  end
  cel.data = string.format('动作=实体破裂；源稿帧=1；图集取样=(%d,%d),%d×%d；来自用户微调源稿', piece.x, piece.y + piece.h, piece.w, piece.h)
end

local function snapshot(atlas, path)
  local image = blank(1024, 1024)
  image:drawSprite(atlas, 1)
  image:saveAs(path)
  return image
end

local keyAtlas = assert(app.open(keyAtlasPath))
local before = snapshot(keyAtlas, outBefore)
clearVisibleLayers(keyAtlas.layers)
for _, piece in ipairs(pieces) do writeOverlay(keyAtlas, piece, mappedByPiece[piece.name]) end
keyAtlas:saveAs(keyAtlasPath)
keyAtlas:close()

local reopened = assert(app.open(keyAtlasPath))
local after = snapshot(reopened, outAfter)
after:saveAs(pngPath)
reopened:close()
source:close()

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

local function isRed(p)
  local r, g, b = pc.rgbaR(p), pc.rgbaG(p), pc.rgbaB(p)
  return alpha(p) > 0 and r > 0 and g == 0 and b == 0
end

local skinColors = {
  [0xE9A68C] = true, [0xD79479] = true, [0xCF8C71] = true,
  [0xB57C65] = true, [0xAA735D] = true, [0xC3866D] = true,
}
local function rgbKey(p)
  return pc.rgbaR(p) * 0x10000 + pc.rgbaG(p) * 0x100 + pc.rgbaB(p)
end

local changedPixels, outsideChanges = 0, 0
for y = 0, after.height - 1 do
  for x = 0, after.width - 1 do
    local a, b = before:getPixel(x, y), after:getPixel(x, y)
    if a ~= b then
      changedPixels = changedPixels + 1
      if not inRect(x, y) then outsideChanges = outsideChanges + 1 end
    end
  end
end

local afterSemiTransparent, sourceSemiTransparent = 0, 0
local addedOutsideNative, removedNative, changedSkin, changedBlood = 0, 0, 0, 0
local geometryChanges = {}
for _, piece in ipairs(pieces) do
  for yy = 0, piece.h - 1 do
    for xx = 0, piece.w - 1 do
      local x, y = piece.x + xx, piece.y + yy
      local nativePixel, mappedPixel = native:getPixel(x, y), mapped:getPixel(piece.sx + xx, piece.sy + yy)
      local nativeAlpha, mappedAlpha = alpha(nativePixel), alpha(mappedPixel)
      local afterPixel = after:getPixel(x, y)
      local afterAlpha = alpha(afterPixel)
      if nativeAlpha > 0 and nativeAlpha < 255 then sourceSemiTransparent = sourceSemiTransparent + 1 end
      if afterAlpha > 0 and afterAlpha < 255 then afterSemiTransparent = afterSemiTransparent + 1 end
      if nativeAlpha == 0 and afterAlpha > 0 then
        addedOutsideNative = addedOutsideNative + 1
        geometryChanges[#geometryChanges + 1] = string.format('{"piece":"%s","atlas":[%d,%d],"source":[%d,%d],"type":"added"}', piece.name, x, y, piece.sx + xx, piece.sy + yy)
      elseif nativeAlpha > 0 and afterAlpha == 0 then
        removedNative = removedNative + 1
        geometryChanges[#geometryChanges + 1] = string.format('{"piece":"%s","atlas":[%d,%d],"source":[%d,%d],"type":"removed"}', piece.name, x, y, piece.sx + xx, piece.sy + yy)
      end
      if skinColors[rgbKey(nativePixel)] and nativePixel ~= mappedPixel then changedSkin = changedSkin + 1 end
      if isRed(nativePixel) and nativePixel ~= mappedPixel then changedBlood = changedBlood + 1 end
    end
  end
end

local f = assert(io.open(outChecks, 'w'))
f:write('{\n')
f:write('  "action": "实体破裂",\n')
f:write('  "sourceFrame": 1,\n')
f:write('  "reference": "Rambro body atlas",\n')
f:write('  "source": "CustomBro/Aquabro/tools/海王_Aseprite关键文件/04_死亡碎块/haiwang_trident_death_gibs-v1.aseprite",\n')
f:write('  "atlas": "CustomBro/Aquabro/tools/海王_Aseprite关键文件/03_游戏图集/haiwang_trident_body_atlas.aseprite",\n')
f:write('  "samples": [\n')
for i, piece in ipairs(pieces) do
  local c = counts[piece.name]
  f:write(string.format('    {"name":"%s","lowerLeftPixel":[%d,%d],"pixelDimensions":[%d,%d],"pngRect":[%d,%d,%d,%d],"originalPixels":%d,"mappedPixels":%d,"changedPixels":%d,"semiTransparent":%d}%s\n',
    piece.name, piece.x, piece.y + piece.h, piece.w, piece.h, piece.x, piece.y, piece.w, piece.h,
    c.originalPixels, c.mappedPixels, c.changedPixels, c.semiTransparent, i == #pieces and '' or ','))
end
f:write('  ],\n')
f:write(string.format('  "geometryChanged": %s,\n  "geometryChanges": [%s],\n  "atlasChangedPixels": %d,\n  "outsideSamplePixels": %d,\n  "sourceSemiTransparentPixels": %d,\n  "afterSemiTransparentPixels": %d,\n  "addedOutsideNative": %d,\n  "removedNative": %d,\n  "changedSkinPixels": %d,\n  "changedBloodPixels": %d,\n',
  tostring(#geometryChanges > 0), table.concat(geometryChanges, ','), changedPixels, outsideChanges, sourceSemiTransparent, afterSemiTransparent, addedOutsideNative, removedNative, changedSkin, changedBlood))
f:write('  "preview": {"normal":"normal.gif","slow":"slow.gif","compare":"rambro-aquabro-compare-annotated.png"}\n')
f:write('}\n')
f:close()
native:close()

print('Synced the user-edited gibs source into the canonical body atlas and sprite.png.')
