using System.Collections.ObjectModel;
using System.Windows.Input;
using 工业传感器实时监控上位机.Services;

namespace 工业传感器实时监控上位机.ViewModels;

/// <summary>
/// 数据统计视图模型
/// 展示传感器历史数据的统计分析结果（最大值、最小值、平均值、标准差、趋势等）
/// 提供快捷时间选择（最近1小时、今天、最近7天）
/// </summary>
public class StatisticsViewModel : ViewModelBase
{
    private readonly DataStatisticsService _statisticsService;  // 数据统计服务

    // 时间范围
    private DateTime _startTime = DateTime.Today.AddDays(-1);
    /// <summary>统计起始时间</summary>
    public DateTime StartTime
    {
        get => _startTime;
        set => SetProperty(ref _startTime, value);
    }

    private DateTime _endTime = DateTime.Now;
    /// <summary>统计结束时间</summary>
    public DateTime EndTime
    {
        get => _endTime;
        set => SetProperty(ref _endTime, value);
    }

    /// <summary>统计结果列表（绑定到 UI 列表控件）</summary>
    public ObservableCollection<SensorStatistics> StatisticsResults { get; } = new();

    private bool _hasResults;
    /// <summary>是否有统计结果（控制结果显示区域可见性）</summary>
    public bool HasResults
    {
        get => _hasResults;
        set => SetProperty(ref _hasResults, value);
    }

    private bool _isCalculating;
    /// <summary>是否正在计算中（控制加载动画显示）</summary>
    public bool IsCalculating
    {
        get => _isCalculating;
        set => SetProperty(ref _isCalculating, value);
    }

    private string _statusText = "就绪";
    /// <summary>状态文本（就绪/计算中/完成/失败）</summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    /// <summary>开始统计命令</summary>
    public ICommand CalculateCommand { get; }

    /// <summary>快捷选择：最近1小时</summary>
    public ICommand QuickLastHourCommand { get; }

    /// <summary>快捷选择：今天</summary>
    public ICommand QuickTodayCommand { get; }

    /// <summary>快捷选择：最近7天</summary>
    public ICommand QuickLastWeekCommand { get; }

    public StatisticsViewModel(DataStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;

        CalculateCommand = new AsyncRelayCommand(_ => CalculateAsync());
        QuickLastHourCommand = new RelayCommand(_ =>
        {
            EndTime = DateTime.Now;
            StartTime = EndTime.AddHours(-1);
        });
        QuickTodayCommand = new RelayCommand(_ =>
        {
            StartTime = DateTime.Today;
            EndTime = DateTime.Now;
        });
        QuickLastWeekCommand = new RelayCommand(_ =>
        {
            EndTime = DateTime.Now;
            StartTime = EndTime.AddDays(-7);
        });
    }

    /// <summary>
    /// 异步执行统计计算
    /// 调用统计服务计算指定时间范围内所有传感器的统计数据
    /// </summary>
    private async Task CalculateAsync()
    {
        IsCalculating = true;
        StatusText = "正在计算统计...";
        StatisticsResults.Clear();

        try
        {
            var results = await _statisticsService.CalculateAllStatisticsAsync(StartTime, EndTime);

            foreach (var stat in results)
            {
                StatisticsResults.Add(stat);
            }

            HasResults = StatisticsResults.Count > 0;
            StatusText = HasResults
                ? $"统计完成，共 {StatisticsResults.Count} 个传感器"
                : "指定时间范围内无数据";
        }
        catch (Exception ex)
        {
            StatusText = $"统计失败: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[StatisticsViewModel] {ex.Message}");
        }
        finally
        {
            IsCalculating = false;
        }
    }
}
