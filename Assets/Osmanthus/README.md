# Osmanthus VR — 项目说明 / README

一个约 4 分钟、运行在 **Meta Quest 3** 上的独立 VR 体验：跟随飘浮的桂花穿过颐和园长廊，触摸桂花展开两段视频，最后走到湖边看到「现代都市 × 园林」融合的重构景象。

---

## ⚠️ 最重要的一件事：用哪个场景

**所有人都只用这一个主场景：**

```
Assets/Osmanthus/Scenes/04_CompleteCorridorLake.unity
```

`Scenes/` 里的 **`01_Courtyard` / `02_Gallery` / `03_Observatory` 都是早期废弃的，不要用**。主体故事线、玩家、交互、视频全部在 `04` 里。

---

## 一、如何在本地打开编辑

1. `git clone https://github.com/okokelly/Osmanthus.git`（如果是这个分支的改动，先 checkout 对应分支再合并）。
2. 打开 **Unity Hub**，安装 Unity 版本 **`6000.5.7f1`**（必须完全一致，含 **Android Build Support** 模块：Android SDK/NDK + OpenJDK）。
3. Unity Hub → *Add* → 选择这个项目根目录打开。首次打开会 import，比较久。
4. 打开场景：`Assets/Osmanthus/Scenes/04_CompleteCorridorLake.unity`。
5. **Build 到 Meta Quest 3**：USB 连上 Quest 3，`File → Build Settings` → `Build And Run`（场景勾 `04`）。
6. 编辑器里想「走一走」：需要 Quest Link 或 XR 模拟器进 Play 模式。**注意：Unity 编辑器窗口在后台不聚焦时，Play 模式会被降速，动画会变慢——聚焦窗口或直接真机就正常。**

---

## 二、当前状态 (Status)

Console **0 error**。以下都已完成并可通过菜单一键重建（见第五节）：

- **玩家 / 移动**：Meta `OVRPlayerController`（摇杆连续走 + snap turn），固定眼高（EyeLevel + 运行时锁定 1.5m）。
- **边界**：一圈隐形碰撞墙 + 贴合地面的隐形「安全地面」，防止走出栈道掉下去。
- **Scene 3 湖景**：远/中/雾景改成**柱面弧形背景**包住视野（宽 240°、更高，减少看到幕布边缘）；远景城市被推远并调暖，和园林融合成一体；便宜的 URP 湖面 shader（渐变 + UV 波纹 + 假倒影）。
- **桂花**：导入金色桂花模型 → prefab（浮动动画 + 3 档明暗），把 29 个橙色占位替换成大小/明暗/朝向随机的飘浮桂花。
- **触摸→视频（Phase C）**：右手柄射线瞄准引导桂花 + 扣食指扳机 → **头锁定的大幕布展开 + 环境全黑** → 播视频一 → 桂花往前飘 → 再触发 → 视频二 → 桂花引到湖边。

---

## 三、操作方式 (Controls)

- **移动**：手柄摇杆连续走；**转向**：snap turn。
- **触摸桂花**：用**右手柄的射线**瞄准桂花，**扣下食指扳机**触发。

---

## 四、Teammate：拿到 MP4 之后怎么把视频加进来

视频目前是**占位**（暖色/冷色的色块，代表"视频一/视频二"）。加真视频只需 3 步，不用改代码：

1. 把两个 `.mp4` 拖进工程，比如新建文件夹 `Assets/Osmanthus/Video/`，放进去（Unity 会识别成 `VideoClip`）。
2. 在 `04` 场景的 Hierarchy 里找到 **`07 Osmanthus Interaction → Osmanthus Video Sequence`** 这个物体，看它的 `Osmanthus Video Sequence` 组件。
3. 把第一段视频拖到 **`Clip 1`** 槽位、第二段拖到 **`Clip 2`** 槽位。保存场景即可。

说明：
- 一旦某个 Clip 有值，就会**自动播放真视频**（占位色块/文字被替换），播完自动收起。
- 视频**声音**走 VideoPlayer 的 Direct 输出，正常有声。
- 幕布默认 `4.4m × 2.5m`、在眼前 `3.2m`、**头锁定**（永远在正前方）、背景**全黑**。这些参数在 `VideoScreen` 组件上可调（`width/height/distance/dimAlpha`）。
- `Placeholder Seconds`（默认 8 秒）只在**没有 Clip**时决定占位停留多久；有真视频时以视频长度为准。

---

## 五、如何修改

关键可调参数（都是脚本里的常量/组件字段）：
- 眼高：`MetaPlayerRigSetup.EyeHeight` 和 `FixedEyeHeight.eyeHeight`（当前 1.5m）。
- 边界墙/安全地面：`BoundaryBuilder.cs` 里的 `Walls` / `SafetyFloors` 数组。
- 背景弧形大小/角度：`Scene3Quick2DBuilder.cs` 里的 `CreateArcCard(...)` 调用。
- 幕布/全黑/大小：`VideoScreen` 组件字段。

---

## 六、Pending / TODO

1. **走廊外景**：从走廊往外看的假山、树丛、桂花树等点缀。不影响主体故事线。
2. **眼高微调**：已修「视点跑到天花板高度」的 bug（原因是 Floor-level 追踪把真实头高叠加到了 rig 偏移上；现在强制 EyeLevel + 运行时锁定 1.5m）。真机上如仍偏高/偏低，改 `FixedEyeHeight.eyeHeight`。目标大约「柱子 2/3 高度」。
3. **湖面背景边缘穿帮**：走到湖前面沉浸感 OK；离得远时以前会看到幕布边缘——已把弧形加宽到 240°、加高，边缘基本移出视野。若仍有穿帮，可继续加大 `angleSpan`（甚至做成接近 360° 的全景）。
4. **边界墙**：已收紧安全地面到真实地面范围、补齐周界。**仍需真机走一遍确认没有漏的地方**；参数全在 `BoundaryBuilder.cs`。
5. **视频接入**：见第四节（等 teammate 的 MP4）。

已按反馈处理的两点：
- 触摸后幕布位置怪、夹在柱子间 → 已改成**头锁定大幕布**，位置不再依赖眼高。
- 「压暗」难做 → 已**直接全黑**，中间一块大幕布。

---

## 七、需要真机 (Quest 3) 验证的点

编辑器出不了这些，最终得在 Quest 3 上过一遍：走路手感、边界是否漏、射线瞄准与扣扳机、眼高、视频时长与节奏、背景边缘、幕布大小与距离。
