using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Win32;
using 工业传感器实时监控上位机.Models;
using 工业传感器实时监控上位机.Services;

namespace 工业传感器实时监控上位机.ViewModels;

/// <summary>
/// 报警日志视图模型
/// 展示报警记录列表，支持按传感器名称和时间范围筛选，支持导出为 CSV
/// </summary>
public class AlarmLogViewModel : ViewModelBase
{
    private readonly IAlarmService _alarmService;    // 报警服务
    private readonly IDataStorage _dataStorage;       // 数据持久化服务
    private readonly DialogService _dialogService;    // 弹窗服务

    // 筛选条件
    private string _filterSensorName = string.Empty;
    /// <summary>筛选：传感器名称（模糊匹配）</summary>
    public string FilterSensorName
    {
        get => _filterSensorName;
        set => SetProperty(ref _filterSensorName, value);
    }

    private DateTime _filterStart = DateTime.Today.AddDays(-1);
    /// <summary>筛选：开始时间（默认昨天）</summary>
    public DateTime FilterStart
    {
        get => _filterStart;
        set => SetProperty(ref _filterStart, value);
    }

    private DateTime _filterEnd = DateTime.Now;
    /// <summary>筛选：结束时间（默认现在）</summary>
    public DateTime FilterEnd
    {
        get => _filterEnd;
        set => SetProperty(ref _filterEnd, value);
    }

    /// <summary>显示的报警记录列表（绑定到 UI 列表控件）</summary>
    public ObservableCollection<AlarmRecord> DisplayRecords { get; } = new();

    private bool _hasRecords;
    /// <summary>是否有报警记录（控制空状态提示的显示）</summary>
    public bool HasRecords
    {
        get => _hasRecords;
        set => SetProperty(ref _hasRecords, value);
    }

    private int _recordCount;
    /// <summary>报警记录总数</summary>
    public int RecordCount
    {
        get => _recordCount;
        set => SetProperty(ref _recordCount, value);
    }

    /// <summary>刷新报警记录命令</summary>
    public ICommand RefreshCommand { get; }

    /// <summary>清除所有报警记录命令</summary>
    public ICommand ClearCommand { get; }

    /// <summary>导出报警记录到 CSV 命令</summary>
    public ICommand ExportCommand { get; }

    public AlarmLogViewModel(IAlarmService alarmService, IDataStorage dataStorage, DialogService dialogService)
    {
        _alarmService = alarmService;
        _dataStorage = dataStorage;
        _dialogService = dialogService;

        RefreshCommand = new RelayCommand(_ => RefreshRecords());
        ClearCommand = new RelayCommand(_ => ClearRecords());
        ExportCommand = new AsyncRelayCommand(_ => ExportAsync());

        // 监听新报警
        _alarmService.AlarmTriggered += _ => RefreshRecords();

        RefreshRecords();
    }

    /// <summary>
    /// 刷新报警记录列表（按当前筛选条件从报警服务获取数据）
    /// 通过 Dispatcher 调度到 UI 线程，避免后台线程触发时修改 ObservableCollection 导致异常
    /// </summary>
    private void RefreshRecords()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => RefreshRecordsCore());
        }
        else
        {
            RefreshRecordsCore();
        }
    }

    /// <summary>
    /// 实际刷新逻辑（始终在 UI 线程执行）
    /// </summary>
    private void RefreshRecordsCore()
    {
        DisplayRecords.Clear();

        var records = _alarmService.FilterAlarms(
            string.IsNullOrWhiteSpace(FilterSensorName) ? null : FilterSensorName,
            FilterStart, FilterEnd);

        foreach (var record in records.OrderByDescending(r => r.AlarmTime))
        {
            DisplayRecords.Add(record);
        }

        HasRecords = DisplayRecords.Count > 0;
        RecordCount = DisplayRecords.Count;
    }

    /// <summary>
    /// 清除所有报警记录（需用户确认）
    /// </summary>
    private void ClearRecords()
    {
        if (_dialogService.ShowConfirm("确定清除所有报警记录？"))
        {
            _alarmService.ClearAlarms();
            RefreshRecords();
        }
    }

    /// <summary>
    /// 导出当前筛选条件下的报警记录为 CSV 文件
    /// </summary>
    private async Task ExportAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV 文件 (*.csv)|*.csv",
            FileName = $"报警日志_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var records = _alarmService.FilterAlarms(
                    string.IsNullOrWhiteSpace(FilterSensorName) ? null : FilterSensorName,
                    FilterStart, FilterEnd);
                await _dataStorage.ExportAlarmLogAsync(records, dialog.FileName);
                _dialogService.ShowInfo($"报警日志已导出至：{dialog.FileName}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"导出失败: {ex.Message}");
            }
        }
    }
}
