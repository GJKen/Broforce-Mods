local root=assert(app.params['root'])
local work=assert(app.params['work'])
local keyRoot=app.params['key'] or root..'tools/海王_Aseprite关键文件/'
if keyRoot:sub(-1)~='/' then keyRoot=keyRoot..'/' end
local actions=keyRoot..'01_动作主稿/'
local terrain=keyRoot..'02_地形独立稿/'
local atlases=keyRoot..'03_游戏图集/'
local function open(dir,name,frames)
  local s=app.open(dir..'haiwang_trident_'..name..'.aseprite')
  assert(s and #s.frames==frames,name..': unexpected frame count')
  return s
end
local function blank() return Image(32,32,ColorMode.RGB) end
local function flat(s,f)
  local im=Image(s.width,s.height,ColorMode.RGB);im:drawSprite(s,f);return im
end
local function same(a,b,label)
  assert(a.width==b.width and a.height==b.height,label..': dimensions')
  for p in a:pixels() do assert(p()==b:getPixel(p.x,p.y),label..': pixel mismatch at '..p.x..','..p.y) end
end
local function part(s,f,first,last)
  local im=blank()
  for i=first,last do
    local l=s.layers[i]
    if l.isVisible then
      assert(l.opacity==255 and l.blendMode==BlendMode.NORMAL,'Unsupported source layer blend')
      local c=l:cel(f)
      if c then assert(c.opacity==255);im:drawImage(c.image,c.position) end
    end
  end
  return im
end
local function copyCel(s,layer,f,source)
  local old=layer:cel(f)
  local data=old and old.data or ''
  if old then s:deleteCel(old) end
  local c=s:newCel(layer,f,source and Image(source.image) or blank(),source and source.position or Point(0,0))
  c.opacity=source and source.opacity or 255;c.data=data
end
local body,gun=open(atlases,'body_atlas',1),open(atlases,'gun_atlas',1)
assert(body.width==1024 and body.height==1024 and gun.width==1024 and gun.height==1024)
local held,run,jump,crouch=open(actions,'held-v2',1),open(actions,'run-v3',8),open(actions,'jump-v2',18),open(actions,'crouch-v2',9)
assert(#held.layers==4 and #run.layers==8 and #jump.layers==6 and #crouch.layers==7)
for i=1,6 do copyCel(jump,jump.layers[i],9,run.layers[i+2]:cel(1)) end
for i=1,6 do
  local index=({1,0,0,2,3,4})[i]
  copyCel(jump,jump.layers[i],18,index>0 and held.layers[index]:cel(1) or nil)
end
same(flat(jump,9),flat(run,1),'Landing to run-v3')
same(flat(jump,18),flat(held,1),'Landing to standing')
jump:saveAs(actions..'haiwang_trident_jump-v2.aseprite')
local bodyCells,gunCells,checks,bodyOnlyChecks={},{},{},{}
local function put(cells,cell,im)
  assert(not cells[cell],'Duplicate cell '..cell);cells[cell]=im
end
local function movement(s,f,firstWeapon,cells,weapon)
  local b,w=part(s,f,1,firstWeapon-1),part(s,f,firstWeapon,#s.layers)
  local merged=Image(b);merged:drawImage(w)
  same(merged,flat(s,f),'Movement split '..s.filename..':'..f)
  for _,cell in ipairs(cells) do
    put(bodyCells,cell,b)
    checks[#checks+1]={body=cell,gun=weapon,image=merged}
  end
  put(gunCells,weapon,w)
end
local function paired(index) return 9+math.floor(index/7)*16+index%7 end
movement(held,1,3,{0},paired(0))
for f=1,8 do movement(run,f,7,{31+f,95+f},paired(f)) end
for f=1,6 do movement(jump,f,5,{63+f},paired(8+f)) end
for f=10,15 do movement(jump,f,5,{60+f},paired(5+f)) end
for f=7,9 do movement(jump,f,5,{41+f},paired(14+f)) end
for f=16,18 do movement(jump,f,5,{88+f},paired(8+f)) end
for f=1,9 do movement(crouch,f,5,{f==1 and 6 or 38+f},63+f) end
local extraSources={}
local function bodyOnly(name,frames,firstCell)
  local s=open(actions,name,frames)
  assert(#s.layers==6,name..': expected six editable layers')
  assert(s.layers[6].name:find('三叉戟'),name..': final layer must be the empty trident layer')
  for f=1,frames do
    assert(part(s,f,6,6):isEmpty(),name..': trident pixels found in frame '..f)
    local cell,frame=firstCell+f-1,flat(s,f)
    put(bodyCells,cell,frame)
    bodyOnlyChecks[#bodyOnlyChecks+1]={body=cell,image=frame}
  end
  extraSources[#extraSources+1]=s
end
bodyOnly('death-v2',2,4)
bodyOnly('insemination-death-v3',8,235)
bodyOnly('highfive-v2',6,17)
bodyOnly('flex-v2',24,352)
bodyOnly('roll-v2',13,51)
bodyOnly('waterwall-v2',8,145)
bodyOnly('chimney-flip-v2',12,203)
local attackSource=open(actions,'actions',36)
assert(#attackSource.layers==4 and not attackSource.layers[1].isVisible,'Attack body reference must stay hidden')
for f=1,36 do
  local group=math.floor((f-1)/9)
  local cell=math.floor(group/2)*32+(group%2)*16+(f-1)%9
  put(gunCells,cell,flat(attackSource,f))
end
local function install(s,cells,name)
  for _,l in ipairs(s.layers) do if l.isVisible then
    local c=l:cel(1)
    if c then
      local im=Image(c.image)
      for cell in pairs(cells) do
        local left,top=cell%32*32-c.position.x,math.floor(cell/32)*32-c.position.y
        for y=math.max(0,top),math.min(im.height-1,top+31) do
          for x=math.max(0,left),math.min(im.width-1,left+31) do im:putPixel(x,y,0) end
        end
      end
      c.image=im
    end
  end end
  local l
  for _,existing in ipairs(s.layers) do if existing.name==name then l=existing end end
  if not l then l=s:newLayer();l.name=name end
  local im=Image(1024,1024,ColorMode.RGB)
  for cell,frame in pairs(cells) do im:drawImage(frame,Point(cell%32*32,math.floor(cell/32)*32)) end
  local c=l:cel(1)
  if c then c.image=im;c.position=Point(0,0) else s:newCel(l,1,im,Point(0,0)) end
end
install(body,bodyCells,'Key sources movement')
install(gun,gunCells,'Key sources movement and attacks')
local bodyFlat,gunFlat=flat(body,1),flat(gun,1)
for _,c in ipairs(checks) do
  local b=Image(bodyFlat,Rectangle(c.body%32*32,math.floor(c.body/32)*32,32,32))
  b:drawImage(Image(gunFlat,Rectangle(c.gun%32*32,math.floor(c.gun/32)*32,32,32)))
  same(b,c.image,'Atlas composite '..c.body)
end
for _,c in ipairs(bodyOnlyChecks) do
  same(Image(bodyFlat,Rectangle(c.body%32*32,math.floor(c.body/32)*32,32,32)),c.image,
    'Body-only atlas cell '..c.body)
end
for cell,im in pairs(gunCells) do same(Image(gunFlat,Rectangle(cell%32*32,math.floor(cell/32)*32,32,32)),im,'Weapon cell '..cell) end
body:saveAs(atlases..'haiwang_trident_body_atlas.aseprite')
gun:saveAs(atlases..'haiwang_trident_gun_atlas.aseprite')
local runSheet=Image(256,32,ColorMode.RGB)
for f=1,8 do runSheet:drawImage(flat(run,f),Point((f-1)*32,0)) end
runSheet:saveAs(work..'run-v3.png')
local projectile=open(actions,'projectile',2)
local projectileSheet=Image(64,32,ColorMode.RGB)
for f=1,2 do projectileSheet:drawImage(flat(projectile,f),Point((f-1)*32,0)) end
projectileSheet:saveAs(work..'Trident.png')
local traversal=open(actions,'traversal-v2',86)
local ranges={{'wall-v2',10,0},{'hanging-v2',18,10},{'climbing-v2',20,28},{'ladder-v2',20,48},{'zipline-v2',18,68}}
for _,range in ipairs(ranges) do
  local s=open(terrain,range[1],range[2])
  assert(#s.layers==6 and #traversal.layers==6)
  if range[1]=='zipline-v2' and app.params['zipline'] then
    local fixed=app.open(app.params['zipline'])
    assert(#fixed.frames==18 and #fixed.layers==6)
    for f=13,18 do copyCel(s,s.layers[5],f,fixed.layers[5]:cel(f)) end
    s:saveAs(terrain..'haiwang_trident_zipline-v2.aseprite');fixed:close()
  end
  for f=1,range[2] do
    for i=1,6 do copyCel(traversal,traversal.layers[i],range[3]+f,s.layers[i]:cel(f)) end
    traversal.frames[range[3]+f].duration=s.frames[f].duration
    same(flat(traversal,range[3]+f),flat(s,f),'Independent traversal source '..range[1]..':'..f)
  end
  s:close()
end
traversal:saveAs(actions..'haiwang_trident_traversal-v2.aseprite')
for _,s in ipairs(extraSources) do s:close() end
for _,s in ipairs({body,gun,held,run,jump,crouch,attackSource,projectile,traversal}) do s:close() end
print('Verified 93 body cells (44 movement + 49 additional actions), 36 paired weapons, 36 attack poses, 86 traversal source frames and both landing transitions.')
