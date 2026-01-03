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
    [ScriptType(name: "(M11S)AAC Heavyweight M3 (Savage)", territorys: [1324, 1325], guid: "725bcd38-1173-420e-a248-b3e11a1ff1b3", version: "0.0.0.1", author: "RyougiMio", note: "M11S Prediction，脚本同时在M11N/S中生效，注明TTS的机制仅有播报，注明猜测的机制纯主观臆测。")]
    public class RyougiMio_1325
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


            accessory.Method.SendChat("/e M10S Initialized.");
        }

        #endregion
        #region TTSonly 

        [ScriptMethod(name: "铸兵猛攻", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46087|46088|46089|46010|46012|46014)$"])]
        public void WeaponCall_Alert(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            // 46087 -> 斧子
            if (aid == 46087 || aid == 46010)
            {
                QTTS("钢铁");
                QText("钢铁", 3000, true);
            }
            // 46088 -> 镰刀
            else if (aid == 46088 || aid == 46012)
            {
                QTTS("月环");
                QText("月环", 3000, true);
            }
            // 46089 -> 大剑
            else if (aid == 46089 || aid == 46014)
            {
                QTTS("十字");
                QText("十字", 3000, true);
            }
        }
        [ScriptMethod(name: "历战之兵武TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46028|46102)$"])]
        public void TripleCharge_Alert(Event @event, ScriptAccessory accessory)
        {
            // 46028, 46102 -> 准备三连冲锋
            QTTS("准备三连冲锋");
            QText("准备三连冲锋", 3000, true);
        }
        [ScriptMethod(name: "铸兵之令：轰击TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46037|46114|46115)$"])]
        public void TankBuster_Combo_Alert(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;

            // 46037(N), 46114(S) -> 圆形分散 + 死刑
            if (aid == 46037 || aid == 46114)
            {
                QTTS("圆形分散加死刑");
                QText("圆形分散 + 死刑", 3000, true);
            }
            // 46115(S) -> 扇形分摊 + 死刑
            else if (aid == 46115)
            {
                QTTS("扇形分摊加死刑");
                QText("扇形分摊 + 死刑", 3000, true);
            }
        }
        [ScriptMethod(name: "霸王大漩涡TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46039|46117)$"])]
        public void HPtoOne_Alert(Event @event, ScriptAccessory accessory)
        {
            QTTS("清1血");
            QText("清1血", 3000, true);
        }

        [ScriptMethod(name: "万劫不朽的统治TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46042|46120)$"])]
        public void AOE_Alert_46042(Event @event, ScriptAccessory accessory)
        {
            QTTS("AOE");
            QText("AOE", 3000, true);
        }

        [ScriptMethod(name: "重陨石TTS（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46152"])]
        public void Stack_Alert_46152(Event @event, ScriptAccessory accessory)
        {
            QTTS("分摊");
            QText("分摊", 3000, true);
        }
        [ScriptMethod(name: "冲击波TTS（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46140"])]
        public void Meteor_Alert_46140(Event @event, ScriptAccessory accessory)
        {
            QTTS("大陨石");
            QText("大陨石", 3000, true);
        }

        #endregion

        #region 历战兵武

        [ScriptMethod(name: "历战之兵武", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46030|46104|46031|46105|46032|46106)$"])]
        public void TripleCombo_Draw(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;
            if (aid == 46030 || aid == 46104)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"Combo_Circle_{aid}_{DateTime.Now.Ticks}";
                dp.DestoryAt = dur;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Position = @event.SourcePosition;
                dp.Scale = new Vector2(8f);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
            else if (aid == 46031 || aid == 46105)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"Combo_Donut_{aid}_{DateTime.Now.Ticks}";
                dp.DestoryAt = dur;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Position = @event.SourcePosition;
                dp.Radian = float.Pi * 2;
                dp.Scale = new Vector2(60f);       // 外径
                dp.InnerScale = new Vector2(5f); // 内径
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
            }
            // 3. 十字 (40x10, 4个矩形拼接, 方向=TargetRotation)
            else if (aid == 46032 || aid == 46106)
            {
                // --- 获取基准角度 (目标面向) ---
                float baseRotation = @event.SourceRotation;           
                var tidStr = @event["TargetId"];
                if (!string.IsNullOrEmpty(tidStr) && 
                    ulong.TryParse(tidStr.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var tid))
                {
                    var tObj = accessory.Data.Objects.SearchById(tid);
                    if (tObj != null)
                    {
                        baseRotation = tObj.Rotation; // 拿到目标的面向
                    }
                }
                // --- 循环画 4 个矩形 ---
                for (int i = 0; i < 4; i++)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"Combo_Cross_{aid}_{i}_{DateTime.Now.Ticks}";
                    dp.Position = @event.SourcePosition;
                    
                    // 基准角度 + 每次转 90 度 (PI/2)
                    dp.Rotation = baseRotation + (float)(Math.PI / 2 * i);
                    
                    // 矩形 Scale: X=宽(10), Y=长(40)
                    dp.Scale = new Vector2(10f, 40f);
                    
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.DestoryAt = dur;
                    dp.ScaleMode = ScaleMode.YByTime; // 随时间填充                
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                }
            }
        }
        #endregion
        #region 铸兵之令
        [ScriptMethod(name: "铸兵之令：统治", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46035|46112)$"])]
        public void DoubleRectCleave_Draw(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;
            float baseRotation = @event.SourceRotation;
            var tidStr = @event["TargetId"];
            if (!string.IsNullOrEmpty(tidStr) && 
                ulong.TryParse(tidStr.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var tid))
            {
                var tObj = accessory.Data.Objects.SearchById(tid);
                if (tObj != null)
                {
                    baseRotation = tObj.Rotation;
                }
            }
            // 2. 循环画 2 条矩形 (0度 和 180度)
            for (int i = 0; i < 2; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"Rect_Cleave_{aid}_{i}_{DateTime.Now.Ticks}"; 
                dp.Position = @event.SourcePosition;              
                // i=0 -> baseRotation
                // i=1 -> baseRotation + PI (180度)
                dp.Rotation = baseRotation + (float)(Math.PI * i);
                dp.Scale = new Vector2(10f, 60f); 
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.DestoryAt = dur;
                dp.ScaleMode = ScaleMode.YByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
        }



        #endregion





        #region 瞎猜环节

        [ScriptMethod(name: "彗星_重彗星（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46025|46100|46101)$"])]
        public void Comet_Mechanic(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;
            var targetIdStr = @event["TargetId"];
            if (string.IsNullOrEmpty(targetIdStr)) return;
            if (!ulong.TryParse(targetIdStr.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var tid)) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Comet_Circle_{tid}_{DateTime.Now.Ticks}"; 
            dp.Owner = tid;              
            dp.DestoryAt = dur;          
            dp.ScaleMode = ScaleMode.ByTime; 
            // 定义紫色 (R=1, G=0, B=1)
            var purpleColor = new Vector4(1.0f, 0.0f, 1.0f, 2.0f);
            if (aid == 46025 || aid == 46100)
            {
                dp.Color = accessory.Data.DefaultDangerColor;
                if (aid == 46025){dp.Scale = new Vector2(4f);};
                if (aid == 46100){dp.Scale = new Vector2(6f);};

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                if (tid == accessory.Data.Me)
                {
                    QTTS("彗星");
                    QText("彗星点名", dur, true);
                }
            }
            else if (aid == 46101)
            {
                dp.Color = purpleColor;
                dp.Scale = new Vector2(6f);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                if (tid == accessory.Data.Me)
                {
                    QTTS("重彗星");
                    QText("重彗星点名", dur, true);
                }
            }
        }

        [ScriptMethod(name: "重斩击（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46096"])]
        public void TrackingFan_Alert(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;
            var tidStr = @event["TargetId"];
            if (string.IsNullOrEmpty(tidStr) || 
                !ulong.TryParse(tidStr.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var tid)) 
                return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Track_Fan_{aid}_{DateTime.Now.Ticks}";
            dp.Owner = @event.SourceId;
            dp.ScaleMode = ScaleMode.ByTime;
            dp.TargetObject = tid; 
            dp.Scale = new Vector2(60f); 
            dp.Radian = float.Pi / 4;   
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = dur;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
        [ScriptMethod(name: "轰击（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46133"])]
        public void TargetCircle_46133(Event @event, ScriptAccessory accessory)
        {
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;

            // 解析 TargetId
            var tidStr = @event["TargetId"];
            if (string.IsNullOrEmpty(tidStr) || 
                !ulong.TryParse(tidStr.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var tid)) 
                return;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Target_Circle_46133_{tid}_{DateTime.Now.Ticks}";   
            dp.Owner = tid;
            dp.Scale = new Vector2(4f);   
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.ScaleMode = ScaleMode.ByTime;
            dp.DestoryAt = dur;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(name: "兽焰连尾击（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46072|46128|46073|46129)$"])]
        public void FrontBackFan_Draw(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Fan_FB_{aid}_{DateTime.Now.Ticks}";
            dp.Position = @event.SourcePosition;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Scale = new Vector2(60f);    // 半径 60m
            dp.Radian = float.Pi / 2;       // 90度 (π/2)
            dp.DestoryAt = dur;
            dp.ScaleMode = ScaleMode.ByTime;
            // 1. 前扇形 (46072, 46128)
            if (aid == 46072 || aid == 46128)
            {
                dp.Rotation = @event.SourceRotation;
            }
            // 2. 后扇形 (46073, 46129)
            else if (aid == 46073 || aid == 46129)
            {
                dp.Rotation = @event.SourceRotation + float.Pi; // 转180度
            }
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
        [ScriptMethod(name: "登天碎地", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46064|46066|46068|46070|46155|46157|46159|46161)$"])]
        public void Rect_Gradient_40x40(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Rect_Grad_40x40_{aid}_{DateTime.Now.Ticks}";           
            dp.Position = @event.SourcePosition;
            dp.Rotation = @event.SourceRotation;          
            // 尺寸 40x40
            dp.Scale = new Vector2(40f, 40f);   
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = dur;
            dp.ScaleMode = ScaleMode.YByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(name: "喷火（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46137"])]
        public void Rect_Gradient_46137(Event @event, ScriptAccessory accessory)
        {
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Rect_Grad_60x6_46137_{DateTime.Now.Ticks}";
            
            dp.Position = @event.SourcePosition;
            dp.Rotation = @event.SourceRotation;
            dp.Scale = new Vector2(6f, 60f);    
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = dur;
            dp.ScaleMode = ScaleMode.YByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        #endregion


    }
}