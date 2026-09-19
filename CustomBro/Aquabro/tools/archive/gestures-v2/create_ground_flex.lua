-- Run through the Aseprite MCP run_lua_script tool.
-- Every output pixel starts from the matching native Rambro flex cell.

local root = 'E:/Study/C#/Broforce-Mods/'
local temp = 'C:/Users/5700G/AppData/Local/Temp/haiwang-flex-v2-20260917/'
local testOnly = false
local output = temp .. (testOnly and 'flex-test-352.aseprite' or 'haiwang_trident_flex-v2.aseprite')

app.fs.makeAllDirectories(temp)

local pc = app.pixelColor
local function rgba(r, g, b) return pc.rgba(r, g, b, 255) end
local C = {
  hairBright = rgba(255, 221, 94),
  hairMid = rgba(232, 175, 51),
  hairDark = rgba(167, 116, 42),
  armorLight = rgba(247, 154, 60),
  armorMid = rgba(224, 122, 24),
  armorDark = rgba(168, 86, 15),
  waist = rgba(47, 138, 42),
  green = rgba(28, 90, 24),
  boot = rgba(18, 63, 20),
  neck = rgba(170, 115, 93),
  dark = rgba(0, 0, 0)
}

local N = {
  black = rgba(0, 0, 0),
  red1 = rgba(194, 0, 0),
  red2 = rgba(135, 0, 0),
  red3 = rgba(97, 0, 0),
  red4 = rgba(156, 0, 0),
  red5 = rgba(154, 0, 0),
  headDark = rgba(88, 43, 32),
  shirt = rgba(39, 70, 32),
  shirtDark = rgba(30, 54, 25),
  pants = rgba(111, 64, 27),
  pantsDark = rgba(76, 39, 9),
  skin = rgba(233, 166, 140),
  skinLow = rgba(207, 140, 113),
  skinMid = rgba(215, 148, 121),
  faceShade = rgba(195, 134, 109),
  neck = rgba(170, 115, 93),
  skinDeep = rgba(181, 124, 101),
  oldSkin = rgba(211, 139, 110)
}

local function blank()
  return Image(32, 32, ColorMode.RGB)
end

local function opaque(im, x, y)
  return x >= 0 and x < im.width and y >= 0 and y < im.height and pc.rgbaA(im:getPixel(x, y)) > 0
end

local function same(a, b)
  return a == b
end

local function isSkin(v)
  return same(v, N.skin) or same(v, N.skinLow) or same(v, N.skinMid) or
    same(v, N.faceShade) or same(v, N.neck) or same(v, N.skinDeep) or same(v, N.oldSkin)
end

local function isRed(v)
  return same(v, N.red1) or same(v, N.red2) or same(v, N.red3) or same(v, N.red4) or same(v, N.red5)
end

local function isShirt(v)
  return same(v, N.shirt) or same(v, N.shirtDark)
end

local function isPants(v)
  return same(v, N.pants) or same(v, N.pantsDark)
end

local function put(im, x, y, value)
  assert(x >= 0 and x < 32 and y >= 0 and y < 32, 'pixel outside 32x32')
  im:putPixel(x, y, value)
end

local nativeSprite = assert(app.open(root .. 'Broforce_src/GameAssets/hero/RAMBO_anim_1024x512.png'))
local native = Image(nativeSprite.width, nativeSprite.height, ColorMode.RGB)
native:drawSprite(nativeSprite, 1)

local names = {
  '后侧手臂',
  '裤子与靴子',
  '躯干服装与鳞甲',
  '头发与脸部',
  '前侧手臂',
  '武器或道具（空手留空）'
}

local spr = Sprite(32, 32, ColorMode.RGB)
spr.layers[1].name = names[1]
for i = 2, #names do
  local layer = spr:newLayer()
  layer.name = names[i]
end

local function nativeFrame(sourceIndex)
  local im = blank()
  im:drawImage(native, Point(-((sourceIndex % 32) * 32), -math.floor(sourceIndex / 32) * 32))
  return im
end

local function ranges(im, predicate)
  local minX, minY, maxX, maxY = 32, 32, -1, -1
  for y = 0, 31 do
    for x = 0, 31 do
      if predicate(im:getPixel(x, y)) then
        minX = math.min(minX, x)
        minY = math.min(minY, y)
        maxX = math.max(maxX, x)
        maxY = math.max(maxY, y)
      end
    end
  end
  return { left = minX, top = minY, right = maxX, bottom = maxY }
end

local function near(mask, x, y)
  for dy = -1, 1 do
    for dx = -1, 1 do
      if mask[(y + dy) * 32 + (x + dx)] then return true end
    end
  end
  return false
end

local function buildFrame(sourceIndex, gameCell, frameIndex, duration)
  local src = nativeFrame(sourceIndex)
  local parts = { blank(), blank(), blank(), blank(), blank(), blank() }
  local shirtMask, pantsMask = {}, {}
  local skinPixels = {}

  local firstOpaque = ranges(src, function(v) return pc.rgbaA(v) > 0 end)
  local firstBlack = { left = 32, top = 32, right = -1, bottom = -1 }
  for y = firstOpaque.top, math.min(31, firstOpaque.top + 8) do
    for x = 0, 31 do
      if same(src:getPixel(x, y), N.black) then
        firstBlack.left = math.min(firstBlack.left, x)
        firstBlack.top = math.min(firstBlack.top, y)
        firstBlack.right = math.max(firstBlack.right, x)
        firstBlack.bottom = math.max(firstBlack.bottom, y)
      end
    end
  end
  local head = {
    left = math.max(0, firstBlack.left - 1),
    top = firstOpaque.top,
    right = math.min(31, firstBlack.right),
    bottom = math.min(31, firstOpaque.top + 8)
  }
  local face = {
    left = math.max(0, head.left + 1),
    top = head.top + 3,
    right = math.min(31, head.right - 2),
    bottom = math.min(31, head.top + 9)
  }

  local shirt = ranges(src, function(v) return isShirt(v) end)
  local pants = ranges(src, function(v) return isPants(v) end)

  for y = 0, 31 do
    for x = 0, 31 do
      local v = src:getPixel(x, y)
      local key = y * 32 + x
      if isShirt(v) then shirtMask[key] = true end
      if isPants(v) then pantsMask[key] = true end
      if isSkin(v) then skinPixels[key] = v end
    end
  end

  -- Keep black boot pixels attached to the native trouser silhouette.
  for y = pants.top - 1, 31 do
    for x = math.max(0, pants.left - 3), math.min(31, pants.right + 3) do
      local v = src:getPixel(x, y)
      if same(v, N.black) and x >= pants.left - 3 and x <= pants.right + 3 then pantsMask[y * 32 + x] = true end
    end
  end

  local armPixels = {}
  for y = 0, 31 do
    for x = 0, 31 do
      local key = y * 32 + x
      local v = skinPixels[key]
      if v then
        local inFace = x >= face.left and x <= face.right and y >= face.top and y <= face.bottom
        local inNeck = same(v, N.neck) and x >= shirt.left - 1 and x <= shirt.right + 1 and y <= shirt.top + 2
        if inFace then
          local mapped = same(v, N.oldSkin) and N.skinMid or v
          put(parts[4], x, y, mapped)
        elseif inNeck then
          put(parts[3], x, y, C.neck)
        else
          armPixels[key] = same(v, N.oldSkin) and N.skinMid or v
        end
      end
    end
  end

  -- Recolour the two native arm components by side so both remain editable.
  for key, v in pairs(armPixels) do
    local x, y = key % 32, math.floor(key / 32)
    local layer = x < math.floor((shirt.left + shirt.right) / 2) and 1 or 5
    put(parts[layer], x, y, v)
  end

  for y = 0, 31 do
    for x = 0, 31 do
      local key = y * 32 + x
      local v = src:getPixel(x, y)
      if shirtMask[key] then
        local phase = (x - shirt.left) % 2
        local material = phase == 0 and C.armorLight or C.armorMid
        if x == shirt.left or y == shirt.bottom or (same(v, N.shirtDark) and x == shirt.left + 1) then
          material = C.armorDark
        end
        put(parts[3], x, y, material)
      elseif pantsMask[key] then
        if isPants(v) then
          put(parts[2], x, y, same(v, N.pants) and C.waist or C.green)
        else
          put(parts[2], x, y, C.boot)
        end
      elseif same(v, N.black) and x >= head.left and x <= head.right and y <= head.bottom then
        local dy = y - head.top
        local material = dy <= 1 and C.hairBright or (x <= head.left + 1 and C.hairDark or C.hairMid)
        put(parts[4], x, y, material)
      elseif same(v, N.headDark) and x >= head.left - 1 and x <= head.right and y <= head.bottom then
        put(parts[4], x, y, C.hairDark)
      elseif isRed(v) and x >= head.left and x <= head.right and y <= head.bottom then
        put(parts[4], x, y, C.hairMid)
      elseif isShirt(v) then
        assert(false, 'unhandled shirt pixel')
      elseif isPants(v) then
        assert(false, 'unhandled pants pixel')
      end
    end
  end

  local merged = blank()
  for i = 1, 6 do merged:drawImage(parts[i]) end
  local metadata = '地面秀肌肉；身体格=' .. gameCell .. '；原版来源=RAMBO_anim_1024x512.png:' .. sourceIndex ..
    '；原版动作逐像素换装；外观基准=haiwang_trident_held-v2.aseprite；空手；武器层留空'
  for i = 1, 6 do
    local cel = spr:newCel(spr.layers[i], frameIndex, parts[i], Point(0, 0))
    cel.data = metadata .. '；层=' .. names[i]
  end
  spr.frames[frameIndex].duration = duration
  return merged, { source = sourceIndex, cell = gameCell, bounds = firstOpaque, face = face, head = head }
end

local firstCell = 352
local count = testOnly and 1 or 24
local audit = {}
for i = 2, count do spr:newEmptyFrame() end
for i = 0, count - 1 do
  local internalFrame = i
  local duration = 0.0667
  if internalFrame == 7 or internalFrame == 12 or internalFrame == 19 then
    duration = 0.24
  elseif internalFrame == 8 or internalFrame == 18 then
    duration = 0.1
  end
  local merged, record = buildFrame(i + firstCell, firstCell + i, i + 1, duration)
  record.durationMs = math.floor(duration * 1000 + 0.5)
  merged:saveAs(temp .. string.format('frame-%03d.png', firstCell + i))
  audit[#audit + 1] = record
end

if not testOnly then
  local tag = spr:newTag(1, count)
  tag.name = '地面秀肌肉352-375'
  tag.aniDir = AniDir.FORWARD
  tag.repeats = 1
end

spr.data = '海王地面秀肌肉352-375；原版逐帧换装；测试帧=' .. tostring(testOnly) ..
  '；身体图集1024x1024；单格32x32；402-406空中秀肌肉不包含在本稿。'
spr:saveAs(output)
local auditFile = io.open(temp .. 'audit.json', 'w')
auditFile:write(json.encode({
  source = 'RAMBO_anim_1024x512.png',
  sourceCells = { firstCell, firstCell + count - 1 },
  frames = audit,
  layerNames = names,
  weaponPixels = 0,
  airFlexCellsExcluded = { 402, 403, 404, 405, 406 }
}))
auditFile:close()
nativeSprite:close()
print('saved ' .. output .. ' frames=' .. count .. ' firstCell=' .. firstCell)
