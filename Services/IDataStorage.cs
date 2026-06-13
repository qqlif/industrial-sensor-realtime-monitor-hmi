using 工业传感器实时监控上位机.Models;

namespace 工业传感器实时监控上位机.Services;

/// <summary>
/// 数据持久化接口
/// 提供传感器数据、报警记录和配置文件的读写操作
/// 实现类可选 CSV 文件存储或 SQLite 数据库存储
/// </summary>
public interface IDataStorage
{
    /// <summary>保存单条传感器数据</summary>
    Task SaveSensorDataAsync(SensorData data);

    /// <summary>批量保存传感器数据（提高高频采集时的写入性能）</summary>
    Task SaveSensorDataBatchAsync(IEnumerable<SensorData> dataList);

    /// <summary>导出指定时间段的传感器数据到 CSV 文件</summary>
    Task ExportSensorDataAsync(DateTime start, DateTime end, string filePath);

    /// <summary>保存单条报警记录</summary>
    Task SaveAlarmRecordAsync(AlarmRecord record);

    /// <summary>导出报警日志列表到 CSV 文件</summary>
    Task ExportAlarmLogAsync(IEnumerable<AlarmRecord> records, string filePath);

    /// <summary>从持久化存储加载全局配置</summary>
    Task<GlobalConfig> LoadConfigAsync();

    /// <summary>保存全局配置到持久化存储</summary>
    Task SaveConfigAsync(GlobalConfig config);

    /// <summary>强制刷写缓冲区（在导出/关闭前调用确保数据完整性）</summary>
    Task FlushAsync();
}
