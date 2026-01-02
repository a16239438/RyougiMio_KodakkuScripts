using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.GameEvent.Struct;
using Dalamud.Utility.Numerics;

using KodakkuAssist.Data;
using KodakkuAssist.Extensions;

namespace RyougiMioScriptNamespace
{
    [ScriptType(name: "(M10S)AAC Heavyweight M2 (Savage)", territorys: [1322, 1323], guid: "d244ff34-5a89-41e4-a937-7d5c7c6b1348", version: "0.0.0.1", author: "RyougiMio", note: "M10S Prediction，脚本同时在M10N/S中生效，注明TTS的机制仅有播报，注明猜测的机制纯主观臆测。")]
    public class RyougiMio_1323
    {
        #region Settings
        // ==================== 用户设置区域 ====================
        [UserSetting("是否开启屏幕文字提示")]
        public bool EnableText { get; set; } = true;
        [UserSetting("是否开启TTS语音提示")]
        public bool EnableTTS { get; set; } = true;

        [UserSetting("常用危险色")]
        public ScriptColor DangerColor { get; set; } = new ScriptColor() { V4 = new Vector4(1.0f, 0.0f, 0.0f, 0.01f) };
        [UserSetting("常用安全色")]
        public ScriptColor SafeColor { get; set; } = new ScriptColor() { V4 = new Vector4(0.0f, 1.0f, 0.0f, 0.01f) };

        [UserSetting("指路/引导颜色 (默认为青)")]
        public ScriptColor GuideColor { get; set; } = new ScriptColor() { V4 = new Vector4(0.0f, 1.0f, 1.0f, 0.01f) };
        #endregion

        #region Variables
        // M10S 火浪回切：存储点名目标与时间 {(ID, Time)}
        private List<(uint Id, DateTime Time)> _fireWaveMarkList = new();
        private ScriptAccessory _acc;


        #endregion

        #region Methods


        // 自定义TTS方法：自动检查 EnableTTS 开关
        private void QTTS(string text, int rate = 0)
        {
            if (!EnableTTS) return;
            _acc.Method.TTS(text, rate);
        }
        // 自定义文字提示方法：自动检查 EnableText 开关
        private void QText(string text, int duration, bool isWarning = false)
        {
            if (!EnableText) return;
            _acc.Method.TextInfo(text, duration, isWarning);
        }



        #endregion

        #region Initialization 

        public void Init(ScriptAccessory accessory)
        {
            accessory.Method.RemoveDraw(".*");
            _acc = accessory;

            lock (_fireWaveMarkList)
            {
                _fireWaveMarkList.Clear();
            }

            accessory.Method.SendChat("/e M10S Initialized.");
        }

        #endregion
        #region TTSonly 
        [ScriptMethod(name: "斗志昂扬TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46466|46520|46467|46521)$"])]
        public void Elemental_AOE_Alert(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            // 46466(N), 46520(S) -> 火 AOE
            if (aid == 46466 || aid == 46520)
            {
                QTTS("火AOE");
                QText("火 AOE", 5000, true);
            }
            // 46467(N), 46521(S) -> 水 AOE
            else if (aid == 46467 || aid == 46521)
            {
                QTTS("水AOE");
                QText("水 AOE", 5000, true);
            }
        }
        [ScriptMethod(name: "浪花飞溅_浪涛翻涌TTS（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46543|46544|46551|46552)$"])]
        public void SpreadStack_Alert(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;

            if (aid == 46543||aid == 46551)
            {
                QTTS("分散");
                QText("分散", 5000, true);
            }
            else if (aid == 46544||aid == 46552)
            {
                QTTS("分摊");
                QText("分摊", 5000, true);
            }
        }
        [ScriptMethod(name: "浪尖转体TTS（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47249|47250)$"])]
        public void DelayedMechanic_Alert(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;

            if (aid == 47249)
            {
                QTTS("稍后扇形");
                QText("稍后扇形", 5000, true);
            }
            else if (aid == 47250)
            {
                QTTS("稍后击退");
                QText("稍后击退", 5000, true);
            }
        }
        [ScriptMethod(name: "炽焰冲击_深海冲击TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46464|46518|46465|46519)$"])]
        public void TankBuster_Alert(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;

            // 46464(N), 46518(S) -> 分摊死刑
            if (aid == 46464 || aid == 46518)
            {
                QTTS("分摊死刑");
                QText("分摊死刑", 5000, true);
            }
            // 46465(N), 46519(S) -> 单体死刑
            else if (aid == 46465 || aid == 46519)
            {
                QTTS("死刑");
                QText("死刑", 5000, true);
            }
        }
        [ScriptMethod(name: "极限炫技TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46499|46500)$"])]
        public void LimitCut_AOE_Alert(Event @event, ScriptAccessory accessory)
        {
            // 46499(N), 46500(S)
            QTTS("连续AOE");
            QText("连续AOE", 5000, true);
        }
        [ScriptMethod(name: "腾水踏浪_腾火踏浪TTS（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46532|46563)$"])]
        public void JumpAndTower_Alert(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;

            // 46532 -> 炽红4连跳
            if (aid == 46532)
            {
                QTTS("炽红4连跳");
                QText("炽红4连跳", 3000, true);
            }
            // 46563 -> 双人塔
            else if (aid == 46563)
            {
                QTTS("双人塔");
                QText("双人塔", 3000, true);
            }
        }
        [ScriptMethod(name: "旋绕巨火_空中旋火TTS（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46472|46530|46486|46470|46528)$"])]
        public void StackAndUnknown_Alert(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            // 46472(N), 46530(S) -> 分摊
            if (aid == 46472 || aid == 46530)
            {
                QTTS("分摊");
                QText("分摊", 3000, true);
            }
            // 46486 -> 未知
            else if (aid == 46486)
            {
                QTTS("未知");
                QText("未知", 3000, true);
            }
            else if (aid == 46470 || aid == 46528)
            {
                QTTS("分散");
                QText("分散", 3000, true);
            }
        }
        [ScriptMethod(name: "空中旋水TTS（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46557|46560|46494)$"])]
        public void HydroWaves_Alert(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;

            if (aid == 46557)
            {
                QTTS("万变水波分摊");
                QText("万变水波 分摊", 3000, true);
            }
            else if (aid == 46560)
            {
                QTTS("万变水波分散");
                QText("万变水波 分散", 3000, true);
            }
            else if (aid == 46494)
            {
                QTTS("万变水波");
                QText("万变水波", 3000, true);
            }
        }

        #endregion

        #region 火浪回切
        [ScriptMethod(name: "M10S：火浪回切点名记录", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0298)$"],userControl:false)]
        public void FireWave_MarkRecord(Event @event, ScriptAccessory accessory)
        {
            var idStr = @event["TargetId"];
            if (string.IsNullOrEmpty(idStr)) return;
            if (!uint.TryParse(idStr.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var tid)) return;
            lock (_fireWaveMarkList)
            {
                _fireWaveMarkList.Add((tid, DateTime.Now));
            }
        }

        [ScriptMethod(name: "火浪回切", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46478|46537)$"])]
        public void FireWave_Draw(Event @event, ScriptAccessory accessory)
        {
            // 1. 解析 SourceId (Boss ID)
            var srcIdStr = @event["SourceId"];
            if (string.IsNullOrEmpty(srcIdStr)) return;
            if (!uint.TryParse(srcIdStr.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var sid)) return;
            // 2. 解析 ActionId
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            // 3. 解析 Duration
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;
            // 获取 Boss 位置
            var boss = accessory.Data.Objects.SearchById(sid);
            if (boss == null) return;
            var spos = boss.Position;
            var now = DateTime.Now;
            float fanAngle = (aid == 46478) ? float.Pi / 3 : float.Pi * 2 * 330 / 360;
            lock (_fireWaveMarkList)
            {
                // 清理 10秒 以前的过期数据
                _fireWaveMarkList.RemoveAll(x => (now - x.Time).TotalSeconds > 10);
                foreach (var mark in _fireWaveMarkList)
                {
                    var tObj = accessory.Data.Objects.SearchById(mark.Id);
                    if (tObj == null) continue;
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"火浪回切-{mark.Id}-{aid}";
                    dp.Scale = new Vector2(60f);
                    dp.Radian = fanAngle;
                    dp.Owner = sid; 
                    dp.TargetObject = mark.Id; 
                    dp.Rotation = 0;     
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.DestoryAt = dur; 
                    dp.ScaleMode = ScaleMode.ByTime; 

                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                }
            }
        }

        #endregion




        #region 瞎猜环节
        [ScriptMethod(name: "浪尖转体（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46547|46488|46550)$"])]
        public void FanAndKnockback_Draw(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Draw_{aid}";
            dp.Position = @event.SourcePosition;
            dp.Rotation = @event.SourceRotation;
            dp.DestoryAt = dur;
            dp.Color = accessory.Data.DefaultDangerColor;
            if (aid == 46547 || aid == 46488)
            {
                dp.Radian = float.Pi * 2 / 3; 
                dp.Scale = new Vector2(60f);  
                dp.ScaleMode = ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
            }
            // 击退 (箭头)
            else if (aid == 46550)
            {
                // 箭头 Scale: X=宽度, Y=长度
                dp.Scale = new Vector2(5f, 30f);
                dp.ScaleMode = ScaleMode.YByTime; 
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Arrow, dp);
            }
        }


        #endregion


    }
}