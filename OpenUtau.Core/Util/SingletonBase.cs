using System;

namespace OpenUtau.Core.Util {
    /// <summary>
    /// 抽象的单例基类
    /// </summary>
    /// <typeparam name="T">
    /// 传入需要实现单例的类
    /// </typeparam>
    public abstract class SingletonBase<T> where T : class {
        private static readonly Lazy<T> inst = new Lazy<T>(
            () => (T)Activator.CreateInstance(typeof(T), true),
            isThreadSafe: true);
        public static T Inst => inst.Value;
    }
}
