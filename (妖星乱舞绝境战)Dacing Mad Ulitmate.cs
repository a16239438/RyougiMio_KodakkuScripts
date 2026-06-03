using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.STD.Helper;
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

        [UserSetting("P1 技能特效屏蔽（实验性，默认关闭）")]
        public bool EnableP1SkillVfxHide { get; set; } = false;

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
        private const int VisibilityRecoveryDelay = 125;
        private const int P1StatusGuideDuration = 15000;
        private const float P1StatusGuideCircleRadius = 1.2f;
        private const string P1StatusGuideDrawPrefix = "DMU_P1_StatusGuide";
        private static readonly Vector3 ArenaCenter = new Vector3(100.0f, 0.0f, 100.0f);
        private static readonly Vector4 SolidDangerRed = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
        private static readonly Vector4 SolidSafeGreen = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);

        private ScriptAccessory _acc;
        private Phase _phase = Phase.P1;
        private int _generation;
        private long _lastMechanicAt;
        private bool _p1ObjectEffect64128PlayerRaysDrawn;
        private int _p1ObjectEffect64128Count;
        private string _p1PendingPlayerTargetIcon = string.Empty;
        private string _p1PendingBossTargetIcon = string.Empty;
        private int _p1HeadMarkerPairGeneration;
        private int _p1StatusGuideGeneration;
        private bool _p1StatusGuideActive;
        private P1StatusGuideStep _p1StatusGuideStep1;
        private P1StatusGuideStep _p1StatusGuideStep2;
        private readonly List<P1StatusRecord> _p1StatusGuideStatuses = new List<P1StatusRecord>();
        private readonly object _lock = new object();
        private readonly HashSet<uint> _seenCasts = new HashSet<uint>();
        private readonly HashSet<uint> _loggedActionIds = new HashSet<uint>();

        private struct P1StatusRecord
        {
            public uint StatusId;
            public long AddedAt;
            public int DurationMs;
        }

        private struct P1StatusGuideStep
        {
            public uint StatusId;
            public Vector3 Position;
        }

        #endregion

        #region Initialization

        public void Init(ScriptAccessory accessory)
        {
            _acc = accessory;
            _phase = Phase.P1;
            _generation++;
            _lastMechanicAt = 0;
            _p1ObjectEffect64128PlayerRaysDrawn = false;
            _p1ObjectEffect64128Count = 0;
            _p1PendingPlayerTargetIcon = string.Empty;
            _p1PendingBossTargetIcon = string.Empty;
            _p1HeadMarkerPairGeneration = 0;
            _p1StatusGuideGeneration = 0;
            _p1StatusGuideActive = false;
            _p1StatusGuideStatuses.Clear();

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
            _p1StatusGuideGeneration++;
            _p1StatusGuideActive = false;
            _p1StatusGuideStatuses.Clear();

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

        private static int StatusDuration(Event @event, int fallback = 15000)
        {
            if (int.TryParse(@event["DurationMilliseconds"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var durationMs) && durationMs > 0)
                return durationMs;

            if (int.TryParse(@event["DurationMs"], NumberStyles.Integer, CultureInfo.InvariantCulture, out durationMs) && durationMs > 0)
                return durationMs;

            if (float.TryParse(@event["Duration"], NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) && duration > 0)
                return duration <= 1000.0f ? (int)MathF.Round(duration * 1000.0f) : (int)MathF.Round(duration);

            return fallback;
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

        private static bool TryGetStatusId(Event @event, out uint statusId)
        {
            var raw = @event["StatusID"];
            if (string.IsNullOrWhiteSpace(raw))
                raw = @event["StatusId"];

            return TryParseUInt(raw, out statusId);
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

        private static string NormalizeIconId(string iconId)
        {
            if (string.IsNullOrWhiteSpace(iconId)) return string.Empty;

            iconId = iconId.Trim();
            if (iconId.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                iconId = iconId[2..];

            return iconId.ToUpperInvariant().PadLeft(4, '0');
        }

        private static bool TryGetTargetPlayerId(Event @event, ScriptAccessory accessory, out uint targetId)
        {
            targetId = 0;
            return TryGetTargetId(@event, out targetId) && GetPlayerIndex(accessory, targetId) >= 0;
        }

        private static string FormatObjectId(uint objectId)
        {
            return objectId == 0 ? "-" : objectId.ToString("X8", CultureInfo.InvariantCulture);
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

        private static bool IsNear(Vector3 value, Vector3 expected, float tolerance = 0.5f)
        {
            return MathF.Abs(value.X - expected.X) <= tolerance
                && MathF.Abs(value.Y - expected.Y) <= tolerance
                && MathF.Abs(value.Z - expected.Z) <= tolerance;
        }

        private static Vector3 RotateAround(Vector3 point, Vector3 center, float radians)
        {
            var x = point.X - center.X;
            var z = point.Z - center.Z;
            var sin = MathF.Sin(radians);
            var cos = MathF.Cos(radians);

            return new Vector3(
                center.X + x * cos + z * sin,
                point.Y,
                center.Z - x * sin + z * cos);
        }

        #endregion

        #region Unsafe Commons

        public static unsafe void AdjustVisibility(ScriptAccessory accessory, IGameObject? targetIGameObject, bool isVisible, int recoveryDelay = -1)
        {
            if (targetIGameObject == null || !targetIGameObject.IsValid())
                return;

            try
            {
                var targetGameObject = (GameObject*)targetIGameObject.Address;
                var originalVisibility = targetGameObject->RenderFlags;

                targetGameObject->RenderFlags = isVisible ? VisibilityFlags.None : VisibilityFlags.Model;

                if (recoveryDelay <= 0)
                    return;

                Task.Delay(recoveryDelay).ContinueWith(_ =>
                {
                    if (targetIGameObject == null || !targetIGameObject.IsValid())
                        return;

                    try
                    {
                        var targetGameObject = (GameObject*)targetIGameObject.Address;
                        targetGameObject->RenderFlags = originalVisibility;
                    }
                    catch (Exception ex)
                    {
                        accessory.Log.Error(ex.ToString());
                    }
                });
            }
            catch (Exception ex)
            {
                accessory.Log.Error(ex.ToString());
            }
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

        private void DrawStaticCircle(ScriptAccessory accessory, string name, Vector3 position, float radius, int duration, Vector4? color = null)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = position;
            dp.Scale = new Vector2(radius);
            dp.Color = color ?? accessory.Data.DefaultSafeColor;
            dp.DestoryAt = duration;
            dp.ScaleMode = ScaleMode.None;
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

        private void DrawRect(Event @event, ScriptAccessory accessory, string name, float width, float length, int duration, Vector4? color = null, ScaleMode scaleMode = ScaleMode.YByTime)
        {
            DrawRect(accessory, name, @event.SourcePosition, @event.SourceRotation, width, length, duration, color, scaleMode);
        }

        private void DrawRect(ScriptAccessory accessory, string name, Vector3 position, float rotation, float width, float length, int duration, Vector4? color = null, ScaleMode scaleMode = ScaleMode.YByTime)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = position;
            dp.Rotation = rotation;
            dp.Scale = new Vector2(width, length);
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.DestoryAt = duration;
            dp.ScaleMode = scaleMode;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        private void DrawFan(Event @event, ScriptAccessory accessory, string name, float radius, float radian, int duration, Vector4? color = null, ScaleMode scaleMode = ScaleMode.ByTime)
        {
            DrawFan(accessory, name, @event.SourcePosition, @event.SourceRotation, radius, radian, duration, color, scaleMode);
        }

        private void DrawFan(ScriptAccessory accessory, string name, Vector3 position, float rotation, float radius, float radian, int duration, Vector4? color = null, ScaleMode scaleMode = ScaleMode.ByTime)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = position;
            dp.Rotation = rotation;
            dp.Scale = new Vector2(radius);
            dp.Radian = radian;
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.DestoryAt = duration;
            dp.ScaleMode = scaleMode;
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

        private void DrawGuide(ScriptAccessory accessory, string name, Vector3 targetPosition, int duration, int delay = 0, Vector4? color = null)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Owner = GetMyId(accessory);
            dp.TargetPosition = targetPosition;
            dp.Scale = new Vector2(0.5f);
            dp.ScaleMode = ScaleMode.YByDistance;
            dp.Color = color ?? GuideColor.V4;
            dp.Delay = delay;
            dp.DestoryAt = duration;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        }

        #endregion

        #region Mechanisms

        #region P1

        [ScriptMethod(name: "P1 技能特效屏蔽", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47768|47771|47775|47776)$"], userControl: true)]
        public void P1_SkillVfxHide(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            _lastMechanicAt = NowMs();

            if (!EnableP1SkillVfxHide) return;

            if (_phase == Phase.Init)
                SetPhase(accessory, Phase.P1);

            if (_phase != Phase.P1) return;
            if (!TryGetActionId(@event, out var actionId)) return;
            if (!TryGetSourceId(@event, out var sourceId)) return;

            var sourceObject = accessory.Data.Objects.SearchById(sourceId);
            if (sourceObject == null || !sourceObject.IsValid())
            {
                DebugEcho(accessory, $"SkillVfxHide skip invalid source action={actionId} source={FormatObjectId(sourceId)}");
                return;
            }

            AdjustVisibility(accessory, sourceObject, false, VisibilityRecoveryDelay + 5000);
            DebugEcho(accessory, $"SkillVfxHide hide source action={actionId} source={FormatObjectId(sourceId)} recovery={VisibilityRecoveryDelay + 5000}");
        }

        [ScriptMethod(name: "P1 扩大大冰封", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47768|47774)$"], userControl: true)]
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
            DrawFan(@event, accessory, drawName, 40.0f, MathF.PI / 2.0f, duration, SolidDangerRed, ScaleMode.None);
        }

        [ScriptMethod(name: "P1 劈啪啪暴雷", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47775|47777)$"], userControl: true)]
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
            DrawRect(accessory, drawName, @event.EffectPosition, @event.SourceRotation, 10.0f, 40.0f, duration, SolidDangerRed, ScaleMode.None);
        }

        [ScriptMethod(name: "P1 状态指路-收集", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(4876|4877|4878|4879|5079|5080|5081|5082)$"], userControl: true)]
        public void P1_StatusGuide_StatusAdd(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            _lastMechanicAt = NowMs();

            if (_phase == Phase.Init)
                SetPhase(accessory, Phase.P1);

            if (_phase != Phase.P1) return;
            if (!TryGetStatusId(@event, out var statusId)) return;
            if (!TryGetTargetId(@event, out var targetId) || !IsMe(accessory, targetId)) return;

            var record = new P1StatusRecord
            {
                StatusId = statusId,
                AddedAt = NowMs(),
                DurationMs = StatusDuration(@event, P1StatusGuideDuration),
            };

            var shouldDraw = false;
            var circle1 = default(Vector3);
            var circle2 = default(Vector3);
            var step1 = default(P1StatusGuideStep);
            var step2 = default(P1StatusGuideStep);
            var drawGeneration = 0;
            var debugMessage = string.Empty;

            lock (_lock)
            {
                if (_p1StatusGuideActive)
                {
                    debugMessage = $"StatusGuide add ignored active status={statusId} duration={record.DurationMs} count={_p1StatusGuideStatuses.Count}";
                }
                else
                {
                    _p1StatusGuideStatuses.Add(record);
                    debugMessage = $"StatusGuide add status={statusId} duration={record.DurationMs} count={_p1StatusGuideStatuses.Count}";

                    if (_p1StatusGuideStatuses.Count == 2)
                    {
                        if (TryBuildP1StatusGuidePlan(_p1StatusGuideStatuses[0], _p1StatusGuideStatuses[1], out circle1, out circle2, out step1, out step2, out var caseName))
                        {
                            _p1StatusGuideGeneration++;
                            _p1StatusGuideActive = true;
                            _p1StatusGuideStep1 = step1;
                            _p1StatusGuideStep2 = step2;
                            drawGeneration = _p1StatusGuideGeneration;
                            shouldDraw = true;
                            debugMessage += $" matched={caseName} first={step1.StatusId}@{FormatPosition(step1.Position)} second={step2.StatusId}@{FormatPosition(step2.Position)} generation={_p1StatusGuideGeneration}";
                        }
                        else
                        {
                            _p1StatusGuideGeneration++;
                            _p1StatusGuideActive = false;
                            _p1StatusGuideStatuses.Clear();
                            debugMessage += " invalid pair, cleared";
                        }
                    }
                    else if (_p1StatusGuideStatuses.Count > 2)
                    {
                        _p1StatusGuideGeneration++;
                        _p1StatusGuideActive = false;
                        _p1StatusGuideStatuses.Clear();
                        debugMessage += " overflow, cleared";
                    }
                }
            }

            DebugEcho(accessory, debugMessage);

            if (shouldDraw)
                DrawP1StatusGuidePlan(accessory, drawGeneration, circle1, circle2, step1.Position);
        }

        [ScriptMethod(name: "P1 状态指路-消失", eventType: EventTypeEnum.StatusRemove, eventCondition: ["StatusID:regex:^(4876|4877|4878|4879|5079|5080|5081|5082)$"], userControl: true)]
        public void P1_StatusGuide_StatusRemove(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            _lastMechanicAt = NowMs();

            if (_phase != Phase.P1) return;
            if (!TryGetStatusId(@event, out var statusId)) return;
            if (!TryGetTargetId(@event, out var targetId) || !IsMe(accessory, targetId)) return;

            var shouldClear = false;
            var shouldSwitch = false;
            var nextPosition = default(Vector3);
            var nextDuration = P1StatusGuideDuration;
            var drawGeneration = 0;
            var debugMessage = string.Empty;

            lock (_lock)
            {
                var removeIndex = FindP1StatusGuideRecordIndex(statusId);
                if (removeIndex < 0)
                {
                    debugMessage = $"StatusGuide remove ignored missing status={statusId} active={_p1StatusGuideActive} count={_p1StatusGuideStatuses.Count}";
                }
                else
                {
                    _p1StatusGuideStatuses.RemoveAt(removeIndex);
                    debugMessage = $"StatusGuide remove status={statusId} remaining={_p1StatusGuideStatuses.Count}";

                    if (_p1StatusGuideActive && _p1StatusGuideStatuses.Count <= 0)
                    {
                        _p1StatusGuideGeneration++;
                        _p1StatusGuideActive = false;
                        shouldClear = true;
                        debugMessage += $" clear generation={_p1StatusGuideGeneration}";
                    }
                    else if (_p1StatusGuideActive && _p1StatusGuideStatuses.Count == 1)
                    {
                        var remaining = _p1StatusGuideStatuses[0];
                        var nextStep = ResolveP1StatusGuideStepAfterRemove(remaining);
                        nextPosition = nextStep.Position;
                        drawGeneration = _p1StatusGuideGeneration;
                        shouldSwitch = true;
                        debugMessage += $" switch={nextStep.StatusId}@{FormatPosition(nextPosition)} duration={nextDuration}";
                    }
                }
            }

            DebugEcho(accessory, debugMessage);

            if (shouldClear)
            {
                accessory.Method.RemoveDraw($"{P1StatusGuideDrawPrefix}_.*");
                return;
            }

            if (shouldSwitch)
            {
                accessory.Method.RemoveDraw(P1StatusGuideDrawName(drawGeneration, "Guide_1"));
                DrawGuide(accessory, P1StatusGuideDrawName(drawGeneration, "Guide_2"), nextPosition, nextDuration, color: SolidSafeGreen);
            }
        }

        private void DrawP1StatusGuidePlan(ScriptAccessory accessory, int generation, Vector3 circle1, Vector3 circle2, Vector3 firstGuidePosition)
        {
            DrawStaticCircle(accessory, P1StatusGuideDrawName(generation, "Circle_1"), circle1, P1StatusGuideCircleRadius, P1StatusGuideDuration, SolidSafeGreen);
            DrawStaticCircle(accessory, P1StatusGuideDrawName(generation, "Circle_2"), circle2, P1StatusGuideCircleRadius, P1StatusGuideDuration, SolidSafeGreen);
            DrawGuide(accessory, P1StatusGuideDrawName(generation, "Guide_1"), firstGuidePosition, P1StatusGuideDuration, color: SolidSafeGreen);
            DebugEcho(accessory, $"StatusGuide draw sent circle1={FormatPosition(circle1)} circle2={FormatPosition(circle2)} guide={FormatPosition(firstGuidePosition)} duration={P1StatusGuideDuration}");
        }

        private static string P1StatusGuideDrawName(int generation, string suffix)
        {
            return $"{P1StatusGuideDrawPrefix}_{generation}_{suffix}";
        }

        private int FindP1StatusGuideRecordIndex(uint statusId)
        {
            for (var i = 0; i < _p1StatusGuideStatuses.Count; i++)
            {
                if (_p1StatusGuideStatuses[i].StatusId == statusId)
                    return i;
            }

            return -1;
        }

        private P1StatusGuideStep ResolveP1StatusGuideStepAfterRemove(P1StatusRecord remaining)
        {
            if (_p1StatusGuideStep1.StatusId == _p1StatusGuideStep2.StatusId)
                return _p1StatusGuideStep2;

            return remaining.StatusId == _p1StatusGuideStep2.StatusId ? _p1StatusGuideStep2 : _p1StatusGuideStep1;
        }

        private static bool TryBuildP1StatusGuidePlan(P1StatusRecord first, P1StatusRecord second, out Vector3 circle1, out Vector3 circle2, out P1StatusGuideStep step1, out P1StatusGuideStep step2, out string caseName)
        {
            circle1 = default;
            circle2 = default;
            step1 = default;
            step2 = default;
            caseName = string.Empty;

            if (first.StatusId == second.StatusId)
                return TryBuildP1DuplicateStatusGuide(first.StatusId, out circle1, out circle2, out step1, out step2, out caseName);

            if (TryBuildP1MixedStatusGuide(first, second, 4878, new Vector3(108.0f, 0.0f, 100.0f), 5080, new Vector3(114.0f, 0.0f, 100.0f), "情况5", out circle1, out circle2, out step1, out step2, out caseName))
                return true;

            if (TryBuildP1MixedStatusGuide(first, second, 4879, new Vector3(92.0f, 0.0f, 100.0f), 5079, new Vector3(86.0f, 0.0f, 100.0f), "情况6", out circle1, out circle2, out step1, out step2, out caseName))
                return true;

            if (TryBuildP1MixedStatusGuide(first, second, 4876, new Vector3(100.0f, 0.0f, 92.0f), 5081, new Vector3(100.0f, 0.0f, 86.0f), "情况7", out circle1, out circle2, out step1, out step2, out caseName))
                return true;

            if (TryBuildP1MixedStatusGuide(first, second, 4877, new Vector3(100.0f, 0.0f, 108.0f), 5082, new Vector3(100.0f, 0.0f, 114.0f), "情况8", out circle1, out circle2, out step1, out step2, out caseName))
                return true;

            return false;
        }

        private static bool TryBuildP1DuplicateStatusGuide(uint statusId, out Vector3 circle1, out Vector3 circle2, out P1StatusGuideStep step1, out P1StatusGuideStep step2, out string caseName)
        {
            circle1 = default;
            circle2 = default;
            step1 = default;
            step2 = default;
            caseName = string.Empty;

            var rotation = 0.0f;
            var caseIndex = 0;
            if (statusId == 4876)
            {
                rotation = 0.0f;
                caseIndex = 1;
            }
            else if (statusId == 4877)
            {
                rotation = MathF.PI;
                caseIndex = 2;
            }
            else if (statusId == 4878)
            {
                rotation = MathF.PI / 2.0f;
                caseIndex = 3;
            }
            else if (statusId == 4879)
            {
                rotation = -MathF.PI / 2.0f;
                caseIndex = 4;
            }
            else
            {
                return false;
            }

            var baseNear = new Vector3(94.0f, 0.0f, 108.0f);
            var baseFar = new Vector3(94.0f, 0.0f, 114.0f);
            circle1 = RotateAround(baseNear, ArenaCenter, rotation);
            circle2 = RotateAround(baseFar, ArenaCenter, rotation);
            step1 = new P1StatusGuideStep { StatusId = statusId, Position = circle2 };
            step2 = new P1StatusGuideStep { StatusId = statusId, Position = circle1 };
            caseName = $"情况{caseIndex} {statusId}+{statusId}";
            return true;
        }

        private static bool TryBuildP1MixedStatusGuide(P1StatusRecord first, P1StatusRecord second, uint statusA, Vector3 positionA, uint statusB, Vector3 positionB, string caseLabel, out Vector3 circle1, out Vector3 circle2, out P1StatusGuideStep step1, out P1StatusGuideStep step2, out string caseName)
        {
            circle1 = positionA;
            circle2 = positionB;
            step1 = default;
            step2 = default;
            caseName = string.Empty;

            if (!IsStatusPair(first.StatusId, second.StatusId, statusA, statusB))
                return false;

            var recordA = first.StatusId == statusA ? first : second;
            var recordB = first.StatusId == statusB ? first : second;
            var stepA = new P1StatusGuideStep { StatusId = statusA, Position = positionA };
            var stepB = new P1StatusGuideStep { StatusId = statusB, Position = positionB };

            if (P1StatusExpiresAt(recordA) <= P1StatusExpiresAt(recordB))
            {
                step1 = stepA;
                step2 = stepB;
            }
            else
            {
                step1 = stepB;
                step2 = stepA;
            }

            caseName = $"{caseLabel} {first.StatusId}+{second.StatusId}";
            return true;
        }

        private static bool IsStatusPair(uint first, uint second, uint statusA, uint statusB)
        {
            return first == statusA && second == statusB || first == statusB && second == statusA;
        }

        private static long P1StatusExpiresAt(P1StatusRecord record)
        {
            return record.AddedAt + record.DurationMs;
        }

        private static string FormatPosition(Vector3 position)
        {
            return $"({position.X:F1},{position.Y:F1},{position.Z:F1})";
        }

        [ScriptMethod(name: "P1 记录玩家TargetIcon", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^[0-9A-Fa-f]{4}$"], userControl: false)]
        public void P1_RecordPlayerTargetIcon(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            _lastMechanicAt = NowMs();

            if (_phase != Phase.P1) return;

            var iconId = NormalizeIconId(@event["Id"]);
            if (iconId == "02A1" || iconId == "02A2")
            {
                DebugEcho(accessory, $"TargetIcon record skip boss-marker icon={iconId}");
                return;
            }

            if (!TryGetTargetId(@event, out var targetId))
            {
                DebugEcho(accessory, $"TargetIcon record skip no-target icon={iconId}");
                return;
            }

            var targetIndex = GetPlayerIndex(accessory, targetId);
            if (targetIndex < 0)
            {
                DebugEcho(accessory, $"TargetIcon record skip non-player icon={iconId} target={FormatObjectId(targetId)}");
                return;
            }

            DebugEcho(accessory, $"TargetIcon record player icon={iconId} target={FormatObjectId(targetId)} partyIndex={targetIndex}");

            if (iconId == "007F" || iconId == "0080")
                CaptureP1HeadMarkerPart(accessory, iconId, true);
        }

        [ScriptMethod(name: "P1 玩家头标真假", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(02A1|02A2)$"], userControl: true)]
        public void P1_PlayerHeadMarkerTrueFalse(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            _lastMechanicAt = NowMs();

            if (_phase != Phase.P1) return;

            var iconId = NormalizeIconId(@event["Id"]);
            var hasTarget = TryGetTargetId(@event, out var targetId);
            var targetIndex = hasTarget ? GetPlayerIndex(accessory, targetId) : -1;
            DebugEcho(accessory, $"HeadMarker boss icon={iconId} target={(hasTarget ? FormatObjectId(targetId) : "-")} partyIndex={targetIndex}");
            CaptureP1HeadMarkerPart(accessory, iconId, false);
        }

        private void CaptureP1HeadMarkerPart(ScriptAccessory accessory, string iconId, bool isPlayerIcon)
        {
            string call = string.Empty;
            string debugMessage;
            int waitGeneration = 0;

            lock (_lock)
            {
                if (isPlayerIcon)
                    _p1PendingPlayerTargetIcon = iconId;
                else
                    _p1PendingBossTargetIcon = iconId;

                _p1HeadMarkerPairGeneration++;

                if (TryGetP1HeadMarkerCall(_p1PendingBossTargetIcon, _p1PendingPlayerTargetIcon, out call))
                {
                    debugMessage = $"HeadMarker pair matched playerIcon={_p1PendingPlayerTargetIcon} bossIcon={_p1PendingBossTargetIcon} call={call}";
                    _p1PendingPlayerTargetIcon = string.Empty;
                    _p1PendingBossTargetIcon = string.Empty;
                    _p1HeadMarkerPairGeneration++;
                }
                else
                {
                    waitGeneration = _p1HeadMarkerPairGeneration;
                    debugMessage = $"HeadMarker pair pending side={(isPlayerIcon ? "player" : "boss")} icon={iconId} playerIcon={ValueOrDash(_p1PendingPlayerTargetIcon)} bossIcon={ValueOrDash(_p1PendingBossTargetIcon)} generation={waitGeneration}";
                }
            }

            DebugEcho(accessory, debugMessage);

            if (!string.IsNullOrWhiteSpace(call))
            {
                Alert(call, 5000, true);
                return;
            }

            _ = ClearP1HeadMarkerPairAfterDelayAsync(accessory, waitGeneration);
        }

        private async Task ClearP1HeadMarkerPairAfterDelayAsync(ScriptAccessory accessory, int generation)
        {
            await Task.Delay(1000);

            string debugMessage = string.Empty;
            lock (_lock)
            {
                if (_p1HeadMarkerPairGeneration != generation) return;

                if (!string.IsNullOrWhiteSpace(_p1PendingPlayerTargetIcon) || !string.IsNullOrWhiteSpace(_p1PendingBossTargetIcon))
                {
                    debugMessage = $"HeadMarker pair timeout playerIcon={ValueOrDash(_p1PendingPlayerTargetIcon)} bossIcon={ValueOrDash(_p1PendingBossTargetIcon)} generation={generation}";
                    _p1PendingPlayerTargetIcon = string.Empty;
                    _p1PendingBossTargetIcon = string.Empty;
                    _p1HeadMarkerPairGeneration++;
                }
            }

            if (!string.IsNullOrWhiteSpace(debugMessage))
                DebugEcho(accessory, debugMessage);
        }

        private static bool TryGetP1HeadMarkerCall(string bossIconId, string playerIconId, out string call)
        {
            call = string.Empty;

            if (bossIconId == "02A2")
            {
                if (playerIconId == "0080") call = "分摊";
                if (playerIconId == "007F") call = "分散";
            }

            if (bossIconId == "02A1")
            {
                if (playerIconId == "007F") call = "分摊";
                if (playerIconId == "0080") call = "分散";
            }

            return !string.IsNullOrWhiteSpace(call);
        }

        [ScriptMethod(name: "P1 ObjectEffect 64/128 播报与玩家射线", eventType: EventTypeEnum.ObjectEffect, eventCondition: ["Id1:64", "Id2:128"], userControl: true)]
        public void P1_ObjectEffect64128_PlayerRays(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            _lastMechanicAt = NowMs();

            if (_phase != Phase.P1) return;

            var sourcePosition = @event.SourcePosition;
            int count;
            lock (_lock)
            {
                _p1ObjectEffect64128Count++;
                count = _p1ObjectEffect64128Count;
            }

            AnnounceP1ObjectEffect64128(sourcePosition, count);

            lock (_lock)
            {
                if (_p1ObjectEffect64128PlayerRaysDrawn) return;
                _p1ObjectEffect64128PlayerRaysDrawn = true;
            }

            var sourceId = @event.SourceId;
            const int duration = 5125;

            foreach (var playerId in accessory.Data.PartyList)
            {
                var drawName = $"DMU_P1_Obj64128_PlayerRay_{sourceId}_{playerId}_{DateTime.Now.Ticks}";
                DrawRectFromPositionToTarget(accessory, drawName, sourcePosition, playerId, 6.0f, 100.0f, duration);
            }
        }

        private void AnnounceP1ObjectEffect64128(Vector3 sourcePosition, int count)
        {
            if (count == 2 || count == 3)
            {
                if (IsNear(sourcePosition, new Vector3(116.0f, 6.5f, 43.0f)))
                {
                    Alert("右刀", 3000, true);
                    return;
                }

                if (IsNear(sourcePosition, new Vector3(92.0f, 15.0f, 27.0f)))
                    Alert("左刀", 3000, true);
            }

            if (count == 4)
            {
                if (IsNear(sourcePosition, new Vector3(105.25f, 13.5f, 34.0f)))
                {
                    Alert("背对", 3000, true);
                    return;
                }

                if (IsNear(sourcePosition, new Vector3(95.0f, 12.5f, 25.0f)))
                    Alert("面对", 3000, true);
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
