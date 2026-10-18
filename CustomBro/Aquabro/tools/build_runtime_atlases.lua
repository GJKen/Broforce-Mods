local actions=assert(app.params['actions'])
local skills=assert(app.params['skills'])
local terrain=assert(app.params['terrain'])
local gibs=assert(app.params['gibs'])
local output=assert(app.params['output'])

local function path(dir,name)
  return app.fs.joinPath(dir,name..'.aseprite')
end

local function open(dir,name,frames)
  local sprite=assert(app.open(path(dir,name)))
  assert(#sprite.frames==frames,name..': unexpected frame count')
  return sprite
end

local function blank()
  return Image(32,32,ColorMode.RGB)
end

local function flat(sprite,frame)
  local image=Image(sprite.width,sprite.height,ColorMode.RGB)
  image:drawSprite(sprite,frame)
  return image
end

local function part(sprite,frame,first,last,originX)
  local image=blank()
  for index=first,last do
    local layer=sprite.layers[index]
    assert(layer,index..': missing layer in '..sprite.filename)
    if layer.isVisible then
      local cel=layer:cel(frame)
      if cel then image:drawImage(cel.image,Point(cel.position.x-(originX or 0),cel.position.y)) end
    end
  end
  return image
end

local function findLayer(layers,name)
  for _,layer in ipairs(layers) do
    if layer.name==name then return layer end
    if layer.isGroup then
      local found=findLayer(layer.layers,name)
      if found then return found end
    end
  end
  return nil
end

local function partByNames(sprite,frame,names,originX)
  local image=blank()
  for _,name in ipairs(names) do
    local layer=assert(findLayer(sprite.layers,name),'Missing layer in '..sprite.filename..': '..name)
    local cel=layer:cel(frame)
    if cel then image:drawImage(cel.image,Point(cel.position.x-(originX or 0),cel.position.y)) end
  end
  return image
end

local function layerPixel(layer,x,y)
  local cel=layer:cel(1)
  if not cel then return 0 end
  local sx,sy=x-cel.position.x,y-cel.position.y
  if sx<0 or sy<0 or sx>=cel.image.width or sy>=cel.image.height then return 0 end
  return cel.image:getPixel(sx,sy)
end

local body=Image(1024,640,ColorMode.RGB)
local gun=Image(1024,512,ColorMode.RGB)

local function put(atlas,cell,image)
  atlas:drawImage(image,Point((cell%32)*32,math.floor(cell/32)*32))
end

local crouch=open(actions,'haiwang_trident_crouch-v2',9)
-- Body and held arm/weapon use the same source frame and canvas origin.
-- Slot 64 is crouch idle; 65-72 are the eight crouch-walk frames.
for frame=1,9 do
  put(body,63+frame,part(crouch,frame,1,4))
  put(gun,63+frame,part(crouch,frame,5,#crouch.layers))
end
crouch:close()

local bodyOnly={
  {'haiwang_trident_death-v2',2,4},
  {'haiwang_trident_insemination-death-v4',8,235},
  {'haiwang_trident_highfive-v2',6,17},
  {'haiwang_trident_flex-v2',24,352},
  {'haiwang_trident_roll-v2',13,51},
  {'haiwang_trident_waterwall-v2',8,145},
  {'haiwang_trident_chimney-flip-v2',12,203}
}
for _,entry in ipairs(bodyOnly) do
  local sprite=open(actions,entry[1],entry[2])
  for frame=1,entry[2] do put(body,entry[3]+frame-1,flat(sprite,frame)) end
  sprite:close()
end

-- 小刀动作：前四帧是地面攻击，后四帧是空中攻击；身体与武器保持独立图层。
-- 地面帧映射到 25-28、177-180 和 128-135，空中帧映射到
-- 608-611 和 136-139，避免覆盖跑步、梯子等既有图集区域。
local knife=open(actions,'haiwang_trident_knife-v2',8)
assert(knife.width==39 and knife.height==32,'Melee source must use a 39x32 canvas')
assert(#knife.frames==8,'Melee source must use 8 frames: 4 ground and 4 air')
local bodyLayers={'身体','头和面部'}
local weaponLayers={'三叉戟','左手臂 复制','右手臂 复制'}
-- The source canvas is 39px wide while SpriteSM samples 32px cells. Keep
-- every source pixel by moving only the crop window; Aquabro.cs restores this
-- per-frame crop origin through SetGunPosition.
local weaponOrigins={0,0,2,6}
for frame=1,4 do
  put(body,24+frame,partByNames(knife,frame,bodyLayers,0))
  put(body,176+frame,partByNames(knife,frame,bodyLayers,0))
  put(gun,127+frame,partByNames(knife,frame,weaponLayers,weaponOrigins[frame]))
  put(gun,131+frame,partByNames(knife,frame,weaponLayers,weaponOrigins[frame]))
end
for frame=5,8 do
  local airFrame=frame-4
  put(body,607+airFrame,partByNames(knife,frame,bodyLayers,0))
  put(gun,135+airFrame,partByNames(knife,frame,weaponLayers,weaponOrigins[airFrame]))
end
knife:close()

local traversal=open(actions,'haiwang_trident_traversal-v2',66)
for frame=1,66 do
  -- 废弃爬梯组已删除；滑索保留原图集槽号，560-579 仍留空。
  put(body,(frame<=48 and 511 or 531)+frame,flat(traversal,frame))
end
traversal:close()

-- New-ladder cells 160-173, 192-197 and 560-579 stay transparent.
-- Preserve the four tested legacy poses, not the new-ladder source artwork.
local legacy=open(terrain,'haiwang_legacy_ladder_compat',4)
for frame=1,4 do
  put(body,183+frame,flat(legacy,frame))
end
legacy:close()

-- The death GibHolder samples these four fixed regions from sprite.png.
-- Keep the source artwork separate and copy only the mapped fragments.
local gibSource=assert(app.open(path(gibs,'haiwang_trident_death_gibs-v1')))
assert(gibSource.width==30 and gibSource.height==30,'Unexpected death gibs source dimensions')
local gibPieces={
  {layer='金发与脸部（头部碎块）',x=320,y=1,w=16,h=16,sx=0,sy=0},
  {layer='金色鳞甲与颈部（躯干碎块）',x=320,y=14,w=16,h=16,sx=0,sy=13},
  {layer='前侧手臂（双臂共用碎块）',x=341,y=5,w=8,h=8,sx=21,sy=4},
  {layer='绿色裤靴（双腿碎块）',x=341,y=19,w=8,h=8,sx=21,sy=18}
}
for _,piece in ipairs(gibPieces) do
  local layer=assert(findLayer(gibSource.layers,piece.layer),'Missing death gibs layer: '..piece.layer)
  for yy=0,piece.h-1 do
    for xx=0,piece.w-1 do
      body:putPixel(piece.x+xx,piece.y+yy,layerPixel(layer,piece.sx+xx,piece.sy+yy))
    end
  end
end
gibSource:close()

-- 旧武器素材已删除，0-35格保持透明；基类武器选帧由角色按当前身体重新配对。

-- 身体层只保存裤腿，上半身和水流在武器层中去重。
local function canvas() return Image(35,32,ColorMode.RGB) end
local function same(a,b)
  for p in a:pixels() do
    local expected=b:getPixel(p.x,p.y)
    if p()~=expected and (app.pixelColor.rgbaA(p())>0 or app.pixelColor.rgbaA(expected)>0) then return false end
  end
  return true
end
local function bounds(image)
  local x,y,right,bottom=image.width,image.height,-1,-1
  for p in image:pixels() do if app.pixelColor.rgbaA(p())>0 then
    x=math.min(x,p.x);y=math.min(y,p.y)
    right=math.max(right,p.x);bottom=math.max(bottom,p.y)
  end end
  assert(right>=x,'Empty animation component')
  return Rectangle(x,y,right-x+1,bottom-y+1)
end
local function key(image)
  local pixels={}
  for p in image:pixels() do
    pixels[#pixels+1]=string.pack('<I4',app.pixelColor.rgbaA(p())>0 and p() or 0)
  end
  return table.concat(pixels)
end
local function layerImage(layer,frame,dx,dy)
  local image=canvas()
  local cel=layer:cel(frame)
  if cel then image:drawImage(cel.image,Point(cel.position.x+(dx or 0),cel.position.y+(dy or 0))) end
  return image
end
local function upperImage(layers,frame,first,last,dx,dy)
  local image=canvas()
  for li=first,last do
    local layer=layers[li]
    if layer.isVisible then image:drawImage(layerImage(layer,frame,dx,dy)) end
  end
  return image
end
local function tagFirst(sprite,name,count)
  for _,tag in ipairs(sprite.tags) do if tag.name==name then
    assert(tag.toFrame.frameNumber-tag.fromFrame.frameNumber+1==count,'Unexpected tag length: '..name)
    return tag.fromFrame.frameNumber
  end end
  error('Missing tag: '..name)
end

local lowerKeys,upperKeys,lowerImages,upperImages={},{},{},{}
local frames={}
local function lowerCell(image)
  local cropped=blank();cropped:drawImage(image)
  local restored=canvas();restored:drawImage(cropped)
  assert(same(image,restored),'Leg pixels exceed the body cell')
  local k=key(cropped)
  if lowerKeys[k] then return lowerKeys[k] end
  local cell=612+#lowerImages
  assert(cell<640,'Too many leg variants for reserved body row')
  lowerKeys[k]=cell;lowerImages[#lowerImages+1]=cropped
  put(body,cell,cropped)
  return cell
end
local function upperFrame(image)
  local box=bounds(image)
  assert(box.width<=35 and box.height<=32,'Upper animation exceeds runtime sprite dimensions')
  local normalized=canvas()
  normalized:drawImage(Image(image,box))
  local k=key(normalized)
  local cell=upperKeys[k]
  if not cell then
    cell=160+#upperImages*2
    assert(cell%32<=30 and cell+1<512,'Upper animation atlas capacity exceeded')
    upperKeys[k]=cell;upperImages[#upperImages+1]=normalized
    put(gun,cell,normalized)
  end
  return {cell,box.x,box.y}
end
local function combine(legs,upper,dy)
  local image=canvas();image:drawImage(legs);image:drawImage(upper,Point(0,dy or 0))
  return image
end

-- 四套施法动作保留身体格 384-415；狂潮仅六帧，末两格留空。
for index,name in ipairs({'tidal-wave','shark-hunt','maelstrom','abyssal-spring'}) do
  local frameCount=name=='tidal-wave' and 6 or 8
  local castVersion=name=='shark-hunt' and 'v2' or 'v3'
  local cast=open(skills,'skill-'..name..'-cast-'..castVersion,frameCount)
  assert(cast.width==32 and cast.height==32,'Skill cast must fit 32x32 cells')
  for frame=1,frameCount do put(body,384+(index-1)*8+frame-1,flat(cast,frame)) end
  cast:close()
end

local function openJump()
  -- 正式v4包含整体下移1px的空中姿态及补齐接缝的快速下落帧。
  local filename=app.params['jumpSource'] or path(actions,'haiwang_trident_jump-v4')
  local sprite=assert(app.open(filename))
  assert(#sprite.frames==45,'Jump source must include six fast-fall frames')
  return sprite
end

-- 普通动作保留身体格号，手臂与三叉戟按可见形状去重。
-- 头部在源稿中覆盖后侧摆臂；导出到前景武器层时保留这个遮挡关系。
local movementKeys,movementImages,movementFrames={},{},{}
local function movementPart(sprite,frame,indices)
  local image=blank()
  for _,index in ipairs(indices) do
    local layer=assert(sprite.layers[index],'Missing movement layer')
    local cel=layer:cel(frame)
    if layer.isVisible and cel then image:drawImage(cel.image,cel.position) end
  end
  return image
end
local function buildMovement(sprite,count,firstCell,bodyLayers,weaponLayers,sourceFirst)
  local firstFrame=sourceFirst or 1
  for frame=firstFrame,firstFrame+count-1 do
    local base=movementPart(sprite,frame,bodyLayers)
    local weapon=movementPart(sprite,frame,weaponLayers)
    local expected=blank();expected:drawImage(flat(sprite,frame))
    for p in weapon:pixels() do
      if expected:getPixel(p.x,p.y)==base:getPixel(p.x,p.y) then p(0) end
    end
    local restored=blank();restored:drawImage(base);restored:drawImage(weapon)
    assert(same(restored,expected),'Movement layer composition changed: '..sprite.filename..' frame '..frame)
    local box=bounds(weapon)
    local normalized=blank();normalized:drawImage(Image(weapon,box))
    local k=key(normalized)
    local gunCell=movementKeys[k]
    if not gunCell then
      gunCell=36+#movementImages
      assert(gunCell<64,'Movement weapons exceed reserved gun cells 36-63')
      movementKeys[k]=gunCell;movementImages[#movementImages+1]=normalized
      put(gun,gunCell,normalized)
    end
    local bodyCell=firstCell+frame-firstFrame
    put(body,bodyCell,base)
    movementFrames[#movementFrames+1]={bodyCell,gunCell,box.x,box.y}
  end
end
local held=open(actions,'haiwang_trident_held-v2',1)
buildMovement(held,1,0,{1,2},{3,4,5});held:close()
local run=open(actions,'haiwang_trident_run-v3',29)
buildMovement(run,8,32,{3,4,6},{5,7,8});run:close()
local jump=openJump()
buildMovement(jump,18,96,{1,2,4},{3,5,6})
buildMovement(jump,6,114,{1,2,4},{3,5,6},40);jump:close()

-- 可选的旧图集只用于验证；正式构建始终由 Aseprite 源稿生成。
local referenceBody=app.params['referenceBody'] and Image{fromFile=app.params['referenceBody']}
local referenceGun=app.params['referenceGun'] and Image{fromFile=app.params['referenceGun']}
local checked=0
local function verify(image,bodyCell,gunCell)
  if not referenceBody then return end
  local expected=canvas()
  expected:drawImage(Image(referenceBody,Rectangle(bodyCell%32*32,math.floor(bodyCell/32)*32,32,32)))
  expected:drawImage(Image(referenceGun,Rectangle(gunCell%32*32,math.floor(gunCell/32)*32,3,32)),Point(32,0))
  assert(same(image,expected),'Animation pixels changed at legacy body cell '..bodyCell)
  checked=checked+1
end

local standingLegs={}
local charge=open(actions,'haiwang_trident_charge-release-blue-flow-v8',22)
for pose=1,21 do
  local frame=pose+1
  local torso=layerImage(charge.layers[1],frame)
  local legs=canvas()
  for p in torso:pixels() do if p.y>=26 then legs:putPixel(p.x,p.y,p());p(0) end end
  local upper=torso
  upper:drawImage(upperImage(charge.layers,frame,2,#charge.layers))
  standingLegs[pose]=lowerCell(legs)
  frames[#frames+1]=upperFrame(upper)
  local image=combine(legs,upper)
  assert(same(image,flat(charge,frame)),'Standing source composition changed')
  verify(image,640+pose,160+pose)
end
charge:close()

local run=open(actions,'haiwang_trident_run-v3',29)
local runGroup=assert(findLayer(run.layers,'跑步攻击适配'))
local runningLegs,runningBob,runLegImages={},{},{}
local anchor=run.layers[4]:cel(1).position.y
for phase=1,8 do
  local image=layerImage(run.layers[3],phase,1)
  runLegImages[phase]=image;runningLegs[phase]=lowerCell(image)
  runningBob[phase]=run.layers[4]:cel(phase).position.y-anchor
end
local runStart=tagFirst(run,'跑步攻击_起手',2)
tagFirst(run,'跑步攻击_蓝色水流蓄力循环',16)
for pose=1,21 do
  local frame=runStart+pose-1
  local sourceLegs=layerImage(runGroup.layers[1],frame)
  local sourcePhase=nil
  for phase=1,8 do if same(sourceLegs,runLegImages[phase]) then sourcePhase=phase;break end end
  assert(sourcePhase,'Running attack legs differ from the source cycle')
  local upper=upperImage(runGroup.layers,frame,2,#runGroup.layers,0,-runningBob[sourcePhase])
  frames[#frames+1]=upperFrame(upper)
  for phase=1,8 do
    local image=combine(runLegImages[phase],upper,runningBob[phase])
    if phase==sourcePhase then assert(same(image,flat(run,frame)),'Running source composition changed') end
    verify(image,672+(pose-1)*8+phase-1,192+(pose-1)*8+phase-1)
  end
end
run:close()

local jump=openJump()
local jumpGroup=assert(findLayer(jump.layers,'跳跃攻击适配'))
local jumpStart=tagFirst(jump,'跳跃攻击_起手',2)
tagFirst(jump,'跳跃攻击_蓝色水流蓄力循环',16)
local jumpingLegs,jumpLegImages={},{},{}
local jumpingRecovery={96,99,105,108}
for phase=1,4 do
  local image=layerImage(assert(findLayer(jump.layers,'跳跃腿部参考'..phase)),jumpStart)
  jumpLegImages[phase]=image;jumpingLegs[phase]=lowerCell(image)
end
for pose=1,21 do
  local frame=jumpStart+pose-1
  local upper=pose<21 and upperImage(jumpGroup.layers,frame,2,8) or nil
  frames[#frames+1]=upper and upperFrame(upper) or {-1,0,0}
  for phase=1,4 do
    local image
    if pose==21 then
      image=canvas();image:drawImage(flat(jump,jumpingRecovery[phase]-95),Point(1,0))
    else image=combine(jumpLegImages[phase],upper) end
    if phase==1 then assert(same(image,flat(jump,frame)),'Jumping source composition changed') end
    verify(image,840+(pose-1)*4+phase-1,360+(pose-1)*4+phase-1)
  end
end
jump:close()

local ladderSources=actions
local ladderCharge=open(ladderSources,'haiwang_trident_ladder-charge-blue-flow-v2',18)
local ladderThrow=open(ladderSources,'haiwang_trident_ladder-throw-v1',5)
local ladderLegs,ladderBob,ladderLegImages={},{},{}
for phase=1,4 do
  local name='梯上腿部参考'..phase
  local image=layerImage(assert(findLayer(ladderCharge.layers,name)),1,-1)
  assert(same(image,layerImage(assert(findLayer(ladderThrow.layers,name)),1,-1)),'Ladder leg references differ')
  ladderLegImages[phase]=image;ladderLegs[phase]=lowerCell(image)
  ladderBob[phase]=bounds(image).y-25
end
local water=tagFirst(ladderCharge,'蓝色水流蓄力循环',16)
local start=tagFirst(ladderThrow,'起手',2)
local ladderPoses={{ladderThrow,start},{ladderThrow,start+1}}
for f=water,water+15 do ladderPoses[#ladderPoses+1]={ladderCharge,f} end
for _,name in ipairs({'投掷出手','投掷收势','恢复持戟'}) do
  ladderPoses[#ladderPoses+1]={ladderThrow,tagFirst(ladderThrow,name,1)}
end
for pose,entry in ipairs(ladderPoses) do
  local source,frame=entry[1],entry[2]
  -- 投掷稿的裤腿会随身体前倾，按腰线归一上半身，不再要求裤腿形状完全相等。
  local sourceBob=bounds(layerImage(source.layers[1],frame,-1)).y-25
  assert(sourceBob==0 or sourceBob==1,'Unexpected ladder waist height')
  local upper=upperImage(source.layers,frame,2,8,-1,-sourceBob)
  frames[#frames+1]=upperFrame(upper)
  for phase=1,4 do
    local image=combine(ladderLegImages[phase],upper,ladderBob[phase])
    verify(image,924+(pose-1)*4+phase-1,444+(pose-1)*4+phase-1)
  end
end
ladderCharge:close();ladderThrow:close()

-- 滑索攻击只读取这一份正式源稿，包含起手、水流、投掷、恢复与六阶段裤腿。
local zipSource=open(terrain,'haiwang_trident_zipline-charge-blue-flow-v3',27)
local zipLegStart=tagFirst(zipSource,'滑索腿部六阶段',6)
local ziplineLegs,zipLegImages={},{}
-- 先保留原固定腿型的格号，避免改动其他既有格。
lowerCell(layerImage(zipSource.layers[1],zipLegStart+5,-2))
for phase=1,6 do
  local image=layerImage(zipSource.layers[1],zipLegStart+phase-1,-2)
  zipLegImages[phase]=image;ziplineLegs[phase]=lowerCell(image)
end
local zipWater=tagFirst(zipSource,'蓝色水流蓄力循环',16)
local zipStart=tagFirst(zipSource,'起手',2)
local zipPoses={{zipSource,zipStart},{zipSource,zipStart+1}}
for f=zipWater,zipWater+15 do zipPoses[#zipPoses+1]={zipSource,f} end
for _,name in ipairs({'投掷出手','投掷收势','恢复持戟'}) do
  zipPoses[#zipPoses+1]={zipSource,tagFirst(zipSource,name,1)}
end
for _,entry in ipairs(zipPoses) do
  local source,frame=entry[1],entry[2]
  assert(source.width==36 and source.height==32,'Unexpected zipline canvas')
  local sourceLegs=layerImage(source.layers[1],frame,-2)
  local matchingLegs=false
  for _,image in ipairs(zipLegImages) do if same(image,sourceLegs) then matchingLegs=true;break end end
  assert(matchingLegs,'Zipline preview legs differ from original frames 13-18')
  local upper=Image(36,32,ColorMode.RGB)
  assert(#source.layers==8 and source.layers[1].name=='悬空裤靴' and
    source.layers[7].name=='金发与脸部','Unexpected zipline production layers')
  -- 保留正式源稿的遮挡顺序，允许编辑者调整衣服、手臂和武器的前后关系。
  for _,name in ipairs({'金色鳞甲与颈部','投掷手臂','金色三叉戟','握柄手指','抓绳手臂_固定支点','蓝色水流汇聚'}) do
    assert(findLayer(source.layers,name),'Missing zipline layer: '..name)
  end
  for l=2,#source.layers do
    assert(source.layers[l].isVisible,'Hidden zipline production layer')
    local c=source.layers[l]:cel(frame)
    if c then upper:drawImage(c.image,c.position) end
  end
  local reconstructed=Image(36,32,ColorMode.RGB)
  reconstructed:drawImage(sourceLegs,Point(2,0));reconstructed:drawImage(upper)
  assert(same(reconstructed,flat(source,frame)),'Zipline source composition changed')
  local mapping=upperFrame(upper)
  mapping[2]=mapping[2]-2 -- 源稿左右留白，游戏支点仍与原滑索稿一致。
  frames[#frames+1]=mapping
end
zipSource:close()

local layout=assert(io.open(app.params['layout'] or app.fs.joinPath(output,'TridentAtlas.cs'),'w'))
layout:write('// 由 tools/build_runtime_atlases.lua 生成，与贴图一起重建。\nnamespace AquabroMod\n{\n    internal static class TridentAtlas\n    {\n')
local function writeArray(name,array)
  local values={};for _,v in ipairs(array) do values[#values+1]=tostring(v) end
  layout:write('        internal static readonly int[] '..name..' = { '..table.concat(values,', ')..' };\n')
end
writeArray('StandingLegs',standingLegs)
writeArray('RunningLegs',runningLegs);writeArray('RunningBob',runningBob)
writeArray('JumpingLegs',jumpingLegs);writeArray('JumpingRecovery',jumpingRecovery)
writeArray('LadderLegs',ladderLegs);writeArray('LadderBob',ladderBob)
writeArray('ZiplineLegs',ziplineLegs)
layout:write('        // 普通待机、跑步、跳跃及落地：身体格、手臂/武器格、裁剪X、裁剪Y。\n        internal static readonly int[,] MovementFrames = {\n')
for _,frame in ipairs(movementFrames) do layout:write('            { '..table.concat(frame,', ')..' },\n') end
layout:write('        };\n')
layout:write('        // 每组21个攻击阶段，依次为站立、跑步、跳跃、梯子、滑索；列为武器格、X、Y。\n        internal static readonly int[,] UpperFrames = {\n')
for _,frame in ipairs(frames) do layout:write('            { '..table.concat(frame,', ')..' },\n') end
layout:write('        };\n    }\n}\n');layout:close()
assert(#frames==105)
local gunHeight=math.ceil((160+#upperImages*2)/32)*32
assert(body:saveAs(app.fs.joinPath(output,'sprite.png')))
assert(Image(gun,Rectangle(0,0,1024,gunHeight)):saveAs(app.fs.joinPath(output,'gunSprite.png')))
if app.params['report'] then
local report=assert(io.open(app.params['report'],'w'))
report:write(json.encode({movementWeaponCount=#movementImages,movementFrames=movementFrames,legCount=#lowerImages,upperCount=#upperImages,verifiedLegacyFrames=checked,bodyHeight=640,gunHeight=gunHeight,frames=frames,standingLegs=standingLegs,runningLegs=runningLegs,runningBob=runningBob,jumpingLegs=jumpingLegs,jumpingRecovery=jumpingRecovery,ladderLegs=ladderLegs,ladderBob=ladderBob,ziplineLegs=ziplineLegs}));report:close()
end
print('Built layered movement: '..#movementFrames..' frames, '..#movementImages..' shared arm/weapon poses')
print('Built compact attack atlas: '..#lowerImages..' legs, '..#upperImages..' upper poses; verified '..checked..' legacy frames')
