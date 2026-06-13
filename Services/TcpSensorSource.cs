using System.IO;
using System.Net.Sockets;
using System.Text;
using 工业传感器实时监控上位机.Models;

namespace 工业传感器实时监控上位机.Services;

/// <summary>
/// TCP 网络传感器数据源
/// 通过 TCP 连接接收远程传感器数据，支持可配置的主机和端口
/// 数据格式约定：JSON 行协议，每行一个 SensorData JSON 对象，以换行符 \n 分隔
/// 适用于与远程设备或 PLC 通过以太网通信
/// </summary>
public class TcpSensorSource : ISensorSource, IDisposable
{
    private readonly GlobalConfig _config;      // 全局配置
    private TcpClient? _tcpClient;               // TCP 客户端
    private NetworkStream? _networkStream;        // 网络数据流
    private CancellationTokenSource? _cts;        // 取消标记
    private Task? _readTask;                      // 后台读取任务
    private bool _disposed;                       // 是否已释放

    /// <summary>数据源名称（包含主机和端口）</summary>
    public string SourceName => $"TCP 数据源 ({_host}:{_port})";

    /// <summary>TCP 连接是否已建立</summary>
    public bool IsRunning => _tcpClient?.Connected == true;

    /// <summary>数据到达事件</summary>
    public event Action<SensorData>? DataReceived;

    /// <summary>状态变化事件</summary>
    public event Action<string>? StatusChanged;

    /// <summary>异常事件</summary>
    public event Action<Exception>? ErrorOccurred;

    private string _host = "127.0.0.1";  // 默认本地地址
    private int _port = 502;              // 默认 Modbus TCP 端口

    /// <summary>服务器主机地址（IP 或域名）</summary>
    public string Host
    {
        get => _host;
        set => _host = value;
    }

    /// <summary>服务器端口号</summary>
    public int Port
    {
        get => _port;
        set => _port = value;
    }

    public TcpSensorSource(GlobalConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// 启动 TCP 数据源
    /// 建立 TCP 连接（带 5 秒超时），启动后台数据读取任务
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsRunning) return;

        try
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _tcpClient = new TcpClient();
            StatusChanged?.Invoke($"正在连接 TCP {_host}:{_port}...");

            // 异步连接，带 5 秒超时控制
            using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, connectCts.Token);

            await _tcpClient.ConnectAsync(_host, _port, linkedCts.Token);

            _networkStream = _tcpClient.GetStream();
            _networkStream.ReadTimeout = 3000;

            StatusChanged?.Invoke($"TCP {_host}:{_port} 已连接");

            // 启动后台读取任务
            _readTask = Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
        }
        catch (OperationCanceledException)
        {
            StatusChanged?.Invoke($"连接 {_host}:{_port} 超时");
            throw new TimeoutException($"连接 TCP {_host}:{_port} 超时（5秒）");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"TCP 连接失败: {ex.Message}");
            ErrorOccurred?.Invoke(ex);
            throw;
        }
    }

    /// <summary>
    /// 停止 TCP 数据源
    /// 取消读取任务并关闭 TCP 连接
    /// </summary>
    public void Stop()
    {
        try
        {
            _cts?.Cancel();

            _networkStream?.Close();
            _tcpClient?.Close();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"停止 TCP 数据源异常: {ex.Message}");
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _networkStream?.Dispose();
            _networkStream = null;
            _tcpClient?.Dispose();
            _tcpClient = null;
        }

        StatusChanged?.Invoke("TCP 数据源已停止");
    }

    /// <summary>
    /// 后台 TCP 数据读取循环
    /// 从网络流读取字节数据，按换行符分割为 JSON 行并反序列化
    /// 处理不完整行缓冲和 JSON 解析异常
    /// </summary>
    private async Task ReadLoopAsync(CancellationToken token)
    {
        StatusChanged?.Invoke("TCP 数据源运行中");

        var buffer = new byte[4096];         // 网络读取缓冲区
        var lineBuffer = new StringBuilder(); // 行缓冲，处理跨包的不完整行

        try
        {
            while (!token.IsCancellationRequested && _tcpClient?.Connected == true)
            {
                try
                {
                    var bytesRead = await _networkStream!.ReadAsync(buffer, 0, buffer.Length, token);

                    if (bytesRead == 0)
                    {
                        // 对方关闭连接
                        StatusChanged?.Invoke("TCP 连接已断开");
                        break;
                    }

                    // 将接收到的字节转换为字符串并按行处理
                    var chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    lineBuffer.Append(chunk);

                    var fullText = lineBuffer.ToString();
                    var lines = fullText.Split('\n');

                    // 如果末尾不是换行符，最后一段可能是不完整的行，保留到下次处理
                    if (!fullText.EndsWith("\n"))
                    {
                        lineBuffer.Clear();
                        lineBuffer.Append(lines[^1]);
                        lines = lines[..^1];
                    }
                    else
                    {
                        lineBuffer.Clear();
                    }

                    // 逐行解析 JSON 并触发数据事件
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed)) continue;

                        try
                        {
                            var data = System.Text.Json.JsonSerializer.Deserialize<SensorData>(trimmed);
                            if (data != null)
                            {
                                data.Timestamp = DateTime.Now;
                                DataReceived?.Invoke(data);
                            }
                        }
                        catch (System.Text.Json.JsonException ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[TcpSensorSource] JSON 解析失败: {ex.Message}, 原始数据: {trimmed}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TcpSensorSource] 读取异常: {ex.Message}");
                    StatusChanged?.Invoke($"TCP 读取异常: {ex.Message}");

                    // 短暂延迟后退出循环，等待上层处理重连
                    await Task.Delay(1000, token);
                    break;
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
            StatusChanged?.Invoke($"TCP 数据源异常: {ex.Message}");
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
