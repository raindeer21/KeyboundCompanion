# 键影随行

一键随行，键影从容。

适用于《最终幻想 XIV》国服「魔光键影」的 Dalamud 词条助手，使用简体中文界面。

## 功能

- **自动确认出发和结算退出**：两个独立开关默认开启并保存，关闭插件窗口仍生效。只在匹配到魔光键影时点击“出发”，只在魔光键影结算窗口点击“退出”。按钮连续可用 1 秒后点击，同一次弹窗只执行一次。排队仍由你发起。
- **首领优先**：存在可用的首领词条时优先提交，否则随机选择有效敌人。
- **手动单次提交**：点击一次只提交一个词条，过早的点击不会排队补发。
- **自动间隔提交**：首次使用默认关闭，之后记住你的选择；自动开关、间隔和防闲置选项在修改后自动保存，重载和重启后恢复。
- **延迟滑块**：1000～10000 毫秒，每轮在设定值的 ±20% 范围内重新随机，最低不低于 1000 毫秒。
- **共用硬性冷却**：手动和自动的两次实际提交至少相隔 1 秒；词条未匹配也计算冷却。
- **窗口随副本显示**：插件启动或重载时不弹窗；之后进入魔光键影自动显示，离开自动隐藏。本局手动关闭后不会反复弹出。副本外可用 `/ktgclick` 或插件设置入口手动打开，所有设置仍可编辑；实际提交只在副本内执行。
- **两种前台防闲置方式**：开关默认开启，仅自动提交运行且游戏位于前台时生效，约每 20 秒执行一次，可在界面切换。
  - 默认「错误输入 1 + Enter」：先模拟输入数字 1，确认输入框文字后再回车，等待输入框清空。可能影响正确率。与正常自动和手动提交共用至少 1 秒冷却。
  - 「功能键与方向键」：依次短按 F13、F14、左右 Shift、左右方向键，不额外提交词条。
- 两种方式均跨帧释放按键，不阻塞游戏线程。普通鼠标点击不会被当作键盘输入冲突；状态显示累计执行次数和下一次倒计时。
- 防闲置在禁止输入、输入框有文字、聊天框取得输入焦点、出现确认框或用户正在按键时暂停。关闭窗口、停止自动、切到后台、离开或卸载时释放插件持有的按键。
- 禁止打字、没有有效敌人或输入界面不可用时暂停提交；关闭窗口、离开副本或卸载插件会暂停词条提交和防闲置，保留自动开关和间隔；下次进本或在本内重新打开窗口后按已保存设置继续。

错误输入方案来自用户的国服手动实测；功能键方案参考国服项目。当前自动化实现尚待整局实机防踢验证，不保证后台或所有客户端条件下有效。中断时如输入框残留数字 1，请手动清空后继续；插件不会把其他文字当作防闲置答案提交。

例如，延迟设为 1000 毫秒时实际范围为 1000～1200 毫秒，设为 2000 毫秒时为 1600～2400 毫秒。

## 安装与使用

在 Dalamud 设置的第三方插件库中添加：

```text
https://raw.githubusercontent.com/raindeer21/MyDalamudPlugins/master/pluginmaster.json
```

在插件安装器中搜索 **键影随行**。进入魔光键影后窗口自动显示，再点击「提交一次」或勾选「自动间隔提交」。任何地点都可用 `/ktgclick` 手动打开设置；不能攻击时仍可调整自动开关和间隔。插件加载或重载时不主动弹窗，副本外手动打开也不会提交。

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

前台防闲置按键方案参考 [SiGeoMa/ff14-keybound-brawler](https://github.com/SiGeoMa/ff14-keybound-brawler/blob/a20c4687f029049c9bd4bf90120c8a5fa66a2324/dalamud-plugin/KeyboundBrawler/Core/AutoTyper.cs)。本项目独立实现了按键调度、前台检查与释放处理，没有复制该项目源代码。
