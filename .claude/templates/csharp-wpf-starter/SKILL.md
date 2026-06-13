# C# WPF 项目启动模板

## 描述
从零开始创建 C# WPF 工业上位机项目的标准步骤。

## 1. 创建项目
```bash
dotnet new wpf -n YourProjectName
cd YourProjectName
```

## 2. 安装依赖包
```bash
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package MaterialDesignThemes
dotnet add package ScottPlot.WPF
dotnet add package Microsoft.Data.Sqlite
```

## 3. 创建目录结构
```
YourProjectName/
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs
├── Views/
│   ├── MonitorView.xaml / .cs
│   ├── ConfigView.xaml / .cs
│   └── AlarmLogView.xaml / .cs
├── ViewModels/
│   ├── MainViewModel.cs
│   ├── MonitorViewModel.cs
│   ├── ConfigViewModel.cs
│   ├── AlarmLogViewModel.cs
│   └── ViewModelBase.cs
├── Models/
│   ├── SensorData.cs
│   ├── SensorConfig.cs
│   └── AlarmRecord.cs
├── Services/
│   ├── ISensorSource.cs / MockSensorSource.cs
│   ├── IDataStorage.cs / CsvDataStorage.cs
│   ├── IAlarmService.cs / AlarmService.cs
│   ├── DialogService.cs
│   ├── AgentMemoryService.cs
│   └── McpServerService.cs
├── config/
│   └── appsettings.json
└── output/
```

## 4. 配置 DI (App.xaml.cs)
参考 `skills/csharp-wpf-mvvm-patterns` 中的 DI 注册模式。

## 5. 实现核心接口
- ISensorSource → 数据源抽象
- IDataStorage → 数据持久化
- IAlarmService → 报警服务

## 6. 实现 ViewModel
- 继承 ViewModelBase
- 使用 RelayCommand / AsyncRelayCommand
- 构造函数注入服务

## 7. 实现 View
- XAML 数据绑定
- Code-Behind 初始化 ScottPlot 等第三方控件
- 注册 BooleanToVisibilityConverter

## 8. 配置 appsettings.json
参考 `config/appsettings.json` 模板。
