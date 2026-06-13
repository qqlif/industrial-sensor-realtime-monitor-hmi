# C# WPF MVVM 模式技能

## 描述
WPF MVVM 架构的标准实现模式，包括 ViewModelBase、RelayCommand、AsyncRelayCommand、依赖注入注册。

## 核心文件模板

### ViewModelBase.cs
```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace YourProject.ViewModels;

public class ViewModelBase : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;

	protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value)) return false;
		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}
}
```

### RelayCommand.cs
```csharp
public class RelayCommand : ICommand
{
	private readonly Action<object?> _execute;
	private readonly Func<object?, bool>? _canExecute;

	public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
	{
		_execute = execute ?? throw new ArgumentNullException(nameof(execute));
		_canExecute = canExecute;
	}

	public event EventHandler? CanExecuteChanged
	{
		add => CommandManager.RequerySuggested += value;
		remove => CommandManager.RequerySuggested -= value;
	}

	public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
	public void Execute(object? parameter) => _execute(parameter);
}
```

### AsyncRelayCommand.cs
```csharp
public class AsyncRelayCommand : ICommand
{
	private readonly Func<object?, Task> _execute;
	private readonly Func<object?, bool>? _canExecute;
	private bool _isExecuting;

	public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
	{
		_execute = execute ?? throw new ArgumentNullException(nameof(execute));
		_canExecute = canExecute;
	}

	public event EventHandler? CanExecuteChanged
	{
		add => CommandManager.RequerySuggested += value;
		remove => CommandManager.RequerySuggested -= value;
	}

	public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

	public async void Execute(object? parameter)
	{
		if (_isExecuting) return;
		_isExecuting = true;
		CommandManager.InvalidateRequerySuggested();
		try { await _execute(parameter); }
		finally { _isExecuting = false; CommandManager.InvalidateRequerySuggested(); }
	}
}
```

### DI 注册模式 (App.xaml.cs)
```csharp
_host = Host.CreateDefaultBuilder()
	.ConfigureServices((context, services) =>
	{
		services.AddSingleton<GlobalConfig>(...);
		services.AddSingleton<IService, ServiceImpl>();
		services.AddTransient<ViewModel>();
		services.AddTransient<View>();
	})
	.Build();
```

## 使用场景
- 新建 WPF 项目时复制这些模板
- 确保所有 ViewModel 继承 ViewModelBase
- 所有命令使用 RelayCommand / AsyncRelayCommand
- 所有服务通过 DI 容器注册
