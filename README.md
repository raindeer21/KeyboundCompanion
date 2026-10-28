# 键影随行

一键随行，键影从容。

适用于《最终幻想 XIV》国服「魔光键影」的 Dalamud 词条助手，使用简体中文界面。

## 功能

- **首领优先**：存在可用的首领词条时优先提交，否则随机选择有效敌人。
- **手动单次提交**：点击一次只提交一个词条，过早的点击不会排队补发。
- **自动间隔提交**：默认关闭，手动勾选后开启。
- **延迟滑块**：1000～10000 毫秒，每轮在设定值的 ±20% 范围内重新随机，最低不低于 1000 毫秒。
- **共用硬性冷却**：手动和自动的两次实际提交至少相隔 1 秒；词条未匹配也计算冷却。
- 禁止打字、没有有效敌人或输入界面不可用时暂停提交；关闭窗口、离开副本或卸载插件会停止自动模式。

例如，延迟设为 1000 毫秒时实际范围为 1000～1200 毫秒，设为 2000 毫秒时为 1600～2400 毫秒。

## 安装与使用

在 Dalamud 设置的第三方插件库中添加：

```text
https://raw.githubusercontent.com/raindeer21/MyDalamudPlugins/master/pluginmaster.json
```

在插件安装器中搜索 **键影随行**。进入魔光键影后，输入 `/ktgclick` 打开窗口，再点击「提交一次」或勾选「自动间隔提交」。

发布包也可以在 [Releases](https://github.com/raindeer21/KeyboundCompanion/releases) 下载。开发模式使用解压后的 `KeyboundClick.dll`；如已加载开发版，请先停用开发版再安装插件库版本，避免重复加载。

## 构建与测试

需要 .NET 10 SDK 和国服 Dalamud API 15 对应的开发程序集。DLL 由你的 XIVLauncherCN 安装提供，不包含在本仓库中。

在 Windows PowerShell 中运行：

```powershell
dotnet build KeyboundClick/KeyboundClick.csproj -c Release "-p:DalamudLibPath=$env:APPDATA/XIVLauncherCN/addon/Hooks/dev"
dotnet run --project KeyboundClick.Tests/KeyboundClick.Tests.csproj -c Release
```

插件输出在 `KeyboundClick/bin/Release`。安装包内应直接包含 `KeyboundClick.dll`、`KeyboundClick.json` 和第三方许可说明，不能多套一层文件夹。

## 兼容性与实现

当前面向 2026-10-28 验证的国服客户端与 Dalamud API 15。原生函数签名和结构可能随客户端更新变化。

通过客户端 HUD 读取当前词条，调用游戏的词条提交入口。模块内部标识仍为 `KeyboundClick`，命令为 `/ktgclick`。

原生结构参考 [FFXIVClientStructs.Expansion](https://github.com/AtmoOmen/FFXIVClientStructs.Expansion/tree/22b9e96bca56aab9d18a60956dfb71c14d3c50c2)，相关 MIT 许可保留在 [第三方许可说明](KeyboundClick/THIRD-PARTY-NOTICES.txt)。
