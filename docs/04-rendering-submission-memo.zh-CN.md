# Unity 渲染提交技术备忘录

本文用于梳理 `Draw Call`、`Batch`、`SetPass`、SRP Batcher 和 GPU Instancing
之间的关系。核心结论是：减少 Draw Call 只是优化渲染提交的一种方法；判断 CPU
渲染成本时，还必须结合状态切换、SRP Batcher 兼容性、主线程和渲染线程耗时。

## 1. 04.1 阶段要说明什么

04.1 关注的是 CPU 如何为 GPU 组织和提交绘制工作，而不是完整的 GPU 性能优化。
当前 Demo 对比四条路径：

1. 每个 Renderer 使用独立 Material。
2. 多个 Renderer 共享 Material，不提供逐物体覆盖数据。
3. 多个 Renderer 共享 Material，并通过 `MaterialPropertyBlock` 提供逐物体颜色。
4. 不创建逐物体 Renderer，改用 `Graphics.DrawMeshInstanced` 显式提交矩阵和颜色。

该阶段主要回答以下问题：

- Draw Call 和 Statistics 中的 Batches 分别表示什么。
- SetPass 为什么可能比 Draw Call 数量更能反映 CPU 状态设置成本。
- SRP Batcher 为什么可以在不减少 Draw Call 的情况下提升性能。
- MaterialPropertyBlock、SRP Batcher 与 GPU Instancing 为什么不是同一种优化。
- 为什么“Draw Call 更少”不等于“帧时间一定更短”。

## 2. 从 Renderer 到 GPU 的基本过程

Unity 首先在 CPU 侧为可见对象准备绘制任务。一个绘制任务通常需要包含：

- Mesh 和 SubMesh。
- Material、Shader Variant 和 Shader Pass。
- Transform 等逐物体数据。
- 颜色、纹理参数等材质数据。
- 渲染状态、排序条件和目标 RenderTexture。

渲染线程随后将这些信息编码为图形命令。概念上可以简化为：

```text
URP 剔除、排序并确定渲染 Pass
    ↓
为可见对象准备绘制任务
    ↓
绑定 Pipeline、Shader、Mesh 和常量缓冲区
    ↓
发出 Draw / DrawIndexed / DrawInstanced 命令
    ↓
写入图形 API 的命令缓冲区
    ↓
GPU 在稍后执行命令
```

调用图形 API 并不意味着 GPU 会在 CPU 调用的同一时刻立即完成绘制。CPU 通常先将
命令写入 Command Buffer，GPU 再异步消费这些命令。

## 3. Draw Call、Batches 和 SetPass

### Draw Call

Draw Call 是 Unity 在一帧中发出的绘制调用。它告诉 GPU 使用当前已经绑定的资源和
渲染状态绘制一组顶点、索引或实例。

Shader 不会主动创建 Draw Call。Vertex Shader 和 Fragment Shader 只是处理某次
Draw Call 提供的顶点、实例和像素数据。

### Statistics Batches

Game View Statistics 和 Profiler 中的 `Batches Count` 表示 Unity 在一帧内处理的
绘制批次数量。对常见的 MeshRenderer 绘制路径，可以先近似理解为：

```text
1 Statistics Batch ≈ 1 最终 Draw Call
```

因此当前 Demo 经常得到相同的计数：

```text
Profiler Batches:    2029
Profiler Draw Calls: 2029
```

这里的 Batch 是最终绘制统计中的批次，不应与后文的 `SRP Batch` 混为一谈。

### Saved by batching

`Saved by batching` 表示 Unity 通过合批减少了多少次原本可能产生的绘制。例如：

```text
实际 Batches + Saved by batching = 未合批时预计的绘制批次
1033 + 996 = 2029
33 + 1996 = 2029
```

这个等式可以帮助理解合批效果，但不能单独用于判断性能，因为它没有描述每个 Draw
Call 之前发生了多少状态设置工作。

### SetPass Call

SetPass 表示 Unity 为绘制对象切换 Shader Pass 或相关渲染状态的次数。一个简化例子：

```text
SetPass A
Draw 1
Draw 2
Draw 3

SetPass B
Draw 4
Draw 5
```

这里有 5 个 Draw Call，但只有 2 次 SetPass。多个 Draw Call 可以连续复用兼容的
Shader 状态，因此 Draw Call 和 SetPass 并不要求一一对应。

## 4. 为什么“Batch”有时小于 Draw Call

Unity 在不同系统中重复使用了 Batch 一词，必须先确认语境。

| 名称 | 表示什么 | 与 Draw Call 的常见关系 |
| --- | --- | --- |
| Statistics Batches | Profiler 统计的最终绘制批次 | 通常约等于最终 Draw Call |
| Dynamic、Static、Instancing Batch | 将多个原始对象绘制合并后的批次 | 一个合并批次通常生成一个 Draw Call |
| SRP Batch | 一段共享兼容 Shader 状态的 Bind 和 Draw 命令序列 | 一个 SRP Batch 可以包含多个 Draw Call |
| BatchRendererGroup Draw Command | BRG 的逻辑绘制命令 | 受平台限制时可能拆成多个底层 Draw Call |

因此，“Batch 小于或等于 Draw Call”通常说的是 SRP Batch 或更高层的逻辑批次，
而不是当前 Demo 中读取的 `Batches Count`。

## 5. SRP Batcher 做了什么

SRP Batcher 不以合并 Mesh 或减少 Draw Call 为主要目标。它将使用相同 Shader
Variant、且数据布局兼容的一段 `bind` 和 `draw` 命令组织为一个 SRP Batch，减少
Draw Call 之间重复的 CPU 准备和 GPU 状态设置。

兼容 Shader 会将数据组织到两个约定的常量缓冲区：

```hlsl
CBUFFER_START(UnityPerDraw)
    // Transform、光照探针等逐物体数据
CBUFFER_END

CBUFFER_START(UnityPerMaterial)
    // 颜色、纹理参数等材质数据
CBUFFER_END
```

SRP Batcher 路径具有以下特点：

- 材质数据可以持续保留在 GPU 内存中。
- 材质内容没有变化时，不必为每次 Draw 重新收集和上传全部材质参数。
- Unity 使用大型逐物体常量缓冲区管理 Transform 等对象数据。
- 相同 Shader Variant 的 Draw Call 可以连续提交，减少昂贵的状态切换。

可以将传统路径简化为：

```text
准备并上传材质 A → 绑定对象 A → Draw A
准备并上传材质 B → 绑定对象 B → Draw B
准备并上传材质 C → 绑定对象 C → Draw C
```

SRP Batcher 路径更接近：

```text
绑定共同的 Shader Variant
选择已经驻留在 GPU 的材质 A 数据 → 选择对象 A 数据 → Draw A
选择已经驻留在 GPU 的材质 B 数据 → 选择对象 B 数据 → Draw B
选择已经驻留在 GPU 的材质 C 数据 → 选择对象 C 数据 → Draw C
```

三个 Draw Call 仍然存在，但每次 Draw 前重复执行的工作减少了。SRP Batcher 主要
优化 CPU 准备和提交成本，并不会自动减少顶点、三角形、像素或 Overdraw。

## 6. MaterialPropertyBlock 为什么可能更慢

在 URP 中，Renderer 使用 `MaterialPropertyBlock` 后会失去 SRP Batcher 兼容性。
MPB 可以避免调用 `renderer.material` 时产生材质副本，也可以为 Renderer 提供独立
属性，但它并不保证是 URP 中成本最低的路径。

04.1最初三模式基线在1000实例时的一次Editor实测为：

| 模式 | Batches / Draw Calls | SetPass | CPU Main | Render Thread |
| --- | ---: | ---: | ---: | ---: |
| Unique Material Renderers | 2029 | 37 | 6.7 ms | 1.7 ms |
| Shared Material + MPB | 1033 | 1033 | 10.7 ms | 3.7 ms |
| Explicit GPU Instancing | 33 | 31 | 5.2 ms | 1.2 ms |

这组数字采集于增加“纯共享材质”对照和自定义实例颜色Shader之前，需要在补充完成后
重新测量。它们只描述当时的机器、Editor、URP配置和测试场景，不能作为跨平台性能
结论。但它们
清楚地说明：MPB 模式虽然减少了 Batches，却因为离开 SRP Batcher 路径而产生大量
SetPass，最终 CPU 帧时间反而更高。

因此不能只说“共享材质一定比独立材质快”，而应先确认对象实际走的是 SRP Batcher、
GPU Instancing、动态合批还是普通 SRP 路径。

## 7. 一次 C# API 调用不一定等于一次 Draw Call

下面的代码只代表一次高层 API 调用：

```csharp
Graphics.DrawMeshInstanced(mesh, 0, material, matrices);
```

最终可能产生多个 Draw Call，原因包括：

- URP 在 Depth、DepthNormal、Shadow、Forward 等多个 Pass 中绘制对象。
- Mesh 包含多个 SubMesh。
- 场景中存在多个相机。
- 实例数量超过单次 API 支持的上限。
- 平台图形 API 对单次绘制数据量存在更低限制。

当前实现按每批最多1023个矩阵和颜色切分显式实例，并通过支持
`UNITY_DEFINE_INSTANCED_PROP` 的独立Shader读取逐实例 `_BaseColor`。即使1000个实例
只调用一次 C# API，对象仍可能在 DepthNormal 和 Forward 等不同渲染阶段分别生成
实例化 Draw Call。

正确顺序是：

```text
一次高层绘制请求
    ↓
URP 根据相机和 Pass 安排渲染
    ↓
每个阶段形成相应绘制批次
    ↓
产生一个或多个 Draw Call
```

不是 Shader 在执行过程中再次发起 Draw Call。

## 8. 当前 2029 次绘制的解释

1000实例时，未合批模式出现约2029个 Batches。结合 Frame Debugger 中出现的
`DepthNormalPrepass`、`SSAO`、`DrawOpaqueObjects` 和 `FinalBlit`，当前最合理的解释是：

```text
约 1000 次 DepthNormal 几何提交
+ 约 1000 次 Opaque 几何提交
+ 约 29 次 URP、Blit 和 UI 等固定开销
≈ 2029
```

这是结合当前计数做出的场景分析。要完全确认，应在 Frame Debugger 中展开
`DepthNormalPrepass` 和 `DrawOpaqueObjects`，分别检查每个阶段实际包含的绘制事件。

右侧预览只显示少量方块时，通常是因为 Frame Debugger 停在某个中间 Event，只回放
到该事件为止，并不表示其余方块没有渲染。

## 9. 正确的性能验证方法

在 Editor 中进行机制验证时：

1. 最大化 Game View，避免其他可见视图影响全帧计数。
2. 切换模式后等待若干帧，让统计值稳定。
3. 使用 Frame Debugger 判断 Draw Call 属于哪个相机和 Pass。
4. 记录 Draw Calls、Batches、SetPass、Main Thread 和 Render Thread，而不是只看 FPS。
5. 分析帧时间时关闭 Frame Debugger，因为它本身会改变渲染执行方式。

准备形成性能结论时：

1. 使用 Development Build 和 Autoconnect Profiler。
2. 在目标设备而不是只在 Editor 中测试。
3. 预热场景并采集一段时间的中位数或百分位数。
4. 保证不同模式的画面、Pass、分辨率和实例数据尽可能等价。
5. 将 CPU 提交瓶颈和 GPU 顶点、像素、带宽瓶颈分开分析。

## 10. 可以直接记住的结论

- Statistics 中的 `Batches` 通常接近最终 Draw Call，但 `SRP Batch` 是另一层概念。
- 一个 SRP Batch 可以包含多个 Draw Call。
- SRP Batcher 通常不减少 Draw Call，而是减少它们之间的重复准备和状态设置。
- SetPass 少通常意味着状态切换较少，但仍需结合线程耗时判断实际收益。
- MPB 可以减少材质实例，却会让 URP Renderer 失去 SRP Batcher 兼容性。
- 一次 `DrawMeshInstanced` 调用可能因为多个 Pass、SubMesh或实例上限产生多个 Draw Call。
- Shader 执行 Draw Call 提供的工作，不会主动创建 Draw Call。
- Draw Call 少不一定更快；性能结论必须以目标设备上的帧时间为准。

## 参考资料

- [Unity Rendering Profiler](https://docs.unity3d.com/cn/current/Manual/ProfilerRendering.html)
- [Unity Rendering Statistics](https://docs.unity3d.com/cn/2021.2/Manual/RenderingStatistics.html)
- [Unity SRP Batcher](https://docs.unity3d.com/cn/current/Manual/SRPBatcher.html)
- [Unity SRP Batcher compatibility](https://docs.unity3d.com/cn/6000.0/Manual/SRPBatcher-Materials.html)
- [Unity Draw Call Batching](https://docs.unity3d.com/cn/2022.2/Manual/DrawCallBatching.html)
- [Unity BatchRendererGroup](https://docs.unity3d.com/cn/2022.1/Manual/batch-renderer-group-how.html)
