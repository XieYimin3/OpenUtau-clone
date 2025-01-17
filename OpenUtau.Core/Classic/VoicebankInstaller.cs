using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using K4os.Hash.xxHash;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace OpenUtau.Classic {

    public class VoicebankInstaller {
        const string kCharacterTxt = "character.txt";
        const string kCharacterYaml = "character.yaml";
        const string kInstallTxt = "install.txt";

        private string basePath;
        //进度条
        private readonly Action<double, string> progress;
        private readonly Encoding archiveEncoding;
        private readonly Encoding textEncoding;

        /// <summary>
        /// 声库安装器构造函数
        /// </summary>
        /// <param name="basePath">安装路径</param>
        /// <param name="progress">委托类型。该参数用于传递一个回调函数，该函数将在安装过程中被调用，以报告当前的进度和状态消息。</param>
        /// <param name="archiveEncoding">压缩包编码方式</param>
        /// <param name="textEncoding">文本编码方式</param>
        public VoicebankInstaller(string basePath, Action<double, string> progress, Encoding archiveEncoding, Encoding textEncoding) {
            Directory.CreateDirectory(basePath);
            this.basePath = basePath;
            this.progress = progress;
            this.archiveEncoding = archiveEncoding;
            this.textEncoding = textEncoding;
        }

        /// <summary>
        /// 开始安装
        /// </summary>
        /// <param name="path">声库压缩包路径</param>
        /// <param name="singerType">歌手类型</param>
        public void Install(string path, string singerType) {
            progress.Invoke(0, "Analyzing archive...");
            var readerOptions = new ReaderOptions {
                ArchiveEncoding = new ArchiveEncoding {
                    Forced = archiveEncoding,
                }
            };
            var extractionOptions = new ExtractionOptions {
                Overwrite = true,
            };
            //打开压缩包
            using (var archive = ArchiveFactory.Open(path, readerOptions)) {
                var touches = new List<string>();
                //？
                AdjustBasePath(archive, path, touches);
                int total = archive.Entries.Count();
                int count = 0;
                //是否有character.yaml
                bool hasCharacterYaml = archive.Entries.Any(e => Path.GetFileName(e.Key) == kCharacterYaml);
                //遍历压缩包中的文件
                foreach (var entry in archive.Entries) {
                    //报告进度
                    progress.Invoke(100.0 * ++count / total, entry.Key);
                    if (entry.Key.Contains("..")) {
                        // Prevent zipSlip attack
                        // https://snyk.io/research/zip-slip-vulnerability
                        // 避免zipSlip攻击
                        continue;
                    }
                    //拼接输出路径
                    var filePath = Path.Combine(basePath, entry.Key);
                    //先创建文件夹，避免文件夹不存在错误
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    //如果不是文件夹且不是install.txt
                    if (!entry.IsDirectory && entry.Key != kInstallTxt) {
                        //那么就将文件写入到输出路径
                        entry.WriteToFile(Path.Combine(basePath, entry.Key), extractionOptions);
                        //接下来处理character.txt和character.yaml
                        //没有character.yaml但当前条目是character.txt的情况
                        if (!hasCharacterYaml && Path.GetFileName(filePath) == kCharacterTxt) {
                            var config = new VoicebankConfig() {
                                TextFileEncoding = textEncoding.WebName,
                                SingerType = singerType,
                            };
                            using (var stream = File.Open(filePath.Replace(".txt", ".yaml"), FileMode.Create)) {
                                config.Save(stream);
                            }
                        }
                        //有character.yaml并且当前条目就是character.yaml的情况
                        if (hasCharacterYaml && Path.GetFileName(filePath) == kCharacterYaml) {
                            //先清空配置
                            VoicebankConfig? config = null;
                            //尝试读取配置
                            using (var stream = File.Open(filePath, FileMode.Open)) {
                                config = VoicebankConfig.Load(stream);
                            }
                            //如果失败了
                            if (string.IsNullOrEmpty(config.SingerType)) {
                                config.SingerType = singerType;
                                using (var stream = File.Open(filePath, FileMode.Open)) {
                                    config.Save(stream);
                                }
                            }
                        }
                    }
                }
                foreach (var touch in touches) {
                    File.WriteAllText(touch, "\n");
                    var config = new VoicebankConfig() {
                        TextFileEncoding = textEncoding.WebName,
                    };
                    using (var stream = File.Open(touch.Replace(".txt", ".yaml"), FileMode.Create)) {
                        config.Save(stream);
                    }
                }
            }
        }

        /// <summary>
        /// 用来干什么的？
        /// </summary>
        /// <param name="archive"></param>
        /// <param name="archivePath"></param>
        /// <param name="touches">
        /// 传入的touches是什么？
        /// </param>
        private void AdjustBasePath(IArchive archive, string archivePath, List<string> touches) {
            var dirsAndFiles = archive.Entries.Select(e => e.Key).ToHashSet();
            var rootDirs = archive.Entries
                .Where(e => e.IsDirectory)
                .Where(e => (e.Key.IndexOf('\\') < 0 || e.Key.IndexOf('\\') == e.Key.Length - 1)
                         && (e.Key.IndexOf('/') < 0 || e.Key.IndexOf('/') == e.Key.Length - 1))
                .ToArray();
            var rootFiles = archive.Entries
                .Where(e => !e.IsDirectory)
                .Where(e => !e.Key.Contains('\\') && !e.Key.Contains('/') && e.Key != kInstallTxt)
                .ToArray();
            if (rootFiles.Count() > 0) {
                // Need to create root folder.
                basePath = Path.Combine(basePath, Path.GetFileNameWithoutExtension(archivePath).Trim());
                if (rootFiles.Where(e => e.Key == kCharacterTxt).Count() == 0) {
                    // Need to create character.txt.
                    touches.Add(Path.Combine(basePath, kCharacterTxt));
                }
                return;
            }
            foreach (var rootDir in rootDirs) {
                if (!dirsAndFiles.Contains($"{rootDir.Key}{kCharacterTxt}") &&
                    !dirsAndFiles.Contains($"{rootDir.Key}\\{kCharacterTxt}") &&
                    !dirsAndFiles.Contains($"{rootDir.Key}/{kCharacterTxt}")) {
                    touches.Add(Path.Combine(basePath, rootDir.Key, kCharacterTxt));
                }
            }
        }

        static string HashPath(string path) {
            string file = Path.GetFileName(path);
            string dir = Path.GetDirectoryName(path);
            file = $"{XXH32.DigestOf(Encoding.UTF8.GetBytes(file)):x8}";
            if (string.IsNullOrEmpty(dir)) {
                return file;
            }
            return Path.Combine(HashPath(dir), file);
        }
    }
}
