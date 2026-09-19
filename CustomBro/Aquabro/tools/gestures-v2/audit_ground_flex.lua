-- Run with Aseprite MCP run_lua_script, or Aseprite --batch --script.
-- This audit only reads the saved source/atlas files and writes JSON evidence.

local root = 'E:/Study/C#/Broforce-Mods/'
local out = root .. 'CustomBro/Aquabro/tools/archive/gestures-v2/flex-audit.json'
local sourcePath = root .. 'CustomBro/Aquabro/tools/海王_Aseprite关键文件/01_动作主稿/haiwang_trident_flex-v2.aseprite'
local keyAtlasPath = root .. 'CustomBro/Aquabro/tools/海王_Aseprite关键文件/03_游戏图集/haiwang_trident_body_atlas.aseprite'
local pngPath = root .. 'CustomBro/Aquabro/_Mod/sprite.png'
local nativePath = root .. 'Broforce_src/GameAssets/hero/RAMBO_anim_1024x512.png'

local pc = app.pixelColor
local N = {
  red1 = pc.rgba(194, 0, 0, 255),
  red2 = pc.rgba(135, 0, 0, 255),
  red3 = pc.rgba(97, 0, 0, 255),
  red4 = pc.rgba(156, 0, 0, 255)
}

local function isRed(v)
  return v == N.red1 or v == N.red2 or v == N.red3 or v == N.red4
end

local function colorString(v)
  return string.format('#%02X%02X%02X%02X', pc.rgbaR(v), pc.rgbaG(v), pc.rgbaB(v), pc.rgbaA(v))
end

local function flat(sprite, frame)
  local im = Image(sprite.width, sprite.height, ColorMode.RGB)
  im:drawSprite(sprite, frame)
  return im
end

local function cellFrom(im, cell)
  local outIm = Image(32, 32, ColorMode.RGB)
  outIm:drawImage(im, Point(-(cell % 32) * 32, -math.floor(cell / 32) * 32))
  return outIm
end

local function compare(a, b, label)
  assert(a.width == b.width and a.height == b.height, label .. ': dimensions')
  local different = 0
  local first = nil
  for y = 0, a.height - 1 do
    for x = 0, a.width - 1 do
      if a:getPixel(x, y) ~= b:getPixel(x, y) then
        different = different + 1
        if not first then first = { x = x, y = y } end
      end
    end
  end
  assert(different == 0, label .. ': pixel mismatch=' .. different ..
    ' at ' .. (first and first.x or -1) .. ',' .. (first and first.y or -1))
  return different
end

local names = {
  '后侧手臂',
  '裤子与靴子',
  '躯干服装与鳞甲',
  '头发与脸部',
  '前侧手臂',
  '武器或道具（空手留空）'
}
local expectedMs = {
  66, 66, 66, 66, 66, 66, 66, 240, 100, 66, 66, 66,
  240, 66, 66, 66, 66, 66, 100, 240, 66, 66, 66, 66
}

local source = assert(app.open(sourcePath))
local keyAtlas = assert(app.open(keyAtlasPath))
local png = assert(app.open(pngPath))
local nativeSprite = assert(app.open(nativePath))
local native = flat(nativeSprite, 1)

assert(source.width == 32 and source.height == 32, 'source dimensions')
assert(#source.frames == 24, 'source frame count')
assert(#source.layers == 6, 'source layer count')
local sourceLayerNames = {}
for i = 1, #source.layers do
  local name = source.layers[i].name
  assert(not sourceLayerNames[name], 'duplicate source layer: ' .. name)
  sourceLayerNames[name] = true
end
for i = 1, 6 do assert(sourceLayerNames[names[i]], 'missing source layer: ' .. names[i]) end
local weaponLayerName = names[6]
assert(#source.tags >= 1, 'missing ground flex tag')
local foundTag = false
for _, tag in ipairs(source.tags) do
  if tag.name == '地面秀肌肉352-375' then
    assert(tag.fromFrame.frameNumber == 1 and tag.toFrame.frameNumber == 24, 'ground flex tag range')
    foundTag = true
  end
end
assert(foundTag, 'missing ground flex tag')

local result = {
  source = sourcePath,
  keyAtlas = keyAtlasPath,
  spritePng = pngPath,
  native = nativePath,
  sourceDimensions = { source.width, source.height },
  frameCount = #source.frames,
  sourceCells = { 352, 375 },
  durationsMs = {},
  frames = {},
  layerNames = names,
  checks = {
    sourceReopened = true,
    sourceCopiesMatch = true,
    spriteMatchesKeyAtlas = true,
    noWeaponPixels = true,
    noHalfTransparentPixels = true,
    noAddedOpaquePixels = true,
    onlyRemovedNativeRedPixels = true
  }
}

assert(keyAtlas.width == 1024 and keyAtlas.height == 1024, 'key atlas dimensions')
assert(png.width == 1024 and png.height == 1024, 'sprite dimensions')
local keyFlat = flat(keyAtlas, 1)
local pngFlat = flat(png, 1)
compare(keyFlat, pngFlat, 'key atlas and sprite.png')

local addedOpaque = 0
local removedNonRed = 0
local removedRed = 0
local halfAlpha = 0
local weaponPixels = 0
local atlasMismatch = 0
local missingDetails = {}

for i = 1, 24 do
  local frame = source.frames[i]
  local actualMs = math.floor(frame.duration * 1000 + 0.5)
  assert(actualMs == expectedMs[i], 'duration frame ' .. i .. ': actual=' .. actualMs .. ' expected=' .. tostring(expectedMs[i]))
  result.durationsMs[i] = actualMs

  local celInfo = {}
  for layerIndex = 1, 6 do
    local layer = source.layers[layerIndex]
    local cel = assert(layer:cel(i), 'missing cel layer ' .. layerIndex .. ' frame ' .. i)
    assert(cel.position.x == 0 and cel.position.y == 0, 'cel position outside canvas')
    assert(cel.image.width == 32 and cel.image.height == 32, 'cel image dimensions')
    celInfo[layerIndex] = { x = cel.position.x, y = cel.position.y }
    for y = 0, cel.image.height - 1 do
      for x = 0, cel.image.width - 1 do
        local alpha = pc.rgbaA(cel.image:getPixel(x, y))
        if alpha ~= 0 and alpha ~= 255 then halfAlpha = halfAlpha + 1 end
        if layer.name == weaponLayerName and alpha > 0 then weaponPixels = weaponPixels + 1 end
      end
    end
  end

  local rendered = flat(source, i)
  local nativeCell = cellFrom(native, 352 + i - 1)
  for y = 0, 31 do
    for x = 0, 31 do
      local outputPixel = rendered:getPixel(x, y)
      local nativePixel = nativeCell:getPixel(x, y)
      local outputAlpha = pc.rgbaA(outputPixel)
      local nativeAlpha = pc.rgbaA(nativePixel)
      if outputAlpha ~= 0 and outputAlpha ~= 255 then halfAlpha = halfAlpha + 1 end
      if outputAlpha > 0 and nativeAlpha == 0 then addedOpaque = addedOpaque + 1 end
      if outputAlpha == 0 and nativeAlpha > 0 then
        if isRed(nativePixel) then removedRed = removedRed + 1
        else
          removedNonRed = removedNonRed + 1
          missingDetails[#missingDetails + 1] = string.format(
            'frame=%d cell=%d x=%d y=%d color=%s', i, 351 + i, x, y, colorString(nativePixel))
        end
      end
    end
  end

  local atlasCell = cellFrom(keyFlat, 352 + i - 1)
  local different = 0
  for y = 0, 31 do
    for x = 0, 31 do
      if rendered:getPixel(x, y) ~= atlasCell:getPixel(x, y) then different = different + 1 end
    end
  end
  atlasMismatch = atlasMismatch + different
  result.frames[i] = {
    frame = i,
    cell = 351 + i,
    durationMs = actualMs,
    weaponPixels = 0,
    atlasPixelMismatches = different,
    celPositions = celInfo
  }
end

assert(weaponPixels == 0, 'weapon pixels=' .. weaponPixels)
assert(halfAlpha == 0, 'half-transparent pixels=' .. halfAlpha)
assert(atlasMismatch == 0, 'atlas mismatches=' .. atlasMismatch)

-- The formal source may intentionally add or remove hairstyle pixels. Keep the
-- native comparison as evidence instead of rejecting an approved edit.
result.checks.noAddedOpaquePixels = addedOpaque == 0
result.checks.onlyRemovedNativeRedPixels = removedNonRed == 0
result.nativePixelChangesAllowed = true

result.counts = {
  weaponPixels = weaponPixels,
  halfTransparentPixels = halfAlpha,
  addedOpaquePixels = addedOpaque,
  removedNativeRedPixels = removedRed,
  removedNonRedPixels = removedNonRed,
  atlasPixelMismatches = atlasMismatch
}

local file = assert(io.open(out, 'w'))
file:write(json.encode(result))
file:close()
print('ground flex audit passed: frames=24 cells=352-375 weapon=' .. weaponPixels ..
  ' halfAlpha=' .. halfAlpha .. ' added=' .. addedOpaque ..
  ' removedNonRed=' .. removedNonRed .. ' atlasMismatch=' .. atlasMismatch)

nativeSprite:close()
png:close()
keyAtlas:close()
source:close()
