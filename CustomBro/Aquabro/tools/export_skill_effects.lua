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
for _,entry in ipairs({{'tidal-wave',16},{'shark-hunt',28},{'maelstrom',32},{'abyssal-spring',28}}) do
  local name='skill-'..entry[1]
  local suffix=entry[1]=='shark-hunt' and '-fx-v7.aseprite' or '-fx-v3.aseprite'
  local source=root..'/aseprite-source/03_特殊技能/'..name..suffix
  local sprite=assert(app.open(source),'Missing skill source: '..source)
  assert(sprite.width==128 and sprite.height==128 and #sprite.frames==entry[2],name..': unexpected layout')
  local atlas=Image(128*#sprite.frames,128,ColorMode.RGB)
  local masks={}
  if entry[1]=='shark-hunt' then
    assert(#sprite.layers==8,'Shark mask requires the eight-layer source')
    for i=2,6 do assert(sprite.layers[i].isImage and sprite.layers[i].isVisible,'Missing visible shark body layer '..i) end
  end
  for f=1,#sprite.frames do
    local image=Image(128,128,ColorMode.RGB)
    image:drawSprite(sprite,f)
    atlas:drawImage(image,Point((f-1)*128,0))
    local maskImage=image
    if entry[1]=='shark-hunt' then
      -- Only the shark layers contribute. Opaque foreground water hides body
      -- pixels, and therefore removes them from the visible damage silhouette.
      maskImage=Image(128,128,ColorMode.RGB)
      for i=2,6 do
        local cel=sprite.layers[i]:cel(f)
        if cel then maskImage:drawImage(cel.image,cel.position) end
      end
      for i=7,8 do
        local layer=sprite.layers[i]
        local cel=layer.isVisible and layer:cel(f)
        if cel then
          for py=0,cel.image.height-1 do for px=0,cel.image.width-1 do
            local x,y=px+cel.position.x,py+cel.position.y
            if x>=0 and x<128 and y>=0 and y<128 and app.pixelColor.rgbaA(cel.image:getPixel(px,py))==255 then
              maskImage:putPixel(x,y,0)
            end
          end end
        end
      end
    end
    do
      local bytes={}
      for y=0,127 do
        for x=0,127,8 do
          local byte=0
          for bit=0,7 do
            if app.pixelColor.rgbaA(maskImage:getPixel(x+bit,y))>0 then byte=byte+2^bit end
          end
          bytes[#bytes+1]=byte
        end
      end
      masks[#masks+1]='            Convert.FromBase64String("'..base64(bytes)..'"),'
    end
  end
  atlas:saveAs(output..'/'..name..'.png')
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
