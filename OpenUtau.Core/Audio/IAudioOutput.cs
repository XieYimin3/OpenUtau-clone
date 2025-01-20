using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace OpenUtau.Audio {
    /// <summary>
    /// 一个用于描述音频输出设备的类
    /// </summary>
    public class AudioOutputDevice {
        public string name; // 设备名称
        public string api; // 设备使用的API，如
        public int deviceNumber; // 设备序号
        public Guid guid; // 设备的GUID

        /// <summary>
        /// 重写ToString方法，返回设备的名称和API
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"[{api}] {name}";
    }

    /// <summary>
    /// 规范了音频输出操作的接口
    /// </summary>
    public interface IAudioOutput {
        //当前播放状态，包括停止、播放、暂停
        PlaybackState PlaybackState { get; }
        //当前使用的设备序号
        int DeviceNumber { get; }

        //通过GUID和设备序号选中设备
        void SelectDevice(Guid guid, int deviceNumber);
        //使用一个音频样本提供器初始化音频输出
        void Init(ISampleProvider sampleProvider);
        //暂停
        void Pause();
        //播放
        void Play();
        //停止
        void Stop();
        //获取当前播放位置，64位整数
        long GetPosition();

        //获取所有的音频输出设备
        List<AudioOutputDevice> GetOutputDevices();
    }
}
