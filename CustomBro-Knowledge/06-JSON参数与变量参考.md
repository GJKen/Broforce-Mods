# JSON 参数与变量参考

来源：[Bro-Maker Wiki - Parameters](https://github.com/Gorzontrok/Bro-Maker/wiki/Parameters) / [Variables](https://github.com/Gorzontrok/Bro-Maker/wiki/Variables)。Wiki 表格是字段导航，不保证适用于所有 BroMaker 版本；实际支持字段与序列化方式应对照当前使用的 `BroMakerLib.dll`。

## 角色 JSON 的区域

- `parameters`：贴图和角色界面资源，例如 `Sprite`、`GunSprite`、`Avatar`、`SpecialIcons`、偏移和动画选项。
- `beforeAwake`、`afterAwake`、`beforeStart`、`afterStart`：在相应生命周期阶段给角色对象字段赋值。可用字段取决于角色类及其基类。
- `cutscene`：出场动画资料；具体结构参考当前 BroMaker 文档和已加载角色定义。

本地 BroMaker 源码的 `CustomBroforceObjectInfo` 在每个阶段调用 `DynamicFieldsValueSetter`，传入对应阶段的字典。未提供的键不会由这段赋值逻辑主动写成零值；但最终值仍取决于所选 `HeroType` 复制的预制体、Unity 初始化及角色 `Awake` / `Start` 自身逻辑。因此，不要把某个旧角色出现的零值现象推广成“所有留空字段都会清零”，也不要假定空对象必然保留想要的预制体值。重要参数应显式配置，并在运行时核对最终值。

最小示例：

```json
{
  "parameters": {
    "BetterAnimation": true,
    "Sprite": "sprite.png",
    "GunSprite": "gunSprite.png",
    "Avatar": "avatar.png",
    "SpecialIcons": "special.png"
  },
  "beforeAwake": {
    "fireRate": 0.1,
    "originalSpecialAmmo": 1
  },
  "afterAwake": {},
  "beforeStart": {},
  "afterStart": {}
}
```

`parameters` 中常见贴图键：

| 字段 | 用途 |
|---|---|
| `Sprite` | 角色动作图集 |
| `GunSprite` | 枪动作图集 |
| `Avatar` | 角色头像图 |
| `SpecialIcons` | 特技弹药图标 |
| `SpecialIconOffset`、`SpecialIconSpacing` | 特技图标布局 |
| `GunSpriteOffset` | 枪图偏移 |
| `BetterAnimation` | 启用 BroMaker 扩展动画配置；仍需核对各 `useNew*Frames` 字段 |
| `Halo`、`JetPackSprite` | 可选外观行为，确认当前版本支持后再设置 |

## 贴图变体

部分贴图参数支持数组，供 BroMaker 的角色变体逻辑选取。数组可用性及长度规则应以当前版本 `Parameters` 页面或源码为准；不要仅凭字段名推断所有键都可变体化。

`SpecialIcons` 还承载多弹药槽结构：单图标可用字符串；单角色的多个弹药图标可用一维数组。需要同时表示多个角色变体和多个弹药槽时，使用嵌套数组，并在目标版本确认解析顺序：

```json
"SpecialIcons": [["ammo1.png", "ammo2.png"], ["alt1.png", "alt2.png"]]
```

## 字段清单怎么用

旧版 wiki 的全局默认值、逐角色专有字段和 `projectile` / `specialGrenade` 名称列表未在所有目标版本逐项核实，因此本页不继续复制整份默认值表或把字符串检索结果当作有效性证明。需要查找字段时：

1. 在项目当前使用的 `BroMakerLib.dll` / 源码中查角色字段和 JSON 解析入口。
2. 对 `Assembly-CSharp.dll` 中的游戏字段、枚举和方法签名，用反编译源码核对。
3. 在小型测试角色中验证字段写入时机和运行效果，再将配置纳入正式角色。

可运行的字段实例参考 [Aquabro 角色 JSON](../CustomBro/Aquabro/_Mod/Aquabro.json)；其中数值只描述该角色，不构成通用默认值。
