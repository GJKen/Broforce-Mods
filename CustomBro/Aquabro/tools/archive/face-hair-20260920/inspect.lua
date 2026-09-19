local s=app.activeSprite
for i,l in ipairs(s.layers) do
 print(i,l.name,l.isVisible)
 for _,c in ipairs(l.cels) do print(c.frame.frameNumber,c.position.x,c.position.y,c.image.width,c.image.height) end
end
