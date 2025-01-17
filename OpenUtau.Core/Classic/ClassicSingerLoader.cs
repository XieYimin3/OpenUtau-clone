using System.Collections.Generic;
using System.Linq;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Classic {
    /// <summary>
    /// 传统歌手加载器
    /// </summary>
    public static class ClassicSingerLoader {
        static USinger AdjustSingerType(Voicebank v) {
            switch (v.SingerType) {
                case USingerType.Enunu:
                    return new Core.Enunu.EnunuSinger(v) as USinger;
                case USingerType.DiffSinger:
                    return new Core.DiffSinger.DiffSingerSinger(v) as USinger;
                case USingerType.Voicevox:
                    return new Core.Voicevox.VoicevoxSinger(v) as USinger;
                default:
                    return new ClassicSinger(v) as USinger;
            }
        }
        /// <summary>
        /// 查找所有歌手
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<USinger> FindAllSingers() {
            List<USinger> singers = new List<USinger>();
            foreach (var path in PathManager.Inst.SingersPaths) {
                //实例化声库加载器
                var loader = new VoicebankLoader(path);
                //将搜索到的声库添加到 singers
                singers.AddRange(loader.SearchAll()
                    .Select(AdjustSingerType));
            }
            return singers;
        }
    }
}
