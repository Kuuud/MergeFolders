# MergeFolders

Windows 资源管理器多选文件夹后，右键使用“合并文件夹”。

## 功能

- 多选文件夹后一次打开合并窗口
- 同名文件：覆盖 / 跳过 / 自动重命名 / 每次询问
- 同名文件夹：递归合并 / 跳过 / 自动重命名
- 合并复制 / 合并移动
- 8 MiB 流式复制，适合大文件
- 保留文件时间戳
- 复制后可选：不校验 / 文件大小 / SHA-256
- 支持 Windows 本地磁盘、UNC、网络盘以及 rclone mount 暴露出的 Windows 路径
- 不需要管理员权限，安装到当前用户 HKCU

## 环境

- Windows 10/11 x64
- .NET 8 SDK（构建时）

## 构建和安装

在 PowerShell 中运行：

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\install.ps1
```

脚本会把程序发布到：

```text
%LOCALAPPDATA%\MergeFolders
```

并注册：

```text
HKCU\Software\Classes\Directory\shell\MergeFolders
```

右键多个文件夹后，在 Windows 11 中可能需要进入“显示更多选项”。

## 卸载

```powershell
.\scripts\uninstall.ps1
```

## 多选机制

程序注册 `MultiSelectModel=Player`。同时使用命名 Mutex + Named Pipe，把资源管理器可能分别启动的多个实例短时间汇聚到一个窗口，因此不依赖 `%*` 之类未在 Shell verb 文档中明确保证的命令行展开方式。

## rclone

只要 rclone 是以 Windows mount 的形式提供路径，例如 `X:\`，程序按普通 Windows 文件系统访问即可，不依赖 rclone CLI。

建议对重要数据首次使用“合并复制”，确认结果无误后再使用“合并移动”。


### PowerShell / multi-selection notes

The installer intentionally avoids PowerShell backtick line continuations so it works with Windows PowerShell 5.1 as well as PowerShell 7.
The Shell verb is registered with `MultiSelectModel=Player` and passes positional arguments `%1` through `%100`, allowing legacy Shell verbs to receive multiple selected directories (up to the documented 100-item default limit).
