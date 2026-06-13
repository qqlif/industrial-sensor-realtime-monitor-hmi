using System.Windows;

namespace 工业传感器实时监控上位机.Services;

/// <summary>
/// 弹窗服务，封装 WPF MessageBox 操作
/// 提供信息、警告、错误、确认四种标准弹窗
/// 用于 ViewModel 中显示用户交互对话框，保持 MVVM 架构的 UI 解耦
/// </summary>
public class DialogService
{
    /// <summary>显示信息提示弹窗</summary>
    public void ShowInfo(string message, string title = "提示")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>显示警告提示弹窗</summary>
    public void ShowWarning(string message, string title = "警告")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <summary>显示错误提示弹窗</summary>
    public void ShowError(string message, string title = "错误")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    /// <summary>显示确认对话框（返回用户是否点击"是"）</summary>
    public bool ShowConfirm(string message, string title = "确认")
    {
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
}
