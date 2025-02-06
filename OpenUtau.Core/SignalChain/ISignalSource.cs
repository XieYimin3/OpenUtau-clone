namespace OpenUtau.Core.SignalChain {
    /// <summary>
    /// openutau自定义的音频样本提供器接口
    /// </summary>
    public interface ISignalSource {
        bool IsReady(int position, int count);
        /// <summary>
        /// Add float audio samples to existing buffer values.
        /// 将float型单精度浮点音频样本添加到现有缓冲区中。
        /// </summary>
        /// <param name="position"></param>
        /// <param name="buffer"></param>
        /// <param name="index"></param>
        /// <param name="count"></param>
        /// <returns>End position after read.</returns>
        int Mix(int position, float[] buffer, int index, int count);
    }
}
