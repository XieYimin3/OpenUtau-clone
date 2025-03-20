using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData.Binding;
using OpenUtau.Classic;
using OpenUtau.Core;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace OpenUtau.App.ViewModels {
    public class SingerSetupViewModel : ViewModelBase {
        [Reactive] public int Step { get; set; }
        public ObservableCollection<string> TextItems => textItems;
        [Reactive] public string ArchiveFilePath { get; set; } = string.Empty;
        public Encoding[] Encodings { get; set; } = new Encoding[] {
            Encoding.GetEncoding("shift_jis"),
            Encoding.UTF8,
            Encoding.GetEncoding("gb2312"),
            Encoding.GetEncoding("big5"),
            Encoding.GetEncoding("ks_c_5601-1987"),
            Encoding.GetEncoding("Windows-1252"),
            Encoding.GetEncoding("macintosh"),
        };

        //压缩包编码
        [Reactive] public Encoding ArchiveEncoding { get; set; }
        //文本编码
        [Reactive] public Encoding TextEncoding { get; set; }
        //是否缺少歌手信息
        [Reactive] public bool MissingInfo { get; set; }
        //支持的歌手类型
        public string[] SingerTypes { get; set; } = new[] { "utau", "enunu", "diffsinger" };
        [Reactive] public string SingerType { get; set; }

        private ObservableCollectionExtended<string> textItems;

        /// <summary>
        /// 初始构造函数
        /// </summary>
        /// <exception cref="MessageCustomizableException"></exception>
        public SingerSetupViewModel() {
#if DEBUG
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
            //设置各种默认值
            SingerType = SingerTypes[0];
            ArchiveEncoding = Encodings[0];
            TextEncoding = Encodings[0];
            textItems = new ObservableCollectionExtended<string>();
            this.WhenAnyValue(vm => vm.ArchiveFilePath)
                .Subscribe(_ => {
                    if (!string.IsNullOrEmpty(ArchiveFilePath)) {
                        //排除加密的压缩包
                        if (IsEncrypted(ArchiveFilePath)) {
                            throw new MessageCustomizableException(
                                "Encrypted archive file isn't supported",
                                "<translate:errors.encryptedarchive>", 
                                new Exception("Encrypted archive file: " + ArchiveFilePath)
                            );
                        }                        
                        var config = LoadCharacterYaml(ArchiveFilePath);
                        MissingInfo = string.IsNullOrEmpty(config?.SingerType);
                        if (!string.IsNullOrEmpty(config?.TextFileEncoding)) {
                            try {
                                TextEncoding = Encoding.GetEncoding(config.TextFileEncoding);
                            } catch { }
                        }
                    }
                });
            this.WhenAnyValue(vm => vm.Step, vm => vm.ArchiveEncoding, vm => vm.ArchiveFilePath)
                .Subscribe(_ => RefreshArchiveItems());
            this.WhenAnyValue(vm => vm.Step, vm => vm.TextEncoding)
                .Subscribe(_ => RefreshTextItems());
        }

        /// <summary>
        /// 后退，上一步
        /// </summary>
        public void Back() {
            Step--;
        }

        /// <summary>
        /// 前进，下一步
        /// </summary>
        public void Next() {
            Step++;
        }

        /// <summary>
        /// 刷新压缩包编码样本
        /// </summary>
        private void RefreshArchiveItems() {
            if (Step != 0) {
                return;
            }
            if (string.IsNullOrEmpty(ArchiveFilePath)) {
                textItems.Clear();
                return;
            }
            var readerOptions = new ReaderOptions {
                ArchiveEncoding = new ArchiveEncoding { Forced = ArchiveEncoding },
            };
            using (var archive = ArchiveFactory.Open(ArchiveFilePath, readerOptions)) {
                textItems.Clear();
                textItems.AddRange(archive.Entries
                    .Select(entry => entry.Key)
                    .ToArray());
            }
        }

        /// <summary>
        /// 判断压缩包是否加密
        /// </summary>
        /// <param name="archiveFilePath">要检查的压缩文件路径</param>
        /// <returns>true表示加密</returns>
        private bool IsEncrypted(string archiveFilePath) {
            using (var archive = ArchiveFactory.Open(archiveFilePath)) {
                return archive.Entries.Any(e => e.IsEncrypted);
            }
        }

        /// <summary>
        /// 从压缩包解出character.yaml以获取歌手信息
        /// </summary>
        /// <param name="archiveFilePath"></param>
        /// <returns>空返回表示压缩包中没有那个文件或者配置解析失败</returns>
        private VoicebankConfig? LoadCharacterYaml(string archiveFilePath) {
            using (var archive = ArchiveFactory.Open(archiveFilePath)) {
                var entry = archive.Entries.FirstOrDefault(e => Path.GetFileName(e.Key)=="character.yaml");
                if (entry == null) {
                    return null;
                }
                //打开文件流，并获取配置信息
                using (var stream = entry.OpenEntryStream()) {
                    return VoicebankConfig.Load(stream);
                }
            }
        }

        /// <summary>
        /// 刷新文本编码样本
        /// </summary>
        private void RefreshTextItems() {
            if (Step != 1) {
                return;
            }
            if (string.IsNullOrEmpty(ArchiveFilePath)) {
                textItems.Clear();
                return;
            }
            var readerOptions = new ReaderOptions {
                ArchiveEncoding = new ArchiveEncoding { Forced = ArchiveEncoding },
            };
            using (var archive = ArchiveFactory.Open(ArchiveFilePath, readerOptions)) {
                try {
                    textItems.Clear();
                    foreach (var entry in archive.Entries.Where(entry => entry.Key.EndsWith("character.txt") || entry.Key.EndsWith("oto.ini"))) {
                        using (var stream = entry.OpenEntryStream()) {
                            using var reader = new StreamReader(stream, TextEncoding);
                            textItems.Add($"------ {entry.Key} ------");
                            int count = 0;
                            while (count < 256 && !reader.EndOfStream) {
                                string? line = reader.ReadLine();
                                if (!string.IsNullOrWhiteSpace(line)) {
                                    textItems.Add(line);
                                    count++;
                                }
                            }
                            if (!reader.EndOfStream) {
                                textItems.Add($"...");
                            }
                        }
                    }
                } catch (Exception ex) {
                    DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
                    Step--;
                }
            }
        }

        //声库安装任务
        /// <summary>
        /// 关键的声库安装任务
        /// </summary>
        /// <returns></returns>
        public Task Install() {
            //将各种参数转化为局部变量
            string archiveFilePath = ArchiveFilePath;
            var archiveEncoding = ArchiveEncoding;
            var textEncoding = TextEncoding;
            return Task.Run(() => {
                try {
                    //字符串
                    //获取声库安装路径
                    var basePath = PathManager.Inst.SingersInstallPath;
                    //开始安装

                    //关键：初始化安装器
                    var installer = new VoicebankInstaller(basePath, (progress, info) => {
                        //产生进度条通知，将一个可以发送进度通知的命令执行器传递给安装器，以便安装器更新进度条
                        DocManager.Inst.ExecuteCmd(new ProgressBarNotification(progress, info));
                    }, archiveEncoding, textEncoding);

                    //关键：开始安装
                    installer.Install(archiveFilePath, SingerType);

                } finally {
                    //安装完成后
                    new Task(() => {
                        //重置进度条
                        DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, ""));
                        //通知歌手已更改
                        DocManager.Inst.ExecuteCmd(new SingersChangedNotification());
                    }).Start(DocManager.Inst.MainScheduler);
                }
            });
        }
    }
}
