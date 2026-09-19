-- Export the final reopened source as an enlarged, numbered review sheet.
-- Character pixels are copied with nearest-neighbour blocks only for preview.

local root = 'E:/Study/C#/Broforce-Mods/'
local sourcePath = root .. 'CustomBro/Aquabro/tools/海王_Aseprite关键文件/01_动作主稿/haiwang_trident_flex-v2.aseprite'
local outDir = root .. 'CustomBro/Aquabro/tools/gestures-v2/flex'
app.fs.makeAllDirectories(outDir)

local pc = app.pixelColor
local function rgba(r, g, b) return pc.rgba(r, g, b, 255) end
local bg = rgba(24, 27, 31)
local label = rgba(255, 239, 180)
local border = rgba(94, 103, 112)
local source = assert(app.open(sourcePath))
assert(source.width == 32 and source.height == 32 and #source.frames == 24)

local glyphs = {
  ['0'] = {'111','101','101','101','111'},
  ['1'] = {'110','010','010','010','111'},
  ['2'] = {'111','001','111','100','111'},
  ['3'] = {'111','001','111','001','111'},
  ['4'] = {'101','101','111','001','001'},
  ['5'] = {'111','100','111','001','111'},
  ['6'] = {'111','100','111','101','111'},
  ['7'] = {'111','001','010','010','010'},
  ['8'] = {'111','101','111','101','111'},
  ['9'] = {'111','101','111','001','111'},
  ['/'] = {'001','001','010','100','100'}
}

local function put(im, x, y, color)
  if x >= 0 and x < im.width and y >= 0 and y < im.height then im:putPixel(x, y, color) end
end

local function fill(im, color)
  for y = 0, im.height - 1 do
    for x = 0, im.width - 1 do im:putPixel(x, y, color) end
  end
end

local function text(im, x, y, value, scale, color)
  local cursor = x
  for i = 1, #value do
    local glyph = glyphs[value:sub(i, i)]
    if glyph then
      for gy = 1, 5 do
        for gx = 1, 3 do
          if glyph[gy]:sub(gx, gx) == '1' then
            for dy = 0, scale - 1 do
              for dx = 0, scale - 1 do put(im, cursor + (gx - 1) * scale + dx, y + (gy - 1) * scale + dy, color) end
            end
          end
        end
      end
      cursor = cursor + 4 * scale
    else
      cursor = cursor + 2 * scale
    end
  end
end

local function flat(frame)
  local im = Image(32, 32, ColorMode.RGB)
  im:drawSprite(source, frame)
  return im
end

local function enlarge(src, dst, ox, oy, scale)
  for y = 0, 31 do
    for x = 0, 31 do
      local value = src:getPixel(x, y)
      for dy = 0, scale - 1 do
        for dx = 0, scale - 1 do dst:putPixel(ox + x * scale + dx, oy + y * scale + dy, value) end
      end
    end
  end
end

local tileW, tileH, scale = 280, 280, 8
local sheet = Image(tileW * 4, tileH * 6, ColorMode.RGB)
fill(sheet, bg)
for i = 1, 24 do
  local frame = flat(i)
  local col = (i - 1) % 4
  local row = math.floor((i - 1) / 4)
  local ox, oy = col * tileW + 12, row * tileH + 20
  for x = 0, 255 do
    put(sheet, ox + x, oy - 1, border)
    put(sheet, ox + x, oy + 256, border)
  end
  for y = 0, 255 do
    put(sheet, ox - 1, oy + y, border)
    put(sheet, ox + 256, oy + y, border)
  end
  enlarge(frame, sheet, ox, oy, scale)
  text(sheet, col * tileW + 12, row * tileH + 5,
    string.format('%02d/%03d', i, 351 + i), 3, label)
  local enlargedFrame = Image(32 * scale, 32 * scale, ColorMode.RGB)
  enlarge(frame, enlargedFrame, 0, 0, scale)
  enlargedFrame:saveAs(outDir .. string.format('/frame-%02d-cell-%03d.png', i, 351 + i))
end
sheet:saveAs(outDir .. '/frames.png')

source:close()
print('ground flex preview exported: ' .. outDir .. '/frames.png')
