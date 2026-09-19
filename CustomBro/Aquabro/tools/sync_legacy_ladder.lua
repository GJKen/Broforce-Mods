-- 仅同步旧版梯子兼容格；保留两个图集各自的其他像素及源稿图层。
-- 参数 root 为 Aquabro 项目目录，可通过 Aseprite --script-param 传入。
local root = assert(app.params['root'], 'Missing Aquabro root')
if root:sub(-1) ~= '/' then root = root .. '/' end
local sourcePath = root .. 'tools/海王_Aseprite关键文件/03_游戏图集/haiwang_trident_body_atlas.aseprite'
local pngPath = root .. '_Mod/sprite.png'
local source = assert(app.open(sourcePath))
local png = assert(app.open(pngPath))
assert(#source.frames == 1 and #png.frames == 1)
assert(source.width == 1024 and source.height == 1024)
assert(png.width == 1024 and png.height == 1024)

local function flatten(sprite)
    local image = Image(sprite.width, sprite.height, ColorMode.RGB)
    image:drawSprite(sprite, 1)
    return image
end
local function same(a, b, label)
    for p in a:pixels() do
        local other = b:getPixel(p.x, p.y)
        assert(p() == other or (app.pixelColor.rgbaA(p()) == 0 and
            app.pixelColor.rgbaA(other) == 0), label .. ': pixel mismatch')
    end
end

local beforeSource, beforePng = flatten(source), flatten(png)
local strip = Image(128, 32, ColorMode.RGB)
-- 停留沿用站姿；移动三相取正式跑步中的不同腿姿。
-- 禁止再复制 1–3：这三格仍保留旧角色头型及棋盘纹衣服。
local cells = {0, 34, 36, 38}
for phase, cell in ipairs(cells) do
    local rect = Rectangle(cell % 32 * 32, math.floor(cell / 32) * 32, 32, 32)
    local image = Image(beforeSource, rect)
    same(image, Image(beforePng, rect), 'Source/runtime pose ' .. cell)
    strip:drawImage(image, Point((phase - 1) * 32, 0))
end

local function install(sprite, layers)
    for _, layer in ipairs(layers) do
        if layer.isVisible then
            if layer.isGroup then
                install(sprite, layer.layers)
            else
                local cel = layer:cel(1)
                if cel then
                    local image = Image(cel.image)
                    for y = math.max(0, 160 - cel.position.y), math.min(image.height - 1, 191 - cel.position.y) do
                        for x = math.max(0, 768 - cel.position.x), math.min(image.width - 1, 895 - cel.position.x) do
                            image:putPixel(x, y, 0)
                        end
                    end
                    cel.image = image
                end
            end
        end
    end
end
install(source, source.layers)
local layer
for _, candidate in ipairs(source.layers) do
    if candidate.name == 'Legacy ladder 184-187' then layer = candidate end
end
if not layer then layer = source:newLayer(); layer.name = 'Legacy ladder 184-187' end
local cel = layer:cel(1)
if cel then cel.image = Image(strip); cel.position = Point(768, 160)
else source:newCel(layer, 1, strip, Point(768, 160)) end

-- PNG 与源稿在其他动作上可能已有独立修改，不能整图覆盖。
assert(#png.layers == 1 and png.layers[1]:cel(1))
local pngCel = png.layers[1]:cel(1)
local pngImage = Image(pngCel.image)
for p in strip:pixels() do
    pngImage:putPixel(768 + p.x - pngCel.position.x, 160 + p.y - pngCel.position.y, p())
end
pngCel.image = pngImage

for _, pair in ipairs({{source, beforeSource}, {png, beforePng}}) do
    local after = flatten(pair[1])
    for p in after:pixels() do
        if p.x < 768 or p.x >= 896 or p.y < 160 or p.y >= 192 then
            assert(p() == pair[2]:getPixel(p.x, p.y), 'Changed a non-ladder pixel')
        end
    end
    same(Image(after, Rectangle(768, 160, 128, 32)), strip, 'Installed ladder strip')
end
source:saveAs(sourcePath)
png:saveAs(pngPath)
print('Updated legacy ladder cells 184-187 from Aquabro poses 0/34/36/38; other pixels preserved.')
