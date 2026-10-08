local s=app.activeSprite
local target=s.filename
local ref=app.open(app.params['reference'])
local layer=s.layers[3]
local first=Image(32,32,ColorMode.RGB)
local cel=layer:cel(1)
first:drawImage(cel.image,cel.position)
local pc=app.pixelColor
local neck=pc.rgba(170,115,93,255)
s:saveCopyAs(app.params['backup'])
for f=2,8 do
 local im=Image(32,32,ColorMode.RGB)
 local rc=ref.layers[4]:cel(f)
 for p in rc.image:pixels() do
  if p()==neck then im:putPixel(p.x+rc.position.x,p.y+rc.position.y,neck) end
 end
 -- Transfer the user's own entire face/hair pixels through the reference
 -- poses. Do not substitute the reference face or invent static hair cels.
 for p in first:pixels() do
  if pc.rgbaA(p())>0 and p()~=neck then
   local x,y=p.x,p.y
   if f==3 then
    if y<=27 then y=y-1 end
   elseif f>=4 then
    x,y=48-p.y,p.x+4
   end
   im:putPixel(x,y,p())
  end
 end
 if f==3 then
  -- The reference stretches the bottom cheek as the eye line rises.
  for x=18,25 do
   local value=first:getPixel(x,27)
   if pc.rgbaA(value)>0 and value~=neck then im:putPixel(x,27,value) end
  end
 end
 s:deleteCel(layer:cel(f))
 s:newCel(layer,f,im,Point(0,0))
end
s:saveAs(target)
print('Transferred the custom first-frame face and hair together through all reference poses.')
