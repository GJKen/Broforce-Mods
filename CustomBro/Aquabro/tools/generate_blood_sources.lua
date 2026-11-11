local root=assert(app.params['root'])
local folder=root..'/aseprite-source/03_特殊技能/血水'
app.fs.makeAllDirectories(folder)
-- Current WM_Hell BloodPool sampled from the running game via GPU readback.
-- Native shadow/body/highlight: 730F1C / 951223 / B70D23.
local blue={0x1961A5,0x1E76C8,0x1F7BD1,0x35BFF3,0x71CEF0,0xBDEDFF,0xFFFFFF}
local blood={0x730F1C,0x851121,0x951223,0xB70D23,0xC83B4A,0xE46B76,0xF59BA3}
for _,kind in ipairs({'tidal-wave','shark-hunt','maelstrom','abyssal-spring'}) do
  local suffix=kind=='shark-hunt' and '-fx-v7.aseprite' or '-fx-v3.aseprite'
  local sprite=assert(app.open(root..'/aseprite-source/03_特殊技能/skill-'..kind..suffix))
  local visited={}
  for _,cel in ipairs(sprite.cels) do
    local water=kind~='shark-hunt' or cel.layer==sprite.layers[3] or cel.layer==sprite.layers[7] or cel.layer==sprite.layers[8]
    if water then
      local id=cel.image.id
      if not visited[id] then
        local image=cel.image:clone()
        for pixel in image:pixels() do
          local value=pixel();local alpha=app.pixelColor.rgbaA(value)
          if alpha>0 then
            local r,g,b=app.pixelColor.rgbaR(value),app.pixelColor.rgbaG(value),app.pixelColor.rgbaB(value)
            local best,distance=1,math.huge
            for i,c in ipairs(blue) do
              local dr,dg,db=r-math.floor(c/65536)%256,g-math.floor(c/256)%256,b-c%256
              local d=dr*dr+dg*dg+db*db
              if d<distance then best,distance=i,d end
            end
            local c=blood[best]
            pixel(app.pixelColor.rgba(math.floor(c/65536)%256,math.floor(c/256)%256,c%256,alpha))
          end
        end
        visited[id]=image
      end
      cel.image=visited[id]
    end
  end
  local palette=Palette(#blood)
  for i,c in ipairs(blood) do palette:setColor(i-1,Color{r=math.floor(c/65536)%256,g=math.floor(c/256)%256,b=c%256}) end
  sprite:setPalette(palette)
  sprite:saveAs(folder..'/skill-'..kind..'-blood.aseprite')
  print('SAVED blood source',kind,#sprite.frames,#sprite.layers)
  sprite:close()
end
local preview=Sprite(384,512,ColorMode.RGB)
for row,entry in ipairs({{'tidal-wave',5},{'shark-hunt',8},{'maelstrom',12},{'abyssal-spring',10}}) do
  local kind,frame=entry[1],entry[2]
  local suffix=kind=='shark-hunt' and '-fx-v7.aseprite' or '-fx-v3.aseprite'
  local base=assert(app.open(root..'/aseprite-source/03_特殊技能/skill-'..kind..suffix))
  local target=assert(app.open(folder..'/skill-'..kind..'-blood.aseprite'))
  local a,b=Image(128,128,ColorMode.RGB),Image(128,128,ColorMode.RGB)
  a:drawSprite(base,frame);b:drawSprite(target,frame)
  for column=0,2 do
    local image=a:clone();local t=column/2
    for y=0,127 do for x=0,127 do
      local p,q=a:getPixel(x,y),b:getPixel(x,y)
      local function channel(fn) return math.floor(fn(p)+(fn(q)-fn(p))*t) end
      image:putPixel(x,y,app.pixelColor.rgba(channel(app.pixelColor.rgbaR),channel(app.pixelColor.rgbaG),channel(app.pixelColor.rgbaB),app.pixelColor.rgbaA(p)))
    end end
    local layer=preview:newLayer();layer.name=kind..'-'..column
    preview:newCel(layer,1,image,Point(column*128,(row-1)*128))
  end
  base:close();target:close()
end
preview:saveCopyAs(folder..'/preview.png');preview:close()
