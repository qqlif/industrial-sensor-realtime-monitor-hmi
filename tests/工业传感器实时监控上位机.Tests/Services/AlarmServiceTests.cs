using Xunit;
using Moq;
using 工业传感器实时监控上位机.Models;
using 工业传感器实时监控上位机.Services;

namespace 工业传感器实时监控上位机.Tests.Services;

/// <summary>
/// AlarmService 单元测试
/// 验证报警检查逻辑、阈值判断、去重和事件触发
/// </summary>
public class AlarmServiceTests
{
    private readonly Mock<IDataStorage> _mockStorage;
    private readonly AlarmService _alarmService;

    public AlarmServiceTests()
    {
        _mockStorage = new Mock<IDataStorage>();
        _alarmService = new AlarmService(_mockStorage.Object);
    }

    [Fact]
    public void CheckAlarm_值超过上限_应触发报警()
    {
        var data = new SensorData { Value = 40.0 };
        var config = new SensorConfig { AlarmUpper = 35.0, AlarmLower = 10.0 };

        var result = _alarmService.CheckAlarm(data, config);

        Assert.True(result);
        Assert.Single(_alarmService.AlarmRecords);
        Assert.Equal("上限超限", _alarmService.AlarmRecords[0].AlarmType);
    }

    [Fact]
    public void CheckAlarm_值低于下限_应触发报警()
    {
        var data = new SensorData { Value = 5.0 };
        var config = new SensorConfig { AlarmUpper = 35.0, AlarmLower = 10.0 };

        var result = _alarmService.CheckAlarm(data, config);

        Assert.True(result);
        Assert.Single(_alarmService.AlarmRecords);
        Assert.Equal("下限超限", _alarmService.AlarmRecords[0].AlarmType);
    }

    [Fact]
    public void CheckAlarm_值在正常范围_不应触发报警()
    {
        var data = new SensorData { Value = 22.0 };
        var config = new SensorConfig { AlarmUpper = 35.0, AlarmLower = 10.0 };

        var result = _alarmService.CheckAlarm(data, config);

        Assert.False(result);
        Assert.Empty(_alarmService.AlarmRecords);
    }

    [Fact]
    public void CheckAlarm_值等于上限_不应触发报警()
    {
        var data = new SensorData { Value = 35.0 };
        var config = new SensorConfig { AlarmUpper = 35.0, AlarmLower = 10.0 };

        var result = _alarmService.CheckAlarm(data, config);

        Assert.False(result);
    }

    [Fact]
    public void CheckAlarm_值等于下限_不应触发报警()
    {
        var data = new SensorData { Value = 10.0 };
        var config = new SensorConfig { AlarmUpper = 35.0, AlarmLower = 10.0 };

        var result = _alarmService.CheckAlarm(data, config);

        Assert.False(result);
    }

    [Fact]
    public void 报警记录_超出上限应记录正确阈值()
    {
        var data = new SensorData
        {
            SensorName = "温湿度传感器",
            SensorType = SensorType.温湿度,
            Value = 36.5,
            Unit = "°C"
        };
        var config = new SensorConfig { AlarmUpper = 35.0, AlarmLower = 10.0 };

        _alarmService.CheckAlarm(data, config);
        var record = _alarmService.AlarmRecords[0];

        Assert.Equal("温湿度传感器", record.SensorName);
        Assert.Equal(SensorType.温湿度, record.SensorType);
        Assert.Equal(36.5, record.CurrentValue);
        Assert.Equal(35.0, record.Threshold);
        Assert.Equal("°C", record.Unit);
    }

    [Fact]
    public void FilterAlarms_按传感器名称筛选_应返回匹配结果()
    {
        // 准备：添加几条报警记录
        _alarmService.CheckAlarm(
            new SensorData { SensorName = "温湿度传感器", Value = 36.0, Timestamp = DateTime.Now },
            new SensorConfig { AlarmUpper = 35.0, AlarmLower = 10.0 });
        _alarmService.CheckAlarm(
            new SensorData { SensorName = "压力传感器", Value = 11.0, Timestamp = DateTime.Now },
            new SensorConfig { AlarmUpper = 10.0, AlarmLower = 1.0 });

        var result = _alarmService.FilterAlarms(sensorName: "温湿度").ToList();

        Assert.Single(result);
        Assert.All(result, r => Assert.Contains("温湿度", r.SensorName));
    }

    [Fact]
    public void ClearAlarms_应清空所有记录()
    {
        _alarmService.CheckAlarm(
            new SensorData { Value = 36.0 },
            new SensorConfig { AlarmUpper = 35.0, AlarmLower = 10.0 });

        _alarmService.ClearAlarms();

        Assert.Empty(_alarmService.AlarmRecords);
    }

    [Fact]
    public void 报警事件_触发时应通知订阅者()
    {
        var triggered = false;
        _alarmService.AlarmTriggered += _ => triggered = true;

        _alarmService.CheckAlarm(
            new SensorData { Value = 36.0 },
            new SensorConfig { AlarmUpper = 35.0, AlarmLower = 10.0 });

        Assert.True(triggered);
    }
}
