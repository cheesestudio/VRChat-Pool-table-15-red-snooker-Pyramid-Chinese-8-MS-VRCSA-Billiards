[中文](README.md) · [English](README.en.md) · [日本語](README.ja.md)

---

![OG$UI_JKGDD6QL21LEK)9DE](https://github.com/user-attachments/assets/04739895-457d-4806-8d93-6f3ed2b80bbf)
# 中文台球俱乐部桌 CBC Pool Table

VRChat 台球桌预制件，用于 VRChat 地图 **[中文台球俱乐部 Chinese Billiards Club](https://vrchat.com/home/launch?worldId=wrld_0a35397b-2e7d-4f01-8552-034ab8e76e2e)**。

感谢朋友们的支持：eijis-pan, RokaOvO, WangQAQ, COCOA GAME
球杆：Tempest, catte paling, tarsan, カボ
QQ 群：780855553

> 原项目：[MS-VRCSA-Billiards](https://github.com/Sacchan-VRC/MS-VRCSA-Billiards)

---

## 游戏模式

| 模式 | 说明 |
|------|------|
| 中式八球 | 含完整规则和集球器 |
| 15 球斯诺克 | 6 红/15 红可选 |
| 俄罗斯台球 | 大球窄袋，可击打任意球，入袋计分 |
| 10 球 | 1–10 号，须先碰最小号，指定球袋，可选 WPA 规则 |
| 开仑台球 | 0/1/2/3 库开仑 |
| 9 球 | 1–9 号，须先碰最小号，先打进 9 号球胜 |
| 比球 | 开球前定先后手，双方各击一球，停得离对面库边更近者胜 |

---

## NPC AI 对手 (PracticeManager)

单人练习模式下可与 AI 对手对打。NPC 具备完整的台球决策能力：

### 击球策略（优先级从高到低）

| 优先级 | 类型 | 说明 |
|--------|------|------|
| PASS 1 | 直接球 | 直接入袋的击球，含走位评估 |
| PASS 2 | 单库翻袋 | 目标球碰一次库边后入袋 |
| PASS 2b | 两库翻袋 | 目标球碰两次库边后入袋 |
| PASS 2.5 | 薄切球 | 大切角薄切入袋 |
| PASS 3 | 单库勾库 | 母球碰一次库边后击中目标球 |
| PASS 3b | 两库勾库 | 母球碰两次库边后击中目标球 |
| — | K 球 | 安全球，无进球路线时的防守策略 |

### 技术特点

- **库边摩擦补偿**：翻袋时考虑库边摩擦导致的反弹角度偏移，自动调整瞄准方向
- **轨迹验证**：击球前模拟目标球路径，验证是否经过袋口
- **路径遮挡检测**：检查母球→目标球、目标球→库边、库边→袋口三段路径是否有球遮挡
- **犯规预测**：预测母球击球后轨迹，避免意外犯规（如母球进袋、碰到非目标球）
- **走位评估**：评估击球后母球停留位置，优先选择有利于下一杆的位置
- **力度控制**：根据距离和球型自动调整击球力度，翻袋/勾库时限制最大力度
- **重复检测**：同一球同一袋重复 3 次以上强制切换安全球

### 状态机

```
IDLE → CALCULATING → CHARGING → DELAYING → SHOOTING → OBSERVING → IDLE
 空闲    计算中       蓄力中      等待中      击球中      观察结果
```

### 测试模式

PracticeManager 支持自动测试模式（`testMode`），可无人值守自动对打多局：
- 自动开球、轮流击球、记录每杆结果
- 日志输出到 `Assets/npc_log.txt`
- 包含：球位、瞄准方向、切角、力度、旋转、轨迹验证结果
- 可通过 `Editor/NpcLogExporter.cs` 导出日志

---

## 桌子功能

### 玩家自定义 (TableHook)

- 自选球杆皮肤、球材质、桌子颜色（球杆网络同步，球和桌子本地同步）
- 球杆大小、粗细、平滑度、颜色偏移可调
- 自动保存和加载设置（VRC PlayerData）
- 可通过 Discord/QQ 群上传/下载设置

### 计分系统 (ScoreManagerV4)

- 自动记录对局分数
- 排行榜上传到后端（`wangqaq.com`，HMAC 加密认证）
- 45 秒计时器

### 其他功能

- 全自动翻译系统（检测 VRChat 本地语言，中/日/英）
- 专属名字颜色功能
- 可持久化个人数据及排行榜
- 库边颜色切换
- 郊狼联动支持（输家被电）

### 设置

1. 将 `BilliardsModule` 设置为 22 层，物理设置为只与自己交互（上面 MS 按钮可自动设置）
2. 在场景内放一个 `TableHook`（运行场景会自动检测添加，调整位置即可）

![image](https://github.com/user-attachments/assets/f453ae11-0735-4885-b700-87101d5971c7)

![Q84OOB{37Q{XY946MTR$E`F](https://github.com/user-attachments/assets/6bf18499-5926-4ca2-8a8c-8f8e33fd9faa)

- 自定义球杆皮肤和球材质：在 TableHook 里加贴图，代码里预留了几个空位
- UdonChips 对应：在 BilliardsModule 里点按钮
- 用现成包即可；如克隆库需自行添加 VRCSDK (>=3.7.5) 和 UdonSharp

> [设置自定义球杆 How to set custom cue](https://youtu.be/YnoQ9jsUg0k?si=EfdxX1FDMUZXM2RX)

---

## VRC Light Volumes (VRCLV) 支持

台球桌提供相互独立的标准光照和 VRC Light Volumes（VRCLV）着色器。这样，未安装 VRCLV 包的项目可以继续使用标准着色器，不会因为缺少 `LightVolumes.cginc` 而让桌子显示成全白。

**一键切换**：
1. 在 Hierarchy 中选择任意带有 `BilliardsModule` 组件的台球桌。
2. 在 Inspector 的 **VRC Light Volumes** 区域查看包和材质状态。
3. 安装 VRCLV 后，点击 **Use VRC Light Volumes** 启用；点击 **Use Standard Lighting** 恢复为不依赖 VRCLV 的标准着色器。

切换操作会显示确认框，并一次更新项目内所有 BilliardsModule 共用的相关材质。也可以使用菜单 `Tools > VRC LV > Enable For Billiards Materials` 或 `Tools > VRC LV > Use Standard Billiards Materials`。UI、准线、排行榜、重置按钮和投影等不受光照的材质不会被切换。

**着色器对应关系**：

| 标准光照 | VRC Light Volumes |
|---|---|
| `Standard` | `cheese/VRC LV Standard` |
| `metaphira/TableSurface` | `cheese/TableSurface VRCLV` |
| `metaphira/TableSurface (Glass)` | `cheese/TableSurface Glass VRCLV` |
| `metaphira/TableSurface (Quest)` | `cheese/TableSurface Quest VRCLV` |

**包检测与注意事项**：编辑器通过 `Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc` 检测 VRCLV，不依赖不存在的 Shader 安装检测宏。未安装包时，启用按钮会被禁用，但 **Use Standard Lighting** 仍可用于恢复材质。如果准备移除 VRCLV 包，建议先切回标准光照。启用后还需要按照 VRCLV 文档在场景中配置 `LightVolumeManager`、Light Volumes 并完成所需烘焙；仅安装包和切换材质不会自动生成 Light Volume 光照。

已在 Unity 2022.3.22f1 与 VRC Light Volumes 3.0.0-dev.14 下通过 Shader 导入和编辑器脚本编译检查。

---

## 可能性功能（未来）

- 桌子控件更多功能
- 更丰富的名字颜色
- 球轨迹线

---

## 人员

最终解释权：
Roka：俄式球桌和中式球桌是我按照 Sacc 台球桌的 10ft 桌和 12ft 斯诺克桌模型为基础更改的。起初中式球桌仅用于我的地图 "FIVI Flight" 使用，之后允许了中文台球俱乐部使用且协助开发。但使用权并未明确声明仅用于我的地图和中文台球俱乐部地图，因此不限制使用。最终解释权归中文台球俱乐部所有。

![image](https://github.com/user-attachments/assets/362abbc4-c159-4617-a6a2-23b64765709a)
![image](https://github.com/user-attachments/assets/8da69556-b526-488a-8127-5fc319de84a9)
![image](https://github.com/user-attachments/assets/f1ff2b1e-e0a0-49d5-becb-be3bf18a4ea8)
![9DH{L{LM 4~0@{)PZ4TD_tmb](https://github.com/cheesestudio/VRChat-Pool-table-with-15-red-snooker-Pyramid-Chinese-8-ball-based-on-MS-VRCSA-Billiards/assets/52149451/7f894791-cf72-473e-bbe6-20bec9804917)
![image](https://github.com/user-attachments/assets/969415da-7bda-4689-9e19-54c2f88e8d73)
![image](https://github.com/user-attachments/assets/36cfebe4-d929-4ac5-a14d-f71371f40442)
