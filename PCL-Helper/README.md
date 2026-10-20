# PCL-Helper v1.1 · UI 优化版

原生 WPF 下载功能恢复工具。支持 PCL 2.99.0.0，单文件运行，无需安装。

## 来源与修改声明

**本工具基于 [Turing-Mirror/PCL-Helper](https://github.com/Turing-Mirror/PCL-Helper) 修改。** 原项目作者为 Turing Mirror Software / 图灵镜，基线提交为 `b2acb231351c8b8a4cab20d3fabe566fa346dbcf`。

本修改版主要优化 WPF 界面、增加一键查找 PCL、搜索结果选择与取消功能，并完善便携打包和测试。原补丁引擎 `src/Patcher.cs` 保持原样。保留原作者版权声明及 MIT 许可证，详细声明见 [NOTICE.md](NOTICE.md)。本修改版不是上游官方发布版本，也不是 PCL 官方工具，与 PCL 官方无关联。

## 下载

- [单文件 EXE](https://github.com/19065801095-maker/git/blob/main/PCL-Helper/dist/v1.1/PCL-Helper-v1.1.exe?raw=true)
- [便携 ZIP（含说明、许可证和验证记录）](https://github.com/19065801095-maker/git/blob/main/PCL-Helper/dist/v1.1/PCL-Helper-v1.1-portable.zip?raw=true)
- [离线构建源码包](https://github.com/19065801095-maker/git/blob/main/PCL-Helper/dist/v1.1/PCL-Helper-v1.1-source.zip?raw=true)
- [SHA256 校验值](https://github.com/19065801095-maker/git/blob/main/PCL-Helper/dist/v1.1/SHA256SUMS.txt)

## 使用方法

1. 运行 `PCL-Helper-v1.1.exe`，点击“一键查找 PCL”。找到一个兼容版本会自动填入并检查；找到多个时，从列表中选择实际使用的程序，再点击“使用选中的程序”。也可点击“选择文件”、拖入文件，或粘贴路径后检查状态。
2. 等待检查通过，退出 PCL，然后点击“恢复下载”。
3. 显示“恢复完成”后，点击“启动 PCL”。需要撤销时，点击“还原原版”。

原文件备份为 `<程序名>.pcl-helper.bak`，校验记录为 `<程序名>.pcl-helper.json`，二者都保存在 PCL 程序旁，请一起保留。操作中禁止切换文件和关闭窗口，防止中断写入。遇到错误时可复制详情，排除问题后重新检查状态。

快捷键：`Ctrl+O` 选择文件，`F5` 检查状态。详情支持选中和复制。

## 一键查找

先读取桌面和开始菜单快捷方式，再搜索下载目录、桌面、文档和工具所在目录（最多深入三层子目录）。常用位置没有发现兼容版本时，会继续搜索本地固定磁盘。

扫描在后台进行，显示当前目录、已检查目录数和找到的 PCL 数量。可点击“取消搜索”；取消后保留已找到的结果。搜索期间也可以直接关闭窗口。它只读取文件及程序元数据，不会启动或修改找到的程序；使用搜索结果后仍需手动点击“恢复下载”。

通过 PCL 内部类型、限制字段及版本识别候选，排除同名的其他程序。改名后的 PCL 可通过原始文件名/产品信息或快捷方式目标识别。其他版本会标记为不支持，无法从结果列表启用修复。

扫描跳过网络路径、Windows 目录、回收站、重解析点（包括目录和文件链接）、开发缓存和无权访问的文件夹。深度扫描可能较慢；无法访问、移到未连接移动盘、改名且丢失原始产品信息的程序仍可能需要手动选择。当前只支持修复 PCL 2.99.0.0。

## 界面与封装

- 浅色卡片、统一圆角与间距、矢量图标和流程引导。
- 区分待操作、处理中、成功与失败，长路径和错误详情支持滚动。
- 文件检查通过后才启用恢复和启动，路径变化会立即撤销先前的检查结果。
- 无需安装、无附带广告或启动项；Mono.Cecil 和界面资源嵌入 EXE。
- 使用系统 DPI 感知；支持窗口缩放。跨屏 DPI 切换没有进行实机验证。

运行环境：Windows 10/11，.NET Framework 4.5 或更新版本（推荐系统内置的 4.8）。不依赖 .NET 8 SDK，不需要 Electron、WebView 或单独安装 Mono.Cecil。

## 构建与验证

Windows PowerShell 或 PowerShell 7 中运行：

```powershell
.\scripts\build.ps1
.\tests\search.ps1 -PclPath '你的原版 PCL.exe'
.\tests\ui.ps1 -PclPath '你的原版 PCL.exe'
.\tests\smoke.ps1 -PclPath '你的原版 PCL.exe'
.\scripts\package.ps1 -OutputDirectory '输出文件夹'
```

输出为 `release/PCL-Helper-v1.1.exe`。源码包已包含固定版本的 Mono.Cecil 0.11.6，解压即可离线构建。若从 GitHub 原始源码构建且没有该 DLL，构建脚本会从 NuGet 获取。

UI 验证使用真实 WPF 窗口和隔离 PCL 副本，覆盖按钮状态、异步写入、错误恢复、备份损坏拒绝和完整还原，并导出默认及最小尺寸的真实 WPF 渲染图。完整验证记录见 `docs/UI-VALIDATION.md`。

尚未完成完整 Minecraft 下载及所有整合包的端到端安装；渲染和集成测试不代表已人工点击验证全部桌面交互。

## 上游

原版下载：[GitHub Release](https://github.com/Turing-Mirror/PCL-Helper/releases/tag/v1) · [CNB Release](https://cnb.cool/Turing-Mirror/PCL-Helper/-/releases/tag/v1)

选择 PCL 的 exe，退出 PCL，点击“恢复下载”。需要撤销时点击“还原原版”。工具会在 exe 旁保留原文件备份与校验记录，请一起保存。

本工具由 Turing Mirror Software 独立开发，与 PCL 官方无关联。PCL 本体、资源及修改后的程序不随工具分发。

构建、实现与验证说明见 [docs/README.md](docs/README.md)。

