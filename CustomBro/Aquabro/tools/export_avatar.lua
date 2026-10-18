local root=assert(app.params['root'])
local output=assert(app.params['output'])
local art=app.fs.joinPath(root,'aseprite-source','05_贴图')
local sprite=assert(app.open(app.fs.joinPath(art,'avatar.aseprite')),'Missing avatar source')
assert(sprite.width==32 and sprite.height==64 and #sprite.frames==4,'Expected four 32x64 avatar frames')
local atlas=Image(128,64,ColorMode.RGB)
for frame=1,4 do
  local image=Image(32,64,ColorMode.RGB)
  image:drawSprite(sprite,frame)
  atlas:drawImage(image,Point((frame-1)*32,0))
end
atlas:saveAs(app.fs.joinPath(output,'avatar.png'))
if app.fs.normalizePath(output)~=app.fs.normalizePath(art) then
  atlas:saveAs(app.fs.joinPath(art,'avatar.png'))
end
sprite:close()
print('EXPORTED avatar (four 32x64 frames)')
