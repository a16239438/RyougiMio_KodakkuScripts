using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using KodakkuAssist.Data;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Script;

namespace RyougiMioScriptNamespace
{
    [ScriptType(name: "(妖星乱舞绝境战)Dacing Mad Ulitmate", territorys: [1363], guid: "8041b30e-db21-4687-9175-2903eb7bc94d", version: "0.0.0.1", author: "RyougiMio", note: "初始脚本框架")]
    public class Script1363
    {
        #region Settings

        [UserSetting("-----全局设置-----")]
        public bool _____Global_Settings_____ { get; set; } = true;

        [UserSetting("是否开启屏幕文字提示")]
        public bool EnableText { get; set; } = true;

        [UserSetting("是否开启TTS语音提示")]
        public bool EnableTTS { get; set; } = true;

        [UserSetting("Developer mode")]
        public bool DeveloperMode { get; set; } = false;

        [UserSetting("开发模式：记录读条ActionId")]
        public bool LogStartCastingActionId { get; set; } = false;

        [UserSetting("常用危险色")]
        public ScriptColor DangerColor { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 0.0f, 0.0f, 0.01f) };

        [UserSetting("常用安全色")]
        public ScriptColor SafeColor { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 0.0f, 0.01f) };

        [UserSetting("指路/引导颜色")]
        public ScriptColor GuideColor { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 1.0f, 1.0f) };

        [UserSetting("P1打法")]
        public Phase1Strategy P1StrategySetting { get; set; } = Phase1Strategy.Default;

        public enum Phase1Strategy
        {
            Default,
        }

        #endregion

        #region State

        private enum Phase
        {
            Init,
            P1,
            P2,
            P3,
            P4,
            P5,
            P6,
            Done,
        }

        private const uint InvalidObjectId = 0xE0000000;
        private const int MaxLoggedActionIds = 256;
        private static readonly Vector3 ArenaCenter = new Vector3(100.0f, 0.0f, 100.0f);

        private ScriptAccessory _acc;
        private Phase _phase = Phase.P1;
        private int _generation;
        private long _lastMechanicAt;
        private readonly object _lock = new object();
        private readonly HashSet<uint> _seenCasts = new HashSet<uint>();
        private readonly HashSet<uint> _loggedActionIds = new HashSet<uint>();

        #endregion

        #region Initialization

        public void Init(ScriptAccessory accessory)
        {
            _acc = accessory;
            _phase = Phase.P1;
            _generation++;
            _lastMechanicAt = 0;

            lock (_lock)
            {
                _seenCasts.Clear();
                _loggedActionIds.Clear();
            }

            accessory.Method.RemoveDraw(".*");
            DebugEcho(accessory, "Dacing Mad Ulitmate initialized.");
        }

        private void ResetMechanic(ScriptAccessory accessory, bool removeDraw = true)
        {
            _generation++;
            _lastMechanicAt = 0;

            lock (_lock)
            {
                _seenCasts.Clear();
            }

            if (removeDraw)
                accessory.Method.RemoveDraw("DMU_.*");
        }

        private void SetPhase(ScriptAccessory accessory, Phase phase)
        {
            if (_phase == phase) return;

            _phase = phase;
            ResetMechanic(accessory);
            DebugEcho(accessory, $"Phase => {phase}");
        }

        #endregion

        #region Helpers

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

        private void Alert(string text, int duration = 3000, bool isWarning = true)
        {
            QTTS(text);
            QText(text, duration, isWarning);
        }

        private void DebugEcho(ScriptAccessory accessory, string message)
        {
            accessory.Log.Debug($"[DMU] {message}");
            if (DeveloperMode)
                accessory.Method.SendChat($"/e [DMU] {message}");
        }

        private static long NowMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private static int Duration(Event @event, int fallback = 5000)
        {
            return int.TryParse(@event["DurationMilliseconds"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var duration)
                && duration > 0
                ? duration
                : fallback;
        }

        private static bool TryParseObjectId(string text, out uint id)
        {
            id = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            text = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
            return uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id);
        }

        private static bool TryParseUInt(string text, out uint value)
        {
            value = 0;
            return !string.IsNullOrWhiteSpace(text)
                && uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetActionId(Event @event, out uint actionId)
        {
            return TryParseUInt(@event["ActionId"], out actionId);
        }

        private static bool TryGetSourceId(Event @event, out uint sourceId)
        {
            var raw = @event["SourceId"];
            if (string.IsNullOrWhiteSpace(raw))
                raw = @event["SourceID"];

            if (!TryParseObjectId(raw, out sourceId))
                sourceId = (uint)@event.SourceId;

            return sourceId != 0 && sourceId != InvalidObjectId;
        }

        private static bool TryGetTargetId(Event @event, out uint targetId)
        {
            var raw = @event["TargetId"];
            if (string.IsNullOrWhiteSpace(raw))
                raw = @event["TargetID"];

            if (!TryParseObjectId(raw, out targetId))
                targetId = (uint)@event.TargetId;

            return targetId != 0 && targetId != InvalidObjectId;
        }

        private static uint GetMyId(ScriptAccessory accessory)
        {
            var myObject = accessory.Data.MyObject;
            if (myObject != null)
            {
                var myObjectId = (uint)myObject.EntityId;
                if (myObjectId != 0 && myObjectId != InvalidObjectId)
                    return myObjectId;
            }

            return accessory.Data.Me;
        }

        private static int GetPlayerIndex(ScriptAccessory accessory, uint objectId)
        {
            return accessory.Data.PartyList.IndexOf(objectId);
        }

        private static int GetMyIndex(ScriptAccessory accessory)
        {
            return GetPlayerIndex(accessory, GetMyId(accessory));
        }

        private static bool IsMe(ScriptAccessory accessory, uint objectId)
        {
            return objectId == GetMyId(accessory);
        }

        private static Vector3 ExtendGame(Vector3 position, float gameRotation, float length)
        {
            return new Vector3(
                position.X + MathF.Sin(gameRotation) * length,
                position.Y,
                position.Z + MathF.Cos(gameRotation) * length);
        }

        private static Vector3 ExtendDir(Vector3 position, float directionRadians, float length)
        {
            return new Vector3(
                position.X + MathF.Sin(directionRadians) * length,
                position.Y,
                position.Z - MathF.Cos(directionRadians) * length);
        }

        private static float Norm2Pi(float radians)
        {
            var value = radians % (2.0f * MathF.PI);
            return value < 0 ? value + 2.0f * MathF.PI : value;
        }

        private static float GameToDir(float gameRotation)
        {
            return Norm2Pi(MathF.PI - gameRotation);
        }

        private static float DirToGame(float directionRadians)
        {
            return MathF.PI - directionRadians;
        }

        private static int Dir8(Vector3 position, Vector3 center)
        {
            var direction = Math.Round(4.0 - 4.0 * Math.Atan2(position.X - center.X, position.Z - center.Z) / Math.PI) % 8.0;
            return direction < 0 ? (int)direction + 8 : (int)direction;
        }

        private static string ValueOrDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        #endregion

        #region Draw Helpers

        private void DrawCircle(ScriptAccessory accessory, string name, Vector3 position, float radius, int duration, Vector4? color = null)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = position;
            dp.Scale = new Vector2(radius);
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.DestoryAt = duration;
            dp.ScaleMode = ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        private void DrawCircleOnOwner(ScriptAccessory accessory, string name, ulong ownerId, float radius, int duration, Vector4? color = null)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Owner = ownerId;
            dp.Scale = new Vector2(radius);
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.DestoryAt = duration;
            dp.ScaleMode = ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        private void DrawRect(Event @event, ScriptAccessory accessory, string name, float width, float length, int duration, Vector4? color = null)
        {
            DrawRect(accessory, name, @event.SourcePosition, @event.SourceRotation, width, length, duration, color);
        }

        private void DrawRect(ScriptAccessory accessory, string name, Vector3 position, float rotation, float width, float length, int duration, Vector4? color = null)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = position;
            dp.Rotation = rotation;
            dp.Scale = new Vector2(width, length);
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.DestoryAt = duration;
            dp.ScaleMode = ScaleMode.YByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        private void DrawFan(Event @event, ScriptAccessory accessory, string name, float radius, float radian, int duration, Vector4? color = null)
        {
            DrawFan(accessory, name, @event.SourcePosition, @event.SourceRotation, radius, radian, duration, color);
        }

        private void DrawFan(ScriptAccessory accessory, string name, Vector3 position, float rotation, float radius, float radian, int duration, Vector4? color = null)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = position;
            dp.Rotation = rotation;
            dp.Scale = new Vector2(radius);
            dp.Radian = radian;
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.DestoryAt = duration;
            dp.ScaleMode = ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        private void DrawTargetLine(ScriptAccessory accessory, string name, uint sourceId, uint targetId, float width, int duration, Vector4? color = null)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Owner = sourceId;
            dp.TargetObject = targetId;
            dp.Scale = new Vector2(width, 1.0f);
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.DestoryAt = duration;
            dp.ScaleMode = ScaleMode.YByDistance;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        private void DrawRectFromPositionToTarget(ScriptAccessory accessory, string name, Vector3 position, uint targetId, float width, float length, int duration, Vector4? color = null)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = position;
            dp.TargetObject = targetId;
            dp.Scale = new Vector2(width, length);
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.DestoryAt = duration;
            dp.ScaleMode = ScaleMode.YByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        private void DrawGuide(ScriptAccessory accessory, string name, Vector3 targetPosition, int duration, int delay = 0)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Owner = GetMyId(accessory);
            dp.TargetPosition = targetPosition;
            dp.Scale = new Vector2(0.5f);
            dp.ScaleMode = ScaleMode.YByDistance;
            dp.Color = GuideColor.V4;
            dp.Delay = delay;
            dp.DestoryAt = duration;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }

        #endregion

        #region Mechanisms

        #region P1

        [ScriptMethod(name: "P1 扩大大冰封", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47768|47771)$"], userControl: true)]
        public void P1_ExpandingGreatIcebound(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            _lastMechanicAt = NowMs();

            if (_phase == Phase.Init)
                SetPhase(accessory, Phase.P1);

            if (_phase != Phase.P1) return;
            if (!TryGetActionId(@event, out var actionId)) return;
            if (!TryGetSourceId(@event, out var sourceId)) return;

            var duration = Duration(@event) + 125;
            var drawName = $"DMU_P1_扩大大冰封_{actionId}_{sourceId:X8}";
            var rotation = actionId == 47768 ? @event.SourceRotation + MathF.PI / 2.0f : @event.SourceRotation;
            DrawFan(accessory, drawName, @event.SourcePosition, rotation, 40.0f, MathF.PI / 2.0f, duration, accessory.Data.DefaultSafeColor);
        }

        [ScriptMethod(name: "P1 劈啪啪暴雷", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47775|47776|47777)$"], userControl: true)]
        public void P1_CracklingThunder(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            _lastMechanicAt = NowMs();

            if (_phase == Phase.Init)
                SetPhase(accessory, Phase.P1);

            if (_phase != Phase.P1) return;
            if (!TryGetActionId(@event, out var actionId)) return;
            if (!TryGetSourceId(@event, out var sourceId)) return;

            var duration = Duration(@event) + 125;
            var drawName = $"DMU_P1_劈啪啪暴雷_{actionId}_{sourceId:X8}_{DateTime.Now.Ticks}";
            var color = actionId == 47776 ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
            DrawRect(accessory, drawName, @event.EffectPosition, @event.SourceRotation, 10.0f, 40.0f, duration, color);
        }

        [ScriptMethod(name: "P1 玩家头标真假", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(02A1|02A2)$"], userControl: true)]
        public void P1_PlayerHeadMarkerTrueFalse(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            _lastMechanicAt = NowMs();

            if (_phase != Phase.P1) return;

            var iconId = (@event["Id"] ?? string.Empty).ToUpperInvariant();
            if (iconId == "02A1")
            {
                Alert("玩家头标假", 5000, true);
                return;
            }

            if (iconId == "02A2")
                Alert("玩家头标真", 5000, true);
        }

        [ScriptMethod(name: "P1 ObjectEffect 64/128 玩家射线", eventType: EventTypeEnum.ObjectEffect, eventCondition: ["Id1:64", "Id2:128"], userControl: true)]
        public void P1_ObjectEffect64128_PlayerRays(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            _lastMechanicAt = NowMs();

            if (_phase != Phase.P1) return;

            var sourcePosition = @event.SourcePosition;
            var sourceId = @event.SourceId;
            const int duration = 5125;

            foreach (var playerId in accessory.Data.PartyList)
            {
                var drawName = $"DMU_P1_Obj64128_PlayerRay_{sourceId}_{playerId}_{DateTime.Now.Ticks}";
                DrawRectFromPositionToTarget(accessory, drawName, sourcePosition, playerId, 6.0f, 100.0f, duration);
            }
        }

        #endregion

        #region Developer

        [ScriptMethod(name: "开发模式-记录读条ActionId", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^\\d+$"], userControl: false)]
        public void LogStartCasting(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            _lastMechanicAt = NowMs();

            if (!DeveloperMode || !LogStartCastingActionId) return;
            if (!TryGetActionId(@event, out var actionId)) return;

            lock (_lock)
            {
                if (_loggedActionIds.Count >= MaxLoggedActionIds)
                    _loggedActionIds.Clear();

                if (!_loggedActionIds.Add(actionId))
                    return;
            }

            var sourceDataId = ValueOrDash(@event["SourceDataId"]);
            var duration = Duration(@event);
            var pos = @event.SourcePosition;
            DebugEcho(accessory, $"Cast ActionId={actionId} SourceDataId={sourceDataId} Duration={duration} Pos=({pos.X:F2},{pos.Y:F2},{pos.Z:F2}) Rot={@event.SourceRotation:F3}");
        }

        #endregion

        #endregion
    }
}
