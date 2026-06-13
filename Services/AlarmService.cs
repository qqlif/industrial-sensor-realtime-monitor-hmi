using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Data;
using 工业传感器实时监控上位机.Models;

namespace 工业传感器实时监控上位机.Services;

/// <summary>
/// 报警服务实现
/// 检查数据是否超阈值，记录报警日志
/// 线程安全：ObservableCollection 操作通过 UI 调度器执行
/// </summary>
public class AlarmService : IAlarmService
{
    private readonly IDataStorage _storage;      // 数据持久化服务，用于存储报警记录
    private readonly object _lock = new();        // 去重检查的线程锁

    /// <summary>报警记录集合（绑定到 UI 列表）</summary>
    public ObservableCollection<AlarmRecord> AlarmRecords { get; } = new();

    /// <summary>报警触发事件，通知外部订阅者（如音效播放、弹窗显示）</summary>
    public event Action<AlarmRecord>? AlarmTriggered;

    public AlarmService(IDataStorage storage)
    {
        _storage = storage;

        // 启用集合跨线程同步，允许后台线程安全读写 AlarmRecords
        BindingOperations.EnableCollectionSynchronization(AlarmRecords, _lock);
    }

    /// <summary>
    /// 检查传感器数据是否超出配置的上下限阈值
    /// 若超限则创建报警记录并触发后续处理
    /// </summary>
    /// <param name="data">当前传感器采样数据</param>
    /// <param name="config">该传感器的报警配置（上下限）</param>
    /// <returns>是否触发了报警</returns>
    public bool CheckAlarm(SensorData data, SensorConfig config)
    {
        // 检查是否超出上限
        if (data.Value > config.AlarmUpper)
        {
            var record = new AlarmRecord
            {
                SensorName = data.SensorName,
                SensorType = data.SensorType,
                AlarmTime = data.Timestamp,
                CurrentValue = data.Value,
                Threshold = config.AlarmUpper,
                AlarmType = "上限超限",
                Unit = data.Unit
            };
            AddAlarmRecord(record);
            return true;
        }

        // 检查是否超出下限
        if (data.Value < config.AlarmLower)
        {
            var record = new AlarmRecord
            {
                SensorName = data.SensorName,
                SensorType = data.SensorType,
                AlarmTime = data.Timestamp,
                CurrentValue = data.Value,
                Threshold = config.AlarmLower,
                AlarmType = "下限超限",
                Unit = data.Unit
            };
            AddAlarmRecord(record);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 添加报警记录（含 5 秒内同传感器同类型去重）
    /// 通过 EnableCollectionSynchronization 确保线程安全
    /// </summary>
    private void AddAlarmRecord(AlarmRecord record)
    {
        // 去重检查：同一传感器同一类型 5 秒内不重复报警
        lock (_lock)
        {
            var lastRecord = AlarmRecords.LastOrDefault(r =>
                r.SensorName == record.SensorName &&
                r.AlarmType == record.AlarmType &&
                (record.AlarmTime - r.AlarmTime).TotalSeconds < 5);

            if (lastRecord != null) return;

            // 保持最多 1000 条记录，超出则移除最旧的
            if (AlarmRecords.Count >= 1000)
            {
                AlarmRecords.RemoveAt(0);
            }

            AlarmRecords.Add(record);
        }

        // 异步持久化报警记录到存储（后台线程安全，不阻塞 UI）
        _ = Task.Run(async () =>
        {
            try
            {
                await _storage.SaveAlarmRecordAsync(record);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存报警记录失败: {ex.Message}");
            }
        });

        // 触发报警事件，通知订阅者
        AlarmTriggered?.Invoke(record);
    }

    /// <summary>
    /// 清除所有报警记录（线程安全，通过 BindingOperations 同步）
    /// </summary>
    public void ClearAlarms()
    {
        lock (_lock)
        {
            AlarmRecords.Clear();
        }
    }

    /// <summary>
    /// 按传感器名称和时间范围筛选报警记录
    /// </summary>
    /// <param name="sensorName">传感器名称（支持模糊匹配）</param>
    /// <param name="start">筛选起始时间</param>
    /// <param name="end">筛选结束时间（自动扩展到当天最后一刻）</param>
    /// <returns>符合条件的报警记录列表</returns>
    public IEnumerable<AlarmRecord> FilterAlarms(string? sensorName = null, DateTime? start = null, DateTime? end = null)
    {
        lock (_lock)
        {
            var query = AlarmRecords.AsEnumerable();

            if (!string.IsNullOrEmpty(sensorName))
                query = query.Where(r => r.SensorName.Contains(sensorName));

            if (start.HasValue)
                query = query.Where(r => r.AlarmTime >= start.Value);

            if (end.HasValue)
            {
                // DatePicker 只绑定日期（00:00:00），需扩展到当天最后一刻
                var adjustedEnd = end.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(r => r.AlarmTime <= adjustedEnd);
            }

            return query.ToList();
        }
    }
}
