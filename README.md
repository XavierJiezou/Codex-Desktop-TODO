# Codex Desktop TODO

一个极简的 Windows 桌面 TODO 便签应用。发布版是轻量原生 Windows 程序，不使用 Electron，安装包目标体积小于 1MB。

![Codex Desktop TODO screenshot](docs/screenshot.png)

## 下载

Windows 安装包可以在 GitHub Release 中下载：

[下载 CodexDesktopTODO-Setup-0.2.0.exe](https://github.com/XavierJiezou/Codex-Desktop-TODO/releases/latest)

## 功能

- 无边框桌面小窗，可拖动。
- 默认置顶，可一键取消置顶。
- 可锁定位置，避免误拖动。
- 支持添加、完成、删除、双击编辑 TODO。
- 支持隐藏到系统托盘。
- 自动保存任务、窗口位置、大小和偏好设置。
- 数据只保存在本机，不需要登录。

## 开发

```powershell
.\build.ps1
```

## 打包

构建后的安装包会生成在 `dist/` 目录中。

## 技术栈

- C# / WinForms
- .NET Framework
- NSIS

## License

MIT
