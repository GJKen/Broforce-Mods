# 实现范围与验证说明

当前版本为0.19.4。菜单范围来自已检查的 Utility Mod「Spawn Enemy」66项；其中59项具有增强实现，7项保留原生职责。41组精确单位/PolymorphicAI组合之外，另有4种战斗虫类覆盖6个菜单，以及没有AI组件的钻车。菜单项、类型组合和验证样本不是同一种计数。

## 实现范围

| 菜单组 | 具有增强实现的项目 | 不适用项目 |
| --- | --- | --- |
| Normal（24） | Mook、Riot Shield Mook、Grenadier Mook、Bazooka Mook、Suicide Mook、Bruiser、Suicide Bruiser、Strong Bruiser、Strong Mook、Scientist Mook、Mech、Brown Mech、Scout Mook、Treasure Mook、Ninja Mook、Jetpack Mook、Jetpack Bazooka Mook、Snake、Attack Dog、Skinned Mook、Elite Bruiser、Mook General、Alarmist | 剧情Satan |
| Aliens（6） | Facehugger、Xenomorph、Xenomorph Brainbox、Brute、Screecher、Baneling | — |
| Hell（8） | Hellhound、Undead Mook、Undead Suicide Mook、Warlock、Boomer、Executioner、Lost Soul | Soul Catcher：保留原生释放 |
| Worms（7） | Boneworm、Boneworm Behind、Alien Worm、Alien Facehugger Worm、Alien Facehugger Worm Behind、Large Alien Worm | Sand Worm：固定隧道/弧线路径 |
| Bosses（6） | Satan Miniboss、CR666、Stealth Tank、Terrorkopter、Terrorbot、Megacockter | — |
| Vehicles（11） | Mook Launcher Tank、Cannon Tank、Rocket Tank、Artillery Truck、Blimp、Turret、Drill Carrier、Mook Truck、Motorbike、Motorbike Nuclear、Dump Truck | — |
| Friendly（3） | — | Pig、Rotten Pig、Villager |
| Other（1） | — | Remote Control Car |

不适用项没有持续自主敌人战斗职责，或由固定路径/外部控制负责；没有将它们强行套入枪兵策略。

## 联机与战斗验证

实现阶段已对普通兵种、近身组、指挥组、载具、蠕虫和Boss运行定向受控检查，并取得代表性双端自然移动、武器、召唤或来源匹配伤害。验证按具体入口和样本成立，不将一个共享类型样本推广为每个菜单的完整双向验证。

Ninja拥有端揭示、Scout原生门召援，以及Strong Bruiser/Brown Mech/Treasure精确对象的代表验证已经补齐。Warlock、Lost Soul、Screecher、Baneling和Mega的部分镜像链依赖CMM修复；当前组合为EAI0.19.4、CMM0.5.6/世界协议25。

Mega测试明确受控进入原生榴弹阶段，再由原生武器生成、发射及结束。匹配网络身份的双端真实效果、一次发送/接收及移除成立；没有将其写成自然阶段入口通过。客户端伤害的部分样本只精确归因到launcher/Boss，没有冒认单枚榴弹身份归因。

部分自然阶段、每种朝向、晚加入、角色混装及长会话仍未全面验证。Baneling历史方向差异没有因酸作用覆盖就被声明消除；原生酸作用为0的样本不证明额外酸伤害。

## 代码精简与功能保持

| 项目 | 精简前 | 当前 | 实际变化 |
| --- | ---: | ---: | --- |
| EAI DLL | 0.19.1，135168字节 | 0.19.4，126976字节 | 减8192字节，约6.06% |
| CMM DLL（组合环境） | 0.5.4，521728字节 | 0.5.6，521216字节 | 减512字节，约0.10% |

这些比较针对精简前基线，不表示相对所有更早历史发行版都更小。EAI0.19.3→0.19.4以及CMM0.5.5→0.5.6的文件大小均未进一步下降。

EAI将固定Harmony目标迭代器改为数组，同时保持真实目标顺序和补丁安装；动态查询容器保留。局部复用一次决策中的Kind分类，减少重复查询。CMM减少固定房间字段反射和重复字段查找，继续读取真实可变数组。

近期验收包括23个真实补丁目标、保留方法的解析IL比较，以及同房坦克和受控Mega真实效果/伤害代表。最后一批在双端比较EAI383个和CMM1325个未修改方法；另外对七组单位的协作/开火点、真实路径、动作队列、Memory、RNG及Trooper Tick保护分支做差分。正常成功路径样本限Trooper/Bazooka/Warlock，不把Boomer/Executioner双方均失败的样本写成成功寻路。

最后一批原出生点不满足完整自然入场门禁，实际查询和差分在另一处有支撑且净空的平台完成。未变化的特殊技能复用已有证据，未重跑全部自然战斗矩阵。测得的收益是局部Kind/反射次数减少，没有测定整体FPS或性能百分比。

本次发布整理增加可直接导入的安装包及构建后的打包步骤，生产源码及已验证DLL未改。ZIP中的DLL/Info.json与发行目录逐字节一致，manifest版本同为0.19.4；不以重复构建或重新开房替代已有验收。可复用受控脚本的前提见 [测试说明](../tests/README.md)。
