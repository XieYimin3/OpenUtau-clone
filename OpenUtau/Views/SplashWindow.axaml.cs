using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using OpenUtau.Classic;
using OpenUtau.Core;
using Serilog;


/*
SplashWindow类是开屏窗口，在这里执行了主要的初始化工作
    1.构造函数中初始化窗口；
    2.在窗口打开时触发事件SplashWindow_Opened；
    3.在事件SplashWindow_Opened中调用Start方法；
    4.在Start方法中初始化OpenUtau，具体初始化过程请看Start方法，大致有以下几个步骤：
        4.1.初始化ToolsManager；
        4.2.初始化SingerManager；
        4.3.初始化DocManager；
        4.4.初始化音频；
    5.接下来让我们转到MainWindow类......
    6.请打开OpenUtau/Views/MainWindow.axaml.cs
*/


namespace OpenUtau.App.Views {
    /// <summary>
    /// 开屏窗口
    /// </summary>
    public partial class SplashWindow : Window {
        /// <summary>
        /// 构造函数
        /// </summary>
        public SplashWindow() {
            InitializeComponent();
            //如果是深色模式，则LogoTypeLight不可见，LogoTypeDark可见
            if (ThemeManager.IsDarkMode) {
                LogoTypeLight.IsVisible = false;
                LogoTypeDark.IsVisible = true;
            } else {
            //如果是浅色模式，则LogoTypeLight可见，LogoTypeDark不可见
                LogoTypeLight.IsVisible = true;
                LogoTypeDark.IsVisible = false;
            }
            //Opend是窗口打开时触发的事件
            this.Opened += SplashWindow_Opened; //附加开屏窗口的打开
        }

        /// <summary>
        /// 其实也属于构造函数的一部分
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SplashWindow_Opened(object? sender, System.EventArgs e) {
            if (Screens.Primary == null) {
                return;
            }
            //窗口居中
            var wa = Screens.Primary.WorkingArea;
            int x = wa.Size.Width / 2 - (int)Width / 2;
            int y = wa.Size.Height / 2 - (int)Height / 2;
            Position = new Avalonia.PixelPoint(x, y);

            //开始初始化OpenUtau
            Start();
        }

        /// <summary>
        /// 初始化OpenUtau，主要是后端的初始化，重要
        /// </summary>
        private void Start() {
            //获取当前线程
            var mainThread = Thread.CurrentThread;
            //获取当前线程的调度器
            var mainScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Run(() => {
                Log.Information("Initializing OpenUtau.");
                //初始化ToolsManager
                ToolsManager.Inst.Initialize();
                //初始化SingerManager（歌手管理器）
                SingerManager.Inst.Initialize();
                //初始化DocManager，并且让DocManager接管主线程和主调度器
                DocManager.Inst.Initialize(mainThread, mainScheduler);
                //将PostOnUIThread设置为Avalonia的UI线程
                DocManager.Inst.PostOnUIThread = action => Avalonia.Threading.Dispatcher.UIThread.Post(action);
                Log.Information("Initialized OpenUtau.");
                //初始化音频
                InitAudio();
                //各种初始化完成之后干什么？如下：
            }).ContinueWith(t => {
                //先排除异常
                if (t.IsFaulted) {
                    Log.Error(t.Exception?.Flatten(), "Failed to Start.");
                    MessageBox.ShowError(this, t.Exception, "Failed to Start OpenUtau").ContinueWith(t1 => { Close(); });
                    return;
                }
                //获取当前应用程序的生命周期
                if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                    //如果是经典桌面应用程序生命周期，则构造主窗口
                    var mainWindow = new MainWindow();
                    //显示主窗口
                    mainWindow.Show();
                    desktop.MainWindow = mainWindow;
                    //关闭开屏窗口
                    Close();
                }
            }, CancellationToken.None, TaskContinuationOptions.None, mainScheduler);
        }

        /// <summary>
        /// 根据系统和用户设置，选择合适的音频输出，并初始化
        /// </summary>
        private static void InitAudio() {
            Log.Information("Initializing audio.");
            //如果不是Windows系统或者首选PortAudio，则使用MiniAudioOutput
            if (!OS.IsWindows() || Core.Util.Preferences.Default.PreferPortAudio) {
                try {
                    PlaybackManager.Inst.AudioOutput = new Audio.MiniAudioOutput();
                } catch (Exception e1) {
                    Log.Error(e1, "Failed to init MiniAudio");
                }
            }
            //否则使用NAudioOutput
            else {
                try {
                    PlaybackManager.Inst.AudioOutput = new Audio.NAudioOutput();
                } catch (Exception e2) {
                    Log.Error(e2, "Failed to init NAudio");
                }
            }
            Log.Information("Initialized audio.");
        }
    }
}
