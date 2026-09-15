-- 稳定滑行段第2/3/4帧是回拉起势、蓄力、蓄满的武器主稿。
-- 返回画布坐标中的武器和手臂，供完整图集同步与单独部署共用。
return function(root)
  local source=app.open(root..'tools/haiwang_trident_zipline_charge.aseprite')
  assert(source and source.width==32 and source.height==32 and #source.frames==12 and #source.layers==6,
    '绳索蓄力独立稿结构已变化')
  assert(source.layers[5].name=='金色三叉戟' and source.layers[6].name=='活动手臂',
    '绳索蓄力独立稿的武器或手臂层已变化')
  local result={}
  for pose=1,3 do
    local weapon,hand=Image(32,32,ColorMode.RGB),Image(32,32,ColorMode.RGB)
    local wc,hc=source.layers[5]:cel(pose+1),source.layers[6]:cel(pose+1)
    assert(wc and hc,'绳索蓄力主稿缺少武器或手臂')
    weapon:drawImage(wc.image,wc.position);hand:drawImage(hc.image,hc.position)
    result[pose]={weapon=weapon,hand=hand,grip={8.5,16.5}}
  end
  source:close()
  return result
end
