using System;
using System.Collections.Generic;
using System.Threading;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Core.Editing {
    /// <summary>
    /// 批量编辑接口
    /// </summary>
    public interface BatchEdit {
        string Name { get; }
        bool IsAsync => false;
        void Run(UProject project, UVoicePart part, List<UNote> selectedNotes, DocManager docManager);

        /// <summary>
        /// 异步运行
        /// </summary>
        /// <param name="project">工程对象</param>
        /// <param name="part">分片</param>
        /// <param name="selectedNotes">选中的音符</param>
        /// <param name="docManager">？</param>
        /// <param name="setProgressCallback">回调，用于显示进度</param>
        /// <param name="cancellationToken">中止线程token</param>
        void RunAsync(
            UProject project, UVoicePart part, List<UNote> selectedNotes, DocManager docManager,
            Action<int, int> setProgressCallback, CancellationToken cancellationToken) {
            Run(project, part, selectedNotes, docManager);
        }
    }
}
