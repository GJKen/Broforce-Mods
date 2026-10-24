-- A frame-local spatial model; coding state spans frames, pixel storage does not.
-- 16-bit arithmetic intervals keep every product inside a signed 32-bit integer.
local function contextMask(bytes,bytesPerFrame)
 local size=math.floor(math.sqrt(bytesPerFrame*8))
 assert(size>=8 and size%8==0 and size*size==bytesPerFrame*8 and #bytes%bytesPerFrame==0,'Invalid mask frame layout')
 local zeros,ones={},{}
 for i=1,32 do zeros[i]=1;ones[i]=1 end
 local output,buffer,bits={},0,0
 local low,high,pending=0,65535,0
 local function bit(value)
  buffer=(buffer << 1) | value;bits=bits+1
  if bits==8 then output[#output+1]=buffer;buffer=0;bits=0 end
 end
 local function emit(value)
  bit(value)
  while pending>0 do bit(1-value);pending=pending-1 end
 end
 for first=1,#bytes,bytesPerFrame do
  local function pixel(index) return (bytes[first+(index >> 3)] >> (index & 7)) & 1 end
  local index=0
  for y=0,size-1 do
   local left=0
   local above=y>0 and ((pixel(index-size) << 1) | pixel(index-size+1)) or 0
   for x=0,size-1 do
    local context=((left & 3) << 3) | above
    local a,b=zeros[context+1],ones[context+1]
    local split=low+math.floor((high-low+1)*a/(a+b))-1
    local value=pixel(index)
    if value==0 then high=split;a=a+1 else low=split+1;b=b+1 end
    if a+b>=1024 then a=math.floor((a+1)/2);b=math.floor((b+1)/2) end
    zeros[context+1]=a;ones[context+1]=b
    while true do
     if high<32768 then emit(0)
     elseif low>=32768 then emit(1);low=low-32768;high=high-32768
     elseif low>=16384 and high<49152 then pending=pending+1;low=low-16384;high=high-16384
     else break end
     low=low*2;high=high*2+1
    end
    left=((left << 1) | value) & 3
    above=((above << 1) & 7) | (y>0 and x+2<size and pixel(index-size+2) or 0)
    index=index+1
   end
  end
 end
 pending=pending+1;emit(low<16384 and 0 or 1)
 -- Decoder lookahead is explicit and bounds-checked, including empty input.
 for _=1,16 do bit(0) end
 if bits>0 then output[#output+1]=buffer << (8-bits) end
 local checksum=2166136261
 for _,value in ipairs(bytes) do checksum=((checksum ~ value)*16777619) & 0xffffffff end
 return output,checksum
end

local function pack16(bytes)
 local result={}
 for i=1,#bytes,2 do
  local value=bytes[i]*256+(bytes[i+1] or 0)
  result[#result+1]=string.format('\\u%04X',value)
 end
 return table.concat(result)
end

-- Frame-local alternating bit runs, beginning with zero. Preserve every bit.
local function maskRuns(bytes,bytesPerFrame)
 assert(bytesPerFrame>0 and #bytes%bytesPerFrame==0,'Invalid mask frame layout')
 local result={}
 local function emit(length)
  while length>=128 do result[#result+1]=(length & 127) | 128;length=length >> 7 end
  result[#result+1]=length
 end
 for first=1,#bytes,bytesPerFrame do
  local value,length=0,0
  for i=first,first+bytesPerFrame-1 do
   for bit=0,7 do
    local nextValue=(bytes[i] >> bit) & 1
    if nextValue==value then length=length+1
    else emit(length);value=nextValue;length=1 end
   end
  end
  emit(length)
 end
 return result
end

-- RFC1951 raw DEFLATE with a 32KiB LZ77 window.
-- Module mode shares the encoder with export_skill_effects.lua without exporting art.
-- No external compressor, build dependency or runtime decompression library is needed.
local function deflate(bytes)
 local lengthBase={3,4,5,6,7,8,9,10,11,13,15,17,19,23,27,31,35,43,51,59,67,83,99,115,131,163,195,227,258}
 local lengthBits={0,0,0,0,0,0,0,0,1,1,1,1,2,2,2,2,3,3,3,3,4,4,4,4,5,5,5,5,0}
 local distanceBase={1,2,3,4,5,7,9,13,17,25,33,49,65,97,129,193,257,385,513,769,1025,1537,2049,3073,4097,6145,8193,12289,16385,24577}
 local distanceBits={0,0,0,0,1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,10,10,11,11,12,12,13,13}
 local head,previous={},{}
 local function key(i) return bytes[i]*65536+bytes[i+1]*256+bytes[i+2] end
 local function remember(i)
  if i+2<=#bytes then local k=key(i);previous[i]=head[k];head[k]=i end
 end
 -- Keep the same matches; only the entropy coding of their symbols changes.
 local tokens,literalFrequency,distanceFrequency={},{},{}
 local function add(token)
  tokens[#tokens+1]=token
  local symbol=type(token)=='table' and token[1] or token
  literalFrequency[symbol+1]=(literalFrequency[symbol+1] or 0)+1
  if type(token)=='table' then
   local d=token[4]+1;distanceFrequency[d]=(distanceFrequency[d] or 0)+1
  end
 end
	local i=1
	while i<=#bytes do
	 local best,distance=2,0
	 if i+2<=#bytes then
	  local candidate=head[key(i)]
	  local limit=math.min(258,#bytes-i+1)
	  local searched=0
	  while candidate and i-candidate<=32768 and searched<128 do
	   local length=0
	   while length<limit and bytes[candidate+length]==bytes[i+length] do length=length+1 end
	   if length>best then best=length;distance=i-candidate;if best==limit then break end end
	   candidate=previous[candidate];searched=searched+1
	  end
	 end
	 if best>=3 then
	  local code=#lengthBase
	  while lengthBase[code]>best do code=code-1 end
	  local d=#distanceBase
	  while distanceBase[d]>distance do d=d-1 end
	  add({256+code,best-lengthBase[code],lengthBits[code],d-1,distance-distanceBase[d],distanceBits[d]})
	  for p=i,i+best-1 do remember(p) end
	  i=i+best
	 else
	  add(bytes[i]);remember(i);i=i+1
	 end
 end
 add(256)

 -- A complete, deterministic Huffman tree. Unusual future data that needs
 -- longer codes than RFC1951 allows falls back to the fixed tree below.
 local function lengths(frequency,count,maxBits)
  local nodes,result={},{}
  for symbol=0,count-1 do
   result[symbol+1]=0
   if (frequency[symbol+1] or 0)>0 then
    nodes[#nodes+1]={weight=frequency[symbol+1],order=symbol,symbol=symbol}
   end
  end
  for symbol=0,count-1 do
   if #nodes>=2 then break end
   if result[symbol+1]==0 and not frequency[symbol+1] then
    nodes[#nodes+1]={weight=1,order=symbol,symbol=symbol}
   end
  end
  local function less(a,b) return a.weight<b.weight or (a.weight==b.weight and a.order<b.order) end
  while #nodes>1 do
   table.sort(nodes,less)
   local a,b=table.remove(nodes,1),table.remove(nodes,1)
   nodes[#nodes+1]={weight=a.weight+b.weight,order=math.min(a.order,b.order),left=a,right=b}
  end
  local valid=true
  local function visit(node,depth)
   if node.symbol then
    result[node.symbol+1]=depth
    if depth>maxBits then valid=false end
   else visit(node.left,depth+1);visit(node.right,depth+1) end
  end
  visit(nodes[1],0)
  return valid and result or nil
 end
 local function codes(sizes)
  local counts,nextCode,result={},{},{}
  for _,size in ipairs(sizes) do if size>0 then counts[size]=(counts[size] or 0)+1 end end
  local code=0
  for size=1,15 do code=(code+(counts[size-1] or 0)) << 1;nextCode[size]=code end
  for symbol,size in ipairs(sizes) do
   if size>0 then result[symbol]=nextCode[size];nextCode[size]=nextCode[size]+1 end
  end
  return result
 end
 local order={16,17,18,0,8,7,9,6,10,5,11,4,12,3,13,2,14,1,15}
 local literalSizes=lengths(literalFrequency,286,15)
 local distanceSizes=lengths(distanceFrequency,30,15)
 local header
 if literalSizes and distanceSizes then
  local literalCount,distanceCount=286,30
  while literalCount>257 and literalSizes[literalCount]==0 do literalCount=literalCount-1 end
  while distanceCount>1 and distanceSizes[distanceCount]==0 do distanceCount=distanceCount-1 end
  local combined,runs,frequency={},{},{}
  for j=1,literalCount do combined[#combined+1]=literalSizes[j] end
  for j=1,distanceCount do combined[#combined+1]=distanceSizes[j] end
  local function run(symbol,value,bits)
   runs[#runs+1]={symbol,value or 0,bits or 0}
   frequency[symbol+1]=(frequency[symbol+1] or 0)+1
  end
  local j=1
  while j<=#combined do
   local value,count=combined[j],1
   while j+count<=#combined and combined[j+count]==value do count=count+1 end
   j=j+count
   if value==0 then
    while count>=11 do local n=math.min(count,138);run(18,n-11,7);count=count-n end
    if count>=3 then local n=math.min(count,10);run(17,n-3,3);count=count-n end
   else
    run(value);count=count-1
    while count>=3 do local n=math.min(count,6);run(16,n-3,2);count=count-n end
   end
   for _=1,count do run(value) end
  end
  local sizes=lengths(frequency,19,7)
  if sizes then
   local count=19
   while count>4 and sizes[order[count]+1]==0 do count=count-1 end
   header={literalCount=literalCount,distanceCount=distanceCount,sizes=sizes,count=count,runs=runs}
  end
 end
 local function encode(literalSizes,distanceSizes,header)
  local output,buffer,bits={},0,0
  local function emit(value,count)
   buffer=buffer | (value << bits);bits=bits+count
   while bits>=8 do output[#output+1]=buffer & 255;buffer=buffer >> 8;bits=bits-8 end
  end
  local function huffman(value,count)
   local reversed=0
   for _=1,count do reversed=(reversed << 1) | (value & 1);value=value >> 1 end
   emit(reversed,count)
  end
  local literalCodes,distanceCodes=codes(literalSizes),codes(distanceSizes)
  if header then
   emit(5,3) -- BFINAL=1, BTYPE=10 (dynamic Huffman).
   emit(header.literalCount-257,5);emit(header.distanceCount-1,5);emit(header.count-4,4)
   for j=1,header.count do emit(header.sizes[order[j]+1],3) end
   local headerCodes=codes(header.sizes)
   for _,run in ipairs(header.runs) do
    huffman(headerCodes[run[1]+1],header.sizes[run[1]+1]);emit(run[2],run[3])
   end
  else emit(3,3) end -- BFINAL=1, BTYPE=01 (fixed Huffman).
  for _,token in ipairs(tokens) do
   if type(token)=='table' then
    huffman(literalCodes[token[1]+1],literalSizes[token[1]+1]);emit(token[2],token[3])
    huffman(distanceCodes[token[4]+1],distanceSizes[token[4]+1]);emit(token[5],token[6])
   else huffman(literalCodes[token+1],literalSizes[token+1]) end
  end
  if bits>0 then output[#output+1]=buffer end
  return output
 end
 local fixedLiteral,fixedDistance={},{}
 for symbol=0,287 do fixedLiteral[symbol+1]=symbol<=143 and 8 or symbol<=255 and 9 or symbol<=279 and 7 or 8 end
 for symbol=0,31 do fixedDistance[symbol+1]=5 end
 local fixed=encode(fixedLiteral,fixedDistance)
 if header then
  local dynamic=encode(literalSizes,distanceSizes,header)
  if #dynamic<#fixed then return dynamic end
 end
 return fixed
end
if ...=='mask-deflate' then return deflate end
if ...=='mask-pack16' then return pack16 end
if ...=='mask-runs' then return maskRuns end
if ...=='mask-context' then return contextMask end

local root=assert(app.params['root'])
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
 for _,byte in ipairs(bytes) do masks[#masks+1]=byte end
end
atlas:saveAs((app.params['output'] or root..'/_Mod')..'/water-rocket-explosion.png')
local templateFile=assert(io.open(root..'/tools/ChargedWaterPixelMask.cs.in','r'))
local template=templateFile:read('*a');templateFile:close()
local compressed,checksum=contextMask(masks,512)
local code=template:gsub('@COMPRESSED_LENGTH@',tostring(#compressed)):gsub('@CHECKSUM@',tostring(checksum))
 :gsub('@DATA@',function() return pack16(compressed) end)
local maskFile=assert(io.open((app.params['output'] or root..'/src')..'/ChargedWaterPixelMask.cs','w'))
maskFile:write(code);maskFile:close()
print('EXPORTED','water-rocket-explosion',6)
s:close()
