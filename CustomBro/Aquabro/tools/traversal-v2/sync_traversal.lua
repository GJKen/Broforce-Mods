-- Aseprite MCP：从当前分层源稿生成待核对图集；输出到临时目录，核对后再替换项目图集。
local root=app.params['root'] or 'E:/Study/C#/Broforce-Mods/CustomBro/Aquabro/'
local work=app.params['work'] or 'C:/Users/5700G/AppData/Local/Temp/haiwang-unarmed-traversal/'
local pc=app.pixelColor
local function rgb(r,g,b) return pc.rgba(r,g,b,255) end
local skin,mid,shade=rgb(233,166,140),rgb(215,148,121),rgb(181,124,101)
local function blank(w,h) return Image(w or 32,h or 32,ColorMode.RGB) end
local function round(n) return math.floor(n+0.500001) end
local function pixel(im,x,y,c)
  x,y=round(x),round(y)
  assert(x>=0 and x<im.width and y>=0 and y<im.height,'像素越界 '..x..','..y)
  im:putPixel(x,y,c)
end
local function shifted(im,dx,dy)
  local out=blank()
  for p in im:pixels() do if pc.rgbaA(p())>0 then pixel(out,p.x+dx,p.y+dy,p()) end end
  return out
end
local function stroke(im,a,b,color,width)
  local dx,dy=b[1]-a[1],b[2]-a[2]
  local n=math.max(math.abs(dx),math.abs(dy),1)
  for i=0,n do
    local x,y=round(a[1]+dx*i/n),round(a[2]+dy*i/n)
    pixel(im,x,y,color)
    if width>1 then
      if math.abs(dy)>=math.abs(dx) then pixel(im,x+1,y,color) else pixel(im,x,y+1,color) end
    end
  end
end
local function arm(shoulder,elbow,hand)
  local im=blank()
  shoulder={math.max(1,shoulder[1]),shoulder[2]}
  stroke(im,shoulder,elbow,shade,2);stroke(im,elbow,hand,shade,2)
  stroke(im,{shoulder[1]-1,shoulder[2]},{elbow[1]-1,elbow[2]},mid,2)
  stroke(im,{elbow[1]-1,elbow[2]},{hand[1]-1,hand[2]},mid,2)
  for y=round(hand[2]),round(hand[2])+1 do
    for x=round(hand[1])-1,round(hand[1])+1 do pixel(im,x,y,y==round(hand[2]) and skin or mid) end
  end
  return im
end
local function traversalIndex(body)
  if body>=76 and body<=95 then return body-76 end
  if body>=107 and body<=124 then return 20+body-107 end
  if body>=160 and body<=173 then return 38+body-160 end
  if body>=192 and body<=197 then return 52+body-192 end
  if body>=512 and body<=529 then return 58+body-512 end
  error('未知身体格 '..body)
end
local function globalCel(layer,frame)
  local cel=layer:cel(frame);assert(cel,'缺少图层帧 '..frame)
  local im=blank();im:drawImage(cel.image,cel.position);return im
end
local function merged(parts,first,last)
  local im=blank()
  for i=first,last do im:drawImage(parts[i]) end
  return im
end
local function cropCell(im,cell)
  return Image(im,Rectangle(cell%32*32,math.floor(cell/32)*32,32,32))
end
local function opaque(im,x,y)
  return x>=0 and y>=0 and x<im.width and y<im.height and pc.rgbaA(im:getPixel(x,y))>0
end
local source=app.open(root..'tools/haiwang_trident_traversal-v2.aseprite')
assert(source and source.width==32 and source.height==32 and #source.frames==86 and #source.layers==6,'源稿结构已变化')
local body=app.open(root..'tools/haiwang_trident_body_atlas.aseprite')
local gun=app.open(root..'tools/haiwang_trident_gun_atlas.aseprite')
assert(body and gun and body.width==1024 and gun.width==1024)
local ziplineCharge=dofile(root..'tools/traversal-v2/zipline_charge_source.lua')(root)
local oldGun=blank(gun.width,gun.height);oldGun:drawSprite(gun,1)
local poses={}
for pose=1,8 do poses[pose]=cropCell(oldGun,48+pose) end
local bodyCells,canonical={},{}
local manifest={'frame,tag,stage,reference,body_cell,support,anchor_x,anchor_y,shoulder_x,shoulder_y,grip_x,grip_y,angle,duration_ms'}
for f=1,86 do
  local data=source.layers[1]:cel(f).data
  local cell=tonumber(data:match('身体格=(%d+)'));assert(cell,'源稿 cel 缺少身体格')
  local sx,sy=data:match('持戟肩点=([%d%.%-]+),([%d%.%-]+)')
  if not sx then sx,sy=data:match('活动肩点=([%d%.%-]+),([%d%.%-]+)') end
  assert(sx and sy,'源稿 cel 缺少肩点')
  local parts={}
  for i=1,6 do parts[i]=globalCel(source.layers[i],f) end
  local flat=blank();flat:drawSprite(source,f);flat:saveAs(work..'source-'..f..'.png')
  local tag=data:match('动作=(.-)；');local stage=data:match('阶段=(.-)；')
  -- 抓附手使用 ASCII 名称，避免旧 cel 备注中残缺的中文分隔符混入 CSV。
  local ref=data:match('原版参考=(%d+)');local support=data:match('抓附手=([A-Za-z]+)')
  local ax,ay=data:match('抓附点=([%d%.%-]+),([%d%.%-]+)')
  local gx,gy=data:match('握点=([%d%.%-]+),([%d%.%-]+)')
  if not gx then gx,gy=data:match('手掌点=([%d%.%-]+),([%d%.%-]+)') end
  local angle=data:match('倾角=([%d%.%-]+)')
  manifest[#manifest+1]=table.concat({f,tag,stage,ref,cell,support,ax,ay,sx,sy,gx,gy,angle,round(source.frames[f].duration*1000)},',')
  if canonical[cell] then
    local previous=canonical[cell].flat
    for y=0,31 do for x=0,31 do assert(flat:getPixel(x,y)==previous:getPixel(x,y),'贴墙与攀爬共用格的源稿不一致：'..cell) end end
  else
    canonical[cell]={frame=f,parts=parts,flat=flat,shoulder={tonumber(sx),tonumber(sy)}}
    bodyCells[#bodyCells+1]=cell
  end
end
table.sort(bodyCells);assert(#bodyCells==76)
body:crop(Rectangle(0,0,1024,1024));gun:crop(Rectangle(0,0,1024,1024))

-- 只清理本次明确接管的可见格；旧参考图层和其他动作像素保持原样。
local function clearCells(s,cells)
  for _,layer in ipairs(s.layers) do if layer.isVisible then
    local cel=layer:cel(1)
    if cel then
      local copy=Image(cel.image)
      for _,cell in ipairs(cells) do
        local bx,by=cell%32*32-cel.position.x,math.floor(cell/32)*32-cel.position.y
        for y=math.max(0,by),math.min(copy.height-1,by+31) do
          for x=math.max(0,bx),math.min(copy.width-1,bx+31) do copy:putPixel(x,y,0) end
        end
      end
      cel.image=copy
    end
  end end
end
local gunCells={}
for i=73,756 do gunCells[#gunCells+1]=i end
clearCells(body,bodyCells);clearCells(gun,gunCells)
local function outputLayer(s,name)
  for _,l in ipairs(s.layers) do if l.name==name then l.isVisible=true;return l end end
  local l=s:newLayer();l.name=name;return l
end
local bodyLayer=outputLayer(body,'海王贴墙悬挂攀爬爬梯滑索 v2')
local heldLayer=outputLayer(gun,'海王地形移动配对持戟 v2')
local attackLayer=outputLayer(gun,'海王地形移动配对攻击 v2')
local bodyImage,heldImage,attackImage=blank(1024,1024),blank(1024,1024),blank(1024,1024)
local audit={'body_cell,source_frame,weapon_cell,pose,offset_x,grip_x,grip_y,arm_pixels,weapon_pixels'}
local nativePoseNames={'持戟','抬手','蓄力','蓄满','离手','收手','突刺准备','突刺','突刺收手'}

for _,cell in ipairs(bodyCells) do
  local entry=canonical[cell];local parts=entry.parts
  local bodyFrame=merged(parts,1,4)
  if cell<512 then
    assert(parts[5]:isEmpty(),'空手动作的三叉戟层仍有像素：'..cell)
    bodyFrame:drawImage(parts[6])
  end
  bodyImage:drawImage(bodyFrame,Point(cell%32*32,math.floor(cell/32)*32))
  if cell>=512 then
  local baseCell=73+traversalIndex(cell)*9
  heldImage:drawImage(merged(parts,5,6),Point(baseCell%32*32,math.floor(baseCell/32)*32))
  audit[#audit+1]=table.concat({cell,entry.frame,baseCell,0,0,-1,-1,-1,-1},',')
  for pose=1,8 do
    local offsetX=pose==7 and 9 or 0
    local weapon=Image(poses[pose])
    local shoulder={entry.shoulder[1]-offsetX,entry.shoulder[2]}
    local elbow,grip
    if pose==1 then
      weapon=shifted(weapon,5,0);grip={25,17};elbow={math.max(shoulder[1],23),shoulder[2]+2}
    elseif pose==2 or pose==3 then
      weapon=shifted(weapon,0,-4);grip={19,6}
      elbow={entry.shoulder[1]<15 and 7 or 25,9}
    elseif pose==4 then
      grip={28,20};elbow={math.max(shoulder[1]+3,23),shoulder[2]+2}
    elseif pose==5 then
      grip={math.min(27,math.max(5,shoulder[1]+(shoulder[1]<15 and -2 or 2))),shoulder[2]+4}
      elbow={shoulder[1],shoulder[2]+3}
      -- 恢复水光跟随本帧自由手，避免沿用旧悬挂图中的孤立位置。
      weapon=blank()
      pixel(weapon,grip[1]+1,grip[2]-2,rgb(56,191,183))
      pixel(weapon,grip[1]+2,grip[2]-3,rgb(217,255,240))
    elseif pose==6 then
      grip={20,20};elbow={math.max(shoulder[1],18),shoulder[2]+3}
    elseif pose==7 then
      -- 与现有突刺相同地向前偏移 9 像素；肩点预先回移，显示后仍接在身体上。
      grip={19,20};elbow={math.max(shoulder[1]+3,11),shoulder[2]+2}
    else
      grip={21,20};elbow={math.max(shoulder[1],19),shoulder[2]+3}
    end
    local hand=arm(shoulder,elbow,grip)
    -- 武器经过背后时由身体遮挡；不让横杆盖过脸部和胸甲。持戟帧不使用此遮罩。
    for y=0,31 do for x=0,31 do
      if opaque(bodyFrame,x+offsetX,y) then weapon:putPixel(x,y,0) end
      if opaque(parts[4],x+offsetX,y) then hand:putPixel(x,y,0) end
    end end
    -- 持火时原版滑索只取524–529；使用已修订的肩侧回拉主稿。
    if cell>=524 and cell<=529 and pose<=3 then
      weapon=Image(ziplineCharge[pose].weapon)
      hand=Image(ziplineCharge[pose].hand)
      grip=ziplineCharge[pose].grip
    end
    local combined=Image(weapon);combined:drawImage(hand)
    local gunCell=baseCell+pose
    attackImage:drawImage(combined,Point(gunCell%32*32,math.floor(gunCell/32)*32))
    local armCount,weaponCount=0,0
    for p in hand:pixels() do if pc.rgbaA(p())>0 then armCount=armCount+1 end end
    for p in weapon:pixels() do if pc.rgbaA(p())>0 then weaponCount=weaponCount+1 end end
    assert(armCount>0,'攻击帧缺少自由手臂')
    assert(pose~=4 or weaponCount==0,'离手帧残留持戟')
    audit[#audit+1]=table.concat({cell,entry.frame,gunCell,pose,offsetX,grip[1],grip[2],armCount,weaponCount},',')
    if cell==79 or cell==111 or cell==160 or cell==171 or cell==197 or cell==526 then
      local preview=blank(48,32);preview:drawImage(bodyFrame);preview:drawImage(combined,Point(offsetX,0))
      preview:saveAs(work..'attack-'..cell..'-'..pose..'.png')
    end
  end
  end
end
local function writeCel(s,l,im)
  local cel=l:cel(1)
  if cel then cel.image=im;cel.position=Point(0,0) else s:newCel(l,1,im,Point(0,0)) end
end
writeCel(body,bodyLayer,bodyImage)
writeCel(gun,heldLayer,heldImage)
writeCel(gun,attackLayer,attackImage)
bodyLayer.data='地形移动 v2，76 个身体格；前 58 格为空手且包含完整双臂；滑索使用 512–529。'
heldLayer.data='仅滑索配对持戟，武器格 595+(身体格-512)*9；73–594 已清空，四类空手动作的双臂合入身体图集。'
attackLayer.data='仅滑索保留八种攻击姿态。身体524–529的回拉、蓄力、蓄满读取独立蓄力稿；突刺显示时 +9px，肩点已预先回移。原 0–72 格不变，73–594 为空。'
body:saveAs(work..'body-atlas.aseprite');gun:saveAs(work..'gun-atlas.aseprite')
local bodyFlat=blank(1024,1024);bodyFlat:drawSprite(body,1);bodyFlat:saveAs(work..'sprite.png')
local gunFlat=blank(1024,1024);gunFlat:drawSprite(gun,1);gunFlat:saveAs(work..'gunSprite.png')
local file=io.open(work..'attack-audit.csv','w');file:write(table.concat(audit,'\n'));file:close()
file=io.open(work..'traversal-manifest.csv','w');file:write(table.concat(manifest,'\n'));file:close()
print('Body cells='..#bodyCells..'; unarmed body cells=58; zipline weapon cells=162; body/gun=1024x1024')
source:close();body:close();gun:close()
for _,name in ipairs({'body','gun'}) do
  local saved=app.open(work..name..'-atlas.aseprite')
  local im=blank(saved.width,saved.height);im:drawSprite(saved,1);im:saveAs(work..name..'-reopened.png');saved:close()
end
print('Saved atlases reopened for independent pixel validation')
