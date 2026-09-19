local actions=assert(app.params['actions'])
local bodyPath=assert(app.params['body'])
local gunPath=assert(app.params['gun'])
local spritePath=assert(app.params['sprite'])
local gunSpritePath=assert(app.params['gunSprite'])

local function flat(sprite,frame)
  local image=Image(sprite.width,sprite.height,ColorMode.RGB)
  image:drawSprite(sprite,frame or 1)
  return image
end

local function same(a,b,label)
  assert(a.width==b.width and a.height==b.height,label..': dimensions')
  for pixel in a:pixels() do
    assert(pixel()==b:getPixel(pixel.x,pixel.y),
      label..': pixel mismatch at '..pixel.x..','..pixel.y)
  end
end

local body=assert(app.open(bodyPath))
local gun=assert(app.open(gunPath))
local sprite=assert(app.open(spritePath))
local gunSprite=assert(app.open(gunSpritePath))
assert(body.width==1024 and body.height==1024 and #body.frames==1,'Body atlas structure changed')
assert(gun.width==1024 and gun.height==1024 and #gun.frames==1,'Gun atlas structure changed')
local bodyFlat,gunFlat=flat(body),flat(gun)
same(bodyFlat,flat(sprite),'Body atlas PNG')
same(gunFlat,flat(gunSprite),'Gun atlas PNG')

local definitions={
  {'death-v2',2,4},
  {'highfive-v2',6,17},
  {'roll-v2',13,51},
  {'waterwall-v2',8,145},
  {'chimney-flip-v2',12,203}
}
local checked=0
for _,definition in ipairs(definitions) do
  local name,frames,firstCell=definition[1],definition[2],definition[3]
  local source=assert(app.open(actions..'haiwang_trident_'..name..'.aseprite'))
  assert(source.width==32 and source.height==32 and #source.frames==frames,
    name..': source structure changed')
  for frame=1,frames do
    local cell=firstCell+frame-1
    local atlasCell=Image(bodyFlat,
      Rectangle(cell%32*32,math.floor(cell/32)*32,32,32))
    same(atlasCell,flat(source,frame),name..': body cell '..cell)
    checked=checked+1
  end
  source:close()
end

body:close()
gun:close()
sprite:close()
gunSprite:close()
print('Verified '..checked..' additional body cells and exact atlas/PNG exports.')
