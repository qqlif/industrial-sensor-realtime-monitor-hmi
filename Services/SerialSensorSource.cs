using System.IO.Ports;
using 工业传感器实时监控上位机.Models;

namespace 工业传感器实时监控上位机.Services;

/// <summary>
/// 串口传感器数据源
/// 通过串口接收真实传感器数据，支持可配置的串口参数（波特率、校验位、数据位、停止位）
/// 数据格式约定：JSON 行协议，每行一个 SensorData JSON 对象，以换行符分隔
/// </summary>
public class SerialSensorSource : ISensorSource, IDisposable
{
    private readonly GlobalConfig _config;      // 全局配置
    private SerialPort? _serialPort;             // 串口对象
    private CancellationTokenSource? _cts;       // 取消标记
    private Task? _readTask;                     // 后台读取任务
    private bool _disposed;                      // 是否已释放

    /// <summary>数据源名称（包含串口号）</summary>
    public string SourceName => $"串口数据源 ({_portName})";

    /// <summary>串口是否已打开并运行</summary>
    public bool IsRunning => _serialPort?.IsOpen == true;

    /// <summary>数据到达事件</summary>
    public event Action<SensorData>? DataReceived;

    /// <summary>状态变化事件</summary>
    public event Action<string>? StatusChanged;

    /// <summary>异常事件</summary>
    public event Action<Exception>? ErrorOccurred;

    private string _portName = "COM1";    // 串口名称
    private int _baudRate = 9600;          // 波特率
    private Parity _parity = Parity.None;  // 校验位
    private int _dataBits = 8;             // 数据位
    private StopBits _stopBits = StopBits.One; // 停止位

    /// <summary>串口名称（如 COM1、COM3）</summary>
    public string PortName
    {
        get => _portName;
        set => _portName = value;
    }

    /// <summary>波特率（如 9600、115200）</summary>
    public int BaudRate
    {
        get => _baudRate;
        set => _baudRate = value;
    }

    /// <summary>校验位（None/Odd/Even/Mark/Space）</summary>
    public Parity Parity
    {
        get => _parity;
        set => _parity = value;
    }

    /// <summary>数据位（通常为 7 或 8）</summary>
    public int DataBits
    {
        get => _dataBits;
        set => _dataBits = value;
    }

    /// <summary>停止位（One/OnePointFive/Two）</summary>
    public StopBits StopBits
    {
        get => _stopBits;
        set => _stopBits = value;
    }

    public SerialSensorSource(GlobalConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// 获取系统中可用的串口列表
    /// </summary>
    /// <returns>可用串口名称数组（如 ["COM1", "COM3"]）</returns>
    public static string[] GetAvailablePorts()
    {
        return SerialPort.GetPortNames();
    }

    /// <summary>
    /// 启动串口数据源
    /// 打开串口并启动后台读取任务
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsRunning) return Task.CompletedTask;

        try
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // 创建并配置串口对象
            _serialPort = new SerialPort(_portName, _baudRate, _parity, _dataBits, _stopBits)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000,
                NewLine = "\n",
                Encoding = System.Text.Encoding.UTF8
            };

            _serialPort.Open();
            StatusChanged?.Invoke($"串口 {_portName} 已打开");

            // 启动后台读取循环
            _readTask = Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"串口打开失败: {ex.Message}");
            ErrorOccurred?.Invoke(ex);
            throw;
        }
    }

    /// <summary>
    /// 停止串口数据源
    /// 取消读取任务并关闭串口
    /// </summary>
    public void Stop()
    {
        try
        {
            _cts?.Cancel();

            if (_serialPort?.IsOpen == true)
            {
                _serialPort.Close();
            }

            StatusChanged?.Invoke("串口数据源已停止");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"停止串口数据源异常: {ex.Message}");
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _serialPort?.Dispose();
            _serialPort = null;
        }
    }

    /// <summary>
    /// 后台串口数据读取循环
    /// 按行读取 JSON 格式的传感器数据并反序列化为 SensorData 对象
    /// 读取超时正常跳过，其他异常短暂延迟后重试
    /// </summary>
    private async Task ReadLoopAsync(CancellationToken token)
    {
        StatusChanged?.Invoke("串口数据源运行中");

        try
        {
            while (!token.IsCancellationRequested && _serialPort?.IsOpen == true)
            {
                try
                {
                    // 读取一行数据（JSON 格式，每行一个 SensorData）
                    var line = await Task.Run(() => _serialPort.ReadLine() ?? string.Empty, token);

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // 尝试将 JSON 行解析为 SensorData 对象
                    var data = System.Text.Json.JsonSerializer.Deserialize<SensorData>(line);
                    if (data != null)
                    {
                        data.Timestamp = DateTime.Now;
                        DataReceived?.Invoke(data);
                    }
                }
                catch (TimeoutException)
                {
                    // 读取超时是正常的，继续循环等待下一行
                    continue;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SerialSensorSource] 读取数据异常: {ex.Message}");
                    // 短暂的延迟后重试，避免 CPU 空转
                    await Task.Delay(100, token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
            StatusChanged?.Invoke($"串口数据源异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
