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
    [ScriptType(name: "(M9S)AAC Heavyweight M1 (Savage)", territorys: [1320, 1321], guid: "ced5c285-484c-4750-bc85-241e927848f1", version: "0.0.0.1", author: "RyougiMio", note: "M9S Predict")]
    public class RyougiMio_1321
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
        private static long _chainsawTime = 0;
        private static int _chainsawDelay = 0;

        #endregion

        #region Methods

        // 静态字典：用来跨方法记录第一刀的时间
        private static Dictionary<uint, int> _moonPhaseDurations = new Dictionary<uint, int>();
        // 自定义TTS方法：自动检查 EnableTTS 开关
        private void QTTS(string text, int rate = 0)
        {
            if (!EnableTTS) return;
            _acc.Method.TTS(text,rate);
        }
        // 自定义文字提示方法：自动检查 EnableText 开关
        private void QText(string text, int duration,  bool isWarning = false)
        {
            if (!EnableText) return;
            _acc.Method.TextInfo(text, duration, isWarning);
        }



        #endregion

        #region Initialization 

        public void Init(ScriptAccessory accessory)
        {
            accessory.Method.RemoveDraw(".*");
            _moonPhaseDurations.Clear();
            _acc = accessory;
            _chainsawTime = 0;
            _chainsawDelay = 0;
            accessory.Method.SendChat("/e M9S Initialized.");
        }

        #endregion
        #region TTSonly 

        [ScriptMethod(name: "魅亡之音TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45921|45956)$"])]
        public void AOE_Alert(Event @event, ScriptAccessory accessory)
        {
            QTTS("AOE");
            QText("AOE", 4700, true);
        }

        [ScriptMethod(name: "硬核之声TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45915|45951|45916|45952)$"])]
        public void Hardcore_Voice(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;

            // 45916 和 45952 是强化版
            if (aid == 45916 || aid == 45952)
            {
                QTTS("强化双T死刑");
                QText("强化双T死刑", 5000, true);
            }
            else
            {
                // 45915 和 45951 是普通版
                QTTS("双T死刑");
                QText("双T死刑", 5000, true);
            }
        }
        [ScriptMethod(name: "剥蚀的低嗓_尖锐的音调TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45957|45958)$"])]
        public void Erosion_Voice_Pitch(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            // 45957 全体分散
            if (aid == 45957)
            {
                QTTS("分散");
                QText("全体分散", 5000, true);
            }
            // 45958 分组分摊
            else if (aid == 45958)
            {
                QTTS("分摊");
                QText("分组分摊", 5000, true);
            }
        }
        #endregion





        #region 左右刀

        [ScriptMethod(name: "月之半相_左右半场刀", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45906|45907|45910|45911|45943|45944|45947|45948)$"])]
        public void Half_Moon(Event @event, ScriptAccessory accessory)
        {
            // 1. 获取时间
            var totalDuration = int.Parse(@event["DurationMilliseconds"]);
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            // 2. ID 分组
            var leftSideIds = new HashSet<uint> { 45906, 45911, 45943, 45948 }; // 左组
            var secondHitIds = new HashSet<uint> { 45907, 45911, 45944, 45948 }; // 后手组
            var pairMap = new Dictionary<uint, uint>
            {
                { 45907, 45906 }, { 45911, 45910 },
                { 45944, 45943 }, { 45948, 45947 }
            };
            // 3. 基础参数
            var drawPos = new Vector3(100f, 0f, 100f);
            var srcRot = @event.SourceRotation;
            bool isLeft = leftSideIds.Contains(aid);
            bool isSecond = secondHitIds.Contains(aid);
            float width = 40f;
            float length = 40f;
            // 4. 角度旋转逻辑
            // 左组：转 90 度 (+PI/2)
            // 右组：转 -90 度 (-PI/2)
            float rotOffset = isLeft ? (MathF.PI / 2) : -(MathF.PI / 2);
            float finalRot = srcRot + rotOffset;
            // 5. 时间控制逻辑
            int delay = 0;
            int destory = totalDuration;
            if (!isSecond)
            {
                _moonPhaseDurations[aid] = totalDuration;
            }
            else
            {
                var firstId = pairMap[aid];
                var firstDuration = _moonPhaseDurations.ContainsKey(firstId) ? _moonPhaseDurations[firstId] : 5000;
                delay = firstDuration;
                destory = totalDuration - delay;
            }
            // 6. 绘制
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"月之半相_{aid}";

            dp.Position = drawPos;
            dp.Rotation = finalRot;
            dp.Scale = new Vector2(width, length);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = destory;
            dp.ScaleMode = ScaleMode.YByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            //QTTS("r-l", 0);
           //QText("r-l", 5000);
        }
        [ScriptMethod(name: "强化月之半相_左右刀", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45908|45909|45912|45913|45945|45946|45949|45950)$"])]
        public void Enhanced_Half_Moon(Event @event, ScriptAccessory accessory)
        {
            // 1. 获取时间
            var totalDuration = int.Parse(@event["DurationMilliseconds"]);
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            // 左组：转 +90 度
            var leftSideIds = new HashSet<uint> { 45908, 45913, 45945, 45950 };     
            // 后手组：需要延迟显示 (Wait for First)
            var secondHitIds = new HashSet<uint> { 45909, 45913, 45946, 45950 };
            // 配对表：Key = 后手ID, Value = 先手ID
            var pairMap = new Dictionary<uint, uint>
            {
                { 45909, 45908 }, 
                { 45913, 45912 },
                { 45946, 45945 }, 
                { 45950, 45949 }
            };

            // 3. 坐标处理 (EffectPosition 四舍五入取整, Y=0)
            // 这里使用 EffectPosition 并取整
            float px = MathF.Round(@event.EffectPosition.X);
            float pz = MathF.Round(@event.EffectPosition.Z);
            var drawPos = new Vector3(px, 0f, pz);
            // 基础参数
            var srcRot = @event.SourceRotation;
            bool isLeft = leftSideIds.Contains(aid);
            bool isSecond = secondHitIds.Contains(aid);      
            float width = 40f;
            float length = 40f;
            // 4. 角度旋转逻辑
            // 左组：转 90 度 (+PI/2)
            // 右组：转 -90 度 (-PI/2)
            float rotOffset = isLeft ? (MathF.PI / 2) : -(MathF.PI / 2);
            float finalRot = srcRot + rotOffset;
            // 5. 时间控制逻辑
            int delay = 0;
            int destory = totalDuration;
            // 如果是先手刀，记录 Duration 供后手查询
            if (!isSecond)
            {
                _moonPhaseDurations[aid] = totalDuration;
            }
            // 如果是后手刀，查询先手刀的 Duration 设定延迟
            else
            {
                if (pairMap.TryGetValue(aid, out var firstId))
                {
                    // 尝试获取先手的时间，默认5000
                    var firstDuration = _moonPhaseDurations.ContainsKey(firstId) ? _moonPhaseDurations[firstId] : 5000;                    
                    delay = firstDuration;
                    destory = totalDuration - delay; // 保证同时结束
                }
            }
            // 6. 绘制
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"强化月之半相_{aid}";

            dp.Position = drawPos;
            dp.Rotation = finalRot;
            dp.Scale = new Vector2(width, length);
            dp.Color = accessory.Data.DefaultDangerColor;
            
            dp.Delay = delay;
            dp.DestoryAt = destory;
            
            dp.ScaleMode = ScaleMode.YByTime;
            
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        #endregion
        #region 蝙蝠机制
        [ScriptMethod(name: "血魅的靴踏音", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45899|45940)$"])]
        public void Vamp_Stomp(Event @event, ScriptAccessory accessory)
        {
            var duration = int.Parse(@event["DurationMilliseconds"]);
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "血魅的靴踏音";
            dp.Position = new Vector3(100f, 0f, 100f);
            dp.Scale = new Vector2(10f);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = duration;
            dp.ScaleMode = ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        #endregion
        #region 锤锯刃
        [ScriptMethod(name: "致命刑锯_前进", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45877|45927)$"])]
        public void Chainsaw_Forward(Event @event, ScriptAccessory accessory)
        {
            var duration = int.Parse(@event["DurationMilliseconds"]);
            var rawPos = @event.EffectPosition;
            // 四舍五入取整
            var drawPos = new Vector3(
                MathF.Round(rawPos.X),
                0,
                MathF.Round(rawPos.Z)
            );
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "致命刑锯_前进";
            dp.Position = drawPos;
            dp.Rotation = @event.SourceRotation;
            dp.Scale = new Vector2(20f, 10f);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = duration;
            dp.ScaleMode = ScaleMode.YByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(name: "致命刑锯_冲出", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45878|45879|45880|45928|45929|45930)$"])]
        public void Chainsaw_RushOut(Event @event, ScriptAccessory accessory)
        {
            var duration = int.Parse(@event["DurationMilliseconds"]);
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            // 1. 长度判断
            float length = 32f;
            if (aid == 45879 || aid == 45929) length = 22f;
            if (aid == 45880 || aid == 45930) length = 12f;
            // 2. 时间逻辑
            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            long diff = now - _chainsawTime;     
            int finalDelay = 0;
            int finalDuration = duration;
            // 情况A: 初始化 或 间隔太久(>5s) -> 新的一轮机制
            if (_chainsawTime == 0 || Math.Abs(diff) > 5000)
            {
                finalDelay = 0;         
                _chainsawDelay = 0;
                _chainsawTime = now;
            }
            // 情况B: 时差 < 1s -> 同一波次
            else if (diff < 1000)
            {
                finalDelay = _chainsawDelay;
                finalDuration =duration-_chainsawDelay;
            }
            // 情况C: 时差 > 1s -> 连续波次 (第二波、第三波...)
            else
            {
                finalDelay = duration - (int)diff;
                finalDuration = (int)diff;
                _chainsawDelay = finalDelay; 
                _chainsawTime = now;         
            }
            // 3. 绘制
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"致命刑锯_{aid}";
            dp.Position = @event.SourcePosition;
            dp.Rotation = @event.SourceRotation;
            dp.Scale = new Vector2(5f, length);
            dp.Color = accessory.Data.DefaultDangerColor;           
            dp.Delay = finalDelay;
            dp.DestoryAt = finalDuration;         
            dp.ScaleMode = ScaleMode.YByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(name: "致命刑轮锯", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(19173|19174)$"])]
        public void Straight_Entity_Spawn(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["DataId"], out var id)) return;
            int duration = 60000; 
            float width = 0f;
            float length = 0f;
            if (id == 19173)
            {
                width = 6f;
                length = 8f;
            }
            else if (id == 19174)
            {
                width = 12f;
                length = 5f;
            }
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"致命刑轮锯_{@event.SourceId}";
            dp.Owner = @event.SourceId;
            dp.Rotation = @event.SourceRotation;
            dp.Scale = new Vector2(width, length);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = duration;
            dp.ScaleMode = ScaleMode.None; 
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
        }
        #endregion
        #region 以太流失
        [ScriptMethod(name: "以太流失_十字", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45897|45971)$"])]
        public void Fatal_Cross_AOE(Event @event, ScriptAccessory accessory)
        {
            var duration = int.Parse(@event["DurationMilliseconds"]);
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            float radius = 20f;
            float width = (aid == 45971) ? 10f : 6f;
            // 2. 循环 4 次，画 4 个方向
            for (int i = 0; i < 4; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"以太流失_{aid}_{i}";
                dp.Position = @event.SourcePosition;           
                dp.Rotation = @event.SourceRotation + (float)(Math.PI / 2 * i);
                dp.Scale = new Vector2(width, radius);                
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.DestoryAt = duration;
                dp.ScaleMode = ScaleMode.YByTime;             
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
        }
        [ScriptMethod(name: "以太流失_扇形（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45969)$"])]
        public void Fan_45_AOE(Event @event, ScriptAccessory accessory)
        {
            var duration = int.Parse(@event["DurationMilliseconds"]);          
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "以太流失_扇形";     
            dp.Position = @event.SourcePosition;
            dp.Rotation = @event.SourceRotation;            
            // Scale.X = 弧度 (45度 = PI/4)
            // Scale.Y = 半径 (40米)
            dp.Scale = new Vector2((float)Math.PI / 4, 40f);            
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = duration;         
            dp.ScaleMode = ScaleMode.ByTime;         
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        #endregion


    }
}