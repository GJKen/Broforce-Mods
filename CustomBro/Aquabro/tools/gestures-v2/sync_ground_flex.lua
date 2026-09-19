-- Run through the Aseprite MCP run_lua_script tool.
-- Install the formal flex source into the canonical body atlas.

local root = 'E:/Study/C#/Broforce-Mods/'
local sourcePath = root .. 'CustomBro/Aquabro/tools/海王_Aseprite关键文件/01_动作主稿/haiwang_trident_flex-v2.aseprite'
local keyAtlasPath = root .. 'CustomBro/Aquabro/tools/海王_Aseprite关键文件/03_游戏图集/haiwang_trident_body_atlas.aseprite'
local spritePath = root .. 'CustomBro/Aquabro/_Mod/sprite.png'
local previewPath = root .. 'CustomBro/Aquabro/tools/archive/gestures-v2/flex-atlas-reopened.png'

local function flat(sprite, frame)
  local image = Image(sprite.width, sprite.height, ColorMode.RGB)
  image:drawSprite(sprite, frame)
  return image
end

local function copyCel(sprite, layer, frame, sourceCel)
  local old = layer:cel(frame)
  if old then sprite:deleteCel(old) end
  local cel = sprite:newCel(layer, frame, Image(sourceCel.image), sourceCel.position)
  cel.opacity = sourceCel.opacity
  cel.data = sourceCel.data
end

local function clearCells(sprite, cells)
  for _, layer in ipairs(sprite.layers) do
    local cel = layer:cel(1)
    if cel then
      local image = Image(cel.image)
      for cell in pairs(cells) do
        local left = cell % 32 * 32 - cel.position.x
        local top = math.floor(cell / 32) * 32 - cel.position.y
        for y = math.max(0, top), math.min(image.height - 1, top + 31) do
          for x = math.max(0, left), math.min(image.width - 1, left + 31) do
            image:putPixel(x, y, 0)
          end
        end
      end
      cel.image = image
    end
  end
end

local function installAtlas(atlasPath, outputPng)
  local source = assert(app.open(sourcePath))
  local atlas = assert(app.open(atlasPath))
  assert(atlas.width == 1024 and atlas.height == 1024)
  assert(#source.frames == 24)

  local cells = {}
  for i = 0, 23 do cells[352 + i] = true end
  clearCells(atlas, cells)

  local layer
  for _, candidate in ipairs(atlas.layers) do
    if candidate.name == '地面秀肌肉352-375' then layer = candidate end
  end
  if not layer then
    layer = atlas:newLayer()
    layer.name = '地面秀肌肉352-375'
  end

  local image = Image(1024, 1024, ColorMode.RGB)
  for i = 1, 24 do
    image:drawImage(flat(source, i), Point((351 + i) % 32 * 32, math.floor((351 + i) / 32) * 32))
  end
  local old = layer:cel(1)
  if old then atlas:deleteCel(old) end
  local cel = atlas:newCel(layer, 1, image, Point(0, 0))
  cel.data = '地面秀肌肉；身体格352-375；来源=haiwang_trident_flex-v2.aseprite；空手；武器层留空'
  atlas:saveAs(atlasPath)

  local reopened = assert(app.open(atlasPath))
  local flattened = flat(reopened, 1)
  flattened:saveAs(outputPng)
  reopened:close()
  source:close()
  atlas:close()
end

installAtlas(keyAtlasPath, previewPath)
print('installed body cells 352-375 into the canonical atlas and exported sprite.png')
