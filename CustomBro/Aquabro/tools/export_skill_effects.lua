local root=assert(app.params['root'])
local output=assert(app.params['output'])
local function base64(bytes)
  local chars='ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/'
  local result={}
  for i=1,#bytes,3 do
    local a,b,c=bytes[i],bytes[i+1],bytes[i+2]
    local n=a*65536+(b or 0)*256+(c or 0)
    result[#result+1]=chars:sub(math.floor(n/262144)%64+1,math.floor(n/262144)%64+1)
      ..chars:sub(math.floor(n/4096)%64+1,math.floor(n/4096)%64+1)
      ..(b and chars:sub(math.floor(n/64)%64+1,math.floor(n/64)%64+1) or '=')
      ..(c and chars:sub(n%64+1,n%64+1) or '=')
  end
  return table.concat(result)
end
-- Keep water fixed and preserve source layer ordering. Align entry body pixels
-- with the waterline; flight ends in that same pose without changing its pivot.
local sharkFlightFrames=31
local function describe(image)
  local n,sx,sy,sxx,syy,sxy=0,0,0,0,0,0
  for y=0,127 do for x=0,127 do
    if app.pixelColor.rgbaA(image:getPixel(x,y))>0 then
      local px,py=x+.5,y+.5
      n=n+1;sx=sx+px;sy=sy+py;sxx=sxx+px*px;syy=syy+py*py;sxy=sxy+px*py
    end
  end end
  assert(n>0,'Missing flight body')
  local cx,cy=sx/n,sy/n
  return {x=cx,y=cy,angle=.5*math.atan(2*(sxy/n-cx*cy),(sxx-syy)/n-cx*cx+cy*cy)}
end
local function blend(a,b,t) return a+(b-a)*t end
local function flightPose(bodies,info,time,entryOffset)
  local pose=math.min(19,time<=.27 and 6+time/.03 or 15+(time-.27)/.05)
  local first=math.floor(pose+1e-7);local last=math.min(19,first+1);local part=pose-first
  local nearest=math.floor(pose+.5);local source=bodies[nearest];local center=info[nearest]
  local targetX=blend(info[first].x,info[last].x,part)
  local targetY=blend(first<9 and info[first].y or info[19].y+entryOffset,last<9 and info[last].y or info[19].y+entryOffset,part)
  local angle=blend(info[first].angle,info[last].angle,part)-center.angle
  local c,s=math.cos(angle),math.sin(angle)
  local image=Image(128,128,ColorMode.RGB)
  for y=0,127 do for x=0,127 do
    local dx,dy=x+.5-targetX,y+.5-targetY
    local px=math.floor(center.x+c*dx+s*dy+1e-7)
    local py=math.floor(center.y-s*dx+c*dy+1e-7)
    if px>=0 and px<128 and py>=0 and py<128 then image:putPixel(x,y,source:getPixel(px,py)) end
  end end
  return image
end
local function maskRow(image)
  local bytes={}
  for y=0,127 do for x=0,127,8 do
    local byte=0
    for bit=0,7 do if app.pixelColor.rgbaA(image:getPixel(x+bit,y))>0 then byte=byte+2^bit end end
    bytes[#bytes+1]=byte
  end end
  return '            Convert.FromBase64String("'..base64(bytes)..'"),'
end
for _,entry in ipairs({{'tidal-wave',16},{'shark-hunt',28},{'maelstrom',32},{'abyssal-spring',28}}) do
  local name='skill-'..entry[1]
  local suffix=entry[1]=='shark-hunt' and '-fx-v7.aseprite' or '-fx-v3.aseprite'
  local source=root..'/aseprite-source/03_特殊技能/'..name..suffix
  local sprite=assert(app.open(source),'Missing skill source: '..source)
  assert(sprite.width==128 and sprite.height==128 and #sprite.frames==entry[2],name..': unexpected layout')
  local atlas=Image(128*#sprite.frames,128,ColorMode.RGB)
  local bodyAtlas=entry[1]=='shark-hunt' and Image(128*(#sprite.frames+sharkFlightFrames),128,ColorMode.RGB) or nil
  local backAtlas=bodyAtlas and Image(128*#sprite.frames,128,ColorMode.RGB) or nil
  local frontAtlas=bodyAtlas and Image(128*#sprite.frames,128,ColorMode.RGB) or nil
  local bodies,bodyInfo={},{}
  local entryOffset=0
  local masks={}
  if entry[1]=='shark-hunt' then
    assert(#sprite.layers==8,'Shark mask requires the eight-layer source')
    local entryBody=Image(128,128,ColorMode.RGB)
    for _,i in ipairs({1,2,4,5,6}) do
      assert(sprite.layers[i].isImage and sprite.layers[i].isVisible,'Missing visible shark body layer '..i)
      local cel=sprite.layers[i]:cel(20)
      if cel then entryBody:drawImage(cel.image,cel.position) end
    end
    local bottom=0
    for y=0,127 do for x=0,127 do
      if app.pixelColor.rgbaA(entryBody:getPixel(x,y))>0 then bottom=math.max(bottom,y+1) end
    end end
    assert(bottom>0 and bottom<=112,'Unexpected entry nose position')
    entryOffset=112-bottom
  end
  for f=1,#sprite.frames do
    local image=Image(128,128,ColorMode.RGB)
    -- Shift body cels in memory so Aseprite preserves the original layer order.
    -- Water stays at the surface, and the editable source is never saved here.
    local saved={}
    local hidden={}
    if bodyAtlas and f==20 then
      -- Source pose 19 still contains two fading takeoff water strips. A new
      -- landing begins with nose contact, not a replay of that old waterline.
      for _,i in ipairs({3,7,8}) do
        hidden[i]=sprite.layers[i].isVisible;sprite.layers[i].isVisible=false
      end
    end
    if bodyAtlas and f>=20 then
      for _,i in ipairs({1,2,4,5,6}) do
        local cel=sprite.layers[i]:cel(f)
        if cel then saved[cel]=cel.position;cel.position=Point(cel.position.x,cel.position.y+entryOffset) end
      end
    end
    image:drawSprite(sprite,f)
    for cel,position in pairs(saved) do cel.position=position end
    for i,visible in pairs(hidden) do sprite.layers[i].isVisible=visible end
    atlas:drawImage(image,Point((f-1)*128,0))
    local maskImage=image
    if bodyAtlas then
      local body=Image(128,128,ColorMode.RGB)
      for _,i in ipairs({1,2,4,5,6}) do
        local cel=sprite.layers[i]:cel(f)
        if cel and sprite.layers[i].isVisible then body:drawImage(cel.image,cel.position) end
      end
      bodies[f-1]=body
      if f>=7 and f<=20 then bodyInfo[f-1]=describe(body) end
      maskImage=body
      if f>=20 then
        maskImage=Image(128,128,ColorMode.RGB)
        maskImage:drawImage(body,Point(0,entryOffset))
      end
      bodyAtlas:drawImage(maskImage,Point((f-1)*128,0))
      for _,part in ipairs({{backAtlas,{3}},{frontAtlas,{7,8}}}) do
        local water=Image(128,128,ColorMode.RGB)
        for _,i in ipairs(part[2]) do
          local cel=sprite.layers[i]:cel(f)
          if cel and sprite.layers[i].isVisible and f~=20 then water:drawImage(cel.image,cel.position) end
        end
        part[1]:drawImage(water,Point((f-1)*128,0))
      end
    end
    masks[#masks+1]=maskRow(maskImage)
  end
  if bodyAtlas then
    -- The first emerging fragments do not describe a complete body axis yet.
    for f=6,8 do bodyInfo[f].angle=bodyInfo[9].angle end
    for i=0,sharkFlightFrames-1 do
      local image=flightPose(bodies,bodyInfo,i/60,entryOffset)
      bodyAtlas:drawImage(image,Point((#sprite.frames+i)*128,0))
      masks[#masks+1]=maskRow(image)
    end
  end
  atlas:saveAs(output..'/'..name..'.png')
  if bodyAtlas then bodyAtlas:saveAs(output..'/'..name..'-body.png') end
  if backAtlas then backAtlas:saveAs(output..'/'..name..'-water-back.png') end
  if frontAtlas then frontAtlas:saveAs(output..'/'..name..'-water-front.png') end
  if #masks>0 then
    local templateFile=assert(io.open(root..'/tools/SpringPixelMask.cs.in','r'))
    local template=templateFile:read('*a');templateFile:close()
    local className=entry[1]=='tidal-wave' and 'TidalWavePixelMask'
      or entry[1]=='shark-hunt' and 'SharkPixelMask'
      or entry[1]=='maelstrom' and 'MaelstromPixelMask' or 'SpringPixelMask'
    template=template:gsub('SpringPixelMask',className)
    if entry[1]=='shark-hunt' then
      template=template:gsub('Generated from the visible Aseprite composites',
        'Generated from the visible shark body layers, excluding water')
    end
    local code=template:gsub('@FRAMES@',function() return table.concat(masks,'\n') end)
    local file=assert(io.open(output..'/'..className..'.cs','w'));file:write(code);file:close()
  end
  print('EXPORTED',name,#sprite.frames)
  sprite:close()
end
