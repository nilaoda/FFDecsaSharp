# API Design
## 设计目标
FFDecsaSharp 应当首先是一个 Library。
GUI、CLI 都应建立在 Library 之上。
Library 不应感知任何 UI。
---
# Namespace
FFDecsaSharp
所有公开 API 均位于此命名空间或其子命名空间。
例如：
FFDecsaSharp
FFDecsaSharp.TransportStream
FFDecsaSharp.CSA
禁止：
FFDecsaSharp.Internal
出现在公共 API 中。
---
# Public API
第一版尽量少。
例如：
ControlWord
TransportPacket
Decryptor
CpuFeatures
不要一开始暴露几十个类型。
---
# API 风格
优先：
```csharp
decryptor.Decrypt(packet);

而不是：

CSAUtility.DoDecrypt(...)

避免大量静态工具类。

⸻

Span 优先

所有数据输入：

优先：

ReadOnlySpan

所有输出：

优先：

Span

避免：

byte[]

⸻

不抛异常作为流程控制

例如：

错误 Control Word

返回：

false

或者：

Result

不要：

Exception

热路径禁止异常。

⸻

NativeAOT

禁止：

Reflection

Dynamic

Emit

Expression Compile

Assembly.Load

⸻

异步

第一版：

不提供 async API。

因为：

CSA 属于 CPU 密集型。

由调用方决定是否放入 Task。

⸻

IDisposable

除非真正持有资源。

否则：

不要实现 IDisposable。

⸻

Allocation

热路径：

0 Allocation。

⸻

Thread Safety

Decryptor 实例：

应尽量做到：

无共享状态。

方便：

Parallel.For

Task

线程池。

⸻

XML Documentation

所有 Public API：

必须具有 XML 注释。

Internal API：

无需 XML。

---
# MIGRATION_PLAN.md
这一份我觉得价值最高。
千万不要一句：
> Port FFdecsa.
而是：
**拆任务。**
---
```markdown
# Migration Plan
目标：
逐步迁移 FFdecsa。
禁止一次完成全部迁移。
---
## Phase 1
建立项目。
完成：
基础目录。
CI。
Build。
---
## Phase 2
迁移：
Transport Packet。
包括：
188 Byte
Header
PID
Scrambling Control
Payload Offset
完成测试。
---
## Phase 3
迁移：
Control Word。
包括：
Even CW
Odd CW
Key 数据结构。
完成测试。
---
## Phase 4
实现：
BitSlice 数据结构。
不要优化。
只保证正确。
---
## Phase 5
实现：
BitSlice Transform。
建立 Differential Test。
与 FFdecsa 对比。
---
## Phase 6
实现：
Key Schedule。
测试。
---
## Phase 7
实现：
CSA Core。
保持 Scalar。
完成全部测试。
---
## Phase 8
性能优化。
包括：
Span
Unsafe
MemoryMarshal
AggressiveInlining
Benchmark。
---
## Phase 9
SIMD。
第一步：
Vector<T>
完成测试。
---
## Phase 10
AVX2。
Benchmark。
---
## Phase 11
AdvSimd。
Benchmark。
---
## Phase 12
最终整理。
包括：
XML
README
NuGet
GitHub Actions
NativeAOT 发布验证。