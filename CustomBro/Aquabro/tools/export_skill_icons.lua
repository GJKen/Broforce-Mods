local root=assert(app.params['root'])
local output=assert(app.params['output'])
local iconRoot=app.fs.joinPath(root,'aseprite-source','05_贴图')
local source=app.fs.joinPath(iconRoot,'skill-hud-icons-v3.aseprite')
local sprite=assert(app.open(source),'Missing skill HUD source')
assert(sprite.width==16 and sprite.height==16 and #sprite.frames==4,'Expected four 16x16 HUD icons')
local names={'tidal-wave','shark-hunt','maelstrom','abyssal-spring'}
for frame,name in ipairs(names) do
  local image=Image(16,16,ColorMode.RGB)
  image:drawSprite(sprite,frame)
  local filename='special-'..name..'.png'
  image:saveAs(app.fs.joinPath(output,filename))
  -- 源稿旁同步保留逐技能预览，方便查找；正式像素仍以 Aseprite 源稿为准。
  if app.fs.normalizePath(output)~=app.fs.normalizePath(iconRoot) then
    image:saveAs(app.fs.joinPath(iconRoot,filename))
  end
  if frame==1 then
    image:saveAs(app.fs.joinPath(output,'special.png'))
    if app.fs.normalizePath(output)~=app.fs.normalizePath(iconRoot) then
      image:saveAs(app.fs.joinPath(iconRoot,'special.png'))
    end
  end
end
sprite:close()
print('EXPORTED four skill HUD icons (16x16)')
