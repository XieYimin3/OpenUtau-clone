﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenUtau.Classic;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using Serilog;

namespace OpenUtau.Core {
    /// <summary>
    /// 歌手管理器，该类被确保为单例
    /// </summary>
    public class SingerManager : SingletonBase<SingerManager> {
        //所有歌手
        public Dictionary<string, USinger> Singers { get; private set; } = new Dictionary<string, USinger>();
        //所有歌手按类型分组
        public Dictionary<USingerType, List<USinger>> SingerGroups { get; private set; } = new Dictionary<USingerType, List<USinger>>();

        private readonly ConcurrentQueue<USinger> reloadQueue = new ConcurrentQueue<USinger>();
        private CancellationTokenSource reloadCancellation;

        private HashSet<USinger> singersUsed = new HashSet<USinger>();

        public void Initialize() {
            SearchAllSingers();
        }

        /// <summary>
        /// 查找所有歌手
        /// </summary>
        public void SearchAllSingers() {
            Log.Information("Searching singers.");
            Directory.CreateDirectory(PathManager.Inst.SingersPath);
            var stopWatch = Stopwatch.StartNew();
            //局部变量 singers 临时存放从 ClassicSingerLoader.FindAllSingers() 和 Vogen.VogenSingerLoader.FindAllSingers() 获取的歌手
            var singers = ClassicSingerLoader.FindAllSingers()
                .Concat(Vogen.VogenSingerLoader.FindAllSingers())
                .Distinct();
            //将 singers 转换为字典属性 Singers 
            Singers = singers
                .ToLookup(s => s.Id)
                .ToDictionary(g => g.Key, g => g.First());
            //将 singers 按类型分组
            SingerGroups = singers
                .GroupBy(s => s.SingerType)
                .ToDictionary(s => s.Key, s => s.LocalizedOrderBy(singer => singer.LocalizedName).ToList());
            stopWatch.Stop();
            Log.Information($"Search all singers: {stopWatch.Elapsed}");
        }

        /// <summary>
        /// 根据名称获取歌手对象
        /// </summary>
        /// <param name="name">歌手名</param>
        /// <returns></returns>
        public USinger GetSinger(string name) {
            Log.Information($"Attach singer to track: {name}");
            name = name.Replace("%VOICE%", "");
            if (Singers.ContainsKey(name)) {
                return Singers[name];
            }
            return null;
        }

        public void ScheduleReload(USinger singer) {
            reloadQueue.Enqueue(singer);
            ScheduleReload();
        }

        private void ScheduleReload() {
            var newCancellation = new CancellationTokenSource();
            var oldCancellation = Interlocked.Exchange(ref reloadCancellation, newCancellation);
            if (oldCancellation != null) {
                oldCancellation.Cancel();
                oldCancellation.Dispose();
            }
            Task.Run(() => {
                Thread.Sleep(200);
                if (newCancellation.IsCancellationRequested) {
                    return;
                }
                Refresh();
            });
        }

        private void Refresh() {
            var singers = new HashSet<USinger>();
            while (reloadQueue.TryDequeue(out USinger singer)) {
                singers.Add(singer);
            }
            foreach (var singer in singers) {
                Log.Information($"Reloading {singer.Id}");
                new Task(() => {
                    DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, $"Reloading {singer.Id}"));
                }).Start(DocManager.Inst.MainScheduler);
                int retries = 5;
                while (retries > 0) {
                    retries--;
                    try {
                        singer.Reload();
                        break;
                    } catch (Exception e) {
                        if (retries == 0) {
                            Log.Error(e, $"Failed to reload {singer.Id}");
                        } else {
                            Log.Error(e, $"Retrying reload {singer.Id}");
                            Thread.Sleep(200);
                        }
                    }
                }
                Log.Information($"Reloaded {singer.Id}");
                new Task(() => {
                    DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, $"Reloaded {singer.Id}"));
                    DocManager.Inst.ExecuteCmd(new OtoChangedNotification(external: true));
                }).Start(DocManager.Inst.MainScheduler);
            }
        }

        //Check which singers are in use and free memory for those that are not
        /// <summary>
        /// 释放未使用的歌手，以减少内存占用
        /// </summary>
        /// <param name="project"></param>
        public void ReleaseSingersNotInUse(UProject project) {
            //Check which singers are in use
            var singersInUse = new HashSet<USinger>();
            foreach (var track in project.tracks) {
                var singer = track.Singer;
                if (singer != null && singer.Found && !singersInUse.Contains(singer)) {
                    singersInUse.Add(singer);
                }
            }
            //Release singers that are no longer in use
            foreach (var singer in singersUsed) {
                if (!singersInUse.Contains(singer)) {
                    singer.FreeMemory();
                }
            }
            //Update singers used
            singersUsed = singersInUse;
        }
    }
}
