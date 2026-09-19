local root = 'E:/Study/C#/Broforce-Mods/'
local work = root .. 'CustomBro/Aquabro/tools/gibs-v2/'
local source = assert(app.open(work .. 'haiwang_trident_death_gibs-v1.aseprite'))
source.frames[1].duration = 0.8
source:saveAs(work .. 'normal.gif')
source.frames[1].duration = 3.0
source:saveAs(work .. 'slow.gif')
source:close()
print('Exported normal and slow entity-breakup previews.')
