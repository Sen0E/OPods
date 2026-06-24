# OPods — Windows 端 OPPO 耳机控制程序实现计划

## 1. 项目目标

在 Windows 上实现一个独立运行的桌面程序，用于控制 OPPO / 一加 / 欢律等耳机，功能对标参考项目 `GI/OppoPods-master` 的**独立模式**（`AppRfcommController`），不涉及任何 Xposed / HyperOS 系统集成。

### 参考项目映射关系

| 参考项目（Kotlin） | 本项目（C#） | 说明 |
|----|----|----|
| `pods/Packets.kt` | `Pods/*.cs` | 协议层，1:1 翻译 |
| `pods/OppoRfcommSocketFactory.kt` | `Bluetooth/OppoRfcommClient.cs` | 用 32feet.NET 替换 Android BluetoothSocket |
| `pods/AppRfcommController.kt` | `Controllers/PodController.cs` | 连接状态机 + 轮询 + 包分发 |
| `pods/GameModeImplementation.kt` / `RfcommConnectionMethod.kt` | `Pods/*.cs` | 枚举，1:1 翻译 |
| `ui/*.kt` (Compose) | `UI/*.cs` (WinForms) | 重写为 Windows Forms |
| Xposed Hook 层 | ❌ 不实现 | HyperOS 专属，Windows 无对应概念 |

---

## 2. 技术栈

- **运行时**：.NET 10（`net10.0-windows`，已在 [OPods.csproj](file:///home/en/Code/CS/OPods/OPods.csproj) 配置好）
- **UI**：Windows Forms（已启用 `UseWindowsForms`）
- **蓝牙**：[32feet.NET](https://github.com/inthehand/32feet)（`InTheHand.Net.Bluetooth`），NuGet 包 `InTheHand.Net.Bluetooth`
  - 提供 `BluetoothClient` / `BluetoothDeviceInfo` / `BluetoothEndPoint`，直接支持 RFCOMM SPP 连接与 UUID 服务发现
- **异步**：`async/await` + `CancellationToken`，替代 Kotlin 的协程

---

## 3. 目录结构

```
OPods/
├── OPods.csproj
├── Program.cs                      # 入口（保持现状，仅改 Form1 → MainForm）
├── PLAN.md                         # 本文件
├── .github/
│   └── workflows/
│       └── build.yml               # GitHub Actions：push/PR 自动编译，tag 发 Release
├── .gitignore                      # 忽略 bin/ obj/ .vs/ 等
├── Pods/                           # 协议层（纯 C#，无平台依赖）
│   ├── OppoPackets.cs              # 组包 + 帧拆分器 OppoPacketFramer
│   ├── OppoCommands.cs             # Cmd / AncMode / BatteryComponent / GameModeFeature 常量
│   ├── OppoEnums.cs                # 预构数据包 Enums
│   ├── BatteryParser.cs            # 电量响应解析
│   ├── AncModeParser.cs            # ANC 模式解析
│   ├── GameModeParser.cs           # 游戏模式状态解析
│   ├── SwitchFeatureSetParser.cs   # 功能开关响应解析
│   ├── NoiseControlMode.cs         # 降噪模式枚举
│   ├── GameModeImplementation.cs   # 游戏模式实现枚举
│   └── RfcommConnectionMethod.cs   # 连接方式枚举
├── Bluetooth/                      # Windows 蓝牙层
│   └── OppoRfcommClient.cs         # 32feet.NET 封装的 RFCOMM 客户端
├── Controllers/                    # 业务逻辑层
│   └── PodController.cs            # 连接状态机 + 轮询 + 包分发（对应 AppRfcommController）
└── UI/                             # Windows Forms 界面
    ├── MainForm.cs                 # 主界面（电量、ANC、游戏模式）
    ├── MainForm.Designer.cs
    ├── DevicePickerForm.cs         # 蓝牙设备扫描与选择
    └── DevicePickerForm.Designer.cs
```

---

## 4. 协议层移植（Pods/）

完全照搬 [Packets.kt](file:///home/en/Code/CS/OPods/GI/OppoPods-master/app/src/main/java/moe/chenxy/oppopods/pods/Packets.kt)，纯字节操作，无平台依赖。

### 4.1 帧格式

```
AA [TotalLen:1] 00 00 [Cmd:2 LE] [Seq:1] [PayLen:2 LE] [Payload...]
```

- `TotalLen = 7 + PayLen`（7 = Res(2) + Cmd(2) + Seq(1) + PayLen(2)）
- 整帧长度 = `2 + TotalLen`（2 = Header(1) + TotalLen(1)）

### 4.2 组包 `OppoPackets.BuildPacket`

```csharp
public static byte[] BuildPacket(int cmd, byte seq = 0xF0, byte[]? payload = null)
{
    payload ??= Array.Empty<byte>();
    int payLen = payload.Length;
    int totalLen = 7 + payLen;
    var packet = new byte[2 + totalLen];
    packet[0] = 0xAA;
    packet[1] = (byte)totalLen;
    packet[2] = 0x00;
    packet[3] = 0x00;
    packet[4] = (byte)(cmd & 0xFF);
    packet[5] = (byte)((cmd >> 8) & 0xFF);
    packet[6] = seq;
    packet[7] = (byte)(payLen & 0xFF);
    packet[8] = (byte)((payLen >> 8) & 0xFF);
    Buffer.BlockCopy(payload, 0, packet, 9, payLen);
    return packet;
}
```

### 4.3 帧拆分器 `OppoPacketFramer`

- 维护一个 `MemoryStream` 累积缓冲
- 查找 `0xAA` 头，读取 `[1]` 处的 `TotalLen`，帧长 = `TotalLen + 2`
- 校验 `TotalLen >= 7` 且帧长 `<= 512`
- 凑齐一帧就输出，剩余保留到下次
- 对应 Kotlin `OppoPacketFramer.append()`

### 4.4 命令常量

| 名称 | 值 | 说明 |
|----|----|----|
| `SET_ANC` | `0x0404` | 设置降噪模式 |
| `SET_GAME_MODE` | `0x0403` | 设置游戏模式 |
| `QUERY_BATTERY` | `0x0106` | 查询电量 |
| `BATTERY_RESPONSE` | `0x8106` | 电量响应 |
| `QUERY_ANC_MODE` | `0x010C` | 查询降噪模式 |
| `ANC_MODE_RESPONSE` | `0x810C` | 降噪模式响应 |
| `ANC_MODE_NOTIFY` | `0x0204` | 主动状态上报（电量/降噪/按键） |
| `QUERY_STATUS` | `0x010D` | 批量状态查询 |
| `QUERY_STATUS_RESPONSE` | `0x810D` | 批量状态响应 |
| `SET_GAME_MODE_RESPONSE` | `0x8403` | 功能开关响应 |

### 4.5 ANC 模式值

| 模式 | 设置载荷 | 响应识别（val1,val2） |
|----|----|----|
| 关闭 | `01 01 01` | `08 00` |
| 降噪 | `01 01 02` | `10 00` |
| 通透 | `01 01 04` | `00 01` |
| 自适应 | `01 01 00 08` | `00 08` |

### 4.6 电量解析

- 响应载荷为 `[Index, RawValue]` 对
- `Index`：1=左耳，2=右耳，3=充电盒
- `RawValue`：电量 = `val & 0x7F`，充电中 = `(val & 0x80) != 0`
- 主动上报（`0x0204`，type=`0x01`）：`[0x01] [count] [Index, StatusValue] * count`

### 4.7 预构数据包 `OppoEnums`

直接照搬 [Packets.kt 的 Enums](file:///home/en/Code/CS/OPods/GI/OppoPods-master/app/src/main/java/moe/chenxy/oppopods/pods/Packets.kt#L97)，包括：

- `AncNoiseCancel` / `AncTransparency` / `AncOff` / `AncAdaptive`
- `QueryBattery` / `QueryAnc`
- `GameModeOn` / `GameModeOff` / `GameLowLatencyOn` / `GameLowLatencyOff`
- `QueryStatus`（固定 hex：`AA 13 00 00 0D 01 00 0C 00 0B 05 04 0B 11 13 18 06 1B 1C 27 28`）
- `GameModePackets(enabled, implementation)`：
  - STANDARD：仅 `GameModeOn/Off`
  - COMPATIBLE：开 = `[GameModeOn, GameLowLatencyOn]`，关 = `[GameLowLatencyOff, GameModeOff]`，包间隔 120ms

---

## 5. 蓝牙连接层（Bluetooth/OppoRfcommClient.cs）

用 32feet.NET 替换 [OppoRfcommSocketFactory.kt](file:///home/en/Code/CS/OPods/GI/OppoPods-master/app/src/main/java/moe/chenxy/oppopods/pods/OppoRfcommSocketFactory.kt)。

### 5.1 UUID 与通道

- 首选 UUID：`00001107-D102-11E1-9B23-00025B00A5A5`
- 备选 UUID：`0000079A-D102-11E1-9B23-00025B00A5A5`
- 通道回退：固定 RFCOMM 通道 15

### 5.2 连接流程

```csharp
public sealed class OppoRfcommClient : IDisposable
{
    private const int RfcommChannel = 15;
    private static readonly Guid Uuid1 = Guid.Parse("00001107-D102-11E1-9B23-00025B00A5A5");
    private static readonly Guid Uuid2 = Guid.Parse("0000079A-D102-11E1-9B23-00025B00A5A5");

    private BluetoothClient? _client;
    private NetworkStream? _stream;

    public async Task ConnectAsync(BluetoothAddress address, RfcommConnectionMethod method, CancellationToken ct)
    {
        // UUID 模式：依次尝试两个 UUID
        // CHANNEL 模式：用 BluetoothEndPoint(address, BluetoothService.CreateFrom...) 或直接指定 channel
        // 连接成功后保存 _stream = _client.GetStream()
    }

    public async Task SendAsync(byte[] packet, CancellationToken ct);
    public IAsyncEnumerable<byte[]> ReadFramesAsync(CancellationToken ct); // 内部用 OppoPacketFramer
    public void Disconnect();
}
```

### 5.3 32feet.NET 关键 API

- `BluetoothClient.DiscoverDevices()`：扫描附近设备
- `BluetoothDeviceInfo(addr)`：获取设备名/地址
- `new BluetoothClient().Connect(BluetoothEndPoint)`：建立 RFCOMM 连接
  - `BluetoothEndPoint` 可由 `BluetoothService`（UUID）或自定义通道构造
- `client.GetStream()`：获取 `NetworkStream` 读写

### 5.4 通道 15 的处理

32feet.NET 没有直接暴露「指定 channel」的公开 API，需要通过 `BluetoothService` 枚举或反射。计划：
1. 优先用两个 UUID 尝试 `BluetoothClient.Connect`
2. 若都失败，尝试用 `RfcommService` 的 `CreateFromRfcommChannel` 或等价方式指定 channel 15
3. 若 32feet.NET 版本不支持，则降级为仅 UUID 模式（参考项目里 CHANNEL 也是可选的）

---

## 6. 控制器层（Controllers/PodController.cs）

照搬 [AppRfcommController.kt](file:///home/en/Code/CS/OPods/GI/OppoPods-master/app/src/main/java/moe/chenxy/oppopods/pods/AppRfcommController.kt) 的状态机与轮询逻辑。

### 6.1 状态

```csharp
public enum ConnectionState { Disconnected, Connecting, Connected, Error }

public sealed class PodController : IDisposable
{
    // 状态（对应 Kotlin 的 StateFlow）
    public ConnectionState State { get; private set; }
    public BatteryParams Battery { get; private set; }
    public NoiseControlMode AncMode { get; private set; }
    public bool GameMode { get; private set; }
    public string DeviceName { get; private set; }

    // 事件（替代 StateFlow.collect）
    public event EventHandler<ConnectionState>? StateChanged;
    public event EventHandler<BatteryParams>? BatteryChanged;
    public event EventHandler<NoiseControlMode>? AncModeChanged;
    public event EventHandler<bool>? GameModeChanged;
}
```

### 6.2 连接流程（对应 `connect()`）

1. 设置 `State = Connecting`
2. 延迟 300ms（与 Kotlin 一致，等系统蓝牙就绪）
3. 调用 `OppoRfcommClient.ConnectAsync()`
4. 成功后 `State = Connected`，启动后台读取循环
5. 延迟 300ms，调用 `QueryStatus()`
6. 启动 30 秒电量轮询定时器

### 6.3 读取循环（对应 `startPacketReader`）

- 后台 `Task` 循环读取 `ReadFramesAsync()`
- 每收到一帧调用 `HandlePacket(byte[])`
- 读取返回 -1 或抛异常时断开连接

### 6.4 包分发（对应 `handlePacket`）

按顺序尝试解析，命中即返回：

1. `BatteryParser.Parse` → 更新 `Battery`，触发 `BatteryChanged`
2. `BatteryParser.ParseActiveReport` → 合并更新 `Battery`
3. `AncModeParser.Parse` → 更新 `AncMode`，触发 `AncModeChanged`
4. `GameModeParser.Parse` → 更新 `GameMode`，触发 `GameModeChanged`
5. `SwitchFeatureSetParser.Parse` → 仅记录日志

### 6.5 状态查询组合（对应 `queryStatus`）

```
send(QUERY_STATUS)  → 唤醒 + 游戏模式
delay 50ms
send(QUERY_BATTERY)
delay 50ms
send(QUERY_ANC)
```

### 6.6 控制命令

- `SetAncMode(NoiseControlMode)`：先更新本地状态，再发包
- `SetGameMode(bool)`：先更新本地状态，再按 implementation 发包序列（COMPATIBLE 模式包间隔 120ms）
- `RefreshStatus()`：手动刷新（UI 按钮触发）
- `Disconnect()`：停止轮询、关流、重置状态

### 6.7 线程安全

- 所有后台操作在 `Task` 中进行
- 状态变更通过事件抛出，UI 订阅后用 `Control.Invoke` 切回 UI 线程
- 使用 `CancellationTokenSource` 统一取消后台任务

---

## 7. UI 层（UI/）

### 7.1 MainForm（主界面）

- **设备区**：显示当前设备名、连接状态（圆点 + 文字）、「更换设备」按钮
- **电量区**：三块电池图标（左耳 / 右耳 / 充电盒），显示百分比 + 充电图标
- **降噪区**：四个单选按钮（关闭 / 降噪 / 自适应 / 通透），点击即切换
- **游戏模式区**：开关 + 实现方式下拉框（STANDARD / COMPATIBLE）
- **底部**：「刷新状态」按钮 + 日志输出框（可选，调试用）
- 订阅 `PodController` 事件，用 `BeginInvoke` 更新控件

### 7.2 DevicePickerForm（设备选择）

- 「扫描」按钮 → 调用 `BluetoothClient.DiscoverDevices()`（后台线程，避免 UI 卡死）
- 列表显示设备名 + 地址 + 信号强度
- 「连接」按钮 → 返回选中的 `BluetoothAddress` 给 MainForm
- 连接方式选择（UUID / Channel 15）

### 7.3 Program.cs 调整

```csharp
Application.Run(new MainForm());
```

删除现有的 `Form1.cs` / `Form1.Designer.cs`。

---

## 8. 实施步骤

### 阶段 1：协议层移植（无依赖，可独立验证）
1. 创建 `Pods/` 目录及所有文件
2. 移植 `OppoPackets` + `OppoPacketFramer`
3. 移植命令常量与枚举
4. 移植四个 Parser
5. 移植 `OppoEnums` 预构包
6. 写单元测试验证组包/拆包/解析（可选，用 xUnit）

### 阶段 2：蓝牙连接层
7. 添加 32feet.NET NuGet 包
8. 实现 `OppoRfcommClient`（连接 + 发送 + 帧读取）
9. 用控制台小程序验证能连上耳机并收到数据

### 阶段 3：控制器层
10. 实现 `PodController` 状态机
11. 实现读取循环、包分发、轮询定时器
12. 实现控制命令（ANC / 游戏模式）

### 阶段 4：UI 层
13. 实现 `DevicePickerForm`
14. 实现 `MainForm`（电量、ANC、游戏模式）
15. 接线：UI ↔ PodController 事件
16. 调整 `Program.cs`，删除 `Form1`

### 阶段 5：收尾
17. 异常处理与重连逻辑
18. 配置持久化（最近设备、连接方式、游戏模式实现）—— 用 `Preferences` 或 JSON
19. 编译验证 `dotnet build`
20. 真机测试

### 阶段 6：CI/CD（GitHub Actions 自动编译）
21. 添加 `.gitignore`（忽略 `bin/`、`obj/`、`.vs/`、`*.user`）
22. 编写 `.github/workflows/build.yml`
23. push 到仓库验证 Actions 触发并产物上传成功
24. 打 `v*` tag 验证 Release 自动创建

---

## 9. 关键注意事项

### 9.1 协议细节
- 所有多字节字段为**小端序**
- `Seq` 默认 `0xF0`
- 主动上报包 `0x0204` 同时承载电量（type=`0x01`）、降噪、按键（type=`0x02`），解析时需按 type 区分
- ANC 响应解析要扫描 `01 01 [v1] [v2]` 模式，不能按固定偏移

### 9.2 蓝牙连接
- Windows 上首次连接可能需要系统已配对，32feet.NET 可用 `BluetoothSecurity.PairRequest` 触发配对
- RFCOMM 连接是阻塞的，必须放后台线程
- 断线检测：`NetworkStream.Read` 返回 0 或抛 `IOException` 视为断开

### 9.3 与参考项目的差异
- 不实现：焦点岛弹窗、超级岛常驻通知、状态栏耳机图标、HyperOS 设置页同步、A2DP 检测
- 不需要：Xposed 作用域、广播跨进程通信、`com.milink.service` 镜像
- 新增：Windows 配置文件持久化（参考项目用 Android `SharedPreferences`）

### 9.4 32feet.NET 版本
- 使用最新稳定版（当前为 4.x）
- 注意：32feet.NET 4.x 对 RFCOMM channel 直连支持有限，CHANNEL 模式可能需要降级处理或仅作可选

---

## 10. CI/CD（GitHub Actions 自动编译）

参考 [GI/OppoPods-master/.github/workflows/build.yml](file:///home/en/Code/CS/OPods/GI/OppoPods-master/.github/workflows/build.yml)，改造为 .NET 项目版本。

### 10.1 与参考项目的差异

| 项 | 参考项目（Android） | 本项目（.NET WinForms） |
|----|----|----|
| Runner | `ubuntu-latest` | **`windows-latest`**（WinForms 必须在 Windows 上编译） |
| 构建命令 | `./gradlew assembleRelease` | `dotnet publish -c Release -r win-x64 --self-contained false` |
| 产物 | APK | EXE + 依赖（已配 `PublishSingleFile`） |
| 签名 | Android keystore（zipalign + apksigner） | 不需要（Windows 无强制签名） |
| Java/SDK | 需要 setup-java | 不需要，windows-latest 自带 .NET SDK |

### 10.2 触发条件

```yaml
on:
  push:
    tags: [ "v*" ]              # 打 tag 触发并发布 Release
    branches: [ "main", "master" ]  # 主分支 push 触发编译验证
  pull_request:
    branches: [ "main", "master" ]
  workflow_dispatch:            # 手动触发
```

### 10.3 构建步骤

```yaml
jobs:
  build:
    runs-on: windows-latest
    permissions:
      contents: write
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --configuration Release --no-restore

      - name: Publish
        run: dotnet publish --configuration Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: OPods-win-x64
          path: ./publish/OPods.exe
          if-no-files-found: error

      - name: Create GitHub Release
        if: startsWith(github.ref, 'refs/tags/')
        uses: ncipollo/release-action@v1
        with:
          token: ${{ github.token }}
          artifacts: "./publish/OPods.exe"
          generateReleaseNotes: true
          makeLatest: true
```

### 10.4 关键说明

- **Runner 必须 `windows-latest`**：项目用了 `net10.0-windows` + `UseWindowsForms`，Linux/Ubuntu runner 无法编译 WinForms（即使配了 `EnableWindowsTargeting=true` 也只是允许在非 Windows 上引用 Windows API，编译 WinForms 仍需 Windows SDK）
- **`--self-contained false`**：依赖目标机器的 .NET 运行时，产物小（约几 MB）。若想完全免安装运行时，改为 `--self-contained true`（产物约 150MB+）
- **`-r win-x64`**：固定 x64。如需 ARM64 可加 `win-arm64`，但 32feet.NET 对 ARM64 支持需验证
- **`PublishSingleFile=true`**：已在 [csproj](file:///home/en/Code/CS/OPods/OPods.csproj) 配置，命令行再指定一次确保生效，产物为单个 `OPods.exe`
- **Release 触发**：仅 `v*` tag 触发，普通 push 只编译验证不上传 Release
- **无需签名**：Windows 程序不强制签名，用户首次运行可能遇到 SmartScreen 警告，属正常现象

### 10.5 .gitignore 要点

上传 GitHub 时需要忽略以下内容，避免把构建产物、IDE 缓存、本地配置和参考项目源码推到仓库：

```gitignore
# 构建产物
bin/
obj/
publish/
[Dd]ebug/
[Rr]elease/

# Visual Studio / Rider / VSCode 缓存
.vs/
.idea/
.vscode/
*.suo
*.user
*.userosscache
*.sln.docstates

# 用户本地配置（不随仓库分发）
*.user
appsettings.*.json

# 参考项目源码（仅本地参考，不进仓库）
GI/

# 系统文件
Thumbs.db
ehthumbs.db
Desktop.ini
.DS_Store
```

**关键说明：**

| 忽略项 | 原因 |
|--------|------|
| `bin/` `obj/` `publish/` | 构建产物，CI 会重新生成，不应入库 |
| `.vs/` `.idea/` `.vscode/` | IDE 本地缓存与个人配置，因人而异 |
| `*.user` | 包含本地调试路径、用户首选项，可能含敏感信息 |
| `GI/` | 参考项目 `OppoPods-master` 源码，仅作本地学习参考，避免版权与冗余问题 |
| `Thumbs.db` 等 | Windows/macOS 系统自动生成的缩略图缓存 |

> ⚠️ 注意：`GI/` 文件夹是参考项目源码，已在本地存在但**不应推送到 GitHub**。如果之前已经误提交过，需要用 `git rm -r --cached GI/` 从版本控制中移除（文件本身保留在磁盘上）。

---

## 11. 验收标准

- [ ] 能扫描并列出附近蓝牙设备
- [ ] 能通过 UUID 或通道 15 连上 OPPO 耳机
- [ ] 连接后能正确显示左耳/右耳/充电盒电量与充电状态
- [ ] 能切换 ANC 模式（关闭/降噪/通透/自适应）并反映耳机实际状态
- [ ] 能开关游戏模式（STANDARD 与 COMPATIBLE 两种实现）
- [ ] 30 秒自动轮询电量，断线后状态正确回退
- [ ] `dotnet build` 无错误无警告
- [ ] push 到 GitHub 后 Actions 自动触发编译并产出 `OPods.exe` artifact
- [ ] 打 `v*` tag 后自动创建 GitHub Release 并附带 `OPods.exe`
