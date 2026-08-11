# Summer Palace Osmanthus VR — Visual System Plan

## 1. 视觉系统目标

本项目不是写实复刻颐和园，也不是香水广告。美术系统需要表现的是：

> **一段被气味唤起、因距离而碎片化，并在当下重新组合的文化记忆。**

美术成员不仅负责模型资产，也负责项目完整的视觉表达，包括：

- Art Direction
- 模块化环境资产
- 柱体和彩画
- 材质与 Shader
- 桂花粒子
- 雾片与假光轴
- 记忆碎片
- Unity Animation
- Timeline 编排
- Scene 3 重组效果
- Quest 美术性能优化

声音暂时不包含在本计划中。

---

## 2. 三个场景的视觉变化

| 阶段 | 视觉状态 | 概念含义 |
|---|---|---|
| Scene 1 | 冷、灰、雾、低饱和、结构残缺 | 记忆刚被气味唤起，但仍然遥远 |
| Scene 2 | 柱体局部变暖，彩画和记忆逐渐亮起 | 用户主动触碰并唤醒记忆碎片 |
| Scene 3 | 暖金与冷色并存，园林和现代城市重组 | 文化身份被重新建构，而不是恢复原状 |

视觉判断的核心标准：

- 能否辨认出长廊的文化来源
- 是否有记忆模糊、断裂和漂移的感觉
- 桂花是否像一种无形气味在空间中移动
- 三根柱子是否呈现记忆逐层被唤醒的过程
- Scene 3 是否形成新的混合空间，而不是完整复原颐和园

---

## 3. Art Bible

第一天先完成一页视觉规范，后续所有资产和特效遵循同一套语言。

Art Bible 包括：

- 三个场景的 Color Script
- 冷灰到暖金的色彩变化
- 长廊形态、比例和简化方式
- 彩画的抽象与简化方式
- 园林碎片和现代城市碎片的形状语言
- 桂花粒子的颜色、尺寸和运动感觉
- 雾、光轴和发光效果的统一规则
- Scene 3 最终构图草图

建议核心颜色：

- 冷雾蓝灰
- 暗朱红
- 低饱和青绿
- 桂花金
- 夕阳米白
- 少量现代城市冷白光

Art Bible 不需要做成长篇 PPT，一张信息清楚的大图即可。

---

## 4. 模块化环境资产

不要制作完整颐和园，而是制作一套可以在 Unity 中重复排列的模块。

### P0：必须完成

- Corridor Floor
- Pillar A / B / C
- 横梁
- 简化屋檐
- 栏杆
- 窗格或门洞
- 屋顶轮廓
- 长廊尽头剪影
- 湖边平台
- 湖面 Plane
- 远景园林剪影
- Scene 3 现代城市碎片

建议一个标准长廊单元约 2–3 米，通过重复、旋转、缩放和局部缺失形成空间。

近距离看到的三根交互柱子优先级最高；远景建筑只需要轮廓成立。

### P1：时间允许再做

- 小型装饰构件
- 更多彩画变化
- 石头和植物剪影
- 远景桥体
- 更多城市窗格
- 湖面倒影碎片

### 不在范围内

- 完整颐和园建筑群
- 高精度瓦片和雕花
- 写实植物生态
- 大量独立小物件
- 写实水体模拟

---

## 5. 三根柱子的视觉设计

三根柱子可以共用基础模型，但激活内容和视觉性格需要不同。

### Pillar 1：Place / Cultural Image

- 园林彩画
- 青绿、朱红和少量金色
- 屋檐、水纹、窗格等记忆碎片
- 粒子运动比较稳定、有秩序

### Pillar 2：Everyday Memory

- 更柔和、生活化的图像纹理
- 窗户、布料、光影或模糊人形
- 粒子运动更慢、更接近用户
- 暖色比例增加

### Pillar 3：Distance and the Present

- 冷白城市光、玻璃、网格
- 城市窗格、机场或列车感的抽象碎片
- 运动方向与前两根柱子稍有冲突
- 表达海外生活造成的断裂与重组

每根柱子需要提供：

- 未激活状态
- Hover 状态
- Activated 状态
- Emission Mask
- 激活时出现的记忆碎片
- 对应的粒子爆发样式

程序负责发送交互事件，美术系统负责事件发生后的视觉表现。

---

## 6. 材质系统

使用少量可复用材质，不为每个模型建立独立材质。

### 6.1 Corridor Base Material

- 长廊木结构和彩画共用
- 可调整颜色、饱和度和亮度
- 尽量使用 Opaque
- 可选简单顶点色变化

### 6.2 Pillar Emission Material

- Emission 强度可从 0 淡入
- 使用 Mask 控制图案发光区域
- 暴露颜色和强度参数
- 支持 Hover 和 Activated 两档强度

### 6.3 Memory Fragment Material

- 半透明或 Alpha Clip
- 支持边缘淡出
- 可以有轻微 UV 漂移
- 避免大面积透明层互相重叠

### 6.4 Fog Card Material

- Unlit Transparent
- 柔和无缝噪点
- 可调整颜色、透明度和移动速度
- 不接收实时光照和阴影

### 6.5 Fake Light Shaft Material

- Additive
- 从亮到透明的纵向渐变
- 不写入阴影
- 用于屋檐、柱间和长廊尽头

### 6.6 Lake Material

- 简化颜色渐变
- 非写实反射
- 可加入轻微顶点波动
- 不使用昂贵的屏幕空间反射

### 6.7 Osmanthus Particle Material

- Unlit / Additive 或 Alpha Blend
- 统一桂花金
- 不依赖实时灯光
- 通过粒子颜色控制冷暖变化

当前 Unity 项目已经包含 URP 17.5 和 Shader Graph，可直接制作以上材质。

---

## 7. 特效系统

### 7.1 VFX 1：Scent Trail

用于开场和移动引导：

- 少量桂花粒子从用户附近经过
- 缓慢进入长廊
- 形成柔和、连续的引导轨迹
- 不表现成游戏任务箭头
- 有轻微旋转、上下漂浮和速度差异

### 7.2 VFX 2：Pillar Awakening

用于触碰柱子：

- 粒子先从柱体彩画中亮起
- 短促 Burst 向外扩散
- 一部分围绕柱体旋转
- 一部分飞向记忆碎片
- 激活后保留少量余光

### 7.3 VFX 3：Reconstruction

用于 Scene 3：

- 三组粒子从长廊方向进入湖面
- 粒子在园林和城市碎片之间移动
- 形成视觉连接
- 引导碎片移动到新的位置
- 最后汇入混合空间或飘向天空

### 7.4 Fog Cards

制作 2–3 张无缝雾纹理：

- 近距离轻薄雾
- 中距离横向雾
- 远距离遮挡雾

用途：

- 隐藏长廊边界
- 遮挡未激活区域
- 增加空间层次
- 掩盖场景区域之间的切换

### 7.5 Fake Light Shafts

使用锥体或交叉平面制作：

- 屋檐间斜向光
- 柱体顶部暖光
- 长廊尽头引导光
- Scene 3 湖面上的金色光束

它们是 Additive 材质模型，不是真正照亮环境的灯。

### 7.6 Bloom

Bloom 只作为性能允许后的可选增强：

- 视觉效果不能依赖 Bloom 才成立
- 先用贴图光晕、Emission 和 Additive 材质建立光感
- Quest 真机稳定后才测试低强度 Bloom

---

## 8. Animation 制作方式

本项目的大部分动画直接在 Unity 中制作，不需要复杂角色绑定。

### 8.1 Unity Animation

用于单个物体或 Prefab 的短动画：

- 柱体彩画亮起
- Emission 从 0 淡入
- 记忆碎片出现、缩放和漂浮
- 光轴淡入淡出
- 柱子 Hover 时的呼吸光
- 湖面碎片轻微摆动
- 单个碎片的位置和旋转变化

这些动画使用 Unity Animation Window 制作 Animation Clip。

### 8.2 Unity Timeline

用于编排多个对象组成的完整段落：

- 柱子激活的完整视觉顺序
- 三组记忆碎片依次出现
- Scene 3 Reconstruction
- 粒子系统播放和停止
- 色调从冷变暖
- 雾和假光轴出现
- 最终文字出现和画面淡出

Scene 3 建议时间轴：

```text
0–5s      湖面和远景显现
5–15s     园林碎片出现
15–25s    现代城市碎片进入
25–40s    桂花连接两类碎片
40–55s    碎片重组为混合空间
55–65s    最终画面与文字出现
```

Timeline 的意义是让美术成员可以直接拖动时间轴调整节奏，不需要每次修改代码。

### 8.3 Particle System

桂花运动主要由 Unity Particle System 完成：

- 出生和消失
- 飘动速度
- 旋转
- 颜色与尺寸变化
- 柱体触发时 Burst
- Scene 3 中的连接和汇聚

整体路径可以通过移动 Particle System、使用 Force/Velocity 模块或设置少量引导目标实现。

### 8.4 Blender Animation

只有 Unity 难以直接完成的模型变形才在 Blender 制作，例如：

- 屋檐真正断裂或展开
- 模型从完整状态碎裂成多个部分
- 特殊弯曲和扭转
- 复杂骨骼动画
- 需要精确控制的建筑变形

即使动画来自 Blender，最终播放顺序仍由 Unity Timeline 控制。

### 推荐分工

- 三根柱子：Unity Animation
- 柱体完整触发过程：Timeline
- 桂花：Particle System
- 雾与假光轴：Unity Animation
- Scene 3 碎片移动与重组：Timeline
- 特殊模型变形：必要时使用 Blender

---

## 9. Scene 3 Reconstruction

Scene 3 是整个项目的 Hero Moment，也是美术最高优先级部分。

碎片分组：

- `Garden_Fragments`
- `Everyday_Fragments`
- `PresentCity_Fragments`

动画过程：

1. 湖面最初几乎为空
2. 三组碎片分别从不同方向出现
3. 园林碎片先尝试形成传统空间
4. 城市碎片进入并打断原有结构
5. 桂花粒子连接两类碎片
6. 所有碎片重新排列成混合景观
7. 色调从冷色过渡到暖金
8. 最终仍保留部分冷白城市光和结构断裂

最终空间应当：

- 既不像完整颐和园
- 也不像普通现代城市
- 能看到两种视觉语言互相影响
- 保留没有完全拼合的边缘
- 让“重建”而不是“复原”成为结论

---

## 10. 一周美术计划

### Day 1：Visual Direction

- 三场景 Color Script
- 一页 Art Bible
- Scene 3 最终构图草图
- 完整资产清单
- 确定模型尺度和文件命名
- 与 Unity 灰盒对齐空间大小

当天不要精修模型。

### Day 2：模块化长廊

- 柱子
- 地板
- 横梁
- 简化屋檐
- 栏杆
- 门洞或窗格
- 基础材质
- 在 Unity 中拼出一小段完整长廊

当天必须把第一批模型导入 Unity，不能只停留在 Blender。

### Day 3：三根柱子

- 三种柱体彩画或 Emission Mask
- 未激活、Hover、Activated 三种状态
- 三组临时记忆碎片
- 一次完整柱体激活动画
- Quest 中检查近距离比例和纹理清晰度

如果时间紧，三根柱子共用模型，只替换贴图和激活内容。

### Day 4：桂花与空间特效

- Scent Trail
- Pillar Burst
- Reconstruction Particle
- Fog Cards
- Fake Light Shafts
- 柱体光晕
- 冷色到暖色的材质参数方案

当天需要在 Quest 真机检查透明材质。

### Day 5：Scene 3

- 湖面
- 园林碎片
- 城市碎片
- 远景轮廓
- 最终混合空间
- Reconstruction Timeline
- Scene 3 最终主视角构图

这一天的优先级高于继续增加长廊细节。

### Day 6：整体整合与优化

- 替换临时资产
- 统一材质和颜色
- 删除看不到的背面和小细节
- 合并重复材质
- 减少透明层叠
- 控制粒子数量
- 检查 Scene 3 掉帧
- 检查不同站位能否看到主要动画

### Day 7：Visual Freeze

- Bug 和穿模修复
- 粒子速度和颜色微调
- 湖面、雾和光轴微调
- 最终构图修复
- Quest 截图与录屏
- 删除未使用的测试资产

不再增加新模型或新特效。

---

## 11. 工具

### Unity

用于：

- 场景组装
- URP 材质
- Shader Graph
- Particle System
- Animation
- Timeline
- Prefab
- Quest 真机验证
- Profiler 和 Frame Debugger

当前项目已有 URP、Shader Graph、Particle System 和 Timeline，美术基础工具已经够用。

### Blender / Maya / Cinema 4D / 3ds Max

选择成员最熟悉的一款，用于：

- 模块化长廊建模
- 柱子、屋檐和栏杆
- 城市与园林碎片
- UV
- 简单减面
- Pivot 和模型拆分
- FBX 导出

如果没有既有偏好，建议使用 Blender。

### Photoshop / Procreate / Krita

三选一，用于：

- 彩画简化
- Emission Mask
- 桂花 Sprite
- 雾片纹理
- 光轴渐变
- 记忆影像
- Texture Atlas
- Scene 3 草图

### 可选工具

- Substance 3D Painter：柱体彩画、木材和 Mask
- PureRef / Figma：参考图、Color Script 和构图
- Meshy / AI 3D：远景石头、小型装饰物和低优先级背景资产
- AI Image Generation：Scene 3 草案、记忆影像和彩画方向探索

AI 资产必须经过人工清理、减面、重新整理材质和统一色彩。

### 不建议使用

- Houdini
- VFX Graph
- HDRP
- 写实体积雾
- 流体模拟
- 写实水体插件
- 实时全局光照
- 大量实时阴影
- 复杂程序化建筑系统
- 高复杂度扫描模型

---

## 12. Quest 美术性能规范

第一版使用以下 Guardrails：

- 1 Unity Unit = 1 米
- 普通纹理优先 512 或 1K
- 2K 只用于重要的共享 Atlas
- 每个模块尽量 1 个材质，最多 2 个
- 重复建筑使用共享材质
- 尽量使用 Opaque 或 Alpha Clip
- Transparent 只用于雾、光轴、记忆碎片和少量粒子
- 不依赖 Bloom 才能看出发光
- 粒子使用简单 Quad
- 同一视线避免超过约 3 层大面积透明平面
- Scene 3 碎片分批出现，不一次全部激活
- 尽量不使用实时阴影
- 每天在 Quest 上测试

最终目标是稳定 72 FPS，具体三角面、材质和粒子数量根据 Quest 型号及真机测试调整。

---

## 13. 文件结构与命名

```text
Assets/Art/
├── Models/
│   ├── Corridor/
│   ├── Pillars/
│   ├── Lake/
│   └── Reconstruction/
├── Materials/
├── Textures/
├── VFX/
├── Animations/
├── Prefabs/
└── SourceArt/
```

命名示例：

```text
ENV_Corridor_Floor_A
ENV_Corridor_Pillar_A
ENV_Lake_Platform
FRAG_Garden_Roof_A
FRAG_City_Window_A

MAT_Corridor_Base
MAT_Pillar_Emission
MAT_FogCard
MAT_LightShaft_Add
MAT_Osmanthus

VFX_Osmanthus_Trail
VFX_Pillar_Burst
VFX_Reconstruction
```

模型交付规范：

- Apply Rotation & Scale
- Scale 保持 1
- Pivot 放在适合拼接或旋转的位置
- 不嵌入重复纹理
- 保留源文件
- FBX 只导出需要的对象
- 导入 Unity 后建立 Prefab
- 不直接覆盖别人正在使用的主场景

---

## 14. 美术与程序接口

美术负责 Unity 内的：

- 模型与 Prefab
- 材质和 Shader Graph
- 粒子系统
- Animation Clips
- Timeline
- 场景构图
- 灯光、雾和颜色
- Scene 3 重组视觉

程序负责：

- Quest / XR 配置
- Teleport 和 Controller
- 柱体交互检测
- 三根柱子的完成状态
- 体验流程
- Build 和技术性能问题

建议事件接口：

```text
OnPillarHover(index)
OnPillarActivated(index)
OnAllPillarsActivated()
OnReconstructionStarted()
OnExperienceEnded()
```

美术可以独立调整视觉，程序不把粒子、颜色和动画时间写死在代码中。

---

## 15. 美术成员第一天立即开始的任务

1. 制作三格 Color Script：冷长廊、逐渐点亮、湖面重组
2. 画出 Scene 3 最终构图
3. 建立模块化长廊资产清单
4. 制作一个柱子和一段长廊
5. 导入 Unity 并检查真实 VR 尺度

项目的视觉优先级始终是：

1. Scene 3 最终构图与重组
2. 桂花粒子的运动和光感
3. 三根柱子的触发反馈
4. 整体冷暖与雾
5. 长廊结构
6. 彩画与建筑细节

> **We are not rebuilding the Old World as it was. We are showing how it is reconstructed through memory, distance, and the present.**

---

## References

- [Unity: Optimize for untethered XR devices in URP](https://docs.unity3d.com/ja/current/Manual/xr-untethered-device-optimization.html)
- [Unity Timeline](https://docs.unity3d.com/ja/current/Manual/com.unity.timeline.html)
- [Unity URP Shader Graph](https://docs.unity3d.com/cn/6000.0/Manual/urp/prebuilt-shader-graphs-urp-sixway.html)
- [Blender FBX Manual](https://docs.blender.org/manual/en/3.0/addons/import_export/scene_fbx.html)
