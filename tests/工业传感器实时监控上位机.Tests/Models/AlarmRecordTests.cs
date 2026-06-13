using Xunit;
using 工业传感器实时监控上位机.Models;

namespace 工业传感器实时监控上位机.Tests.Models;

/// <summary>
/// AlarmRecord 模型单元测试
/// 验证报警记录属性的默认值和初始化行为
/// </summary>
public class AlarmRecordTests
{
    [Fact]
    public void 构造函数_应生成非空Id()
    {
        var record = new AlarmRecord();
        Assert.False(string.IsNullOrEmpty(record.Id));
    }

    [Fact]
    public void 构造函数_应设置默认时间戳()
    {
        var record = new AlarmRecord();
        Assert.True((DateTime.Now - record.AlarmTime).TotalSeconds < 1);
    }

    [Fact]
    public void 设置属性_应正确保持值()
    {
        var record = new AlarmRecord
        {
            SensorName = "压力传感器",
            SensorType = SensorType.压力,
            CurrentValue = 15.5,
            Threshold = 10.0,
            AlarmType = "上限超限",
            Unit = "MPa",
            AlarmTime = new DateTime(2026, 6, 13, 10, 30, 0)
        };

        Assert.Equal("压力传感器", record.SensorName);
        Assert.Equal(SensorType.压力, record.SensorType);
        Assert.Equal(15.5, record.CurrentValue);
        Assert.Equal(10.0, record.Threshold);
        Assert.Equal("上限超限", record.AlarmType);
        Assert.Equal("MPa", record.Unit);
        Assert.Equal(new DateTime(2026, 6, 13, 10, 30, 0), record.AlarmTime);
    }

    [Fact]
    public void 同一对象_每次Id不同()
    {
        var r1 = new AlarmRecord();
        var r2 = new AlarmRecord();
        Assert.NotEqual(r1.Id, r2.Id);
    }
}
