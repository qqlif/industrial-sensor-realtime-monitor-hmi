using System.Collections.ObjectModel;
using 工业传感器实时监控上位机.Models;

namespace 工业传感器实时监控上位机.Services;

/// <summary>
/// 报警服务接口
/// 提供报警检查、记录管理、事件通知和筛选查询功能
/// </summary>
public interface IAlarmService
{
    /// <summary>报警记录列表（线程安全的可观察集合，供 UI 绑定）</summary>
    ObservableCollection<AlarmRecord> AlarmRecords { get; }

    /// <summary>检查传感器数据是否超出阈值，若超限则生成报警记录</summary>
    bool CheckAlarm(SensorData data, SensorConfig config);

    /// <summary>报警触发事件，新报警产生时通知订阅者（如音效播放、弹窗等）</summary>
    event Action<AlarmRecord>? AlarmTriggered;

    /// <summary>清除所有报警记录</summary>
    void ClearAlarms();

    /// <summary>按传感器名称和时间范围筛选报警记录</summary>
    IEnumerable<AlarmRecord> FilterAlarms(string? sensorName = null, DateTime? start = null, DateTime? end = null);
}
