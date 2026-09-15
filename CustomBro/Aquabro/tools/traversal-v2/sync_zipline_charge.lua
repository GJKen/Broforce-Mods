-- 只同步滑索身体524–529的回拉、蓄力、蓄满，共18个武器格。
-- 输出到工作目录，核对后再替换本地图集与PNG。
local root='E:/Study/C#/Broforce-Mods/CustomBro/Aquabro/'
local work=app.params['work'] or 'C:/Users/5700G/AppData/Local/Temp/haiwang-zipline-charge-integration-20260915/'
local poses=dofile(root..'tools/traversal-v2/zipline_charge_source.lua')(root)
local atlas=app.open(root..'tools/haiwang_trident_gun_atlas.aseprite')
assert(atlas and atlas.width==1024 and atlas.height==1024 and #atlas.frames==1,'武器图集尺寸已变化')
local cells={}
for body=524,529 do for pose=1,3 do cells[#cells+1]=595+(body-512)*9+pose end end
-- 清除各可见层的旧格，避免旧手臂从新姿态的透明区域露出。
for _,layer in ipairs(atlas.layers) do if layer.isVisible then
  local cel=layer:cel(1)
  if cel then
    local image=Image(cel.image)
    for _,cell in ipairs(cells) do
      local left,top=cell%32*32-cel.position.x,math.floor(cell/32)*32-cel.position.y
      for y=math.max(0,top),math.min(image.height-1,top+31) do
        for x=math.max(0,left),math.min(image.width-1,left+31) do image:putPixel(x,y,0) end
      end
    end
    cel.image=image
  end
end end
local outputLayer
for _,layer in ipairs(atlas.layers) do if layer.name=='海王滑索蓄力手臂修订' then outputLayer=layer end end
if not outputLayer then outputLayer=atlas:newLayer();outputLayer.name='海王滑索蓄力手臂修订' end
local image=Image(1024,1024,ColorMode.RGB)
for body=524,529 do for pose=1,3 do
  local cell=595+(body-512)*9+pose
  local combined=Image(poses[pose].weapon);combined:drawImage(poses[pose].hand)
  image:drawImage(combined,Point(cell%32*32,math.floor(cell/32)*32))
end end
local cel=outputLayer:cel(1)
if cel then cel.image=image;cel.position=Point(0,0) else atlas:newCel(outputLayer,1,image,Point(0,0)) end
outputLayer.data='来自绳索蓄力独立稿第2/3/4帧；身体524–529，姿态1/2/3，共18格；肩侧回拉，握点8.5,16.5。'
atlas:saveAs(work..'gun-atlas.aseprite')
atlas:close()
local reopened=app.open(work..'gun-atlas.aseprite')
local flat=Image(1024,1024,ColorMode.RGB);flat:drawSprite(reopened,1)
flat:saveAs(work..'gunSprite.png');reopened:close()
print('Synchronized 18 zipline charging cells; saved atlas reopened and PNG exported.')
