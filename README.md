# Unity Core Lab

Practical Unity samples covering asynchronous workflows, state machines, asset lifecycle, performance optimization, and runtime diagnostics.

This repository is a personal Unity engineering portfolio for technical demonstration and discussion. It focuses on reusable engineering concepts rather than production-ready frameworks.

## Environment

- Unity 2022.3.62f1
- Universal Render Pipeline 14.0.12
- Unity Test Framework 1.1.33
- UniTask 2.5.11

The Unity project is located in the `Project` directory.

## Labs

### 01 Async Operation Lifecycle

Demonstrates cancellation, timeout classification, exception normalization, observable lifecycle state, generation-based protection against stale asynchronous completions, Task/UniTask lifecycle differences, and safe worker-thread-to-main-thread handoff.

- Runtime code: `Project/Assets/Runtime/AsyncOperations`
- Interactive sample: `Project/Assets/Samples/AsyncOperations`
- EditMode tests: `Project/Assets/Tests/EditMode`
- PlayMode tests: `Project/Assets/Tests/PlayMode`
- Design note: `docs/01-async-operation-lifecycle.md`

## Roadmap

- Asset handle lifecycle and ownership
- Object pooling and allocation analysis
- Rendering performance comparisons
- Runtime diagnostics and fault injection

Copyright © 2026 Jiangbo Li. All rights reserved.
