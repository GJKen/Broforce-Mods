local root=assert(app.params['root'])
local output=assert(app.params['output'])
for _,kind in ipairs({'tidal-wave','maelstrom','abyssal-spring'}) do
 local suffix='-fx-v3.aseprite'
 local base=assert(app.open(root..'/aseprite-source/03_特殊技能/skill-'..kind..suffix))
 for stage=1,2 do
  local name='skill-'..kind..'-acid-'..stage
  local sprite=assert(app.open(root..'/aseprite-source/03_特殊技能/酸蚀水体/'..name..'.aseprite'))
  assert(sprite.width==base.width and sprite.height==base.height and #sprite.frames==#base.frames,'Acid layout changed')
  local atlas=Image(128*#sprite.frames,128,ColorMode.RGB)
  for f=1,#sprite.frames do
   local image=Image(128,128,ColorMode.RGB);image:drawSprite(sprite,f)
   local original=Image(128,128,ColorMode.RGB);original:drawSprite(base,f)
   for y=0,127 do for x=0,127 do
    assert(app.pixelColor.rgbaA(image:getPixel(x,y))==app.pixelColor.rgbaA(original:getPixel(x,y)),name..': alpha/mask changed')
   end end
   atlas:drawImage(image,Point((f-1)*128,0))
  end
  atlas:saveAs(output..'/'..name..'.png');sprite:close()
  print('EXPORTED',name,'alpha verified')
 end
 base:close()
end
