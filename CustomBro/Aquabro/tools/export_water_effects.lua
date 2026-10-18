local root=assert(app.params['root'])
local function base64(bytes)
 local chars='ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/'
 local result={}
 for i=1,#bytes,3 do
  local a,b,c=bytes[i],bytes[i+1],bytes[i+2]
  local n=a*65536+(b or 0)*256+(c or 0)
  local function digit(shift) local index=math.floor(n/2^shift)%64+1;return chars:sub(index,index) end
  result[#result+1]=digit(18)..digit(12)..(b and digit(6) or '=')..(c and digit(0) or '=')
 end
 return table.concat(result)
end
for _,v in ipairs({{'haiwang_water_hit-small-v2','water-hit-small',48,12}}) do
 local s=assert(app.open(root..'/aseprite-source/06_普攻特效/'..v[1]..'.aseprite'))
 assert(s.width==v[3] and #s.frames==v[4])
 local atlas=Image(s.width*#s.frames,s.height,ColorMode.RGB)
 for f=1,#s.frames do
  local im=Image(s.width,s.height,ColorMode.RGB);im:drawSprite(s,f)
  atlas:drawImage(im,Point((f-1)*s.width,0))
 end
 atlas:saveAs((app.params['output'] or root..'/_Mod')..'/'..v[2]..'.png')
 print('EXPORTED',v[2],#s.frames)
 s:close()
end

-- 保留参考源稿的全部22帧，仅导出第16帧之后的17–22帧消散。
local s=assert(app.open(root..'/aseprite-source/06_普攻特效/haiwang_water_rocket-explosion-v2.aseprite'))
assert(s.width==64 and s.height==64 and #s.frames==22)
local atlas=Image(64*6,64,ColorMode.RGB)
local masks={}
for f=17,22 do
 assert(math.abs(s.frames[f].duration-.05)<.00001)
 local im=Image(64,64,ColorMode.RGB);im:drawSprite(s,f)
 atlas:drawImage(im,Point((f-17)*64,0))
 local bytes={}
 for y=0,63 do for x=0,63,8 do
  local byte=0
  for bit=0,7 do
   if app.pixelColor.rgbaA(im:getPixel(x+bit,y))>0 then byte=byte+2^bit end
  end
  bytes[#bytes+1]=byte
 end end
 masks[#masks+1]='            Convert.FromBase64String("'..base64(bytes)..'"),'
end
atlas:saveAs((app.params['output'] or root..'/_Mod')..'/water-rocket-explosion.png')
local templateFile=assert(io.open(root..'/tools/ChargedWaterPixelMask.cs.in','r'))
local template=templateFile:read('*a');templateFile:close()
local code=template:gsub('@FRAMES@',function() return table.concat(masks,'\n') end)
local maskFile=assert(io.open((app.params['output'] or root..'/src')..'/ChargedWaterPixelMask.cs','w'))
maskFile:write(code);maskFile:close()
print('EXPORTED','water-rocket-explosion',6)
s:close()
