using System.Windows;
using 工业传感器实时监控上位机.ViewModels;

namespace 工业传感器实时监控上位机
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Closing += OnClosing;
        }

        /// <summary>
        /// 窗口关闭时释放 MainViewModel 资源（后台任务、串口/TCP 连接等）
        /// </summary>
        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is MainViewModel mainVm)
            {
                mainVm.Dispose();
            }
        }
    }
}