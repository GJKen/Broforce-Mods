-- 同步当前寄生死亡源稿，仅更新身体 235–242 格。
local root=assert(app.params['root'])
if root:sub(-1)~='/' then root=root..'/' end
local key=root..'tools/海王_Aseprite关键文件/'
local source=assert(app.open(key..'01_动作主稿/haiwang_trident_insemination-death-v4.aseprite'))
assert(source.width==32 and source.height==32 and #source.frames==8)
local paths={key..'03_游戏图集/haiwang_trident_body_atlas.aseprite',root..'_Mod/sprite.png'}
local frames={}
local function flat(s,f)
 local im=Image(s.width,s.height,ColorMode.RGB);im:drawSprite(s,f);return im
end
for f=1,8 do frames[f]=flat(source,f) end
local function inside(x,y) return y>=224 and y<256 and x>=352 and x<608 end
local backup=assert(app.params['backup'])
for index,path in ipairs(paths) do
 local s=assert(app.open(path))
 assert(s.width==1024 and s.height==1024 and #s.frames==1)
 s:saveCopyAs(backup..(index==1 and '/body-before.aseprite' or '/sprite-before.png'))
 local before=flat(s,1)
 local function clear(layers)
  for _,l in ipairs(layers) do
   if l.isVisible then
    if l.isGroup then clear(l.layers)
    else
     local c=l:cel(1)
     if c then
      local im=Image(c.image)
      for p in im:pixels() do
       if inside(p.x+c.position.x,p.y+c.position.y) then p(0) end
      end
      c.image=im
     end
    end
   end
  end
 end
 clear(s.layers)
 local strip=Image(256,32,ColorMode.RGB)
 for f=1,8 do strip:drawImage(frames[f],Point((f-1)*32,0)) end
 local l=s:newLayer();l.name='寄生死亡 v4 235-242'
 s:newCel(l,1,strip,Point(352,224))
 local after=flat(s,1)
 for p in after:pixels() do
  local expected
  if inside(p.x,p.y) then expected=strip:getPixel(p.x-352,p.y-224)
  else expected=before:getPixel(p.x,p.y) end
  assert(p()==expected or (app.pixelColor.rgbaA(p())==0 and app.pixelColor.rgbaA(expected)==0),'Unexpected pixel difference')
 end
 if index==1 then s:saveAs(path) else after:saveAs(path) end
end
local report=assert(io.open(backup..'/result.txt','w'))
report:write('PASS: v4 frames 1-8 match atlas/runtime cells 235-242; all other pixels preserved.\n')
report:close()
