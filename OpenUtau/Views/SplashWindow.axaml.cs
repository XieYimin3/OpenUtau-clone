﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using OpenUtau.Classic;
using OpenUtau.Core;
using Serilog;


/*
SplashWindow类是开屏窗口
    1.构造函数中初始化窗口；
    2.在窗口打开时触发事件；
    3.在事件中调用Start方法；
    4.在Start方法中初始化OpenUtau；
    5.在初始化OpenUtau后初始化音频；
    6.接下来让我们转到MainWindow类......
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
            if (ThemeManager.IsDarkMode) {
                LogoTypeLight.IsVisible = false;
                LogoTypeDark.IsVisible = true;
            } else {
                LogoTypeLight.IsVisible = true;
                LogoTypeDark.IsVisible = false;
            }
            //窗口打开时触发事件如下
            this.Opened += SplashWindow_Opened;
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
            var wa = Screens.Primary.WorkingArea;
            int x = wa.Size.Width / 2 - (int)Width / 2;
            int y = wa.Size.Height / 2 - (int)Height / 2;
            Position = new Avalonia.PixelPoint(x, y);

            Start();
        }

        private void Start() {
            var mainThread = Thread.CurrentThread;
            var mainScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Run(() => {
                Log.Information("Initializing OpenUtau.");
                ToolsManager.Inst.Initialize();
                SingerManager.Inst.Initialize();
                DocManager.Inst.Initialize(mainThread, mainScheduler);
                DocManager.Inst.PostOnUIThread = action => Avalonia.Threading.Dispatcher.UIThread.Post(action);
                Log.Information("Initialized OpenUtau.");
                InitAudio();
            }).ContinueWith(t => {
                if (t.IsFaulted) {
                    Log.Error(t.Exception?.Flatten(), "Failed to Start.");
                    MessageBox.ShowError(this, t.Exception, "Failed to Start OpenUtau").ContinueWith(t1 => { Close(); });
                    return;
                }
                if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                    desktop.MainWindow = mainWindow;
                    Close();
                }
            }, CancellationToken.None, TaskContinuationOptions.None, mainScheduler);
        }

        private static void InitAudio() {
            Log.Information("Initializing audio.");
            if (!OS.IsWindows() || Core.Util.Preferences.Default.PreferPortAudio) {
                try {
                    PlaybackManager.Inst.AudioOutput = new Audio.MiniAudioOutput();
                } catch (Exception e1) {
                    Log.Error(e1, "Failed to init MiniAudio");
                }
            } else {
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
