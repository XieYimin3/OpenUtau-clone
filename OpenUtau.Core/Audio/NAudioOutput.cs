using System;
using System.Collections.Generic;
using NAudio.Wave;
using OpenUtau.Core.Util;


/*
让我们看一看naudio的工作流程
首先是总的初始化
1.从配置文件解析guid，并调用SelectDevice选中设备
2.SelectDevice就是设置了deviceNumber字段
总体初始化完成，就是完成了设备的选择
接下来是根据不同的样本提供器初始化
1.Init方法初始化了waveOutEvent的设备号和样本提供器
*/

namespace OpenUtau.Audio {
    //该条件编译指令表示如果不是Windows系统，则继承自DummyAudioOutput，目的是什么？
#if !WINDOWS
    public class NAudioOutput : DummyAudioOutput { }
#else
    public class NAudioOutput : IAudioOutput {
        const int Channels = 2;

        //是什么？
        private readonly object lockObj = new object();
        private WaveOutEvent waveOutEvent;
        private int deviceNumber;

        /// <summary>
        /// 从配置文件解析guid，并调用SelectDevice选中设备
        /// </summary>
        public NAudioOutput() {
            //尝试从配置解析设备的GUID，如果解析失败，则使用第一个设备
            //尝试解析的第一个参数是字符串guid。第二个将现生成的GUID类型的引用传递，如果解析成功，下一步就可以直接使用这个GUID
            if (Guid.TryParse(Preferences.Default.PlaybackDevice, out var guid)) {
                SelectDevice(guid, Preferences.Default.PlaybackDeviceNumber);
            } else {
                SelectDevice(new Guid(), 0);
            }
        }

        public PlaybackState PlaybackState {
            get {
                lock (lockObj) {
                    return waveOutEvent == null ? PlaybackState.Stopped : waveOutEvent.PlaybackState;
                }
            }
        }

        public int DeviceNumber => deviceNumber;

        public long GetPosition() {
            lock (lockObj) {
                return waveOutEvent == null
                    ? 0
                    : waveOutEvent.GetPosition() / Channels;
            }
        }

        /// <summary>
        /// 使用一个音频样本提供器初始化WaveOutEvent
        /// </summary>
        /// <param name="sampleProvider">样本提供器</param>
        public void Init(ISampleProvider sampleProvider) {
            lock (lockObj) {
                //先清理
                if (waveOutEvent != null) {
                    waveOutEvent.Stop();
                    waveOutEvent.Dispose();
                }
                //告诉WaveOutEvent使用哪个设备
                waveOutEvent = new WaveOutEvent() {
                    DeviceNumber = deviceNumber,
                };
                //向WaveOutEvent传递样本提供器
                waveOutEvent.Init(sampleProvider);
            }
        }

        public void Pause() {
            lock (lockObj) {
                if (waveOutEvent != null) {
                    waveOutEvent.Pause();
                }
            }
        }

        public void Play() {
            lock (lockObj) {
                if (waveOutEvent != null) {
                    waveOutEvent.Play();
                }
            }
        }

        public void Stop() {
            lock (lockObj) {
                if (waveOutEvent != null) {
                    waveOutEvent.Stop();
                    waveOutEvent.Dispose();
                    waveOutEvent = null;
                }
            }
        }

        /// <summary>
        /// 该方法主要是判断了一下设备guid和序号是否匹配且合法，然后将该类的deviceNumber字段设为传入的deviceNumber
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="deviceNumber"></param>
        public void SelectDevice(Guid guid, int deviceNumber) {
            //保存要选中设备的GUID和序号到配置文件
            Preferences.Default.PlaybackDevice = guid.ToString();
            Preferences.Default.PlaybackDeviceNumber = deviceNumber;
            Preferences.Save();
            // Product guid may not be unique. Use device number first.
            //如果设备序号小于设备数量且设备序号对应的GUID与传入的GUID相同，则选中该设备
            if (deviceNumber < WaveOut.DeviceCount && WaveOut.GetCapabilities(deviceNumber).ProductGuid == guid) {
                this.deviceNumber = deviceNumber;
                return;
            }
            // If guid does not match, device number may have changed. Search guid instead.
            this.deviceNumber = 0;
            for (int i = 0; i < WaveOut.DeviceCount; ++i) {
                if (WaveOut.GetCapabilities(i).ProductGuid == guid) {
                    this.deviceNumber = i;
                    break;
                }
            }
        }

        public List<AudioOutputDevice> GetOutputDevices() {
            var outDevices = new List<AudioOutputDevice>();
            for (int i = 0; i < WaveOut.DeviceCount; ++i) {
                var capability = WaveOut.GetCapabilities(i);
                outDevices.Add(new AudioOutputDevice {
                    api = "WaveOut",
                    name = capability.ProductName,
                    deviceNumber = i,
                    guid = capability.ProductGuid,
                });
            }
            return outDevices;
        }
    }
#endif
}
