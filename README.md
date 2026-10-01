# 打字小伴侣 TypingPet

Windows 桌面打字小伴侣。使用 Raw Input 接收键盘按键事件，只统计按键，不读取或保存输入的文字。

## 给使用者

打开 `release/TypingPet.exe` 即可运行。此版本已包含 .NET 8 桌面运行时，适用于 Windows x64；无需安装 .NET 或开发工具。首次打开后，在“图片组管理”中拖入图片，再点击顶部“保存并应用”。没有导入图片时，桌宠区域会保持透明。

关闭设置窗口会收进系统托盘；右键桌宠或托盘图标可完全退出。计数、设置和导入图片保存在本机 `%LOCALAPPDATA%\TypingPetPrototype`，更新 EXE 不会清除它们；计数只有点击“重置计数”才清零。分享程序时只需发送 `release` 里的 EXE，个人设置和图片不会随 EXE 发送。

支持 PNG、JPG/JPEG、BMP、GIF 首帧、TIF/TIFF 和 ICO。剪切、复制、粘贴、裁切与去除纯色背景都在“图片组管理”页；复杂背景的自动识别暂不支持。

## 给开发者

| 文件 | 作用 |
| --- | --- |
| `Program.cs` | 启动与单实例控制 |
| `MainForm.cs` | 设置界面、状态机和数据保存 |
| `PetWindow.cs`、`LayeredWindowPainter.cs` | 透明桌宠及动效绘制 |
| `RawKeyboardInput.cs` | Windows Raw Input 按键检测 |
| `CropImageForm.cs`、`BackgroundRemovalForm.cs` | 图片裁切与纯色去背景 |
| `TypingPet.csproj` | .NET 8 WinForms 项目与 AntdUI 依赖 |

源码构建需要 .NET 8 SDK：`dotnet build TypingPet.csproj -c Release`。`运行.ps1` 仅供开发时启动。发布用单文件命令：

```powershell
dotnet publish TypingPet.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o bin\Release\portable-selfcontained
```

`bin/`、`obj/` 是可重建的构建目录；`release/` 是发给普通用户的最小文件包。数据位于用户本机的 LocalAppData，不属于源码或发布包。
