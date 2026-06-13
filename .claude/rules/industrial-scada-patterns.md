---
description: "工业上位机（SCADA）开发模式规范"
globs: ["**/*.cs", "**/*.xaml"]
alwaysApply: false
---

# 工业上位机（SCADA）开发模式规范

## 数据采集
- 数据源抽象为 ISensorSource 接口
- 支持模拟/串口/TCP 等多种实现
- 使用 CancellationToken 控制启停
- 后台 Task 运行采集循环，不阻塞 UI

## 实时监控
- 仪表盘显示最新数据值
- 实时曲线使用 ScottPlot
- 数据缓存使用 ConcurrentDictionary + lock
- UI 更新通过 Dispatcher.Invoke

## 报警系统
- 每个传感器独立配置阈值
- 报警去重（5 秒内同类型不重复）
- 报警记录持久化
- UI 报警状态通过 Binding + Converter 控制可见性

## 配置管理
- 所有可调参数外部化到 JSON
- 配置加载失败使用默认值
- 配置修改后保存到文件

## 数据导出
- 传感器数据按日期分文件
- 支持按时间范围筛选导出
- 报警日志独立文件
- CSV 格式，UTF-8 编码

## 可靠性
- 所有外部操作 try-catch
- 异常后更新 UI 状态提示
- 数据源异常可重试
- 文件写入异常不影响主流程
