local s=app.activeSprite
local out=app.params['out']
local index=tonumber(app.params['layer'])
for f=1,#s.frames do
 local im=Image(32,32,ColorMode.RGB)
 local c=s.layers[index]:cel(f)
 if c then im:drawImage(c.image,c.position) end
 im:saveAs(out..f..'.png')
end
