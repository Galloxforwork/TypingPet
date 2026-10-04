# 打字小伴侣 TypingPet

Windows 桌面打字计数和桌宠。程序只统计按键次数，不读取或保存你实际输入的文字。

## 只想使用？下载这个文件

[**下载 TypingPet.exe（Windows 64 位）**](https://github.com/Galloxforwork/TypingPet/raw/refs/heads/main/release/TypingPet.exe)

普通使用者只需要下载上面的 **TypingPet.exe**（约 70 MB），不需要下载源码压缩包。它适用于 Windows x64，并已包含运行所需的 .NET 8；不必另外安装 .NET 或开发工具。

## 第一次启动

**第 1 步：下载并打开。** 下载完成后，在“下载”文件夹里找到 TypingPet.exe，双击打开。

**第 2 步：打开设置。** 程序会先显示桌宠和系统托盘图标，不会自动打开设置窗口。双击桌宠可打开设置；如果桌宠是透明的，请在屏幕右下角时钟旁找到 TypingPet 托盘图标并双击它。

**第 3 步：加入图片。** 在“图片组管理”里把图片加入图片组，然后点击全局设置区的“保存并应用”。还没加入图片时，桌宠区域会保持透明，这是正常现象。

**第 4 步：退出程序。** 关闭设置窗口会让程序留在系统托盘继续运行。要完全退出，请右键桌宠或托盘图标并选择退出。

支持 PNG、JPG/JPEG、BMP、GIF（使用首帧）、TIF/TIFF 和 ICO。图片组可以分别管理前景和背景图片。

## 设置、计数与更新

- 设置、计数和导入的图片保存在程序旁边的 `data/` 文件夹中。
- 更新时下载新版 `TypingPet.exe` 并替换旧文件，保留原有 `data/` 文件夹。移动程序时也请把 `data/` 一起移动。
- 程序默认在当前 Windows 用户登录时自动启动；可在全局设置中关闭“开机自启”。
- 计数只有点击“重置计数”才会清零。
- `data/` 里可能有你的设置和图片，请不要把自己的这个文件夹上传或发给别人。

## 开发者

普通使用者不需要阅读本节。需要从源码运行或构建时，安装 .NET 8 SDK 后，在项目目录运行：

```powershell
dotnet run --project TypingPet.csproj
dotnet build TypingPet.csproj -c Release
```

生成 Windows x64 单文件程序：

```powershell
dotnet publish TypingPet.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -p:ShareBuild=true -o bin\Release\portable-selfcontained
```

源码项目还包含 `design/`（独立界面设计器）和 `tools/`（配置辅助脚本）。`bin/`、`obj/` 是可重新生成的构建目录；不要把个人 `data/` 放进公开分享包。
