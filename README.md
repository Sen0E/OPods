# OPods

[English](#english) | [中文](#中文)

***

## English

A standalone Windows desktop application for controlling OPPO / OnePlus / HeyMelody earphones over Bluetooth Classic RFCOMM. Built with .NET 10 and Windows Forms, using [32feet.NET](https://github.com/inthehand/32feet) for the Bluetooth stack.

This project is the Windows port of the **standalone mode** from [OppoPods](https://github.com/Art-Chen/HyperPods) (a HyperOS Xposed module). It does **not** require any Xposed framework, system hooks, or HyperOS integration — it talks to the earphones directly.

### Features

- **Device Scanning** — Discover nearby Bluetooth devices and pick the one to connect to
- **Battery Display** — Real-time battery level for left ear, right ear, and charging case, with charging indicator
- **ANC Control** — Switch between Off / Smart / Light / Medium / Deep / Adaptive / Transparency (mode set depends on the detected device profile)
- **Game Mode** — Low-latency audio toggle with two implementation strategies (Standard / Compatible)
- **Auto Polling** — 30-second battery and status polling; graceful fallback on disconnect
- **Per-Model Profiles** — A `DeviceProfile` abstraction drives protocol values, supported modes, and UI generation per earphone model
- **Preferences Persistence** — Last device, connection method, and game-mode implementation are saved to `%APPDATA%\OPods\preferences.json`
- **Single-File Build** — Publishes as a single `OPods.exe` (framework-dependent)

### Requirements

- **OS**: Windows 10 / 11 (WinForms requires Windows)
- **Runtime**: .NET 10 Desktop Runtime
- **Hardware**: Bluetooth Classic (BLE-only adapters are not supported; RFCOMM/SPP is required)
- **Earphones**: OPPO / OnePlus / HeyMelody earphones that speak the HeyMelody RFCOMM protocol (e.g. OPPO Enco Free3 / Free4)

### Getting Started

#### Option 1: Download the prebuilt binary

1. Go to the [Releases page](../../releases) and download `OPods.exe` from the latest release
2. Make sure the .NET 10 Desktop Runtime is installed on your machine
3. Run `OPods.exe`

#### Option 2: Build from source

```bash
# Restore dependencies
dotnet restore

# Debug build
dotnet build

# Release single-file publish (framework-dependent)
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish
```

The published executable will be at `./publish/OPods.exe`.

> To produce a fully self-contained binary (no runtime install needed), replace `--self-contained false` with `--self-contained true`. The output will be larger (\~150 MB+).

### Usage

1. Launch `OPods.exe`
2. Click **更换设备** (Change Device) to open the device picker
3. Click **扫描** (Scan) to discover nearby Bluetooth devices
4. Select your OPPO earphones from the list — the app will auto-detect the model and select the recommended connection method
5. Click **连接** (Connect); on first connection Windows may prompt to pair the device
6. Once connected, the main window displays:
   - Device name and connection status
   - Battery levels for left ear / right ear / charging case
   - ANC mode buttons (dynamically generated from the device profile)
   - Game mode toggle and implementation selector
7. Use **刷新状态** (Refresh) to manually re-query the earphone state

### Architecture

The project is layered to keep the protocol logic platform-independent.

```
OPods/
├── Program.cs                      # Entry point
├── Preferences.cs                  # JSON-backed preferences (%APPDATA%/OPods)
├── OPods.csproj
├── Pods/                           # Protocol layer (pure C#, no platform deps)
│   ├── OppoPackets.cs              # Packet builder + OppoPacketFramer
│   ├── OppoCommands.cs             # Command / battery / feature constants
│   ├── OppoEnums.cs                # Pre-built query & game-mode packets
│   ├── BatteryParser.cs            # Battery response parser
│   ├── AncModeParser.cs            # ANC mode parser (data-driven via profile)
│   ├── GameModeParser.cs           # Game mode state parser
│   ├── SwitchFeatureSetParser.cs   # Feature-switch response parser
│   ├── NoiseControlMode.cs         # ANC mode enum (superset across models)
│   ├── GameModeImplementation.cs   # Standard / Compatible enum
│   ├── RfcommConnectionMethod.cs   # Uuid / Channel enum
│   ├── DeviceProfile.cs            # Per-model configuration abstraction
│   ├── DeviceProfileRegistry.cs    # Name → profile resolver
│   └── Profiles/
│       ├── EncoFree4Profile.cs     # OPPO Enco Free4 (4-level ANC + EQ + spatial)
│       ├── EncoFree3Profile.cs     # OPPO Enco Free3 (4-level ANC + EQ + spatial)
│       └── GenericOppoProfile.cs   # Fallback profile (single-level ANC)
├── Bluetooth/
│   └── OppoRfcommClient.cs         # 32feet.NET RFCOMM client + framer
├── Controllers/
│   ├── PodController.cs            # Connection state machine, polling, dispatch
│   └── BatteryParams.cs            # Battery state DTO
└── UI/
    ├── MainForm.cs / .Designer.cs  # Main window (battery / ANC / game mode)
    └── DevicePickerForm.cs / .Designer.cs  # Scan & pick device
```

| Layer          | Responsibility                                                                                                              |
| -------------- | --------------------------------------------------------------------------------------------------------------------------- |
| `Pods/`        | Pure protocol: packet framing, command constants, response parsers, per-model profiles. No Windows or Bluetooth dependency. |
| `Bluetooth/`   | 32feet.NET wrapper: RFCOMM connect, send, framed read loop.                                                                 |
| `Controllers/` | State machine, 30s polling timer, packet dispatch, command execution.                                                       |
| `UI/`          | Windows Forms: device picker, main dashboard, dynamic ANC buttons.                                                          |

### Device Profiles

All model-specific data is centralized in `DeviceProfile`. The registry resolves the correct profile by matching the Bluetooth device name (case-insensitive substring match); unknown devices fall back to `GenericOppoProfile`.

| Profile              | Model            | ANC Modes                                                     | Spatial Audio | EQ  |
| -------------------- | ---------------- | ------------------------------------------------------------- | ------------- | --- |
| `EncoFree4Profile`   | OPPO Enco Free4  | Off / Smart / Light / Medium / Deep / Adaptive / Transparency | Yes           | Yes |
| `EncoFree3Profile`   | OPPO Enco Free3  | Off / Smart / Light / Medium / Deep / Transparency            | Yes           | Yes |
| `GenericOppoProfile` | Generic fallback | Off / Noise Cancellation / Adaptive / Transparency            | No            | No  |

To add support for a new model, create a class deriving from `DeviceProfile` and register it in `DeviceProfileRegistry._profiles`.

### Protocol

Communication uses Bluetooth Classic **RFCOMM**. The connection method is selectable in the UI: `UUID` mode tries the HeyMelody SPP UUIDs `00001107-D102-11E1-9B23-00025B00A5A5` and `0000079A-D102-11E1-9B23-00025B00A5A5` via SDP; `Channel` mode connects directly to RFCOMM channel 15.

Packet format (multi-byte fields are little-endian):

```
AA [TotalLen:1] 00 00 [Cmd:2 LE] [Seq:1] [PayLen:2 LE] [Payload...]
```

- `TotalLen = 7 + PayLen` (7 = Res(2) + Cmd(2) + Seq(1) + PayLen(2))
- Full frame length = `2 + TotalLen` (2 = Header(1) + TotalLen(1))

| Function              | Cmd      | Payload                                                                                                                     |
| --------------------- | -------- | --------------------------------------------------------------------------------------------------------------------------- |
| Set ANC               | `0x0404` | `01 01 <mode bytes from profile>`                                                                                           |
| Set Game Mode         | `0x0403` | `28 01` = on, `28 00` = off (main); `06 01` / `06 00` (low-latency)                                                         |
| Query Battery         | `0x0106` | (empty)                                                                                                                     |
| Battery Response      | `0x8106` | Pairs of `[Index, RawValue]` — battery = `val & 0x7F`, charging = `(val & 0x80) != 0`. Index: 1 = left, 2 = right, 3 = case |
| Active Battery Report | `0x0204` | `01 <count> [Index, StatusValue]...` — unsolicited, same value encoding                                                     |
| Query ANC             | `0x010C` | `01 01`                                                                                                                     |
| ANC Response          | `0x810C` | Scan for `01 01 [v1] [v2]`; `(v1,v2)` → mode lookup via `profile.AncResponseMap`                                            |
| Batch Status Query    | `0x010D` | Fixed blob (see below), wakes earbuds                                                                                       |
| Batch Status Response | `0x810D` | Key-value stream; byte `0x28` followed by `01`/`00` indicates game-mode state                                               |

**Batch Status Query (fixed hex):**

```
AA 13 00 00 0D 01 00 0C 00 0B 05 04 0B 11 13 18 06 1B 1C 27 28
```

### CI/CD

A GitHub Actions workflow (`.github/workflows/build.yml`) automates builds:

- **Triggers**: push to `main`/`master`, pull requests, `v*` tags, and manual dispatch
- **Runner**: `windows-latest` (WinForms requires Windows SDK)
- **Output**: single-file `OPods.exe` uploaded as a build artifact
- **Release**: pushing a `v*` tag automatically creates a GitHub Release with `OPods.exe` attached

### Troubleshooting

- **Cannot discover devices**: Make sure Bluetooth is enabled and the earphones are in pairing mode. The earphones must already be paired in Windows settings before connecting via RFCOMM in some cases.
- **Connection fails via UUID**: Try switching the connection method to **Channel** in the device picker.
- **SmartScreen warning on first launch**: Normal for unsigned Windows executables; click **More info → Run anyway**.
- **State shows "未连接" but earphones are paired**: RFCOMM is a separate channel from the audio connection. Pairing alone does not guarantee the SPP/UUID channel is available — retry connecting from the app.

### Credits

- [OppoPods](https://github.com/Art-Chen/HyperPods) by Art\_Chen — original Kotlin HyperOS Xposed module that this project ports the standalone mode from
- [32feet.NET](https://github.com/inthehand/32feet) — Bluetooth library for .NET

***

## 中文

一个独立运行的 Windows 桌面程序，通过经典蓝牙 RFCOMM 控制 OPPO / 一加 / 欢律耳机。基于 .NET 10 与 Windows Forms 构建，蓝牙层使用 [32feet.NET](https://github.com/inthehand/32feet)。

本项目是 [OppoPods](https://github.com/Art-Chen/HyperPods)（一个 HyperOS Xposed 模块）**独立模式**的 Windows 移植版。**不依赖**任何 Xposed 框架、系统 Hook 或 HyperOS 集成，直接与耳机通信。

### 功能

- **设备扫描** — 扫描附近蓝牙设备并选择连接目标
- **电量显示** — 实时显示左耳、右耳、充电盒电量及充电状态
- **降噪控制** — 在 关闭 / 智能 / 轻度 / 中度 / 深度 / 自适应 / 通透 之间切换（具体模式集合取决于识别到的机型）
- **游戏模式** — 低延迟音频开关，支持两种实现方式（标准 / 兼容）
- **自动轮询** — 30 秒电量与状态轮询，断线后状态自动回退
- **机型配置抽象** — 通过 `DeviceProfile` 把协议参数、支持模式、UI 按钮按机型收拢，运行时按蓝牙名自动解析
- **配置持久化** — 最近设备、连接方式、游戏模式实现方式保存到 `%APPDATA%\OPods\preferences.json`
- **单文件构建** — 发布为单个 `OPods.exe`（依赖框架）

### 系统要求

- **操作系统**：Windows 10 / 11（WinForms 仅支持 Windows）
- **运行时**：.NET 10 桌面运行时
- **硬件**：支持经典蓝牙的适配器（仅支持 BLE 的适配器不可用，需要 RFCOMM/SPP）
- **耳机**：支持欢律 RFCOMM 协议的 OPPO / 一加 / 欢律耳机（如 OPPO Enco Free3 / Free4）

### 快速开始

#### 方式一：下载预编译版本

1. 前往 [Releases 页面](../../releases) 下载最新版的 `OPods.exe`
2. 确认本机已安装 .NET 10 桌面运行时
3. 运行 `OPods.exe`

#### 方式二：从源码构建

```bash
# 还原依赖
dotnet restore

# 调试构建
dotnet build

# 发布单文件版本（依赖框架）
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish
```

构建产物位于 `./publish/OPods.exe`。

> 如需完全免安装运行时的自包含版本，将 `--self-contained false` 改为 `--self-contained true`，产物会增大到约 150 MB+。

### 使用说明

1. 启动 `OPods.exe`
2. 点击 **更换设备** 打开设备选择窗口
3. 点击 **扫描** 搜索附近蓝牙设备
4. 在列表中选中你的 OPPO 耳机 —— 程序会自动识别机型并选择推荐的连接方式
5. 点击 **连接**；首次连接时 Windows 可能弹出配对请求
6. 连接成功后，主界面会显示：
   - 设备名与连接状态
   - 左耳 / 右耳 / 充电盒电量
   - 降噪模式按钮（根据机型动态生成）
   - 游戏模式开关与实现方式选择
7. 点击 **刷新状态** 可手动重新查询耳机状态

### 架构

项目按层划分，协议层保持平台无关。

```
OPods/
├── Program.cs                      # 入口
├── Preferences.cs                  # JSON 配置持久化（%APPDATA%/OPods）
├── OPods.csproj
├── Pods/                           # 协议层（纯 C#，无平台依赖）
│   ├── OppoPackets.cs              # 组包 + 帧拆分器 OppoPacketFramer
│   ├── OppoCommands.cs             # 命令 / 电量 / 功能常量
│   ├── OppoEnums.cs                # 预构查询与游戏模式包
│   ├── BatteryParser.cs            # 电量响应解析
│   ├── AncModeParser.cs            # ANC 模式解析（数据驱动，查 profile）
│   ├── GameModeParser.cs           # 游戏模式状态解析
│   ├── SwitchFeatureSetParser.cs   # 功能开关响应解析
│   ├── NoiseControlMode.cs         # ANC 模式枚举（机型超集）
│   ├── GameModeImplementation.cs   # 标准 / 兼容 枚举
│   ├── RfcommConnectionMethod.cs   # UUID / 通道 枚举
│   ├── DeviceProfile.cs            # 机型配置抽象
│   ├── DeviceProfileRegistry.cs    # 蓝牙名 → profile 解析器
│   └── Profiles/
│       ├── EncoFree4Profile.cs     # OPPO Enco Free4（4 级降噪 + EQ + 空间音频）
│       ├── EncoFree3Profile.cs     # OPPO Enco Free3（4 级降噪 + EQ + 空间音频）
│       └── GenericOppoProfile.cs   # 通用兜底 profile（单级降噪）
├── Bluetooth/
│   └── OppoRfcommClient.cs         # 32feet.NET RFCOMM 客户端 + 帧拆分
├── Controllers/
│   ├── PodController.cs            # 连接状态机、轮询、包分发
│   └── BatteryParams.cs            # 电量状态数据结构
└── UI/
    ├── MainForm.cs / .Designer.cs  # 主界面（电量 / 降噪 / 游戏模式）
    └── DevicePickerForm.cs / .Designer.cs  # 扫描与选择设备
```

| 层级             | 职责                                                 |
| -------------- | -------------------------------------------------- |
| `Pods/`        | 纯协议层：组包 / 拆帧、命令常量、响应解析、机型 profile。无 Windows 与蓝牙依赖。 |
| `Bluetooth/`   | 32feet.NET 封装：RFCOMM 连接、发送、按帧读取。                   |
| `Controllers/` | 状态机、30 秒轮询定时器、包分发、命令执行。                            |
| `UI/`          | Windows Forms：设备选择、主面板、动态降噪按钮。                     |

### 机型配置

所有机型相关数据集中在 `DeviceProfile` 中。注册表按蓝牙设备名（不区分大小写的子串匹配）解析对应 profile，未匹配的设备使用 `GenericOppoProfile` 兜底。

| Profile              | 机型              | 降噪模式                              | 空间音频 | EQ |
| -------------------- | --------------- | --------------------------------- | ---- | -- |
| `EncoFree4Profile`   | OPPO Enco Free4 | 关闭 / 智能 / 轻度 / 中度 / 深度 / 自适应 / 通透 | 是    | 是  |
| `EncoFree3Profile`   | OPPO Enco Free3 | 关闭 / 智能 / 轻度 / 中度 / 深度 / 通透       | 是    | 是  |
| `GenericOppoProfile` | 通用兜底            | 关闭 / 降噪 / 自适应 / 通透                | 否    | 否  |

新增机型支持：继承 `DeviceProfile` 实现一个新类，并在 `DeviceProfileRegistry._profiles` 中注册即可。

### 协议

通信使用经典蓝牙 **RFCOMM**。连接方式可在界面中选择：`UUID` 模式通过 SDP 尝试欢律 SPP UUID `00001107-D102-11E1-9B23-00025B00A5A5` 和 `0000079A-D102-11E1-9B23-00025B00A5A5`；`通道` 模式直接连接 RFCOMM 通道 15。

数据包格式（多字节字段为小端序）：

```
AA [总长度:1] 00 00 [命令:2 LE] [序列号:1] [载荷长度:2 LE] [载荷...]
```

- `总长度 = 7 + 载荷长度`（7 = 保留(2) + 命令(2) + 序列号(1) + 载荷长度(2)）
- 整帧长度 = `2 + 总长度`（2 = 帧头(1) + 总长度(1)）

| 功能     | 命令       | 载荷                                                                           |
| ------ | -------- | ---------------------------------------------------------------------------- |
| 设置降噪   | `0x0404` | `01 01 <来自 profile 的模式字节>`                                                   |
| 设置游戏模式 | `0x0403` | `28 01`=开, `28 00`=关（主开关）；`06 01` / `06 00`（低延迟）                             |
| 查询电量   | `0x0106` | （空）                                                                          |
| 电量响应   | `0x8106` | `[索引, 原始值]` 对 — 电量=`val & 0x7F`，充电中=`(val & 0x80) != 0`。索引：1=左耳, 2=右耳, 3=充电盒 |
| 电量主动上报 | `0x0204` | `01 <数量> [索引, 状态值]...` — 耳机主动推送，编码同上                                         |
| 查询降噪   | `0x010C` | `01 01`                                                                      |
| 降噪响应   | `0x810C` | 扫描 `01 01 [v1] [v2]`；`(v1,v2)` → 模式 通过 `profile.AncResponseMap` 查表           |
| 批量状态查询 | `0x010D` | 固定数据包（见下），自带唤醒权重                                                             |
| 批量状态响应 | `0x810D` | 键值流；字节 `0x28` 后跟 `01`/`00` 表示游戏模式状态                                          |

**批量状态查询（固定数据）：**

```
AA 13 00 00 0D 01 00 0C 00 0B 05 04 0B 11 13 18 06 1B 1C 27 28
```

### CI/CD

GitHub Actions 工作流（`.github/workflows/build.yml`）自动完成构建：

- **触发条件**：push 到 `main`/`master`、Pull Request、`v*` 标签、手动触发
- **运行环境**：`windows-latest`（WinForms 必须在 Windows 上编译）
- **产物**：单文件 `OPods.exe`，作为构建 artifact 上传
- **发布**：推送 `v*` 标签会自动创建 GitHub Release 并附带 `OPods.exe`

### 常见问题

- **扫描不到设备**：确认蓝牙已开启，耳机处于配对模式。部分情况下需先在 Windows 系统设置中配对耳机，再通过 RFCOMM 连接。
- **UUID 模式连接失败**：在设备选择窗口将连接方式切换为 **通道** 重试。
- **首次启动出现 SmartScreen 警告**：未签名 Windows 程序的正常现象，点击 **更多信息 → 仍要运行** 即可。
- **耳机已配对但状态显示「未连接」**：RFCOMM 与音频连接是独立通道，配对不等于 SPP/UUID 通道可用 —— 请在程序内重新连接。

### 致谢

- [OppoPods](https://github.com/Art-Chen/HyperPods) by Art\_Chen — 原始 Kotlin HyperOS Xposed 模块，本项目移植其独立模式
- [32feet.NET](https://github.com/inthehand/32feet) — .NET 蓝牙库

