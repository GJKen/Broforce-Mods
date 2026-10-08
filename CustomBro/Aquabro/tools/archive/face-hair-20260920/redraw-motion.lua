local s=app.activeSprite
local ref=app.open(app.params['reference'])
local layer=s.layers[3]
local pc=app.pixelColor
local colors={A=pc.rgba(170,115,93,255),B=pc.rgba(167,116,42,255),C=pc.rgba(233,166,140,255),D=pc.rgba(195,134,109,255),E=pc.rgba(232,175,51,255),F=pc.rgba(255,221,94,255)}
s:saveCopyAs(app.params['backup'])
-- Frame 2 keeps the reference's initial face pose. Frame 3 raises the
-- eye line and stretches the lower cheek. Frame 4 turns the face 90 degrees
-- clockwise, just as the original swelling animation does.
local hair={
 [2]={y=23,rows={'....BBB','.....EE','.....EE','.....EF','.....EF','.BBBEEF','..EEEB.','..BBB..'}},
 [3]={y=22,rows={'....BBB','.....EE','.....EE','.....EF','.....EF','.EE.CEF','..BBEEF','..EEEB.','..BBB..'}},
 [4]={y=25,rows={'.B......','.EF....B','.EFEEEEB','.BEEFEB.','..BEEB..','...BB...'}},
 [5]={y=25,rows={'.B......','.EF....B','.EEEEEEF','.BEEFEBB','..BEEEB.','...BBB..'}}
}
for f=2,8 do
 local im=Image(32,32,ColorMode.RGB)
 local c=ref.layers[4]:cel(f)
 -- Preserve the reference's moving neck, eye, cheek and compressed face.
 -- Its lower-left flesh pixels belong to the old hand silhouette, not hair.
 for p in c.image:pixels() do
  local x,y=p.x+c.position.x,p.y+c.position.y
  local value=p()
  if y<28 and (value==colors.A or value==colors.C or value==colors.D) then im:putPixel(x,y,value) end
 end
 local pose=hair[math.min(f,5)]
 for j,row in ipairs(pose.rows) do
  for k=1,#row do
   local key=row:sub(k,k)
   if key~='.' then im:putPixel(17+k,pose.y+j-1,colors[key]) end
  end
 end
 local old=layer:cel(f)
 if old then s:deleteCel(old) end
 s:newCel(layer,f,im,Point(0,0))
end
s:saveAs(s.filename)
print('Redrew frames 2-8 using v3 face deformation and custom golden hair poses.')
