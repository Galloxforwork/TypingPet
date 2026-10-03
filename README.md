# 打字小伴侣 TypingPet

Windows 桌面打字小伴侣。使用 Raw Input 接收键盘按键事件，只统计按键，不读取或保存输入的文字。

## 给使用者

解压分享包后双击 `TypingPet.exe`。分享版包含 .NET 8 桌面运行时，适用于 Windows x64；无需安装 .NET 或开发工具。程序默认只显示桌宠和托盘图标，不自动打开设置面板。双击桌宠或托盘图标可打开设置。首次使用时在“图片组管理”中拖入图片，再点击全局设置区的“保存并应用”。没有导入图片时，桌宠区域会保持透明。

`data/` 文件夹保存 `settings.json`、计数、每日统计和 `assets/` 图片。源码项目目录树中的程序（包括根目录 EXE、`release/` EXE 和 `dotnet run`）统一使用项目根目录 `data/`，避免在 `bin/Debug` 或 `bin/Release` 下创建多份设置；复制到独立文件夹的便携版在 EXE 旁使用 `data/`。全局设置中的“打开数据文件夹”会打开当前实际使用的位置，旁边的“校验并清理”会报告完整路径、检查 JSON/计数/图片引用与图片可读性，并删除当前有效设置中未引用的受支持图片。新版图片组存在时，旧版 `Profiles.Images` 和顶层 `Images` 不再算作使用中图片；`settings.json.bak` 仅作恢复设置的副本，其中单独引用的图片也会清理。此检查会在启动和每次设置成功保存后自动运行；当前设置损坏、有效图片引用缺失或图片列表结构无效时会暂停清理并保留素材。更新时只替换 `TypingPet.exe`，保留原有 `data/`；若解压到新位置，请连同旧 `data/` 一起复制。请放在有写入权限的位置，不要放在 `Program Files`。

第一次运行新版且当前 `data/settings.json` 不存在时，会从旧版 `%LOCALAPPDATA%\TypingPetPrototype` 复制设置、计数和图片；旧目录不会删除。旧版配置没有新版图片组时，仍会从旧图片字段迁移。成功保存设置后，`settings.json.bak` 会与当前已验证配置同步；当前设置校验无法通过时不会清理图片。

如需分享一套现成配置，可用 `tools/package_friend_profile.py` 从自己的 `data/settings.json` 生成一个独立分享包的 `data/`；它会复制被引用图片、把计数归零、去掉本机绝对路径。不要把自己的 `data/` 原样放进公开分享包。旧版 `starter-profile/` 仍可首次导入，但新分享包直接使用 `data/`。

关闭设置窗口会收进系统托盘；右键桌宠或托盘图标可完全退出。默认在当前用户登录 Windows 后自动启动，可在全局设置取消“开机自启”。计数只有点击“重置计数”才清零。向别人分享清洁版时发送不含私人 `data/` 内容的完整压缩包。

支持 PNG、JPG/JPEG、BMP、GIF 首帧、TIF/TIFF 和 ICO。“图片组管理”页可分别添加前景和背景；选中图片后可前置、后置、裁切或删除，剪切、复制、粘贴使用 Ctrl+X/C/V。

## 给开发者

## 项目目录

| 路径 | 用途 |
| --- | --- |
| 根目录 `*.cs`、`TypingPet.csproj` | 主程序源码与项目配置；当前规模保持平铺，避免无必要的多层目录 |
| `design/` | 独立的 HTML 界面设计器，不参与桌宠运行 |
| `tools/` | 配置与朋友版素材整理脚本 |
| `data/` | 本机设置、计数和图片；属于个人数据，不加入源码包或公开包 |
| `release/` | 正式更新 EXE 和可分享的完整压缩包 |
| `local-archive/` | 本地清理归档，含历史源码快照和旧构建数据；Git 忽略，不用于发布 |
| `bin/`、`obj/` | 可重建的编译输出，清理后由 .NET SDK 重新生成 |

| 文件 | 作用 |
| --- | --- |
| `Program.cs`、`StartupRegistration.cs` | 隐藏设置面板启动、单实例控制和当前用户开机自启 |
| `UserDataPaths.cs`、`StarterProfileImporter.cs` | 程序旁的数据目录、旧版迁移与旧分享包导入 |
| `MainForm.cs` | 设置界面、状态机和数据保存 |
| `PetWindow.cs`、`LayeredWindowPainter.cs` | 透明桌宠及动效绘制 |
| `RawKeyboardInput.cs` | Windows Raw Input 按键检测 |
| `CropImageForm.cs` | 图片裁切 |
| `TypingPet.csproj` | .NET 8 WinForms 项目与 AntdUI 依赖 |
| `tools/package_friend_profile.py` | 从便携数据生成不含原电脑绝对路径的私人分享配置 |

源码构建需要 .NET 8 SDK：`dotnet build TypingPet.csproj -c Release`；开发时直接运行 `dotnet run --project TypingPet.csproj`。默认构建保留自用图标；分享版用 `ShareBuild=true` 生成 Windows 默认图标。发布用单文件命令：

```powershell
dotnet publish TypingPet.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -p:ShareBuild=true -o bin\Release\portable-selfcontained
```

`bin/`、`obj/` 是可重建的构建目录；`release/` 是发给普通用户的文件包。各用户生成的 `data/` 不属于源码；打包时只放空目录，绝不附带开发者自己的设置和图片。
