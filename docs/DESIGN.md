# 设计说明

## 1. Explorer 集成

使用当前用户注册表：

`HKCU\Software\Classes\Directory\shell\MergeFolders`

并设置 `MultiSelectModel=Player`。Microsoft 文档说明 Player 用于支持任意数量选择项，而 COM/ExplorerCommand 方案虽然更原生，但实现成本更高。本项目采用无需管理员的传统 verb + 多实例聚合方式。

## 2. 多实例聚合

资源管理器的多选行为在不同版本和注册方式下可能让命令被重复启动。程序因此使用 Mutex 判断主实例，后续实例通过 Named Pipe 发送自己的目录参数，主实例短暂收集后统一打开 UI。

## 3. 合并策略

同名文件夹默认递归合并。同名文件根据所选策略处理；自动重命名采用 `name (1).ext`、`name (2).ext` 等方式。

## 4. 复制

文件以 8 MiB 缓冲流式复制，不把大文件一次性加载进内存。复制成功后才在移动模式下删除源文件。
