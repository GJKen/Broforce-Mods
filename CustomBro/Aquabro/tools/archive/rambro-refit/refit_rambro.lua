-- 用 Aseprite MCP 执行。仅生成临时修订稿，不覆盖用户源文件。
-- 双臂、躯干和裤靴取原版逐像素轮廓；外观取当前 held-v2。
local root = app.params.root or 'E:/Study/C#/Broforce-Mods/CustomBro/BroTemplate/'
local work = app.params.work or 'C:/Users/5700G/AppData/Local/Temp/haiwang-rambro-proportion/'
app.fs.makeAllDirectories(work)
local pc = app.pixelColor
local function rgb(r,g,b) return pc.rgba(r,g,b,255) end
local C = {
  hair=rgb(255,221,94), hairMid=rgb(232,175,51), hairDark=rgb(167,116,42),
  gold=rgb(247,154,60), orange=rgb(224,122,24), armorDark=rgb(168,86,15),
  green=rgb(28,90,24), waist=rgb(47,138,42), boot=rgb(18,63,20),
  skin=rgb(233,166,140), skinMid=rgb(215,148,121), skinLow=rgb(207,140,113),
  skinShade=rgb(203,134,107), skinDeep=rgb(181,124,101), neck=rgb(170,115,93),
  faceShade=rgb(195,134,109), black=rgb(0,0,0),
  shirt=rgb(39,70,32), shirtShade=rgb(30,54,25),
  pants=rgb(111,64,27), pantsShade=rgb(76,39,9)
}
local function blank(w,h) return Image(w or 32,h or 32,ColorMode.RGB) end
local function key(x,y) return y*32+x end
local function opaque(im,x,y)
  return x>=0 and x<im.width and y>=0 and y<im.height and pc.rgbaA(im:getPixel(x,y))>0
end
local function red(v) return pc.rgbaA(v)>0 and pc.rgbaR(v)>0 and pc.rgbaG(v)==0 and pc.rgbaB(v)==0 end
local function globalCel(s,n,f)
  local im=blank();local c=s.layers[n]:cel(f)
  if c then im:drawImage(c.image,c.position) end
  return im
end
local function put(im,x,y,v)
  assert(x>=0 and x<32 and y>=0 and y<32,'像素超出 32×32')
  im:putPixel(x,y,v)
end
local held=app.open(root..'tools/haiwang_trident_held-v2.aseprite')
assert(held and held.width==32 and held.height==32 and #held.frames==1)
local standing=globalCel(held,1,1)
local head=blank()
for p in standing:pixels() do
  local v=p()
  if v==C.hair or v==C.hairMid or v==C.hairDark or
    (p.x>=15 and p.x<=18 and p.y>=14 and p.y<=16 and pc.rgbaA(v)>0) then
    head:putPixel(p.x,p.y,v)
  end
end
head:saveAs(work..'appearance-head.png')
local native=Image{fromFile=root..'../../Broforce_src/GameAssets/hero/RAMBO_anim_1024x512.png'}

-- 面部右缘、原版面部起始行、转身方向。头部只作整像素定位。
-- 原版第二轮横移露出侧后脑；第 115、116 格面部被换手遮住。
local faces={
 [76]={17,15,'profile'},[77]={16,15,'profile'},
 [78]={16,16,'front'},[79]={15,16,'front'},[80]={15,16,'front'},
 [81]={16,17,'front'},[82]={17,16,'front'},[83]={17,15,'front'},
 [84]={17,14,'front'},[85]={17,14,'front'},
 [86]={16,15,'front'},[87]={15,15,'front'},
 [88]={16,16,'profile'},[89]={15,16,'profile'},[90]={15,16,'profile'},
 [91]={16,17,'profile'},[92]={17,16,'profile'},[93]={17,15,'profile'},
 [94]={17,14,'profile'},[95]={17,14,'profile'},
 [107]={18,14,'front'},[108]={19,15,'front'},[109]={19,16,'front'},
 [110]={18,16,'front'},[111]={18,16,'front'},[112]={18,15,'front'},
 [113]={18,14,'profile'},[114]={19,15,'profile'},[115]={19,16,'back'},
 [116]={18,16,'back'},[117]={18,16,'profile'},[118]={18,15,'profile'},
 [119]={18,15,'front'},[120]={18,15,'front'},[121]={18,15,'front'},
 [122]={18,15,'front'},[123]={18,15,'front'},[124]={18,15,'front'}
}
local skinMap={
 [C.skin]=C.skin,[C.skinMid]=C.skinMid,[C.skinLow]=C.skinLow,
 [C.skinDeep]=C.skinShade,[C.neck]=C.skinDeep,[C.faceShade]=C.skinLow
}
local wallHands={
 [76]={{9.5,11.5},{20.5,22.5}},[77]={{7.5,12.5},{19.5,21.5}},
 [78]={{18,14.5},{10,24.5}},[79]={{19.5,12.5},{9,24.5}},
 [80]={{19.5,12.5},{9,24.5}},[81]={{19.5,14.5},{9,24.5}},
 [82]={{19.5,16.5},{9,24.5}},[83]={{19.5,18.5},{9,23.5}},
 [84]={{19.5,20.5},{9,22.5}},[85]={{20.5,20.5},{9,22.5}},
 [86]={{8.5,11.5},{18.5,21.5}},[87]={{6.5,12.5},{18.5,22.5}},
 [88]={{17.5,14.5},{7.5,24.5}},[89]={{19.5,14.5},{7.5,24.5}},
 [90]={{19.5,14.5},{7.5,24.5}},[91]={{19.5,16.5},{7.5,24.5}},
 [92]={{19.5,18.5},{7.5,24.5}},[93]={{19.5,20.5},{7.5,23.5}},
 [94]={{20.5,22.5},{7.5,22.5}},[95]={{20.5,21.5},{7.5,22.5}}
}
local function isFace(ref,x,y,v,f)
  if y<f[2] or y>f[2]+1 then return false end
  if ref==109 then return x>=18 and x<=19 and (v==C.skin or v==C.faceShade) end
  if ref==115 then return x==18 and y==16 and v==C.neck end
  if ref==116 then return false end
  if ref>=113 and ref<=118 then
    return x>=f[1]-1 and x<=f[1] and (v==C.skinLow or v==C.skin)
  end
  local left=f[3]=='profile' and f[1]-1 or f[1]-3
  return x>=left and x<=f[1] and (v==C.skin or v==C.faceShade)
end
local function isNeck(ref,x,y,v,f)
  if v~=C.neck then return false end
  if ref>=78 and ref<=87 then return true end
  if (ref>=107 and ref<=109) or ref>=119 then
    return y>=f[2]+2 and y<=f[2]+3 and x>=f[1]-5 and x<=f[1]+1
  end
  return false
end
local function groups(mask)
  local seen,out={},{}
  for y=0,31 do for x=0,31 do
    local k=key(x,y)
    if mask[k] and not seen[k] then
      local group,pos={{x,y}},1
      seen[k]=true
      while pos<=#group do
        local q=group[pos];pos=pos+1
        for dy=-1,1 do for dx=-1,1 do
          local xx,yy=q[1]+dx,q[2]+dy
          local kk=key(xx,yy)
          if xx>=0 and xx<32 and yy>=0 and yy<32 and mask[kk] and not seen[kk] then
            seen[kk]=true;group[#group+1]={xx,yy}
          end
        end end
      end
      out[#out+1]=group
    end
  end end
  table.sort(out,function(a,b) return #a>#b end)
  return out
end
local cache,audit={},{}
local function repaint(ref)
  if cache[ref] then return cache[ref] end
  local src=Image(native,Rectangle(ref%32*32,math.floor(ref/32)*32,32,32))
  local f=assert(faces[ref]);local faceBottom=f[2]+1
  local parts={blank(),blank(),blank(),blank(),blank(),blank()}
  local pantsMask,armMask,shirtMask,neckMask={},{},{},{}
  local minPantsY,maxShirtY,minShirtX,maxShirtX=32,0,32,0
  for y=0,31 do for x=0,31 do
    local v=src:getPixel(x,y);local k=key(x,y)
    if v==C.pants or v==C.pantsShade then pantsMask[k]=true;minPantsY=math.min(minPantsY,y)
    elseif v==C.shirt or v==C.shirtShade then
      shirtMask[k]=true;maxShirtY=math.max(maxShirtY,y)
      minShirtX=math.min(minShirtX,x);maxShirtX=math.max(maxShirtX,x)
    elseif skinMap[v] and not isFace(ref,x,y,v,f) then
      if isNeck(ref,x,y,v,f) then neckMask[k]=true else armMask[k]=v end
    end
  end end
  -- 裤子相邻的黑色鞋底属于角色；独立刀柄和刀刃不进入裤靴。
  local changed=true
  while changed do
    changed=false
    for y=minPantsY,31 do for x=0,31 do
      local k=key(x,y)
      if not pantsMask[k] and src:getPixel(x,y)==C.black then
        local near=false
        for yy=math.max(minPantsY,y-1),math.min(31,y+1) do
          for xx=math.max(0,x-1),math.min(31,x+1) do if pantsMask[key(xx,yy)] then near=true end end
        end
        if near then pantsMask[k]=true;changed=true end
      end
    end end
  end
  -- 头带压住衣服的位置恢复为鳞甲；不填平双臂原有的负空间。
  for y=0,maxShirtY do
    local first,last=32,-1
    for x=0,31 do if shirtMask[key(x,y)] then first=math.min(first,x);last=math.max(last,x) end end
    for x=first,last do if red(src:getPixel(x,y)) then shirtMask[key(x,y)]=true end end
  end
  -- 仅恢复被红色头带遮住、两端仍有皮肤的腕臂像素。
  local bandRepairs={}
  for y=1,30 do for x=1,30 do
    if red(src:getPixel(x,y)) then
      local k=key(x,y)
      local vertical=armMask[key(x,y-1)] and armMask[key(x,y+1)]
      local horizontal=armMask[key(x-1,y)] and armMask[key(x+1,y)]
      if vertical or horizontal then bandRepairs[k]=C.skinLow end
    end
  end end
  for k,v in pairs(bandRepairs) do armMask[k]=v end
  local armGroups=groups(armMask)
  local splitX=math.floor((minShirtX+maxShirtX)/2)
  local supportLeft=ref==76 or ref==77 or ref==86 or ref==87
  if ref>=107 and ref<=118 then supportLeft=((ref-107)%6)>=4 end
  local armCounts={0,0}
  for gi,g in ipairs(armGroups) do
    local sum=0;for _,p in ipairs(g) do sum=sum+p[1] end
    local left=sum/#g<splitX
    local groupLayer=(left==supportLeft) and 3 or 6
    for _,p in ipairs(g) do
      local layer=groupLayer
      -- 两只手在脸前相交：上方承重手的可见段与下方换手分别可编辑。
      if ref==110 then layer=(p[2]<=12 or p[1]==21) and 3 or 6 end
      put(parts[layer],p[1],p[2]-1,skinMap[armMask[key(p[1],p[2])]] or armMask[key(p[1],p[2])])
      armCounts[layer==3 and 1 or 2]=armCounts[layer==3 and 1 or 2]+1
    end
  end
  -- 鳞甲纹理沿用 held-v2 的竖向高光及底缘暗色；尺寸随原版躯干。
  local center=math.floor((minShirtX+maxShirtX)/2)
  for y=0,31 do for x=0,31 do
    local k=key(x,y);local v=src:getPixel(x,y)
    if pantsMask[k] then
      local c=C.boot
      if v==C.pants then c=y==minPantsY and C.waist or C.green
      elseif v==C.pantsShade then c=C.green end
      put(parts[1],x,y-1,c)
    end
    if shirtMask[k] then
      local c=((x-center+15)%2==0) and C.gold or C.orange
      if y==maxShirtY or (v==C.shirtShade and x<=minShirtX+1) then c=C.armorDark
      elseif v==C.shirtShade then c=((x-center+15)%2==0) and C.orange or C.armorDark end
      put(parts[2],x,y-1,c)
    elseif neckMask[k] then put(parts[2],x,y-1,C.neck) end
  end end
  local hx,hy=f[1]-18,faceBottom-17
  for p in head:pixels() do
    local v=p()
    if pc.rgbaA(v)>0 then
      local xx,yy=p.x+hx,p.y+hy
      local faceColor=v~=C.hair and v~=C.hairMid and v~=C.hairDark
      if faceColor and (f[3]=='back' or (f[3]=='profile' and p.x<17)) then
        v=p.x%2==0 and C.hairDark or C.hairMid
      end
      -- 原版可见的手臂始终盖在头发前，避免抬手帧吞掉手腕。
      if not armMask[key(xx,yy+1)] then put(parts[4],xx,yy,v) end
    end
  end
  -- 背向动作在颈后保留金色长发，补足原版后脑遮住的身体轮廓。
  for y=f[2]+1,f[2]+4 do for x=math.max(0,f[1]-7),f[1]-2 do
    if src:getPixel(x,y)==C.black and not opaque(parts[4],x,y-1) then
      put(parts[4],x,y-1,(x%3==0 and C.hairMid or C.hairDark))
    end
  end end
  local seam=ref==76 and {10,13} or ((ref==108 or ref==114) and {13,12} or nil)
  if seam then put(parts[4],seam[1],seam[2],C.hairDark) end
  local flat=blank();for _,im in ipairs(parts) do flat:drawImage(im) end
  local hand,freeHand
  if wallHands[ref] then
    hand={wallHands[ref][1][1],wallHands[ref][1][2]-1}
    freeHand={wallHands[ref][2][1],wallHands[ref][2][2]-1}
  elseif ref<119 then
    local phase=(ref-107)%6
    hand={({22.5,22.5,21.5,18.5,14.5,10.5})[phase+1],8.5}
    local other={ {9.5,9.5},{12.5,11.5},{16.5,12.5},{19.5,13.5},{21.5,11.5},{22.5,10.5} }
    freeHand={other[phase+1][1],other[phase+1][2]-1}
  else hand={20.5,8.5};freeHand={9.5,18} end
  local contactX,contactY,contacts=0,0,0
  for p in parts[6]:pixels() do if pc.rgbaA(p())>0 then
    local touches=false
    for _,d in ipairs({{-1,0},{1,0},{0,-1},{0,1}}) do
      local xx,yy=p.x+d[1],p.y+1+d[2]
      if xx>=0 and xx<32 and yy>=0 and yy<32 and (shirtMask[key(xx,yy)] or neckMask[key(xx,yy)]) then touches=true end
    end
    if touches then contactX=contactX+p.x;contactY=contactY+p.y;contacts=contacts+1 end
  end end
  local shoulder=contacts>0 and {math.floor(contactX/contacts*2+0.5)/2,math.floor(contactY/contacts*2+0.5)/2} or {0,0}
  if armCounts[2]==0 then freeHand={0,0} end
  local entry={reference=ref,head_offset={hx,hy},direction=f[3],arm_pixels=armCounts,
    arm_components=#armGroups,headband_arm_repairs={},head_seam=seam,
    support=supportLeft and 'left' or 'right',anchor=hand,free_hand=freeHand,shoulder=shoulder}
  for k in pairs(bandRepairs) do entry.headband_arm_repairs[#entry.headband_arm_repairs+1]={k%32,math.floor(k/32)-1} end
  local armRef=blank()
  for k,v in pairs(armMask) do put(armRef,k%32,math.floor(k/32)-1,skinMap[v] or v) end
  armRef:saveAs(work..'reference-arms-'..ref..'.png')
  for n,im in ipairs(parts) do im:saveAs(work..'repaint-'..ref..'-layer-'..n..'.png') end
  flat:saveAs(work..'repaint-'..ref..'.png')
  audit[#audit+1]=entry
  cache[ref]={parts=parts,flat=flat,audit=entry}
  return cache[ref]
end

local names={'wall','hanging','climbing'}
local references={
 wall={76,77,78,79,80,86,87,88,89,90},hanging={},climbing={}
}
for i=107,124 do references.hanging[#references.hanging+1]=i end
for i=76,95 do references.climbing[#references.climbing+1]=i end
for _,name in ipairs(names) do
  local s=app.open(root..'tools/haiwang_trident_'..name..'-v2.aseprite')
  assert(s and #s.layers==6 and #s.frames==#references[name])
  local compare=blank(320,math.ceil(#s.frames/10)*96)
  compare:clear(rgb(52,57,66))
  for i,ref in ipairs(references[name]) do
    local x,y=(i-1)%10*32,math.floor((i-1)/10)*96
    local original=Image(native,Rectangle(ref%32*32,math.floor(ref/32)*32,32,32))
    local aligned=blank();aligned:drawImage(original,Point(0,-1))
    compare:drawImage(aligned,Point(x,y))
    local before=blank();before:drawSprite(s,i);compare:drawImage(before,Point(x,y+32))
    local result=repaint(ref)
    for n,im in ipairs(result.parts) do
      local cel=s.layers[n]:cel(i);local data=cel.data
      local geometry=result.audit
      data=data:gsub('抓附手=[^；]+','抓附手='..geometry.support)
      data=data:gsub('抓附点=[^；]+','抓附点='..geometry.anchor[1]..','..geometry.anchor[2])
      data=data:gsub('活动肩点=[^；]+','活动肩点='..geometry.shoulder[1]..','..geometry.shoulder[2])
      data=data:gsub('手掌点=[^；]+','手掌点='..geometry.free_hand[1]..','..geometry.free_hand[2])
      data=data:gsub('倾角=[^；]+','倾角=0')
      data=data:gsub('接缝补点=[^；]+','接缝补点='..(geometry.head_seam and 1 or 0))
      s:deleteCel(cel)
      local c=s:newCel(s.layers[n],i,Image(im),Point(0,0))
      c.data=data..'；比例修正=原版动作逐像素；外观基准=tools/haiwang_trident_held-v2.aseprite；原版对齐=0,-1'..
        '；手臂取形=原版可见皮肤轮廓及头带遮挡补点；肩点为可见接缝中心，0,0表示遮挡'
    end
    local after=blank();after:drawSprite(s,i);compare:drawImage(after,Point(x,y+64))
  end
  s.data=s.data..'；2026-09-15 按 Rambro 身体格逐像素重画双臂与动作轮廓，外观采用最新 held-v2。'
  s:saveAs(work..'haiwang_trident_'..name..'-v2.aseprite')
  compare:resize(compare.width*5,compare.height*5)
  compare:saveAs(work..name..'-comparison.png')
  s:close()
end
local file=io.open(work..'refit-audit.json','w');file:write(json.encode(audit));file:close()
held:close()
