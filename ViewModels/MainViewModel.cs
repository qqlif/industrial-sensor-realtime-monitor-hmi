using System.Windows.Input;

namespace 工业传感器实时监控上位机.ViewModels;

/// <summary>
/// 主视图模型
/// 管理页面导航和全局状态，通过命令切换实时监控、参数配置、报警日志等页面
/// </summary>
public class MainViewModel : ViewModelBase, IDisposable
{
    private readonly MonitorViewModel _monitorViewModel;    // 实时监控视图模型
    private readonly ConfigViewModel _configViewModel;      // 参数配置视图模型
    private readonly AlarmLogViewModel _alarmLogViewModel;  // 报警日志视图模型

    private ViewModelBase _currentViewModel;
    /// <summary>当前显示的视图模型（绑定到主内容区域）</summary>
    public ViewModelBase CurrentViewModel
    {
        get => _currentViewModel;
        set => SetProperty(ref _currentViewModel, value);
    }

    private string _currentPageName = "实时监控";
    /// <summary>当前页面名称（用于标题或面包屑显示）</summary>
    public string CurrentPageName
    {
        get => _currentPageName;
        set => SetProperty(ref _currentPageName, value);
    }

    /// <summary>导航到实时监控页面命令</summary>
    public ICommand NavigateToMonitorCommand { get; }

    /// <summary>导航到参数配置页面命令</summary>
    public ICommand NavigateToConfigCommand { get; }

    /// <summary>导航到报警日志页面命令</summary>
    public ICommand NavigateToAlarmLogCommand { get; }

    /// <summary>实时监控视图模型（供父级访问）</summary>
    public MonitorViewModel MonitorVM => _monitorViewModel;

    public MainViewModel(MonitorViewModel monitorViewModel, ConfigViewModel configViewModel,
        AlarmLogViewModel alarmLogViewModel)
    {
        _monitorViewModel = monitorViewModel;
        _configViewModel = configViewModel;
        _alarmLogViewModel = alarmLogViewModel;

        _currentViewModel = _monitorViewModel;

        NavigateToMonitorCommand = new RelayCommand(_ => NavigateTo(_monitorViewModel, "实时监控"));
        NavigateToConfigCommand = new RelayCommand(_ => NavigateTo(_configViewModel, "参数配置"));
        NavigateToAlarmLogCommand = new RelayCommand(_ => NavigateTo(_alarmLogViewModel, "报警日志"));
    }

    /// <summary>
    /// 导航到指定页面
    /// </summary>
    /// <param name="vm">目标视图模型</param>
    /// <param name="pageName">页面名称</param>
    private void NavigateTo(ViewModelBase vm, string pageName)
    {
        CurrentViewModel = vm;
        CurrentPageName = pageName;
    }

    /// <summary>
    /// 释放所有子视图模型资源
    /// </summary>
    public void Dispose()
    {
        _monitorViewModel.Dispose();
        (_configViewModel as IDisposable)?.Dispose();
        (_alarmLogViewModel as IDisposable)?.Dispose();
    }
}
