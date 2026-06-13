using 工业传感器实时监控上位机.Models;

namespace 工业传感器实时监控上位机.Services;

/// <summary>
/// 传感器数据源抽象接口
/// 预留扩展：可实现串口、TCP、模拟等不同数据源
/// </summary>
public interface ISensorSource
{
    /// <summary>数据源名称（如"模拟数据源"、"串口数据源"、"TCP 数据源"）</summary>
    string SourceName { get; }

    /// <summary>是否正在运行</summary>
    bool IsRunning { get; }

    /// <summary>启动数据采集（异步）</summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>停止数据采集</summary>
    void Stop();

    /// <summary>数据到达事件，收到新传感器数据时触发</summary>
    event Action<SensorData>? DataReceived;

    /// <summary>数据源状态变化事件（如启动、运行、停止、异常等）</summary>
    event Action<string>? StatusChanged;

    /// <summary>异常事件，数据源发生未处理异常时触发</summary>
    event Action<Exception>? ErrorOccurred;
}
