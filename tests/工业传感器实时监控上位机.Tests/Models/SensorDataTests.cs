using Xunit;
using 工业传感器实时监控上位机.Models;

namespace 工业传感器实时监控上位机.Tests.Models;

/// <summary>
/// SensorData 模型单元测试
/// 验证 SensorData 属性的默认值和序列化行为
/// </summary>
public class SensorDataTests
{
    [Fact]
    public void 构造函数_应生成非空Id()
    {
        var data = new SensorData();
        Assert.False(string.IsNullOrEmpty(data.Id));
    }

    [Fact]
    public void 构造函数_应设置默认时间戳()
    {
        var data = new SensorData();
        Assert.True((DateTime.Now - data.Timestamp).TotalSeconds < 1);
    }

    [Fact]
    public void 构造函数_默认不报警()
    {
        var data = new SensorData();
        Assert.False(data.IsAlarm);
    }

    [Fact]
    public void 设置属性_应正确保持值()
    {
        var data = new SensorData
        {
            SensorType = SensorType.压力,
            SensorName = "压力传感器",
            Value = 5.5,
            Unit = "MPa",
            IsAlarm = true
        };

        Assert.Equal(SensorType.压力, data.SensorType);
        Assert.Equal("压力传感器", data.SensorName);
        Assert.Equal(5.5, data.Value);
        Assert.Equal("MPa", data.Unit);
        Assert.True(data.IsAlarm);
    }

    [Fact]
    public void 同一对象_每次Id不同()
    {
        var data1 = new SensorData();
        var data2 = new SensorData();
        Assert.NotEqual(data1.Id, data2.Id);
    }
}
