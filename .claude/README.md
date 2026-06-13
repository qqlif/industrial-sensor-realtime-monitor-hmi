# C# WPF 工业上位机开发能力包

## 概述
本能力包将 AI Agent 在工业传感器实时监控上位机项目中积累的编程能力系统化为可复用的 ECC 风格能力包。以后所有 C# WPF 项目直接复制 `.claude/` 目录即可应用。

## 目录结构
```
.claude/
├── CLAUDE.md                 # 入口文件，定义项目身份和核心原则
├── README.md                 # 本文件
├── agents/                   # 专用子代理
│   ├── csharp-architect.md       # 系统架构师
│   ├── csharp-reviewer.md        # 代码审查
│   ├── csharp-debugger.md        # 调试修复
│   └── csharp-tdd-guide.md       # TDD 指导
├── skills/                   # 领域技能
│   ├── csharp-wpf-mvvm-patterns/     # MVVM 模式
│   ├── csharp-wpf-exception-handling/ # 异常处理
│   ├── csharp-wpf-real-time-chart/   # 实时曲线(ScottPlot)
│   ├── csharp-wpf-data-persistence/  # 数据持久化
│   ├── csharp-wpf-alarm-system/      # 报警系统
│   └── csharp-wpf-agent-memory/      # Agent 记忆(MCP)
├── rules/                    # 编码规则
│   ├── csharp-coding-style.md       # C# 编码风格
│   ├── wpf-mvvm-rules.md            # WPF MVVM 规则
│   └── industrial-scada-patterns.md # 工业上位机模式
└── templates/                # 项目模板
	├── csharp-wpf-starter/          # 项目启动模板
	├── config-appsettings.json      # 配置模板
	└── csproj-template.xml          # 项目文件模板
```

## 使用方法

### 新项目
1. 复制 `.claude/` 目录到新项目根目录
2. AI Agent 自动加载 CLAUDE.md 获得项目上下文
3. 按需调用 agents/ 中的子代理处理特定任务
4. 参考 skills/ 中的技能文档实现标准功能
5. rules/ 中的编码规则自动生效

### 已有项目
1. 复制 `.claude/` 目录到项目根目录
2. AI Agent 自动识别并应用规则和技能

## 子代理速查

| 代理 | 命令/引用 | 用途 |
|------|----------|------|
| csharp-architect | 引用 csharp-architect | 设计系统架构、接口、模块划分 |
| csharp-reviewer | 引用 csharp-reviewer | 代码审查、MVVM 正确性检查 |
| csharp-debugger | 引用 csharp-debugger | 异常分析、根因定位、修复 |
| csharp-tdd-guide | 引用 csharp-tdd-guide | 测试驱动开发指导 |

## 技能速查

| 技能 | 用途 |
|------|------|
| csharp-wpf-mvvm-patterns | ViewModelBase、RelayCommand、DI 注册模板 |
| csharp-wpf-exception-handling | Dispose 模式、CancellationToken、异常三原则 |
| csharp-wpf-real-time-chart | ScottPlot 实时曲线实现 |
| csharp-wpf-data-persistence | CSV/SQLite 持久化方案 |
| csharp-wpf-alarm-system | 多传感器阈值报警系统 |
| csharp-wpf-agent-memory | Agent 跨会话记忆持久化(MCP) |

## 技术栈
- .NET 8 + WPF + C# 12
- MVVM + DI (Microsoft.Extensions.Hosting)
- ScottPlot.WPF (实时曲线)
- MaterialDesignThemes (UI 样式)
- Microsoft.Data.Sqlite (持久化)
- MCP Protocol (Agent 记忆)
