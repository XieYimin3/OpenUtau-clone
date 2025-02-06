using System;
using NAudio.Wave;

namespace OpenUtau.Core.SignalChain {
    /// <summary>
    /// MasterAdapter类是总线适配器类，实现了ISampleProvider接口，属于一个音频样本提供器
    /// ISampleProvider接口需要实现WaveFormat属性和Read方法
    /// </summary>
    class MasterAdapter : ISampleProvider {
        private readonly WaveFormat waveFormat; //waveFormat只能在构造函数中被赋值。waveFormat用于指定音频的格式，包括采样率、声道数
        private readonly ISignalSource source; // source是一个ISignalSource接口的实例，用于提供音频样本
        private int position;

        /// <summary>
        /// 实现了ISampleProvider接口的WaveFormat属性
        /// </summary>
        public WaveFormat WaveFormat => waveFormat;
        /// <summary>
        /// 
        /// </summary>
        public int Waited { get; private set; }
        public bool IsWaiting { get; private set; }
        /// <summary>
        /// 指定音频格式，初始化音频来源
        /// </summary>
        /// <param name="source"></param>
        public MasterAdapter(ISignalSource source) {
            waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
            this.source = source;
        }

        /// <summary>
        /// 实现了ISampleProvider接口的Read方法
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public int Read(float[] buffer, int offset, int count) {
            //将buffer中的音频样本全部置为0，即空白音频
            for (int i = offset; i < offset + count; ++i) {
                buffer[i] = 0;
            }
            //如果音频来源没有准备好，就等待，播放空白音频
            if (!source.IsReady(position, count)) {
                Waited += count;
                IsWaiting = true;
                return count;
                //如果音频来源准备好了，就将音频来源的音频样本添加到buffer中
            } else {
                int pos = source.Mix(position, buffer, offset, count); // 关键
                int n = Math.Max(0, pos - position);
                position = pos;
                IsWaiting = false;
                return n;
            }
        }

        public void SetPosition(int position) {
            this.position = position;
            Waited = 0;
        }
    }
}
