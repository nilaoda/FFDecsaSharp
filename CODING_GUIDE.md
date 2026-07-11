FFDecsaSharp 开发指南（Codex 工作规范）

本文档用于指导 FFDecsaSharp 的开发、代码生成与架构设计。

本项目的目标是使用现代 C# 与 .NET 10，对 FFdecsa 进行完整移植，而不是进行机械式源码翻译。

⸻

一、项目目标

FFDecsaSharp 是一个使用现代 C# 编写的 FFdecsa 实现。

项目目标：

* 完整兼容 FFdecsa 的算法行为
* 使用现代 .NET 10 特性重新实现
* 保持代码可维护、可阅读
* 支持 Windows / Linux / macOS
* 支持 NativeAOT
* 零 GC 压力（热路径）
* 后续可提供 Avalonia GUI

非目标：

* 不重新设计 DVB-CSA 算法
* 不增加 FFdecsa 原本不存在的新功能
* 不追求第一版即达到极限性能
* 不为了性能牺牲代码可维护性

开发原则：

Rewrite, not Translate.

即：

保持算法一致。

不要保持代码一致。

⸻

二、总体架构

项目采用 Monorepo。

FFDecsaSharp/
│
├── src/
│   ├── FFDecsaSharp/
│   ├── FFDecsaSharp.Gui/
│
├── tests/
│   └── FFDecsaSharp.Tests/
│
├── benchmarks/
│   └── FFDecsaSharp.Benchmarks/
│
├── docs/
│
├── README.md
├── LICENSE
└── FFDecsaSharp.sln

说明：

目前优先开发 Library。

GUI 项目提前建立，但可以为空，仅保留项目结构。

⸻

三、各项目职责

FFDecsaSharp

核心算法库。

包含：

* FFdecsa 算法
* CSA 数据结构
* TS Packet
* Control Word
* Key Schedule
* BitSlice
* SIMD
* CPU Dispatcher

禁止：

* GUI
* Avalonia
* Console UI

该项目必须可以独立发布 NuGet。

⸻

FFDecsaSharp.Gui

未来 Avalonia GUI。

目前仅预留。

不得引用测试项目。

GUI 只能调用 FFDecsaSharp Library。

不得复制算法代码。

⸻

FFDecsaSharp.Tests

单元测试。

包括：

* Known Answer Test
* Compatibility Test
* Regression Test
* Random Test

所有算法模块必须具有测试。

⸻

FFDecsaSharp.Benchmarks

BenchmarkDotNet。

仅用于性能测试。

包括：

* Scalar
* SIMD
* 各阶段优化前后对比

不得影响正式代码。

⸻

四、Library 内部结构

FFDecsaSharp
│
├── CSA/
│
├── BitSlice/
│
├── SIMD/
│
├── TransportStream/
│
├── Internal/
│
└── Utils/

⸻

CSA

负责：

* Control Word
* Key
* Decryptor
* Encryptor（如未来需要）

这里是整个算法入口。

⸻

BitSlice

负责：

BitSlice 数据转换。

包括：

* BitSliceBlock
* BitSliceTransform
* 数据布局

所有位操作均应集中于此。

⸻

SIMD

负责：

不同 SIMD 后端。

建议包括：

Scalar
Vector<T>
AVX2
AdvSimd

Dispatcher 负责自动选择。

算法层不得直接判断 CPU。

⸻

TransportStream

负责：

TS Packet。

例如：

* Packet Header
* Scrambling Control
* Payload Offset

不得包含 CSA 算法。

⸻

Internal

仅内部工具。

例如：

* UnsafeHelper
* CpuDetector
* MemoryHelper

不得公开 API。

⸻

Utils

通用工具。

仅放置真正通用的代码。

避免成为杂项目录。

⸻

五、开发阶段

建议按阶段开发。

⸻

第一阶段

完成：

* TS Packet
* Control Word
* 基础数据结构

要求：

所有类型设计完成。

暂不考虑 SIMD。

⸻

第二阶段

实现：

BitSlice。

要求：

保证算法正确。

先实现 Scalar。

不要考虑性能。

⸻

第三阶段

实现：

CSA 解密。

要求：

能够正确解密。

所有测试通过。

⸻

第四阶段

优化。

包括：

* Span
* MemoryMarshal
* Unsafe
* ref
* stackalloc

此阶段仍保持 Scalar。

⸻

第五阶段

加入 SIMD。

顺序：

Vector

AVX2

AdvSimd

Scalar 永远保留。

⸻

第六阶段

Benchmark。

分析热点。

仅优化真正热点。

禁止凭感觉优化。

⸻

六、Codex 工作规范

Codex 必须遵守以下原则。

原则一

不要逐行翻译 C。

必须重新设计。

例如：

不要翻译宏。

不要翻译 goto。

不要保留 C 文件组织。

⸻

原则二

优先现代 C#。

优先使用：

* Span
* ReadOnlySpan
* ref
* readonly struct
* MemoryMarshal
* Unsafe
* stackalloc

不要大量使用：

byte[]
List<T>
Dictionary
LINQ

在热路径中禁止：

* LINQ
* foreach
* boxing
* object

⸻

原则三

零分配。

所有热路径：

不得产生 GC。

所有缓冲区：

优先：

Span

stackalloc

ArrayPool（必要时）

⸻

原则四

保持数据连续。

尽量：

Structure of Arrays

避免：

Array of Objects

避免：

大量 class。

优先：

readonly struct

ref struct

⸻

原则五

公共 API 保持简洁。

尽量：

Decrypt()
DecryptPacket()
DecryptPackets()

不要暴露内部实现。

⸻

原则六

所有性能优化必须保持可读。

不要为了节省几条指令：

生成数千行重复代码。

不要过度模板化。

⸻

七、SIMD 规范

第一版：

禁止编写 SIMD。

第二版：

实现：

Vector

第三版：

实现：

AVX2

第四版：

实现：

AdvSimd

Dispatcher 自动选择。

不得由算法层判断平台。

⸻

八、测试规范

每完成一个模块：

必须：

* Build 成功
* 编写测试
* 测试通过

之后才能继续。

禁止：

最后统一补测试。

⸻

九、性能规范

优化顺序：

正确性

↓

可维护性

↓

性能

禁止：

未经 Benchmark 即进行优化。

所有优化必须：

能够证明收益。

⸻

十、代码风格

遵循：

Microsoft C# Coding Style。

命名：

PascalCase

私有字段：

_field

局部变量：

camelCase

禁止：

匈牙利命名法。

⸻

十一、提交规范

建议每完成一个逻辑模块进行一次提交。

例如：

feat: implement transport packet parser
feat: implement control word
feat: implement bitslice transform
feat: implement scalar decryptor
test: add known answer tests
perf: optimize bitslice memory layout
perf: add Vector<T> backend
perf: add AVX2 backend
perf: add ARM AdvSimd backend

保持每次提交都可以正常编译，并且测试全部通过。

⸻

十二、GUI 规划（暂不开发）

未来 GUI 使用：

* Avalonia
* MVVM
* CommunityToolkit.Mvvm

建议结构：

FFDecsaSharp.Gui
Views/
ViewModels/
Models/
Services/

GUI 仅负责：

* 文件选择
* 参数输入
* 日志输出
* 进度显示
* Benchmark 展示（可选）

所有解密逻辑必须调用 FFDecsaSharp Library。

GUI 不允许包含任何算法实现。

⸻

十三、最终目标

完成一个具有以下特点的 FFdecsa C# 移植版本：

* 与 FFdecsa 行为一致
* 使用现代 C# 重写
* 支持 NativeAOT
* 支持 Windows、Linux、macOS
* 支持 x64 与 ARM64
* 保持高性能
* 保持代码可维护
* 为未来 Avalonia GUI 提供稳定的核心算法库