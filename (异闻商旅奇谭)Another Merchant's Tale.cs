using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using KodakkuAssist.Data;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Script;

namespace RyougiMioScriptNamespace
{
    [ScriptType(name: "(异闻商旅奇谭)Another Merchant's Tale", territorys: [1317], guid: "41658b2f-191c-4d68-98c3-c26a10ddce67", version: "0.0.0.63", author: "RyougiMio", note: "Another Merchant's Tale test")]
    public class Script1317
    {
        #region Settings
        [UserSetting("是否开启屏幕文字提示")]
        public bool EnableText { get; set; } = true;

        [UserSetting("是否开启TTS语音提示")]
        public bool EnableTTS { get; set; } = true;

        [UserSetting("常用危险色")]
        public ScriptColor DangerColor { get; set; } = new ScriptColor() { V4 = new Vector4(1.0f, 0.0f, 0.0f, 0.01f) };

        [UserSetting("常用安全色")]
        public ScriptColor SafeColor { get; set; } = new ScriptColor() { V4 = new Vector4(0.0f, 1.0f, 0.0f, 0.01f) };

        [UserSetting("指路/引导颜色")]
        public ScriptColor GuideColor { get; set; } = new ScriptColor() { V4 = new Vector4(0.0f, 1.0f, 1.0f, 0.01f) };

        [UserSetting("2161-2164调试输出")]
        public bool Debug2161To2164 { get; set; } = true;
        #endregion

        #region Variables
        private ScriptAccessory _acc;
        private int _phase = 0;
        private long _lastMechanicTicks = 0;
        private readonly (string Name, Vector3 Position, float Rotation, Vector2 Scale)[] _icon0014RowPreset =
        [
            ("Icon0014_Row_0", new Vector3(355f, -29.5f, 514f), MathF.PI * 0.5f, new Vector2(8f, 40f)),
            ("Icon0014_Row_1", new Vector3(355f, -29.5f, 522f), MathF.PI * 0.5f, new Vector2(8f, 40f)),
            ("Icon0014_Row_2", new Vector3(355f, -29.5f, 530f), MathF.PI * 0.5f, new Vector2(8f, 40f)),
            ("Icon0014_Row_3", new Vector3(355f, -29.5f, 538f), MathF.PI * 0.5f, new Vector2(8f, 40f)),
            ("Icon0014_Row_4", new Vector3(355f, -29.5f, 546f), MathF.PI * 0.5f, new Vector2(8f, 40f)),
        ];
        private readonly (string Name, Vector3 Position, float Rotation, Vector2 Scale)[] _icon0014ColPreset =
        [
            ("Icon0014_Col_0", new Vector3(359f, -29.5f, 510f), 0f, new Vector2(8f, 40f)),
            ("Icon0014_Col_1", new Vector3(367f, -29.5f, 510f), 0f, new Vector2(8f, 40f)),
            ("Icon0014_Col_2", new Vector3(375f, -29.5f, 510f), 0f, new Vector2(8f, 40f)),
            ("Icon0014_Col_3", new Vector3(383f, -29.5f, 510f), 0f, new Vector2(8f, 40f)),
            ("Icon0014_Col_4", new Vector3(391f, -29.5f, 510f), 0f, new Vector2(8f, 40f)),
        ];
        #endregion

        #region Methods
        private void QTTS(string text, int rate = 0)
        {
            if (!EnableTTS || _acc == null) return;
            _acc.Method.TTS(text, rate);
        }

        private void QText(string text, int duration, bool isWarning = false)
        {
            if (!EnableText || _acc == null) return;
            _acc.Method.TextInfo(text, duration, isWarning);
        }
        #endregion

        #region Initialization
        public void Init(ScriptAccessory accessory)
        {
            _acc = accessory;
            _phase = 0;
            _lastMechanicTicks = 0;

            accessory.Method.RemoveDraw(".*");

            accessory.Method.SendChat("/e Another Merchant's Tale Initialized.");
        }
        #endregion

        #region ScriptMethods
        [ScriptMethod(name: "45870 AOE TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:45870"])]
        public void AOE_Alert_45870(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            _lastMechanicTicks = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            if (aid != 45870) return;

            QTTS("AOE");
            QText("AOE", 3000, true);
        }

        [ScriptMethod(name: "45839/45841/45840 危险渐变矩形", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45839|45841|45840)$"])]
        public void DrawDangerRect_45839_45841(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            _lastMechanicTicks = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (!uint.TryParse(@event["ActionId"], out var aid)) return;

            int duration = 5000;
            if (int.TryParse(@event["DurationMilliseconds"], out var dur) && dur > 0)
                duration = dur;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Rect_{aid}_{DateTime.Now.Ticks}";
            dp.Position = @event.SourcePosition;
            dp.Rotation = @event.SourceRotation;
            dp.Scale = new Vector2(8f, 40f);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = duration;
            dp.ScaleMode = ScaleMode.YByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        [ScriptMethod(name: "45843 危险渐变45度扇形", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:45843"])]
        public void DrawDangerFan_45843(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            _lastMechanicTicks = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            int duration = 5000;
            if (int.TryParse(@event["DurationMilliseconds"], out var dur) && dur > 0)
                duration = dur;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Fan45_45843_{DateTime.Now.Ticks}";
            dp.Position = @event.SourcePosition;
            dp.Rotation = @event.SourceRotation;
            dp.Scale = new Vector2(45f);
            dp.Radian = float.Pi / 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = duration;
            dp.ScaleMode = ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        [ScriptMethod(name: "45842 危险渐变半圆", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:45842"])]
        public void DrawDangerSemiCircle_45842(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            _lastMechanicTicks = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            int duration = 5000;
            if (int.TryParse(@event["DurationMilliseconds"], out var dur) && dur > 0)
                duration = dur;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"SemiCircle_45842_{DateTime.Now.Ticks}";
            dp.Position = @event.SourcePosition;
            dp.Rotation = @event.SourceRotation;
            dp.Scale = new Vector2(20f);
            dp.Radian = float.Pi;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = duration;
            dp.ScaleMode = ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        [ScriptMethod(name: "45866 危险渐变90度扇形(最后3秒)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:45866"])]
        public void DrawDangerFanLast3s_45866(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            _lastMechanicTicks = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            int duration = 5000;
            if (int.TryParse(@event["DurationMilliseconds"], out var dur) && dur > 0)
                duration = dur;

            int showMs = Math.Min(3000, duration);
            int delayMs = Math.Max(0, duration - 3000);

            float rotation = @event.SourceRotation;
            if (float.TryParse(@event["TargetRotation"], NumberStyles.Float, CultureInfo.InvariantCulture, out var targetRot))
                rotation = targetRot;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Fan90_45866_{DateTime.Now.Ticks}";
            dp.Position = @event.SourcePosition;
            dp.Rotation = rotation;
            dp.Scale = new Vector2(25f);
            dp.Radian = float.Pi / 2;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = delayMs;
            dp.DestoryAt = showMs;
            dp.ScaleMode = ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        [ScriptMethod(name: "45863 中心矩形步进5次", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:45863"])]
        public void DrawStepRect_45863(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            _lastMechanicTicks = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            float rot = @event.SourceRotation;
            Vector3 src = @event.SourcePosition;
            int firstDuration = 2000;
            if (int.TryParse(@event["DurationMilliseconds"], out var dur) && dur > 0)
                firstDuration = dur;

            for (int i = 0; i < 5; i++)
            {
                float forward = i * 8f - 4f;
                Vector3 pos = new(
                    src.X + MathF.Sin(rot) * forward,
                    src.Y,
                    src.Z + MathF.Cos(rot) * forward
                );

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"Rect_45863_{i}_{DateTime.Now.Ticks}";
                dp.Position = pos;
                dp.Rotation = rot;
                dp.Scale = new Vector2(40f, 8f);
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = i == 0 ? 0 : firstDuration + (i - 1) * 2125;
                dp.DestoryAt = i == 0 ? firstDuration : 2125;
                dp.ScaleMode = ScaleMode.None;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
        }

        [ScriptMethod(name: "2015003 危险圆(ObjectChanged Add/Remove)", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:2015003"])]
        public void ObjectChanged_2015003_Circle(Event @event, ScriptAccessory accessory)
        {
            string operate = @event["Operate"] ?? string.Empty;

            ulong sid = @event.SourceId;
            if (sid == 0)
            {
                string rawSourceId = @event["SourceId"];
                if (!string.IsNullOrWhiteSpace(rawSourceId))
                {
                    if (!ulong.TryParse(rawSourceId, out sid))
                    {
                        string normalized = rawSourceId.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                            ? rawSourceId.Substring(2)
                            : rawSourceId;
                        ulong.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out sid);
                    }
                }
            }
            if (sid == 0) return;

            string drawName = $"Circle_2015003_{sid}";
            if (operate.Equals("Add", StringComparison.OrdinalIgnoreCase))
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = drawName;
                dp.Owner = sid;
                dp.Position = @event.SourcePosition;
                dp.Scale = new Vector2(12f);
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.DestoryAt = 3600000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                return;
            }

            if (operate.Equals("Remove", StringComparison.OrdinalIgnoreCase))
            {
                accessory.Method.RemoveDraw(drawName);
            }
        }

        [ScriptMethod(name: "2015004/2015005 位置判定危险区(ObjectEffect16|32)", eventType: EventTypeEnum.ObjectEffect, eventCondition: ["Id1:16", "Id2:32"])]
        public void ObjectEffect_2015004_2015005_ByPosition(Event @event, ScriptAccessory accessory)
        {
            Vector3 pos = @event.SourcePosition;
            bool isSteel = (pos.X >= 370f && pos.X <= 380f) || (pos.Z >= 525f && pos.Z <= 535f);

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Obj_{(isSteel ? "Steel" : "Donut")}_{DateTime.Now.Ticks}";
            dp.Position = pos;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 10000;
            dp.ScaleMode = ScaleMode.ByTime;

            if (isSteel)
            {
                dp.Scale = new Vector2(18f);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
            else
            {
                dp.Scale = new Vector2(20f);
                dp.InnerScale = new Vector2(4f);
                dp.Radian = float.Pi * 2;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
            }
        }

        [ScriptMethod(name: "2161-2164 Buff绑身射线", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(2161|2162|2163|2164)$"])]
        public void OnStatusAdd_2161_2164_BindLine(Event @event, ScriptAccessory accessory)
        {
            string status = @event["StatusID"];
            if (string.IsNullOrWhiteSpace(status)) status = @event["StatusId"];
            string rawTargetId = @event["TargetId"];
            if (string.IsNullOrWhiteSpace(rawTargetId)) rawTargetId = @event["TargetID"];
            if (string.IsNullOrWhiteSpace(rawTargetId)) return;
            string targetIdHex = rawTargetId.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? rawTargetId.Substring(2) : rawTargetId;
            targetIdHex = targetIdHex.PadLeft(16, '0');
            if (!ulong.TryParse(targetIdHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var tid)) return;

            float radOffset = 0f;
            if (status == "2161") radOffset = 0f;                    // 前
            else if (status == "2162") radOffset = MathF.PI;         // 后
            else if (status == "2163") radOffset = 0.5f * MathF.PI;  // 左
            else if (status == "2164") radOffset = -0.5f * MathF.PI; // 右

            var dpLine = accessory.Data.GetDefaultDrawProperties();
            dpLine.Name = $"GuideLine_216x_{tid}_{DateTime.Now.Ticks}";
            dpLine.Owner = tid;
            dpLine.Rotation = radOffset;
            dpLine.Scale = new Vector2(0.5f, 16.5f);
            dpLine.Color = accessory.Data.DefaultSafeColor;
            dpLine.DestoryAt = 19000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dpLine);
        }

        [ScriptMethod(name: "TargetIcon0014 同行同列危险矩形(异步)", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0014"])]
        public void OnTargetIcon_0014_RowColDanger(Event @event, ScriptAccessory accessory)
        {
            string rawTargetId = @event["TargetId"];
            if (string.IsNullOrWhiteSpace(rawTargetId)) rawTargetId = @event["TargetID"];
            if (string.IsNullOrWhiteSpace(rawTargetId)) return;
            string normalizedTargetId = rawTargetId.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? rawTargetId.Substring(2)
                : rawTargetId;
            if (string.IsNullOrWhiteSpace(normalizedTargetId)) return;

            if (!ulong.TryParse(normalizedTargetId, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var tid)) return;
            ulong me = accessory.Data.Me;
            bool isSelf = me != 0 && tid == me;
            if (Debug2161To2164)
                accessory.Method.SendChat($"/e [0014DBG] target={normalizedTargetId} tid={tid} me={me} self={isSelf}");
            if (isSelf) return;

            const float minX = 355f;
            const float minZ = 510f;
            const float cellSize = 8f;
            const int totalMs = 10000;
            Vector4 presetColor = new Vector4(1.0f, 1.0f, 0.0f, 0.5f);
            int startRow = -1;
            int startCol = -1;
            var obj = accessory.Data.Objects.SearchById(tid);
            if (obj != null)
            {
                Vector3 pos = obj.Position;
                startCol = (int)MathF.Floor((pos.X - minX) / cellSize);
                startRow = (int)MathF.Floor((pos.Z - minZ) / cellSize);
                if (startCol < 0) startCol = 0; else if (startCol > 4) startCol = 4;
                if (startRow < 0) startRow = 0; else if (startRow > 4) startRow = 4;

                var rowPreset = _icon0014RowPreset[startRow];
                var colPreset = _icon0014ColPreset[startCol];
                var dpRow = accessory.Data.GetDefaultDrawProperties();
                dpRow.Name = $"{rowPreset.Name}_{normalizedTargetId}";
                dpRow.Position = rowPreset.Position;
                dpRow.Rotation = rowPreset.Rotation;
                dpRow.Scale = rowPreset.Scale;
                dpRow.Color = presetColor;
                dpRow.DestoryAt = totalMs;
                dpRow.ScaleMode = ScaleMode.None;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpRow);

                var dpCol = accessory.Data.GetDefaultDrawProperties();
                dpCol.Name = $"{colPreset.Name}_{normalizedTargetId}";
                dpCol.Position = colPreset.Position;
                dpCol.Rotation = colPreset.Rotation;
                dpCol.Scale = colPreset.Scale;
                dpCol.Color = presetColor;
                dpCol.DestoryAt = totalMs;
                dpCol.ScaleMode = ScaleMode.None;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpCol);
            }

            _ = DrawTargetIcon0014RowColDangerAsync(normalizedTargetId, startRow, startCol, accessory);
        }

        private async Task DrawTargetIcon0014RowColDangerAsync(string targetIdHex, int startRow, int startCol, ScriptAccessory accessory)
        {
            if (!ulong.TryParse(targetIdHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var tid)) return;

            const float minX = 355f;
            const float minZ = 510f;
            const float cellSize = 8f;
            const int intervalMs = 50;
            const int totalMs = 8000;
            Vector4 presetColor = new Vector4(1.0f, 1.0f, 0.0f, 0.5f);

            var obj = accessory.Data.Objects.SearchById(tid);
            int lastRow = startRow;
            int lastCol = startCol;
            string rowDrawName = startRow >= 0 ? $"{_icon0014RowPreset[startRow].Name}_{targetIdHex}" : string.Empty;
            string colDrawName = startCol >= 0 ? $"{_icon0014ColPreset[startCol].Name}_{targetIdHex}" : string.Empty;

            int elapsed = 0;
            while (elapsed < totalMs)
            {
                if (obj != null)
                {
                    Vector3 pos = obj.Position;

                    int col = (int)MathF.Floor((pos.X - minX) / cellSize);
                    int row = (int)MathF.Floor((pos.Z - minZ) / cellSize);
                    if (col < 0) col = 0; else if (col > 4) col = 4;
                    if (row < 0) row = 0; else if (row > 4) row = 4;

                    if (col != lastCol || row != lastRow)
                    {
                        int remain = totalMs - elapsed;
                        if (remain < intervalMs) remain = intervalMs;
                        if (row != lastRow)
                        {
                            if (!string.IsNullOrWhiteSpace(rowDrawName)) accessory.Method.RemoveDraw(rowDrawName);
                            var rowPreset = _icon0014RowPreset[row];
                            rowDrawName = $"{rowPreset.Name}_{targetIdHex}";
                            var dpRow = accessory.Data.GetDefaultDrawProperties();
                            dpRow.Name = rowDrawName;
                            dpRow.Position = rowPreset.Position;
                            dpRow.Rotation = rowPreset.Rotation;
                            dpRow.Scale = rowPreset.Scale;
                            dpRow.Color = presetColor;
                            dpRow.DestoryAt = remain;
                            dpRow.ScaleMode = ScaleMode.None;
                            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpRow);
                            lastRow = row;
                        }
                        if (col != lastCol)
                        {
                            if (!string.IsNullOrWhiteSpace(colDrawName)) accessory.Method.RemoveDraw(colDrawName);
                            var colPreset = _icon0014ColPreset[col];
                            colDrawName = $"{colPreset.Name}_{targetIdHex}";
                            var dpCol = accessory.Data.GetDefaultDrawProperties();
                            dpCol.Name = colDrawName;
                            dpCol.Position = colPreset.Position;
                            dpCol.Rotation = colPreset.Rotation;
                            dpCol.Scale = colPreset.Scale;
                            dpCol.Color = presetColor;
                            dpCol.DestoryAt = remain;
                            dpCol.ScaleMode = ScaleMode.None;
                            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpCol);
                            lastCol = col;
                        }
                    }
                }

                await Task.Delay(intervalMs);
                elapsed += intervalMs;
                obj = accessory.Data.Objects.SearchById(tid);
            }

            if (!string.IsNullOrWhiteSpace(rowDrawName)) accessory.Method.RemoveDraw(rowDrawName);
            if (!string.IsNullOrWhiteSpace(colDrawName)) accessory.Method.RemoveDraw(colDrawName);
        }
        #endregion
    }
}
