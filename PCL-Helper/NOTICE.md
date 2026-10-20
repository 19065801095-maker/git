# 来源与修改声明

本工具基于以下开源项目修改：

- 原项目：PCL-Helper
- 原项目地址：https://github.com/Turing-Mirror/PCL-Helper
- 原作者：Turing Mirror Software / 图灵镜
- 基线提交：b2acb231351c8b8a4cab20d3fabe566fa346dbcf
- 原项目许可证：MIT，完整原文保留在 LICENSE 中。

本修改版的主要变化：

1. 优化原生 WPF 界面、图标、布局、按钮状态、进度和错误反馈。
2. 增加一键查找 PCL，搜索常用位置和快捷方式，必要时回退到本地固定磁盘；支持取消及多个结果选择。
3. 增加界面和搜索回归检查，完善单文件 EXE、便携 ZIP 及离线源码包。

原补丁核心 src/Patcher.cs 未作修改。原作者的版权声明和 MIT 许可证继续适用，不因本次修改而移除或替换。

本修改版并非上游作者或 PCL 官方发布的版本，不代表其认可或担保。PCL 本体和修改后的 PCL 程序不随本工具分发。

本工具使用 Mono.Cecil 0.11.6，其 MIT 版权和许可证保留在 docs/Mono.Cecil.LICENSE.txt 中，也嵌入 EXE；便携包包含该许可证。
