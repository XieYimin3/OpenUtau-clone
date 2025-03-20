using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenUtau.App.ViewModels;
using Serilog;

namespace OpenUtau.App.Views {
    /// <summary>
    /// 歌手安装对话框
    /// </summary>
    public partial class SingerSetupDialog : Window {
        public SingerSetupDialog() {
            InitializeComponent();
        }

        /// <summary>
        /// 点击安装按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arg"></param>
        void InstallClicked(object sender, RoutedEventArgs arg) {
            var viewModel = DataContext as SingerSetupViewModel;
            if (viewModel == null) {
                return;
            }
            var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            var task = viewModel.Install(); // 调用vm安装方法
            task.ContinueWith((task) => {
                if (task.IsFaulted) {
                    Log.Error(task.Exception, "Failed to install singer");
                    if (Parent is Window window) {
                        MessageBox.ShowError(window, task.Exception);
                    }
                }
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, scheduler);
            Close();
        }
    }
}
