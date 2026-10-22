local root=assert(app.params['root'])
local output=assert(app.params['output'])
local folder=root..'/aseprite-source/03_特殊技能/酸蚀水体'
app.fs.makeAllDirectories(folder)
-- Sandworm Spit Ball Acid sampled through Unity MCP: 3BB000,48C400,5EDC00,7EFF00,349303.
local blue={0x1961A5,0x1E76C8,0x1F7BD1,0x35BFF3,0x71CEF0,0xBDEDFF,0xFFFFFF}
local palettes={
 {0x397B68,0x4D9B77,0x58AE7C,0x83D69B,0xA9E8BA,0xD8F7D9,0xF1FFE9},
 {0x286E03,0x349303,0x3BB000,0x48C400,0x5EDC00,0x7EFF00,0xCAFF8A}
}
for _,kind in ipairs({'tidal-wave','maelstrom','abyssal-spring'}) do
 for stage=1,2 do
  local suffix='-fx-v3.aseprite'
  local sprite=assert(app.open(root..'/aseprite-source/03_特殊技能/skill-'..kind..suffix))
  local visited={}
  for _,cel in ipairs(sprite.cels) do
    local originalId=cel.image.id
    if not visited[originalId] then
          local image=cel.image:clone()
     for it in image:pixels() do
      local value=it();local alpha=app.pixelColor.rgbaA(value)
      if alpha>0 then
       local r,g,b=app.pixelColor.rgbaR(value),app.pixelColor.rgbaG(value),app.pixelColor.rgbaB(value)
       local best,dist=1,math.huge
       for i,c in ipairs(blue) do
        local cr,cg,cb=math.floor(c/65536)%256,math.floor(c/256)%256,c%256
        local d=(r-cr)^2+(g-cg)^2+(b-cb)^2
        if d<dist then best,dist=i,d end
       end
       local c=palettes[stage][best]
       it(app.pixelColor.rgba(math.floor(c/65536)%256,math.floor(c/256)%256,c%256,alpha))
      end
     end
     visited[originalId]=image
    end
    cel.image=visited[originalId]
  end
  local name='skill-'..kind..'-acid-'..stage
  sprite:saveAs(folder..'/'..name..'.aseprite')
  local atlas=Image(128*#sprite.frames,128,ColorMode.RGB)
  for f=1,#sprite.frames do
   local image=Image(128,128,ColorMode.RGB);image:drawSprite(sprite,f)
   atlas:drawImage(image,Point((f-1)*128,0))
  end
  atlas:saveAs(output..'/'..name..'.png')
  print('EXPORTED',name)
  sprite:close()
 end
end
