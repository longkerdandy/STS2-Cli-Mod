# FakeMerchant 实现计划

## 1. 扩展 EventStateDto

**文件**: `STS2.Cli.Mod/Models/State/EventStateDto.cs`

添加字段：
```csharp
/// <summary>
///     自定义事件子类型 (如 "FakeMerchant")
/// </summary>
public string? CustomEventType { get; set; }

/// <summary>
///     FakeMerchant: Proceed 按钮是否可用
/// </summary>
public bool? CanProceed { get; set; }

/// <summary>
///     FakeMerchant: 商店是否打开
/// </summary>
public bool? IsShopOpen { get; set; }
```

---

## 2. 扩展 EventStateBuilder

**文件**: `STS2.Cli.Mod/State/Builders/EventStateBuilder.cs`

在 `Build()` 方法中添加自定义事件检测：

```csharp
private static void ExtractCustomEventInfo(NEventRoom eventRoom, EventStateDto result)
{
    // 检测 FakeMerchant
    var fakeMerchant = FindFakeMerchant(eventRoom);
    if (fakeMerchant != null)
    {
        result.CustomEventType = "FakeMerchant";
        
        // 获取 proceed 按钮状态
        var proceedButton = fakeMerchant.GetNodeOrNull<NProceedButton>("%ProceedButton");
        if (proceedButton != null)
        {
            result.CanProceed = proceedButton.IsEnabled && proceedButton.Visible;
        }
        
        // 获取商店状态
        var inventory = fakeMerchant.GetNodeOrNull<NMerchantInventory>("%Inventory");
        if (inventory != null)
        {
            result.IsShopOpen = inventory.IsOpen;
        }
        
        Logger.Info($"Detected FakeMerchant: can_proceed={result.CanProceed}, is_shop_open={result.IsShopOpen}");
    }
}

private static NFakeMerchant? FindFakeMerchant(NEventRoom eventRoom)
{
    // NFakeMerchant 是 NEventRoom 的子节点
    // 使用 FindFirst 递归查找
    return UiHelper.FindFirst<NFakeMerchant>(eventRoom);
}
```

**注意**: 需要添加 `using MegaCrit.Sts2.Core.Nodes.Events.Custom;`

---

## 3. 创建 EventProceedHandler

**新文件**: `STS2.Cli.Mod/Actions/EventProceedHandler.cs`

```csharp
using MegaCrit.Sts2.Core.Nodes.Events.Custom;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using STS2.Cli.Mod.Models.Message;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles clicking the proceed button on custom events (e.g., FakeMerchant).
/// </summary>
public static class EventProceedHandler
{
    private static readonly ModLogger Logger = new("EventProceedHandler");
    
    private const int ProceedTimeoutMs = 5000;
    private const int PollIntervalMs = 100;

    public static object HandleRequest(Request request)
    {
        Logger.Info("Requested to proceed from custom event");
        return MainThreadExecutor.RunOnMainThread(() => Execute());
    }

    private static object Execute()
    {
        try
        {
            // Guard: 在事件房间中
            var eventRoom = NEventRoom.Instance;
            if (eventRoom == null || !eventRoom.IsInsideTree())
            {
                return new { ok = false, error = "NOT_IN_EVENT", message = "Not currently in an event" };
            }

            // 查找 FakeMerchant
            var fakeMerchant = UiHelper.FindFirst<NFakeMerchant>(eventRoom);
            if (fakeMerchant == null)
            {
                return new { ok = false, error = "NOT_FAKE_MERCHANT", message = "Current event is not FakeMerchant" };
            }

            // 查找 Proceed 按钮
            var proceedButton = fakeMerchant.GetNodeOrNull<NProceedButton>("%ProceedButton");
            if (proceedButton == null)
            {
                return new { ok = false, error = "PROCEED_BUTTON_NOT_FOUND", message = "Proceed button not found" };
            }

            // 检查按钮状态
            if (!proceedButton.Visible)
            {
                return new { ok = false, error = "PROCEED_NOT_VISIBLE", message = "Proceed button is not visible" };
            }
            
            if (!proceedButton.IsEnabled)
            {
                return new { ok = false, error = "PROCEED_NOT_ENABLED", message = "Proceed button is not enabled (shop may be open)" };
            }

            // 点击按钮
            Logger.Info("Clicking FakeMerchant proceed button");
            proceedButton.ForceClick();

            // 等待地图打开
            var proceeded = WaitForMapOpen();
            
            return new
            {
                ok = true,
                data = new
                {
                    proceeded,
                    message = proceeded ? "Successfully proceeded to map" : "Proceed initiated but timed out waiting for map"
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to proceed from custom event: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    private static bool WaitForMapOpen()
    {
        var elapsed = 0;
        while (elapsed < ProceedTimeoutMs)
        {
            Thread.Sleep(PollIntervalMs);
            elapsed += PollIntervalMs;

            if (NMapScreen.Instance is { IsOpen: true })
                return true;
        }
        return false;
    }
}
```

---

## 4. 添加 CLI 命令

**文件**: `STS2.Cli.Cmd/Program.cs`

```csharp
var eventProceedCmd = new Command("event_proceed", "Click proceed button on custom events (e.g., FakeMerchant)");
eventProceedCmd.SetHandler(async () =>
{
    var exitCode = await CommandRunner.RunAsync("event_proceed", []);
    Environment.Exit(exitCode);
});
eventCmd.Add(eventProceedCmd);
```

---

## 5. 添加服务器路由

**文件**: `STS2.Cli.Mod/Server/PipeServer.cs`

在命令路由中添加：
```csharp
"event_proceed" => MainThreadExecutor.RunOnMainThread(() => EventProceedHandler.HandleRequest(request)),
```

---

## 6. 添加错误码映射

**文件**: `STS2.Cli.Cmd/Services/CommandRunner.cs`

```csharp
"NOT_IN_EVENT" => 2,
"NOT_FAKE_MERCHANT" => 2,
"PROCEED_BUTTON_NOT_FOUND" => 2,
"PROCEED_NOT_VISIBLE" => 2,
"PROCEED_NOT_ENABLED" => 2,
```

---

## 7. 测试场景

```bash
# 1. 进入 FakeMerchant 事件，检查状态
sts2.exe state
# Expected: screen="EVENT", layout_type="Custom", custom_event_type="FakeMerchant"
#           can_proceed=true, is_shop_open=false

# 2. 点击 Proceed 离开
sts2.exe event_proceed
# Expected: ok=true, proceeded=true

# 3. 商店打开时尝试 Proceed
sts2.exe event_proceed
# Expected: ok=false, error="PROCEED_NOT_ENABLED"

# 4. 不在事件中
sts2.exe event_proceed
# Expected: ok=false, error="NOT_IN_EVENT"
```

---

## 错误码汇总

| Error Code | Exit Code | 描述 |
|------------|-----------|------|
| `NOT_IN_EVENT` | 2 | 不在事件房间 |
| `NOT_FAKE_MERCHANT` | 2 | 当前事件不是 FakeMerchant |
| `PROCEED_BUTTON_NOT_FOUND` | 2 | 找不到 Proceed 按钮 |
| `PROCEED_NOT_VISIBLE` | 2 | Proceed 按钮不可见 |
| `PROCEED_NOT_ENABLED` | 2 | Proceed 按钮未启用（商店可能打开） |

---

## 文件变更清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `EventStateDto.cs` | 修改 | 添加 CustomEventType, CanProceed, IsShopOpen |
| `EventStateBuilder.cs` | 修改 | 添加 FakeMerchant 检测逻辑 |
| `EventProceedHandler.cs` | 新增 | 处理 event_proceed 命令 |
| `Program.cs` | 修改 | 添加 CLI 命令 |
| `PipeServer.cs` | 修改 | 添加命令路由 |
| `CommandRunner.cs` | 修改 | 添加错误码映射 |
| `AGENTS.md` | 修改 | 更新测试命令列表 |

---

*基于 STS2-Reverse-Engineering 分析生成*
