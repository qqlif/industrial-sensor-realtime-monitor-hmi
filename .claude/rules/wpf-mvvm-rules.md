---
description: "WPF MVVM 架构规则，适用于所有 WPF 项目"
globs: ["**/*.xaml", "**/*.cs"]
alwaysApply: true
---

# WPF MVVM 架构规则

## View 层 (.xaml)
- XAML 中只做数据绑定，不写业务逻辑
- Code-Behind 只包含 UI 初始化、动画、第三方控件交互
- 使用 Binding 而非 x:Name + code-behind 赋值
- Converter 在 UserControl.Resources 中声明
- 使用 Command 绑定替代 Click 事件

## ViewModel 层
- 继承 ViewModelBase（实现 INotifyPropertyChanged）
- 所有可绑定属性使用 SetProperty 模式
- 使用 RelayCommand / AsyncRelayCommand
- 通过构造函数注入所有依赖
- 不引用任何 WPF 程序集（PresentationFramework 等）

## Model 层
- 纯数据类，无行为
- 使用 JsonPropertyName 属性标记序列化字段
- 实现 IEquatable 用于比较

## 依赖注入
- 所有服务注册在 App.xaml.cs 的 ConfigureServices 中
- Singleton：无状态服务、配置、数据源
- Transient：ViewModel、View（每次导航新建）
- 禁止 ServiceLocator 反模式

## 数据绑定
- 使用 StringFormat 格式化显示
- Converter 实现 IValueConverter
- 列表使用 ObservableCollection
- 大数据量使用 VirtualizingStackPanel
