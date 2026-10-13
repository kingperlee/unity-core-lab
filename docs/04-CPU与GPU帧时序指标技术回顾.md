# CPU 与 GPU 帧时序指标技术回顾

本文说明 `CPU Frame`、`CPU Main Thread`、`CPU Render Thread` 和
`GPU Frame` 四项指标的计时含义及优化用途。核心结论是：`CPU Frame`
描述 CPU 侧帧节奏，其余三项提供瓶颈定位线索；四项处在并行流水线上，
既不是父子包含关系，也不能相加得到总帧时间。

## 四项指标

| 指标 | 计时含义 | 主要用途 |
| --- | --- | --- |
| CPU Frame | 相邻两个 CPU 帧边界之间的墙钟时间，包含工作、等待和帧间开销 | 判断最终 CPU 帧节奏是否达到目标 |
| CPU Main Thread | 主线程从帧开始到完成该帧主要工作的时间 | 定位脚本、物理、动画、UI、GC、对象管理和同步等待 |
| CPU Render Thread | Render Thread 开始处理渲染工作到 Unity 调用 `Present()` 的时间 | 定位 Draw Call、SetPass、状态切换和驱动提交压力 |
| GPU Frame | GPU 从开始到结束处理一个渲染帧的时间 | 定位顶点、片元、Overdraw、后处理和带宽压力 |

`CPU Total Frame Time` 中的 `Total` 表示完整 CPU 帧周期包含工作与等待，
并不表示所有 CPU 线程耗时之和。

## CPU Frame 的边界

`CPU Frame` 可近似理解为 Main Thread 相邻两次进入帧循环之间的时间：

```text
CPU Frame N ≈ StartMainFrame(N + 1) - StartMainFrame(N)
```

Unity 也将它定义为相邻两个 CPU 帧结束点之间的时间。两种表述描述的是
同一个连续帧周期。这个边界不要求 Render Thread 已完成当前帧提交，也不
要求 GPU 已完成当前帧渲染。

```text
时间 ─────────────────────────────────────────────>

Main Thread   [准备 N 帧    ][准备 N+1 帧  ][准备 N+2 帧]
Render Thread       [提交 N 帧    ][提交 N+1 帧    ]
GPU                       [执行 N 帧     ][执行 N+1 帧]
```

Main Thread 把渲染工作交给 Render Thread 后，通常可以继续下一帧。
Render Thread 和 GPU 可以跨过该 CPU 帧边界继续工作。只有命令队列积压、
资源依赖、`Present`、VSync 或其他同步点阻止 Main Thread 推进时，等待时间
才会延后下一个 CPU 帧边界并计入 `CPU Frame`。

因此下面的结果是合理的：

```text
CPU Frame:         10 ms
CPU Main Thread:    6 ms
CPU Render Thread:  7 ms
GPU Frame:         14 ms
```

GPU 工作可以跨帧，CPU 也可以提前准备后续帧，所以 `GPU Frame` 可以大于
当前的 `CPU Frame`。当这种差距持续存在，队列最终产生反压，CPU 才会在
同步点等待 GPU。

## 时间窗口不是完成屏障

其他线程的工作可能与一个 CPU Frame 时间窗口重叠，但这不代表 CPU Frame
必须等它们全部结束：

```text
CPU Frame 时间窗口
├─ Main Thread 自身工作
├─ Main Thread 实际发生的等待
└─ 同期可能并行运行
   ├─ Render Thread，可能跨越帧边界
   ├─ Job Worker，可能跨越帧边界
   ├─ 加载、音频等其他线程
   └─ GPU，可能跨越多个 CPU 帧
```

只有位于当前帧关键依赖路径上的任务必须完成。例如 Main Thread 调用
`JobHandle.Complete()` 读取 Job 结果时必须等待；无需立刻消费结果的后台
任务则可以跨帧继续执行。

## 四项指标如何指导优化

### CPU Frame 偏高

先判断它是计算时间还是等待时间，不要直接得出“脚本太慢”的结论：

- 接近固定的 `16.67 ms` 或 `33.33 ms` 时，先检查 VSync 和限帧；
- 结合 Profiler 检查 `WaitForTargetFPS`、Present 和线程同步等待；
- 再比较 Main Thread、Render Thread 和 GPU 的稳定分布。

如果另外三项都明显较低，CPU Frame 偏高通常只是帧节奏受到显示或限帧
控制，不是需要消除的性能消耗。

### CPU Main Thread 偏高

重点检查：

- `Update`、`LateUpdate` 和逐对象循环；
- 物理、动画、UI Layout 和 Canvas rebuild；
- `Instantiate/Destroy`、GC 和每帧分配；
- 同步资源加载以及等待 Job 结果；
- LOD、剔除或其他管理策略本身的 CPU 成本。

可考虑缓存、分帧、事件驱动、减少对象数量、降低分配，以及在适合的数据
处理任务上使用 Job System 和 Burst。

### CPU Render Thread 偏高

重点检查 Draw Call、Batch、SetPass、材质和 Shader 状态切换、阴影 Pass、
独立 Renderer 数量以及底层图形 API 提交成本。可结合 SRP Batcher、GPU
Instancing、合批、共享材质和提前剔除降低提交压力。

Draw Call 下降首先改善的是 CPU 渲染提交成本，不保证 GPU 时间同步下降。

### GPU Frame 偏高

先判断主要压力属于哪一类：

- 顶点压力：高面数、蒙皮、阴影重复绘制，考虑 LOD 和网格简化；
- 片元压力：高分辨率、Overdraw、透明叠加、复杂 Fragment Shader；
- 带宽压力：高精度 Render Target、多个全屏 Pass、频繁读写纹理；
- 计算压力：Compute Shader、后处理或其他 GPU 任务。

`Distance LOD` 只降低三角形。如果场景并非顶点瓶颈，GPU Frame 可能几乎
不变。显式剔除会直接移除整次绘制及其顶点、光栅化、片元和 Render Target
工作，因此更可能产生明显差异。

## 快速判断流程

```text
CPU Frame 高
    ├─ 其余指标低且接近固定刷新周期
    │      → 检查 VSync、限帧和 Present 等待
    ├─ CPU Main Thread 高
    │      → 检查游戏逻辑和主线程依赖
    ├─ CPU Render Thread 高
    │      → 检查渲染提交和状态切换
    └─ GPU Frame 高
           → 检查顶点、片元、Overdraw 和带宽
```

该流程只能确定调查方向，不能代替 Profiler Timeline、Rendering、GPU
Usage、Frame Debugger 或平台 GPU 工具。稳态吞吐通常由最慢阶段或显示节奏
限制，而不是四项数值之和。

## 采样与比较

04.4 使用固定预热和采样窗口，报告 Median 与 P95：

- Median 表示典型帧；
- P95 表示 95% 的样本不超过该耗时，用于观察较慢的长尾帧；
- 比较时必须保持分辨率、相机、对象数量、质量、VSync 和工作负载一致；
- 优先在目标设备的 Development Build 中重复采样；
- GPU Timing 不可用时应明确标记，不应使用 FPS 或 CPU 时间代替。

## 参考资料

- [Unity 2022.3 FrameTiming](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/FrameTiming.html)
- [Unity 2022.3 Frame Timing Manager](https://docs.unity3d.com/2022.3/Documentation/Manual/frame-timing-manager.html)
