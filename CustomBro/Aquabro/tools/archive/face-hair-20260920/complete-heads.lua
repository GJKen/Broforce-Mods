local s=app.activeSprite
local reference=app.open(app.params['reference'])
local layer=s.layers[3]
local first=layer:cel(1)
local template=Image(32,32,ColorMode.RGB)
template:drawImage(first.image,first.position)
local neck=app.pixelColor.rgba(170,115,93,255)
local backup=app.params['backup']
s:saveCopyAs(backup)
for f=2,#s.frames do
 local im=Image(template)
 -- Keep the user's face and golden hair; redraw the exposed neck along
 -- the original rising shoulder pose, with the head resting on the floor.
 for p in im:pixels() do if p()==neck then p(0) end end
 local c=reference.layers[4]:cel(f)
 for p in c.image:pixels() do
  if p()==neck then
   local x,y=p.x+c.position.x,p.y+c.position.y
   if app.pixelColor.rgbaA(im:getPixel(x,y))==0 then im:putPixel(x,y,neck) end
  end
 end
 s:newCel(layer,f,im,Point(0,0))
end
s:saveAs(s.filename)
print('Completed head cels 2-8; frame 1 preserved.')
