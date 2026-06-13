using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace 工业传感器实时监控上位机.ViewModels;

/// <summary>
/// ViewModel 基类，提供 INotifyPropertyChanged 实现
/// 所有 ViewModel 继承此类以获得属性变更通知能力
/// </summary>
public class ViewModelBase : INotifyPropertyChanged
{
    /// <summary>属性变更事件，WPF 绑定引擎通过此事件更新 UI</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>触发属性变更通知</summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 设置属性值，在值变化时触发通知
    /// </summary>
    /// <typeparam name="T">属性类型</typeparam>
    /// <param name="field">字段引用</param>
    /// <param name="value">新值</param>
    /// <param name="propertyName">属性名（编译器自动填充）</param>
    /// <returns>值是否发生变化</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

/// <summary>
/// 简化 ICommand 实现
/// 将按钮点击等操作绑定到委托方法
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;       // 执行逻辑
    private readonly Func<object?, bool>? _canExecute; // 是否可执行判断

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

/// <summary>
/// 异步 RelayCommand 实现
/// 支持 async Task 执行逻辑，自动管理执行状态，执行期间禁止重复触发
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;       // 异步执行逻辑
    private readonly Func<object?, bool>? _canExecute;   // 是否可执行判断
    private bool _isExecuting;                            // 是否正在执行

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

    /// <summary>执行中返回 false 以防止重复触发</summary>
    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

    /// <summary>
    /// 异步执行命令，执行期间自动锁定
    /// async void 的异常会直接崩溃进程，必须在顶层捕获
    /// </summary>
    public async void Execute(object? parameter)
    {
        if (_isExecuting) return;
        _isExecuting = true;
        CommandManager.InvalidateRequerySuggested();

        try
        {
            await _execute(parameter);
        }
        catch (Exception ex)
        {
            // 防止 async void 未捕获异常导致进程崩溃
            System.Diagnostics.Debug.WriteLine($"[AsyncRelayCommand] 命令执行异常: {ex.Message}");
        }
        finally
        {
            _isExecuting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
