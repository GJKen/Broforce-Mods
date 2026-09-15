-- 通过 Aseprite MCP 的 run_lua_script 执行。只生成临时审稿文件，不覆盖用户源稿。
local root = 'E:/Study/C#/Broforce-Mods/CustomBro/BroTemplate/'
local work = 'C:/Users/5700G/AppData/Local/Temp/haiwang-traversal-v2/'
local pc = app.pixelColor
local function rgba(r,g,b) return pc.rgba(r,g,b,255) end
local C = {
  skin=rgba(233,166,140), mid=rgba(215,148,121), shade=rgba(181,124,101),
  neck=rgba(170,115,93), gold=rgba(247,154,60), orange=rgba(224,122,24),
  armorDark=rgba(168,86,15), green=rgba(47,138,42), pants=rgba(28,90,24),
  boot=rgba(18,63,20), hair=rgba(255,221,94), hairMid=rgba(232,175,51),
  hairDark=rgba(167,116,42), shaft=rgba(232,175,51), shaftDark=rgba(134,87,18)
}
local function blank() return Image(32,32,ColorMode.RGB) end
local function round(n) return math.floor(n+0.500001) end
local function put(im,x,y,c)
  x,y=round(x),round(y)
  assert(x>=0 and x<im.width and y>=0 and y<im.height, '像素越界 '..x..','..y)
  im:putPixel(x,y,c)
end
local function getLayer(s,name)
  for _,l in ipairs(s.layers) do if l.name==name then return l end end
  error('找不到图层 '..name)
end
local function celImage(s,l,f)
  local im=blank(); local cel=l:cel(f)
  assert(cel,'缺少 cel '..l.name..' '..f)
  im:drawImage(cel.image,cel.position); return im
end
local function shifted(im,dx,dy)
  local out=blank()
  for p in im:pixels() do if pc.rgbaA(p())>0 then put(out,p.x+dx,p.y+dy,p()) end end
  return out
end
local held=app.open(root..'tools/haiwang_trident_held-v2.aseprite')
local jump=app.open(root..'tools/haiwang_trident_jump-v2.aseprite')
local run=app.open(root..'tools/haiwang_trident_run-v3.aseprite')
local crouch=app.open(root..'tools/haiwang_trident_crouch-v2.aseprite')
assert(held and jump and run and crouch,'缺少当前源稿')
local heldFlat=blank();heldFlat:drawSprite(held,1)
local jumpFlat=blank();jumpFlat:drawSprite(jump,18)
for y=0,31 do for x=0,31 do
  assert(heldFlat:getPixel(x,y)==jumpFlat:getPixel(x,y),'站姿与跳跃末帧已变化，须重新确认最新分层')
end end
local head=celImage(jump,jump.layers[4],18)
local rawPole=celImage(held,held.layers[3],1)
local names={'绿色裤靴','金色鳞甲与颈部','抓附手臂','金发与脸部','金色三叉戟','持戟手臂'}
local standing={}
for i,n in ipairs({'绿色裤靴','金色鳞甲与颈部','后侧手臂','金发与脸部','金色三叉戟','持戟手臂'}) do
  standing[i]=celImage(jump,jump.layers[i],18)
end
local original=Image{fromFile='E:/Study/C#/Broforce-Mods/Broforce_src/GameAssets/hero/RAMBO_anim_1024x512.png'}
local lightTorso=rgba(39,70,32);local darkTorso=rgba(30,54,25)
local lightPants=rgba(111,64,27);local darkPants=rgba(76,39,9);local black=rgba(0,0,0)

-- 只取原版躯干和裤靴的姿势轮廓；头部、纹理、肤色、双臂和三叉戟均来自海王。
local function body(ref,rear,extraX,extraY)
  local source=Image(original,Rectangle(ref%32*32,math.floor(ref/32)*32,32,32))
  local pants,armor=blank(),blank()
  local minX,minY,maxX,maxY=32,32,0,0
  local dx,dy=extraX or 0,-1+(extraY or 0)
  local legMask={}
  for y=0,31 do for x=0,31 do
    local v=source:getPixel(x,y)
    if v==lightPants or v==darkPants then legMask[y*32+x]=true end
  end end
  for y=19,31 do for x=0,31 do
    if source:getPixel(x,y)==black then
      for yy=math.max(19,y-1),math.min(31,y+1) do
        for xx=math.max(0,x-1),math.min(31,x+1) do
          if legMask[yy*32+xx] then legMask[y*32+x]=true end
        end
      end
    end
  end end
  for y=0,31 do for x=0,31 do
    local v=source:getPixel(x,y)
    if v==lightTorso or v==darkTorso then
      local xx,yy=x+dx,y+dy
      local color=v==darkTorso and C.armorDark or (x%2==0 and C.gold or C.orange)
      put(armor,xx,yy,color)
      minX=math.min(minX,xx);minY=math.min(minY,yy);maxX=math.max(maxX,xx);maxY=math.max(maxY,yy)
    elseif legMask[y*32+x] then
      put(pants,x+dx,y+dy,v==black and C.boot or (v==lightPants and C.green or C.pants))
    end
  end end
  local cx=round((minX+maxX)/2)
  -- 原版上臂和武器遮住的胸甲须补全，不能把移走旧手臂后留下的缺口带入新稿。
  for y=minY,maxY do
    local first,last=32,-1
    for x=0,31 do if pc.rgbaA(armor:getPixel(x,y))>0 then first=math.min(first,x);last=math.max(last,x) end end
    for x=first,last do
      if pc.rgbaA(armor:getPixel(x,y))==0 then put(armor,x,y,x%2==0 and C.gold or C.orange) end
    end
  end
  for y=minY-1,minY+1 do for x=cx-2,cx+2 do put(armor,x,y,C.neck) end end
  local hair=shifted(head,cx-15,minY-17)
  if rear then
    -- 背向爬梯：保留最新金发外轮廓，把脸部改为同色发束。
    for p in hair:pixels() do
      local v=p()
      if pc.rgbaA(v)>0 and v~=C.hair and v~=C.hairMid and v~=C.hairDark then
        p((p.x%3==0 or p.y>=minY-1) and C.hairDark or C.hairMid)
      end
    end
  end
  return pants,armor,hair,{left={minX,minY+1},right={maxX+1,minY+1},cx=cx,top=minY}
end

local function stroke(im,a,b,color,width)
  local dx,dy=b[1]-a[1],b[2]-a[2]
  local n=math.max(math.abs(dx),math.abs(dy),1)
  for i=0,n do
    local x,y=round(a[1]+dx*i/n),round(a[2]+dy*i/n)
    put(im,x,y,color)
    if width>1 then
      if math.abs(dy)>=math.abs(dx) then put(im,x+1,y,color)
      else put(im,x,y+1,color) end
    end
  end
end
local function arm(shoulder,elbow,hand)
  local out=blank()
  stroke(out,shoulder,elbow,C.shade,3);stroke(out,elbow,hand,C.shade,3)
  stroke(out,{shoulder[1]-1,shoulder[2]},{elbow[1]-1,elbow[2]},C.mid,2)
  stroke(out,{elbow[1]-1,elbow[2]},{hand[1]-1,hand[2]},C.mid,2)
  put(out,shoulder[1]-1,shoulder[2]-1,C.skin);put(out,shoulder[1],shoulder[2]-1,C.skin)
  local hx,hy=round(hand[1]),round(hand[2])
  for y=hy,hy+1 do for x=hx-1,hx+1 do put(out,x,y,(y==hy) and C.skin or C.mid) end end
  return out
end

local function pole(angle,gx,gy,upper,lower)
  upper,lower=upper or 20,lower or 7
  local raw=Image(64,64,ColorMode.RGB)
  for p in rawPole:pixels() do
    if pc.rgbaA(p())>0 and p.y<=13 then raw:putPixel(p.x+8,32-upper+p.y-1,p()) end
  end
  for y=32-upper+13,32+lower-2 do
    raw:putPixel(31,y,C.shaft);raw:putPixel(32,y,C.shaftDark)
  end
  for x=30,33 do raw:putPixel(x,32+lower-1,(x==30 or x==33) and C.shaftDark or C.shaft) end
  raw:putPixel(31,32+lower,C.shaftDark);raw:putPixel(32,32+lower,C.shaftDark)
  local co,si=math.cos(math.rad(angle)),math.sin(math.rad(angle))
  local minX,minY,maxX,maxY=100,100,-100,-100
  for p in raw:pixels() do if pc.rgbaA(p())>0 then
    local x=gx+(p.x-31.5)*co-(p.y-32)*si
    local y=gy+(p.x-31.5)*si+(p.y-32)*co
    minX=math.min(minX,round(x));maxX=math.max(maxX,round(x));minY=math.min(minY,round(y));maxY=math.max(maxY,round(y))
  end end
  assert(maxX-minX<32 and maxY-minY<32,'三叉戟旋转后过大')
  gx=gx+math.max(0,-minX)-math.max(0,maxX-31)
  gy=gy+math.max(0,-minY)-math.max(0,maxY-31)
  local out=blank()
  for y=0,31 do for x=0,31 do
    local xx=round(31.5+(x-gx)*co+(y-gy)*si)
    local yy=round(32-(x-gx)*si+(y-gy)*co)
    if xx>=0 and xx<64 and yy>=0 and yy<64 then out:putPixel(x,y,raw:getPixel(xx,yy)) end
  end end
  return out,{gx,gy}
end

-- 仅补轮廓内 1–4 像素的封闭接缝；保留抬臂、弯腿之间的大块负空间。
local function repairSeams(parts,g)
  local flat=blank()
  for _,im in ipairs(parts) do flat:drawImage(im) end
  local seen,repairs={},0
  for y=0,31 do for x=0,31 do
    local key=y*32+x
    if not seen[key] and pc.rgbaA(flat:getPixel(x,y))==0 then
      local q={{x,y}};seen[key]=true;local pos=1;local edge=false
      while pos<=#q do
        local z=q[pos];pos=pos+1
        if z[1]==0 or z[1]==31 or z[2]==0 or z[2]==31 then edge=true end
        for _,d in ipairs({{-1,0},{1,0},{0,-1},{0,1}}) do
          local xx,yy=z[1]+d[1],z[2]+d[2]
          local k=yy*32+xx
          if xx>=0 and xx<32 and yy>=0 and yy<32 and not seen[k] and pc.rgbaA(flat:getPixel(xx,yy))==0 then
            seen[k]=true;q[#q+1]={xx,yy}
          end
        end
      end
      if not edge and #q<=4 then
        for _,z in ipairs(q) do
          local votes,colors={0,0,0,0,0,0},{ {},{},{},{},{},{} }
          for yy=math.max(0,z[2]-1),math.min(31,z[2]+1) do
            for xx=math.max(0,z[1]-1),math.min(31,z[1]+1) do
              for n=6,1,-1 do
                local v=parts[n]:getPixel(xx,yy)
                if pc.rgbaA(v)>0 then
                  local w=(xx==z[1] or yy==z[2]) and 2 or 1
                  votes[n]=votes[n]+w;colors[n][v]=(colors[n][v] or 0)+w;break
                end
              end
            end
          end
          local best=1
          for n=2,6 do if votes[n]>votes[best] then best=n end end
          local color,most=C.neck,0
          for v,count in pairs(colors[best]) do if count>most then most=count;color=v end end
          if best==1 then color=C.pants
          elseif best==2 then color=z[2]<=g.top+1 and C.neck or (z[1]%2==0 and C.gold or C.orange)
          elseif best==3 or best==6 then color=C.mid
          elseif best==5 then color=C.shaft end
          put(parts[best],z[1],z[2],color);repairs=repairs+1
        end
      end
    end
  end end
  return repairs
end

local poses={}
local function add(p) poses[#poses+1]=p;return p end
local wallRefs={76,77,78,79,80,86,87,88,89,90}
local function wallPose(ref,tag)
  local phase=(ref-76)%10
  local second=ref>=86
  local anchors={{10,10},{13,9},{20,10},{22,12},{22,12},{22,14},{22,16},{22,18},{22,20},{22,22}}
  local a=anchors[phase+1]
  local stages={'探手','伸展','抓附','承重','停驻','抬身一','抬身二','抬身三','换脚','接下一抓'}
  return {tag=tag,ref=ref,cell=ref,stage=(second and '后手' or '前手')..'·'..stages[phase+1],
    support='right',anchor={a[1],a[2]+(second and phase>=3 and 1 or 0)},
    angle=(phase>=5 and 3 or 0),gx=5.5,gy=21+(phase==2 and -1 or 0),upper=19,lower=7,ms=67}
end
for _,ref in ipairs(wallRefs) do add(wallPose(ref,'贴墙')) end

local handCycle={
 {side='right',x=5.5,y=22,angle=0,up=20,lo=7},
 {side='right',x=5.5,y=18,angle=12,up=17,lo=10},
 {side='right',x=6.5,y=9,angle=60,up=16,lo=6},
 {side='right',x=11,y=4,angle=90,up=20,lo=7},
 {side='left',x=18,y=4,angle=90,up=13,lo=14},
 {side='left',x=26,y=21,angle=-14,up=20,lo=8},
 {side='left',x=26,y=22,angle=0,up=20,lo=7},
 {side='left',x=26,y=20,angle=-5,up=19,lo=9},
 {side='left',x=25,y=9,angle=-60,up=16,lo=6},
 {side='left',x=20,y=4,angle=-90,up=20,lo=7},
 {side='right',x=13,y=4,angle=-90,up=13,lo=14},
 {side='right',x=5.5,y=21,angle=14,up=20,lo=8}
}
local function hangingPose(i,zip)
  local tag=zip and '滑索' or '悬挂'
  local ref=107+i
  if i<12 then
    local k=handCycle[i+1]
    -- 沿用原版手掌相对身体的回移；身体横移时，承重手保持在上一抓点附近。
    local gripXs={22,22,21,18,14,10,22,22,21,18,14,10}
    local anchor={gripXs[i+1],9}
    return {tag=tag,ref=ref,cell=zip and (512+i) or ref,stage=(zip and '逆行换手 ' or '横移换手 ')..(i+1),
      support=k.side,anchor=anchor,angle=k.angle,gx=k.x,gy=k.y,upper=k.up,lower=k.lo,ms=67,zip=zip}
  end
  local j=i-12
  return {tag=tag,ref=ref,cell=zip and (512+i) or ref,stage=(zip and '滑行收势 ' or '单手停驻 ')..(j+1),
    support='right',anchor={20,9},angle=zip and (8-j) or (3-j%3),gx=5.5,gy=21+(j%2),upper=19,lower=7,ms=zip and 75 or 45,zip=zip}
end
for i=0,17 do add(hangingPose(i,false)) end
for ref=76,95 do add(wallPose(ref,'攀爬')) end
local ladderAnchors={{10,6},{10,8},{20,8},{20,7},{20,6},{20,8},{10,8},{10,7}}
local ladderCycle={handCycle[7],handCycle[8],handCycle[10],handCycle[11],handCycle[1],handCycle[2],handCycle[4],handCycle[5]}
for i=0,7 do
  local k=ladderCycle[i+1]
  local side=(i<2 or i>=6) and 'left' or 'right'
  add{tag='爬梯',ref=160+i,cell=160+i,stage='上行 '..(i+1),rear=true,support=side,anchor=ladderAnchors[i+1],
    angle=k.angle,gx=k.x,gy=k.y,upper=k.up,lower=k.lo,ms=80}
end
for i=0,2 do add{tag='爬梯',ref=168+i,cell=168+i,stage='下滑 '..(i+1),rear=true,support='left',anchor={10,11},angle=-3+i*3,gx=26,gy=21,upper=19,lower=7,ms=67} end
for i=0,2 do add{tag='爬梯',ref=171+i,cell=171+i,stage='停驻 '..(i+1),support='right',anchor={17,10},angle=i-1,gx=5.5,gy=21,upper=19,lower=7,ms=80} end
for i=0,5 do
  local k=poses[49]
  add{tag='爬梯',ref=i<3 and 171 or 160,cell=192+i,stage='进出梯过渡 '..(i+1),rear=i>=3,support='left',
    anchor={10,22-round(i*16/5)},angle=0,gx=26,gy=22,upper=20,lower=7,ms=50,exact=i==0 and 'standing' or (i==5 and 49 or nil)}
end
for i=0,17 do add(hangingPose(i,true)) end
assert(#poses==86)

local out=Sprite(32,32,ColorMode.RGB)
out:setPalette(run.palettes[1])
for i=2,#poses do out:newEmptyFrame(i) end
local layers={out.layers[1]};layers[1].name=names[1]
for i=2,#names do layers[i]=out:newLayer();layers[i].name=names[i] end
local sheet=Image(32*12,32*8,ColorMode.RGB)
local manifest={'frame,tag,stage,reference,body_cell,support,anchor_x,anchor_y,shoulder_x,shoulder_y,grip_x,grip_y,angle,duration_ms'}
local cached={}
local collisions={}
for i,p in ipairs(poses) do
  local parts,geometry,grip
  if p.exact then
    parts={}
    local originalParts=p.exact=='standing' and standing or cached[p.exact].parts
    for n=1,6 do parts[n]=Image(originalParts[n]) end
    geometry=p.exact=='standing' and {left={10,18},right={20,18},cx=15,top=17} or cached[p.exact].geometry
    grip=p.exact=='standing' and {23.5,21.5} or cached[p.exact].grip
  else
    local pants,armor,hair,g=body(p.ref,p.rear)
    geometry=g
    if p.zip then
      -- 滑索身体稍向运动反方向后仰，腿部沿用同一抓握周期的收放节奏。
      local shiftedArmor,shiftedHair=blank(),blank()
      for q in armor:pixels() do if pc.rgbaA(q())>0 then put(shiftedArmor,q.x-((q.y<g.top+4) and 1 or 0),q.y,q()) end end
      shiftedHair=shifted(hair,-1,0)
      armor,hair=shiftedArmor,shiftedHair
      g.left[1]=g.left[1]-1;g.right[1]=g.right[1]-1
    end
    local weapon;weapon,grip=pole(p.angle,p.gx,p.gy,p.upper,p.lower)
    local supportShoulder=g[p.support]
    local freeSide=p.support=='left' and 'right' or 'left'
    local freeShoulder=g[freeSide]
    local supportElbow
    if p.tag=='贴墙' or p.tag=='攀爬' then
      supportElbow={supportShoulder[1]+2,round((supportShoulder[2]+p.anchor[2])/2)}
      if p.anchor[1]<supportShoulder[1] then supportElbow={supportShoulder[1]+1,p.anchor[2]-1} end
    else
      supportElbow={p.anchor[1],round((supportShoulder[2]+p.anchor[2])/2)}
    end
    local freeElbow
    if grip[2]<11 then freeElbow={freeSide=='left' and (g.left[1]-3) or (g.right[1]+3),9}
    else freeElbow={freeSide=='left' and (g.left[1]-2) or (g.right[1]+2),math.max(freeShoulder[2]+2,grip[2]-2)} end
    parts={pants,armor,arm(supportShoulder,supportElbow,p.anchor),hair,weapon,arm(freeShoulder,freeElbow,grip)}
    for q in hair:pixels() do if pc.rgbaA(q())>0 then parts[6]:putPixel(q.x,q.y,0) end end
  end
  local freeSide=p.support=='left' and 'right' or 'left'
  local shoulder=geometry[freeSide]
  local seamCount=not p.exact and repairSeams(parts,geometry) or 0
  cached[i]={parts=parts,geometry=geometry,grip=grip}
  local data='动作='..p.tag..'；阶段='..p.stage..'；原版参考='..p.ref..'；身体格='..p.cell..
    '；抓附手='..p.support..'；抓附点='..p.anchor[1]..','..p.anchor[2]..'；持戟肩点='..shoulder[1]..','..shoulder[2]..
    '；握点='..grip[1]..','..grip[2]..'；倾角='..p.angle..'；源稿时长='..p.ms..'ms；接缝补点='..seamCount
  for n=1,6 do out:newCel(layers[n],i,parts[n],Point(0,0)).data=data end
  out.frames[i].duration=p.ms/1000
  local flat=blank();flat:drawSprite(out,i)
  flat:saveAs(work..'frame-'..i..'.png')
  for n=1,6 do parts[n]:saveAs(work..'frame-'..i..'-layer-'..n..'.png') end
  sheet:drawImage(flat,Point((i-1)%12*32,math.floor((i-1)/12)*32))
  local count=0
  for y=0,31 do for x=0,31 do
    if pc.rgbaA(parts[5]:getPixel(x,y))>0 and (pc.rgbaA(parts[1]:getPixel(x,y))>0 or pc.rgbaA(parts[2]:getPixel(x,y))>0 or pc.rgbaA(parts[4]:getPixel(x,y))>0) then count=count+1 end
  end end
  if count>0 then collisions[#collisions+1]=i..':'..count end
  manifest[#manifest+1]=table.concat({i,p.tag,p.stage,p.ref,p.cell,p.support,p.anchor[1],p.anchor[2],shoulder[1],shoulder[2],grip[1],grip[2],p.angle,p.ms},',')
end
for _,t in ipairs({{'贴墙',1,10,215,145,50},{'悬挂',11,28,67,177,190},{'攀爬',29,48,227,185,63},{'爬梯',49,68,80,165,70},{'滑索',69,86,114,147,228}}) do
  local tag=out:newTag(t[2],t[3]);tag.name=t[1];tag.aniDir=AniDir.FORWARD;tag.color=Color{r=t[4],g=t[5],b=t[6]}
end
out.data='海王地形移动 v2；32×32，86帧，六个海王可编辑图层。贴墙1–10，悬挂11–28，攀爬29–48，爬梯49–68，滑索69–86。贴墙帧与攀爬接触段共用原版身体格；爬梯63–68为可逆过渡，63匹配最新站姿，68匹配49。滑索使用扩展身体512–529。源稿预览时长不替代原版物理时序。源外观以 tools 根目录最新 held-v2 / run-v3 / jump-v2 / crouch-v2 为准。'
app.activeSprite=out;app.activeLayer=layers[3];app.activeFrame=out.frames[1]
out:saveAs(work..'haiwang_trident_traversal-v2.aseprite')
sheet:saveAs(work..'traversal-sheet.png')
local csv=io.open(work..'traversal-manifest.csv','w');csv:write(table.concat(manifest,'\n'));csv:close()
print('Frames='..#poses..'; weapon/body overlaps='..table.concat(collisions,','))
out:close();held:close();jump:close();run:close();crouch:close()
