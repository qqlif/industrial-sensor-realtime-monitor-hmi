using Xunit;
using Moq;
using 工业传感器实时监控上位机.Models;
using 工业传感器实时监控上位机.Services;

namespace 工业传感器实时监控上位机.Tests.Services;

/// <summary>
/// MockSensorSource 单元测试
/// 验证模拟数据源的数据生成、启停和事件触发
/// </summary>
public class MockSensorSourceTests
{
    private readonly GlobalConfig _config;

    public MockSensorSourceTests()
    {
        _config = new GlobalConfig
        {
            SampleIntervalMs = 100,
            MaxCachePoints = 200,
            Sensors = new List<SensorConfig>
            {
                new()
                {
                    Name = "温湿度传感器",
                    Type = SensorType.温湿度,
                    Unit = "°C",
                    AlarmUpper = 35.0,
                    AlarmLower = 10.0,
                    MinValue = 15.0,
                    MaxValue = 40.0,
                    Enabled = true
                },
                new()
                {
                    Name = "压力传感器",
                    Type = SensorType.压力,
                    Unit = "MPa",
                    AlarmUpper = 10.0,
                    AlarmLower = 1.0,
                    MinValue = 2.0,
                    MaxValue = 12.0,
                    Enabled = true
                },
                new()
                {
                    Name = "振动传感器",
                    Type = SensorType.振动,
                    Unit = "mm/s",
                    AlarmUpper = 15.0,
                    AlarmLower = 0.5,
                    MinValue = 1.0,
                    MaxValue = 20.0,
                    Enabled = true
                }
            }
        };
    }

    [Fact]
    public void 构造函数_初始状态为未运行()
    {
        using var source = new MockSensorSource(_config);
        Assert.False(source.IsRunning);
    }

    [Fact]
    public async Task StartAsync_应启动数据生成()
    {
        using var source = new MockSensorSource(_config);
        var dataReceived = new TaskCompletionSource<bool>();

        source.DataReceived += _ =>
        {
            dataReceived.TrySetResult(true);
        };

        await source.StartAsync(CancellationToken.None);

        // 等待数据到达
        var received = await dataReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(received);
    }

    [Fact]
    public async Task 生成的数据_数值应在量程范围内()
    {
        using var source = new MockSensorSource(_config);
        var receivedData = new List<SensorData>();
        var tcs = new TaskCompletionSource<bool>();

        source.DataReceived += data =>
        {
            receivedData.Add(data);
            if (receivedData.Count >= 3)
                tcs.TrySetResult(true);
        };

        await source.StartAsync(CancellationToken.None);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotEmpty(receivedData);

        foreach (var data in receivedData)
        {
            var sensorConfig = _config.Sensors.First(s => s.Name == data.SensorName);
            Assert.InRange(data.Value, sensorConfig.MinValue, sensorConfig.MaxValue);
        }
    }

    [Fact]
    public async Task 生成的数据_应包含所有启用的传感器类型()
    {
        using var source = new MockSensorSource(_config);
        var receivedTypes = new HashSet<SensorType>();
        var tcs = new TaskCompletionSource<bool>();

        source.DataReceived += data =>
        {
            lock (receivedTypes)
            {
                receivedTypes.Add(data.SensorType);
                if (receivedTypes.Count >= 3)
                    tcs.TrySetResult(true);
            }
        };

        await source.StartAsync(CancellationToken.None);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Contains(SensorType.温湿度, receivedTypes);
        Assert.Contains(SensorType.压力, receivedTypes);
        Assert.Contains(SensorType.振动, receivedTypes);
    }

    [Fact]
    public async Task Stop_应停止数据生成()
    {
        using var source = new MockSensorSource(_config);
        await source.StartAsync(CancellationToken.None);

        // 等待一小段时间确保数据开始生成
        await Task.Delay(200);

        source.Stop();

        Assert.False(source.IsRunning);

        // 停止后不应再收到数据
        var dataReceivedAfterStop = false;
        source.DataReceived += _ => dataReceivedAfterStop = true;

        await Task.Delay(500);
        Assert.False(dataReceivedAfterStop);
    }

    [Fact]
    public async Task 禁用传感器_不应生成数据()
    {
        _config.Sensors[0].Enabled = false;

        using var source = new MockSensorSource(_config);
        var receivedDisabledSensor = false;

        source.DataReceived += data =>
        {
            if (data.SensorName == "温湿度传感器")
                receivedDisabledSensor = true;
        };

        await source.StartAsync(CancellationToken.None);
        await Task.Delay(1000);

        source.Stop();
        Assert.False(receivedDisabledSensor);
    }

    [Fact]
    public void 反复启停_不应抛出异常()
    {
        using var source = new MockSensorSource(_config);

        // 重复启停
        var startTask = source.StartAsync(CancellationToken.None);
        source.Stop();
        source.Stop(); // 重复停止不应异常

        var startTask2 = source.StartAsync(CancellationToken.None);
        source.Stop();
    }
}
