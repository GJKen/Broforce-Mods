local s=app.activeSprite
local old=app.open(app.params['backup'])
assert(#s.frames==#old.frames and #s.layers==#old.layers)
local function renderCel(c)
 local im=Image(32,32,ColorMode.RGB)
 if c then im:drawImage(c.image,c.position) end
 return im
end
for f=1,#s.frames do
 assert(s.frames[f].duration==old.frames[f].duration)
 for i,l in ipairs(s.layers) do
  if i~=3 or f==1 then
   local a,b=renderCel(l:cel(f)),renderCel(old.layers[i]:cel(f))
   assert(a.bytes==b.bytes,'Unexpected change at layer '..i..' frame '..f)
  else assert(l:cel(f)) end
 end
end
print('PASS: frame 1, other layers, frame count and durations unchanged; heads present in all 8 frames.')
if app.params['report'] then
 local file=assert(io.open(app.params['report'],'w'))
 file:write('PASS: frame 1, other layers, frame count and durations unchanged; heads present in all 8 frames.\n')
 file:close()
end
