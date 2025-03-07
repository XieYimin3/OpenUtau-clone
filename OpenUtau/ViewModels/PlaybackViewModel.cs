﻿using System;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using ReactiveUI;

namespace OpenUtau.App.ViewModels {
    public class TimeAxisChangedEvent { }
    public class PlaybackViewModel : ViewModelBase, ICmdSubscriber {
        UProject Project => DocManager.Inst.Project;
        public int BeatPerBar => Project.timeSignatures[0].beatPerBar;
        public int BeatUnit => Project.timeSignatures[0].beatUnit;
        public double Bpm => Project.tempos[0].bpm;
        public int Key => Project.key;
        public string KeyName => MusicMath.KeysInOctave[Key].Item1;
        public int Resolution => Project.resolution;
        public int PlayPosTick => DocManager.Inst.playPosTick;
        public TimeSpan PlayPosTime => TimeSpan.FromMilliseconds((int)Project.timeAxis.TickPosToMsPos(DocManager.Inst.playPosTick));

        public PlaybackViewModel() {
            DocManager.Inst.AddSubscriber(this);
        }

        public void SeekStart() {
            Pause();
            DocManager.Inst.ExecuteCmd(new SeekPlayPosTickNotification(0));
        }
        public void SeekEnd() {
            Pause();
            DocManager.Inst.ExecuteCmd(new SeekPlayPosTickNotification(Project.EndTick));
        }

        /// <summary>
        /// 播放或暂停
        /// </summary>
        /// <param name="tick">
        /// 播放起始位置
        /// </param>
        /// <param name="endTick">
        /// 播放结束位置
        /// </param>
        /// <param name="trackNo">
        /// 播放的轨道编号
        /// </param>
        public void PlayOrPause(int tick = -1, int endTick = -1, int trackNo = -1) 
        {
            // 调用后端播放管理器的播放或暂停方法
            PlaybackManager.Inst.PlayOrPause(tick: tick, endTick: endTick, trackNo: trackNo);
            //是否设置了“按下暂停时将播放标记移回开始播放处”
            var lockStartTime = Convert.ToBoolean(Preferences.Default.LockStartTime);
            // 如果播放器没有在播放，并且没有开始播放，并且设置了“按下暂停时将播放标记移回开始播放处”
            if (!PlaybackManager.Inst.Playing && !PlaybackManager.Inst.StartingToPlay && lockStartTime) {
                // 通知后端播放管理器将播放标记移回开始播放处
                DocManager.Inst.ExecuteCmd(new SeekPlayPosTickNotification(PlaybackManager.Inst.StartTick, true));
            }
        }
        public void Pause() {
            PlaybackManager.Inst.PausePlayback();
        }

        public void MovePlayPos(int tick) {
            if (DocManager.Inst.playPosTick != tick) {
                DocManager.Inst.ExecuteCmd(new SeekPlayPosTickNotification(Math.Max(0, tick)));
            }
        }

        public void SetTimeSignature(int beatPerBar, int beatUnit) {
            if (beatPerBar > 1 && (beatUnit == 2 || beatUnit == 4 || beatUnit == 8 || beatUnit == 16)) {
                DocManager.Inst.StartUndoGroup();
                DocManager.Inst.ExecuteCmd(new TimeSignatureCommand(Project, beatPerBar, beatUnit));
                DocManager.Inst.EndUndoGroup();
            }
        }

        public void SetBpm(double bpm) {
            if (bpm == DocManager.Inst.Project.tempos[0].bpm) {
                return;
            }
            DocManager.Inst.StartUndoGroup();
            DocManager.Inst.ExecuteCmd(new BpmCommand(Project, bpm));
            DocManager.Inst.EndUndoGroup();
        }

        public void SetKey(int key) {
            if (key == DocManager.Inst.Project.key) {
                return;
            }
            DocManager.Inst.StartUndoGroup();
            DocManager.Inst.ExecuteCmd(new KeyCommand(Project, key));
            DocManager.Inst.EndUndoGroup();
        }

        public void OnNext(UCommand cmd, bool isUndo) {
            if (cmd is BpmCommand ||
                cmd is TimeSignatureCommand ||
                cmd is AddTempoChangeCommand ||
                cmd is DelTempoChangeCommand ||
                cmd is AddTimeSigCommand ||
                cmd is DelTimeSigCommand ||
                cmd is KeyCommand ||
                cmd is LoadProjectNotification) {
                this.RaisePropertyChanged(nameof(BeatPerBar));
                this.RaisePropertyChanged(nameof(BeatUnit));
                this.RaisePropertyChanged(nameof(Bpm));
                this.RaisePropertyChanged(nameof(KeyName));
                MessageBus.Current.SendMessage(new TimeAxisChangedEvent());
                if (cmd is LoadProjectNotification) {
                    DocManager.Inst.ExecuteCmd(new SetPlayPosTickNotification(0));
                }
            } else if (cmd is SeekPlayPosTickNotification ||
                cmd is SetPlayPosTickNotification) {
                this.RaisePropertyChanged(nameof(PlayPosTick));
                this.RaisePropertyChanged(nameof(PlayPosTime));
            }
        }
    }
}
