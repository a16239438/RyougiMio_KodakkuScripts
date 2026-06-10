using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using KodakkuAssist.Data;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Module.GameOperate;
using KodakkuAssist.Script;

#nullable disable

namespace RyougiMioScriptNamespace
{
    [ScriptType(name: "(妖星乱舞绝境战)P4 指路&自动移动", territorys: [1363], guid: "79ae48d3-c462-4e4a-8108-9eb507e131b2", version: "0.0.0.3", author: "RyougiMio", note: "P4脚本。\n鸳鸯锅:攻击1234左 锁链123圈右\n后续:攻击12是钢铁 禁止12是背对 锁链12是正对\n!!!!!!!!自动移动依赖于PromeRotation!!!!!!!!")]
    public class Script1363P4
    {
        #region Settings

        [UserSetting("-----全局设置-----")]
        public bool _____Global_Settings_____ { get; set; } = true;

        [UserSetting("是否开启屏幕文字提示")]
        public bool EnableText { get; set; } = true;

        [UserSetting("是否开启TTS语音提示")]
        public bool EnableTTS { get; set; } = true;

        [UserSetting("指挥模式（开启后允许脚本给全员上头标）")]
        public bool EnableCommandMode { get; set; } = false;

        [UserSetting("启用PR GreenMove自动移动")]
        public bool EnableGreenMove { get; set; } = false;

        [UserSetting("PR移动判定距离")]
        public float GreenMoveTolerance { get; set; } = 0.1f;

        [UserSetting("PR移动速度倍率")]
        public float GreenMoveSpeedMultiplier { get; set; } = 1.0f;

        [UserSetting("PR移动忽略Y轴")]
        public bool GreenMoveIgnoreY { get; set; } = true;

        [UserSetting("PR移动前清空队列")]
        public bool GreenMoveClearQueueBeforeMove { get; set; } = true;

        [UserSetting("PR移动最大读条等待ms")]
        public int GreenMoveMaxCastWaitMs { get; set; } = 2500;

        [UserSetting("PR移动等待队列读条/滑步")]
        public bool GreenMoveWaitForQueuedCast { get; set; } = true;

        [UserSetting("Developer mode")]
        public bool DeveloperMode { get; set; } = false;

        [UserSetting("常用危险色")]
        public ScriptColor DangerColor { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 0.0f, 0.0f, 0.01f) };

        [UserSetting("常用安全色")]
        public ScriptColor SafeColor { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 0.0f, 0.01f) };

        [UserSetting("指路/引导颜色")]
        public ScriptColor GuideColor { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 1.0f, 1.0f) };

        #endregion

        #region State

        private enum Phase
        {
            Init,
            P4,
            Done,
        }

        private enum P4HalfSide
        {
            Unknown,
            Left,
            Right,
        }

        private enum P4HalfColor
        {
            Unknown,
            Blue,
            Purple,
        }

        private enum P4ChainStep
        {
            None,
            Move,
            Petrify,
            Element,
        }

        private enum P4ClockDirection
        {
            Unknown,
            Twelve,
            Three,
            Six,
            Nine,
        }

        private enum P4ElementCall
        {
            Unknown,
            FireSteel,
            WaterMoon,
        }

        private const uint InvalidObjectId = 0xE0000000;
        private const uint P4StartActionId = 49884;
        private const uint P4ResolveActionId = 50069;
        private const uint P4ChainStartActionEffectId = 50070;
        private const uint P4TruthStatusId = 2056;
        private const uint P4XDataId = 19510;
        private const uint P4CDataId = 19507;
        private const int P4StatusWindowMs = 11000;
        private const int P4GuideDurationMs = 6000;
        private const int P4ChainGuideDurationMs = 8000;
        private const int P4ChainDelayMs = 500;
        private const int P4StateUnknown = 0;
        private const int P4StateTrue = 1;
        private const int P4StateFalse = 2;
        private const float P4GuideOffset = 4.0f;
        private const float P4ChainGuideOffset = 8.0f;
        private const string DrawPrefix = "KDYD_P4";
        private const string GreenMovePrefix = "PromeRotation.GreenMove.";

        private static readonly Vector3 P4ArenaCenter = new Vector3(100.0f, 0.0f, 100.0f);
        private static readonly Vector3 DefaultNorth = new Vector3(0.0f, 0.0f, -1.0f);
        private static readonly Vector4 SolidDangerRed = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
        private static readonly string[] PartyPriorityLabels = new[] { "MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4" };
        private static readonly uint[] P4TrackedStatusIds = new uint[] { 5541, 5542, 5543, 5544, 5545, 5546, 5547, 5548, 454, 1382 };
        private static readonly MarkType[] P4LeftMarks = new[] { MarkType.Attack1, MarkType.Attack2, MarkType.Attack3, MarkType.Attack4, MarkType.Attack5, MarkType.Attack6, MarkType.Attack7, MarkType.Attack8 };
        private static readonly MarkType[] P4RightMarks = new[] { MarkType.Bind1, MarkType.Bind2, MarkType.Bind3, MarkType.Circle, MarkType.Stop1, MarkType.Stop2, MarkType.Cross, MarkType.Square };
        private static readonly MarkType[] P4PetrifyTrueMarks = new[] { MarkType.Stop1, MarkType.Stop2 };
        private static readonly MarkType[] P4PetrifyFalseMarks = new[] { MarkType.Bind1, MarkType.Bind2 };

        private ScriptAccessory _acc;
        private Phase _phase = Phase.Init;
        private int _generation;
        private readonly object _p4Lock = new object();
        private readonly object _p4CommandMarkTimerLock = new object();
        private readonly int[,] _p4StatusStateByPartyAndStatus = new int[8, 10];
        private readonly long[,] _p4StatusExpiresAtByPartyAndStatus = new long[8, 10];
        private readonly P4HalfSide[] _p4ResolvedSideByPartyIndex = new P4HalfSide[8];
        private readonly P4HalfColor[] _p4ResolvedColorByPartyIndex = new P4HalfColor[8];
        private readonly MarkType?[] _p4CommandMarkByPartyIndex = new MarkType?[8];
        private Timer _p4CommandMarkClearTimer;
        private P4ChainStep _p4ChainStep = P4ChainStep.None;
        private int _p4ChainRound;
        private int _p4ChainStepGeneration;
        private int _p4XParam;
        private long _p4XExpiresAt;
        private long _p4XUpdatedAt;
        private int _p4CParam;
        private long _p4CExpiresAt;
        private long _p4CUpdatedAt;
        private int _p4XEventCount;
        private int _p4FourthXParam;
        private Vector3 _p4FourthTargetPosition;
        private Vector3 _p4FourthNewTwelveDirection;
        private bool _p4FourthDirectionReady;
        private object _greenMoveToPointSub;
        private MethodInfo _greenMoveToPointInvoke;
        private object _greenMoveClearQueueSub;
        private MethodInfo _greenMoveClearQueueInvoke;
        private object _greenMoveStopSub;
        private MethodInfo _greenMoveStopInvoke;
        private bool _warnedGreenMove;

        #endregion

        #region Initialization

        public void Init(ScriptAccessory accessory)
        {
            _acc = accessory;
            _phase = Phase.Init;
            _generation++;
            ResetP4State();
            _warnedGreenMove = false;

            accessory.Method.RemoveDraw(".*");
            CommandMarkClear(accessory);
        }

        private void ResetMechanic(ScriptAccessory accessory, bool removeDraw = true)
        {
            _generation++;
            ResetP4State();

            GreenMoveStopAndClear(accessory, "reset mechanic");

            if (removeDraw)
                accessory.Method.RemoveDraw($"{DrawPrefix}_.*");

            CommandMarkClear(accessory);
        }

        private void SetPhase(ScriptAccessory accessory, Phase phase)
        {
            if (_phase == phase) return;

            _phase = phase;
            ResetMechanic(accessory);
        }

        private void ResetP4State()
        {
            CancelP4CommandMarkClearTimer();

            lock (_p4Lock)
            {
                Array.Clear(_p4StatusStateByPartyAndStatus, 0, _p4StatusStateByPartyAndStatus.Length);
                Array.Clear(_p4StatusExpiresAtByPartyAndStatus, 0, _p4StatusExpiresAtByPartyAndStatus.Length);
                Array.Clear(_p4ResolvedSideByPartyIndex, 0, _p4ResolvedSideByPartyIndex.Length);
                Array.Clear(_p4ResolvedColorByPartyIndex, 0, _p4ResolvedColorByPartyIndex.Length);
                Array.Clear(_p4CommandMarkByPartyIndex, 0, _p4CommandMarkByPartyIndex.Length);

                _p4ChainStep = P4ChainStep.None;
                _p4ChainRound = 0;
                _p4ChainStepGeneration++;
                _p4XParam = 0;
                _p4XExpiresAt = 0;
                _p4XUpdatedAt = 0;
                _p4CParam = 0;
                _p4CExpiresAt = 0;
                _p4CUpdatedAt = 0;
                _p4XEventCount = 0;
                _p4FourthXParam = 0;
                _p4FourthTargetPosition = default;
                _p4FourthNewTwelveDirection = DefaultNorth;
                _p4FourthDirectionReady = false;
            }
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
            if (!DeveloperMode) return;
            accessory.Log.Debug($"[KDYD_P4] {message}");
            accessory.Method.SendChat($"/e [KDYD_P4] {message}");
        }

        private void CommandMarkClear(ScriptAccessory accessory)
        {
            if (!EnableCommandMode) return;
            accessory.Method.MarkClear();
        }

        private void CommandMarkPartyMember(ScriptAccessory accessory, int partyIndex, MarkType markType)
        {
            if (!EnableCommandMode) return;
            if (partyIndex < 0 || partyIndex >= accessory.Data.PartyList.Count) return;

            accessory.Method.Mark(accessory.Data.PartyList[partyIndex], markType);
        }

        private void ClearP4CommandMarksNow(ScriptAccessory accessory)
        {
            CancelP4CommandMarkClearTimer();
            CommandMarkClear(accessory);
        }

        private void CancelP4CommandMarkClearTimer()
        {
            lock (_p4CommandMarkTimerLock)
            {
                _p4CommandMarkClearTimer?.Dispose();
                _p4CommandMarkClearTimer = null;
            }
        }

        private void ReplaceP4CommandMarkClearTimer(Timer timer)
        {
            lock (_p4CommandMarkTimerLock)
            {
                _p4CommandMarkClearTimer?.Dispose();
                _p4CommandMarkClearTimer = timer;
            }
        }

        private void ScheduleP4CommandMarkClear(ScriptAccessory accessory, int generation, int durationMs)
        {
            if (!EnableCommandMode) return;

            Timer timer = null;
            timer = new Timer(_ =>
            {
                try
                {
                    if (_generation != generation)
                        return;

                    accessory.Method.MarkClear();
                }
                finally
                {
                    lock (_p4CommandMarkTimerLock)
                    {
                        if (ReferenceEquals(_p4CommandMarkClearTimer, timer))
                            _p4CommandMarkClearTimer = null;
                    }

                    timer?.Dispose();
                }
            }, null, Math.Max(0, durationMs), Timeout.Infinite);

            ReplaceP4CommandMarkClearTimer(timer);
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

        private static bool TryGetDurationMs(Event @event, out int durationMs)
        {
            durationMs = 0;

            var raw = @event["DurationMilliseconds"];
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var duration))
                return false;

            if (duration <= 0.0 || duration > int.MaxValue)
                return false;

            durationMs = (int)Math.Round(duration);
            return durationMs > 0;
        }

        private static bool TryParseObjectId(string text, out uint id)
        {
            id = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            text = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text.Substring(2) : text;
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

        private static bool TryGetTargetDataId(Event @event, ScriptAccessory accessory, out uint dataId)
        {
            dataId = 0;

            var raw = @event["TargetDataId"];
            if (string.IsNullOrWhiteSpace(raw))
                raw = @event["TargetDataID"];
            if (string.IsNullOrWhiteSpace(raw))
                raw = @event["DataId"];
            if (string.IsNullOrWhiteSpace(raw))
                raw = @event["DataID"];

            if (TryParseUInt(raw, out dataId) && dataId != 0)
                return true;

            if (!TryGetTargetId(@event, out var targetId))
                return false;

            var obj = accessory.Data.Objects.SearchById(targetId);
            if (obj == null) return false;

            dataId = obj.DataId;
            return dataId != 0;
        }

        private static Vector3 ResolveEventTargetPosition(Event @event, ScriptAccessory accessory)
        {
            var position = @event.TargetPosition;
            if (IsLikelyArenaPosition(position))
                return position;

            if (TryGetTargetId(@event, out var targetId))
            {
                var obj = accessory.Data.Objects.SearchById(targetId);
                if (obj != null)
                    return obj.Position;
            }

            return position;
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
            for (var i = 0; i < accessory.Data.PartyList.Count; i++)
            {
                if (accessory.Data.PartyList[i] == objectId)
                    return i;
            }

            return -1;
        }

        private static int GetMyIndex(ScriptAccessory accessory)
        {
            return GetPlayerIndex(accessory, GetMyId(accessory));
        }

        private static string FormatObjectId(uint objectId)
        {
            return objectId == 0 ? "-" : objectId.ToString("X8", CultureInfo.InvariantCulture);
        }

        private static string FormatPosition(Vector3 position)
        {
            return $"({position.X:F2},{position.Y:F2},{position.Z:F2})";
        }

        private static string PartyPriorityLabel(int partyIndex)
        {
            if (partyIndex < 0)
                return "-";

            return partyIndex < PartyPriorityLabels.Length
                ? PartyPriorityLabels[partyIndex]
                : $"P{partyIndex + 1}";
        }

        private static bool IsLikelyArenaPosition(Vector3 position)
        {
            return position.X > 50.0f && position.X < 150.0f
                && position.Z > 50.0f && position.Z < 150.0f;
        }

        private static bool TryNormalizeFromCenter(Vector3 position, out Vector3 direction)
        {
            var delta = new Vector3(position.X - P4ArenaCenter.X, 0.0f, position.Z - P4ArenaCenter.Z);
            var length = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
            if (length < 0.001f)
            {
                direction = DefaultNorth;
                return false;
            }

            direction = new Vector3(delta.X / length, 0.0f, delta.Z / length);
            return true;
        }

        private static Vector3 RightVectorFromNewTwelve(Vector3 newTwelveDirection)
        {
            return new Vector3(-newTwelveDirection.Z, 0.0f, newTwelveDirection.X);
        }

        private static P4HalfSide SideOfPosition(Vector3 position, Vector3 rightVector)
        {
            var delta = new Vector3(position.X - P4ArenaCenter.X, 0.0f, position.Z - P4ArenaCenter.Z);
            var dot = delta.X * rightVector.X + delta.Z * rightVector.Z;
            return dot >= 0.0f ? P4HalfSide.Right : P4HalfSide.Left;
        }

        private static P4HalfSide OppositeSide(P4HalfSide side)
        {
            if (side == P4HalfSide.Left) return P4HalfSide.Right;
            if (side == P4HalfSide.Right) return P4HalfSide.Left;
            return P4HalfSide.Unknown;
        }

        private static P4HalfColor OppositeColor(P4HalfColor color)
        {
            if (color == P4HalfColor.Blue) return P4HalfColor.Purple;
            if (color == P4HalfColor.Purple) return P4HalfColor.Blue;
            return P4HalfColor.Unknown;
        }

        private static bool TryGetP4TrackedStatusIndex(uint statusId, out int index)
        {
            if (statusId >= 5541 && statusId <= 5548)
            {
                index = (int)(statusId - 5541);
                return true;
            }

            if (statusId == 454)
            {
                index = 8;
                return true;
            }

            if (statusId == 1382)
            {
                index = 9;
                return true;
            }

            index = -1;
            return false;
        }

        private static bool TryGetP4StateValueFromParam(int param, out int value)
        {
            if (param == 1122 || param == 1120)
            {
                value = P4StateTrue;
                return true;
            }

            if (param == 1121 || param == 1119)
            {
                value = P4StateFalse;
                return true;
            }

            value = P4StateUnknown;
            return false;
        }

        private static bool TryGetP4DirectTrueStatus(uint statusId, out uint trueStatusId)
        {
            switch (statusId)
            {
                case 4887:
                    trueStatusId = 5542;
                    return true;
                case 4888:
                    trueStatusId = 5541;
                    return true;
                case 5464:
                    trueStatusId = 454;
                    return true;
                default:
                    trueStatusId = 0;
                    return false;
            }
        }

        private bool TryGetActiveP4StateValueLocked(long now, out int value)
        {
            value = P4StateUnknown;

            var hasX = _p4XParam != 0 && now <= _p4XExpiresAt;
            var hasC = _p4CParam != 0 && now <= _p4CExpiresAt;
            if (!hasX && !hasC)
                return false;

            var param = hasX && (!hasC || _p4XUpdatedAt >= _p4CUpdatedAt)
                ? _p4XParam
                : _p4CParam;

            return TryGetP4StateValueFromParam(param, out value);
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

        private static bool TryResolveExclusiveP4Truth(int stateA, uint statusA, int stateB, uint statusB, out uint trueStatus)
        {
            if (stateA == P4StateTrue)
            {
                trueStatus = statusA;
                return true;
            }

            if (stateB == P4StateTrue)
            {
                trueStatus = statusB;
                return true;
            }

            if (stateA == P4StateFalse)
            {
                trueStatus = statusB;
                return true;
            }

            if (stateB == P4StateFalse)
            {
                trueStatus = statusA;
                return true;
            }

            trueStatus = 0;
            return false;
        }

        private static bool TryResolveP4PlayerColor(int[,] states, int partyIndex, out P4HalfColor color, out uint trueShapeStatus, out uint trueColorStatus)
        {
            color = P4HalfColor.Unknown;
            trueShapeStatus = 0;
            trueColorStatus = 0;

            if (partyIndex < 0 || partyIndex >= 8)
                return false;

            if (!TryResolveExclusiveP4Truth(states[partyIndex, 0], 5541, states[partyIndex, 1], 5542, out trueShapeStatus))
                return false;

            if (!TryResolveExclusiveP4Truth(states[partyIndex, 8], 454, states[partyIndex, 9], 1382, out trueColorStatus))
                return false;

            if ((trueShapeStatus == 5541 && trueColorStatus == 454)
                || (trueShapeStatus == 5542 && trueColorStatus == 1382))
            {
                color = P4HalfColor.Blue;
                return true;
            }

            if ((trueShapeStatus == 5541 && trueColorStatus == 1382)
                || (trueShapeStatus == 5542 && trueColorStatus == 454))
            {
                color = P4HalfColor.Purple;
                return true;
            }

            return false;
        }

        private static P4HalfSide SideForColor(P4HalfColor color, P4HalfColor leftColor, P4HalfColor rightColor)
        {
            if (color == leftColor) return P4HalfSide.Left;
            if (color == rightColor) return P4HalfSide.Right;
            return P4HalfSide.Unknown;
        }

        private static string FormatP4Side(P4HalfSide side)
        {
            switch (side)
            {
                case P4HalfSide.Left: return "L";
                case P4HalfSide.Right: return "R";
                default: return "-";
            }
        }

        private static string FormatP4Color(P4HalfColor color)
        {
            switch (color)
            {
                case P4HalfColor.Blue: return "蓝";
                case P4HalfColor.Purple: return "紫";
                default: return "-";
            }
        }

        private static string FormatP4Mark(MarkType? mark)
        {
            return mark.HasValue ? mark.Value.ToString() : "-";
        }

        private static string FormatP4StatusStates(int[,] states, int partyIndex)
        {
            return string.Join(" ", P4TrackedStatusIds.Select((statusId, statusIndex) => $"{statusId}={states[partyIndex, statusIndex]}"));
        }

        private static int RemainingMs(long expiresAt, long now)
        {
            if (expiresAt <= now)
                return 0;

            var remaining = expiresAt - now;
            return remaining > int.MaxValue ? int.MaxValue : (int)remaining;
        }

        private static string FormatRemainingMs(int remainingMs)
        {
            return remainingMs > 0
                ? $"{remainingMs / 1000.0:F1}s"
                : "-";
        }

        private static string FormatP4StatusStatesWithRemaining(int[,] states, long[,] expiresAt, int partyIndex, long now)
        {
            return string.Join(
                " ",
                P4TrackedStatusIds.Select((statusId, statusIndex) =>
                    $"{statusId}={states[partyIndex, statusIndex]}({FormatRemainingMs(RemainingMs(expiresAt[partyIndex, statusIndex], now))})"));
        }

        private static bool IsTnPartyIndex(int partyIndex)
        {
            return partyIndex >= 0 && partyIndex <= 3;
        }

        private static bool IsDpsPartyIndex(int partyIndex)
        {
            return partyIndex >= 4 && partyIndex <= 7;
        }

        private static bool TryGetP4ActiveSoonState(
            int[,] states,
            long[,] expiresAt,
            int partyIndex,
            uint statusId,
            long now,
            out int state,
            out int remainingMs)
        {
            state = P4StateUnknown;
            remainingMs = 0;

            if (partyIndex < 0 || partyIndex >= 8)
                return false;

            if (!TryGetP4TrackedStatusIndex(statusId, out var statusIndex))
                return false;

            state = states[partyIndex, statusIndex];
            remainingMs = RemainingMs(expiresAt[partyIndex, statusIndex], now);
            return state != P4StateUnknown && remainingMs > 0 && remainingMs < 10000;
        }

        private static bool HasP4StatusStateSoon(
            int[,] states,
            long[,] expiresAt,
            int partyIndex,
            uint statusId,
            int expectedState,
            long now,
            out int remainingMs)
        {
            return TryGetP4ActiveSoonState(states, expiresAt, partyIndex, statusId, now, out var state, out remainingMs)
                && state == expectedState;
        }

        private static void NormalizeP4ExclusivePairByTruth(int[,] states, long[,] expiresAt, int partyIndex, int statusIndexA, int statusIndexB, long now)
        {
            if (partyIndex < 0 || partyIndex >= 8)
                return;

            var stateA = RemainingMs(expiresAt[partyIndex, statusIndexA], now) > 0
                ? states[partyIndex, statusIndexA]
                : P4StateUnknown;
            var stateB = RemainingMs(expiresAt[partyIndex, statusIndexB], now) > 0
                ? states[partyIndex, statusIndexB]
                : P4StateUnknown;

            if (stateA == P4StateTrue || stateB == P4StateTrue)
                return;

            if (stateA == P4StateFalse)
            {
                var oldExpiresAt = expiresAt[partyIndex, statusIndexA];
                states[partyIndex, statusIndexA] = P4StateUnknown;
                expiresAt[partyIndex, statusIndexA] = 0;
                states[partyIndex, statusIndexB] = P4StateTrue;
                if (expiresAt[partyIndex, statusIndexB] <= 0)
                    expiresAt[partyIndex, statusIndexB] = oldExpiresAt;
                return;
            }

            if (stateB == P4StateFalse)
            {
                var oldExpiresAt = expiresAt[partyIndex, statusIndexB];
                states[partyIndex, statusIndexB] = P4StateUnknown;
                expiresAt[partyIndex, statusIndexB] = 0;
                states[partyIndex, statusIndexA] = P4StateTrue;
                if (expiresAt[partyIndex, statusIndexA] <= 0)
                    expiresAt[partyIndex, statusIndexA] = oldExpiresAt;
            }
        }

        private static P4ClockDirection ResolveP4MoveClock(int[,] states, long[,] expiresAt, int partyIndex, long now)
        {
            if (!IsTnPartyIndex(partyIndex) && !IsDpsPartyIndex(partyIndex))
                return P4ClockDirection.Unknown;

            var hasTrue5544 = HasP4StatusStateSoon(states, expiresAt, partyIndex, 5544, P4StateTrue, now, out _);
            if (hasTrue5544)
                return IsTnPartyIndex(partyIndex) ? P4ClockDirection.Three : P4ClockDirection.Nine;

            return IsTnPartyIndex(partyIndex) ? P4ClockDirection.Twelve : P4ClockDirection.Six;
        }

        private static Vector3 P4ClockTarget(Vector3 newTwelveDirection, P4ClockDirection clock)
        {
            var rightVector = RightVectorFromNewTwelve(newTwelveDirection);
            switch (clock)
            {
                case P4ClockDirection.Twelve:
                    return P4ArenaCenter + newTwelveDirection * P4ChainGuideOffset;
                case P4ClockDirection.Three:
                    return P4ArenaCenter + rightVector * P4ChainGuideOffset;
                case P4ClockDirection.Six:
                    return P4ArenaCenter - newTwelveDirection * P4ChainGuideOffset;
                case P4ClockDirection.Nine:
                    return P4ArenaCenter - rightVector * P4ChainGuideOffset;
                default:
                    return P4ArenaCenter;
            }
        }

        private static string FormatP4Clock(P4ClockDirection clock)
        {
            switch (clock)
            {
                case P4ClockDirection.Twelve: return "12";
                case P4ClockDirection.Three: return "3";
                case P4ClockDirection.Six: return "6";
                case P4ClockDirection.Nine: return "9";
                default: return "-";
            }
        }

        private static MarkType?[] BuildP4ChainMoveMarks(IReadOnlyList<P4ClockDirection> clocks)
        {
            var marks = new MarkType?[8];
            var markedThree = false;
            var markedNine = false;

            for (var i = 0; i < marks.Length && i < clocks.Count; i++)
            {
                if (clocks[i] == P4ClockDirection.Three && !markedThree)
                {
                    marks[i] = MarkType.Attack1;
                    markedThree = true;
                }
                else if (clocks[i] == P4ClockDirection.Nine && !markedNine)
                {
                    marks[i] = MarkType.Attack2;
                    markedNine = true;
                }
            }

            return marks;
        }

        private static MarkType?[] BuildP4PetrifyMarks(int[,] states, long[,] expiresAt, long now)
        {
            var marks = new MarkType?[8];
            var trueCount = 0;
            var falseCount = 0;

            for (var i = 0; i < marks.Length; i++)
            {
                if (!TryGetP4ActiveSoonState(states, expiresAt, i, 5543, now, out var state, out _))
                    continue;

                if (state == P4StateTrue && trueCount < P4PetrifyTrueMarks.Length)
                {
                    marks[i] = P4PetrifyTrueMarks[trueCount];
                    trueCount++;
                }
                else if (state == P4StateFalse && falseCount < P4PetrifyFalseMarks.Length)
                {
                    marks[i] = P4PetrifyFalseMarks[falseCount];
                    falseCount++;
                }
            }

            return marks;
        }

        private void ApplyP4CommandMarksNoTimer(ScriptAccessory accessory, IReadOnlyList<MarkType?> marks)
        {
            if (!EnableCommandMode) return;

            ClearP4CommandMarksNow(accessory);

            var totalCount = Math.Min(marks.Count, accessory.Data.PartyList.Count);
            for (var i = 0; i < totalCount; i++)
            {
                if (marks[i].HasValue)
                    CommandMarkPartyMember(accessory, i, marks[i].Value);
            }
        }

        private static string P4MoveCallText(int state)
        {
            if (state == P4StateTrue) return "停停停";
            if (state == P4StateFalse) return "动动动";
            return null;
        }

        private static string P4PetrifyCallText(int state, bool isSelf)
        {
            if (state == P4StateTrue) return isSelf ? "你是背对石化" : "背对石化";
            if (state == P4StateFalse) return isSelf ? "你是正对石化" : "正对石化";
            return null;
        }

        private static bool TryResolveP4ElementCall(
            int[,] states,
            long[,] expiresAt,
            long now,
            out P4ElementCall call,
            out int partyIndex,
            out uint statusId,
            out int state,
            out int remainingMs)
        {
            call = P4ElementCall.Unknown;
            partyIndex = -1;
            statusId = 0;
            state = P4StateUnknown;
            remainingMs = 0;

            for (var i = 0; i < 8; i++)
            {
                if (TryGetP4ActiveSoonState(states, expiresAt, i, 5547, now, out state, out remainingMs))
                {
                    call = state == P4StateTrue ? P4ElementCall.FireSteel : P4ElementCall.WaterMoon;
                    partyIndex = i;
                    statusId = 5547;
                    return call != P4ElementCall.Unknown;
                }

                if (TryGetP4ActiveSoonState(states, expiresAt, i, 5548, now, out state, out remainingMs))
                {
                    call = state == P4StateFalse ? P4ElementCall.FireSteel : P4ElementCall.WaterMoon;
                    partyIndex = i;
                    statusId = 5548;
                    return call != P4ElementCall.Unknown;
                }
            }

            return false;
        }

        private static string P4ElementCallText(P4ElementCall call)
        {
            switch (call)
            {
                case P4ElementCall.FireSteel: return "火钢铁 中间集合稍后散开";
                case P4ElementCall.WaterMoon: return "水月环 中间集合稍后不动";
                default: return null;
            }
        }

        private static string FormatP4ElementCall(P4ElementCall call)
        {
            switch (call)
            {
                case P4ElementCall.FireSteel: return "火钢铁";
                case P4ElementCall.WaterMoon: return "水月环";
                default: return "-";
            }
        }

        private bool TryStartP4Chain(out int round, out int stepGeneration)
        {
            lock (_p4Lock)
            {
                if (_p4ChainStep != P4ChainStep.None || _p4ChainRound > 0)
                {
                    round = _p4ChainRound;
                    stepGeneration = _p4ChainStepGeneration;
                    return false;
                }

                _p4ChainRound = 1;
                _p4ChainStep = P4ChainStep.Move;
                _p4ChainStepGeneration++;
                round = _p4ChainRound;
                stepGeneration = _p4ChainStepGeneration;
                return true;
            }
        }

        private bool TryAdvanceP4ChainStep(P4ChainStep requiredStep, P4ChainStep nextStep, out int round, out int stepGeneration)
        {
            lock (_p4Lock)
            {
                if (_p4ChainRound <= 0 || _p4ChainStep != requiredStep)
                {
                    round = _p4ChainRound;
                    stepGeneration = _p4ChainStepGeneration;
                    return false;
                }

                _p4ChainStep = nextStep;
                _p4ChainStepGeneration++;
                round = _p4ChainRound;
                stepGeneration = _p4ChainStepGeneration;
                return true;
            }
        }

        private bool TryAdvanceP4ChainRound(out int round, out int stepGeneration)
        {
            lock (_p4Lock)
            {
                if (_p4ChainRound <= 0 || _p4ChainStep != P4ChainStep.Element)
                {
                    round = _p4ChainRound;
                    stepGeneration = _p4ChainStepGeneration;
                    return false;
                }

                _p4ChainRound++;
                _p4ChainStep = P4ChainStep.Move;
                _p4ChainStepGeneration++;
                round = _p4ChainRound;
                stepGeneration = _p4ChainStepGeneration;
                return true;
            }
        }

        private void ScheduleP4DelayedStep(
            ScriptAccessory accessory,
            int generation,
            int stepGeneration,
            int round,
            P4ChainStep step,
            int delayMs,
            Action action)
        {
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(Math.Max(0, delayMs));

                    if (generation != _generation || _phase != Phase.P4)
                        return;

                    lock (_p4Lock)
                    {
                        if (_p4ChainStepGeneration != stepGeneration
                            || _p4ChainRound != round
                            || _p4ChainStep != step)
                            return;
                    }

                    action();
                }
                catch (Exception ex)
                {
                    DebugEcho(accessory, $"P4链式阶段{step}延迟执行失败：{ex.Message}");
                }
            });
        }

        private void ScheduleP4SelfTtsAtRemaining3(ScriptAccessory accessory, int generation, int round, int partyIndex, int expectedState, int remainingMs, string text)
        {
            if (string.IsNullOrWhiteSpace(text) || remainingMs <= 0)
                return;

            var delayMs = Math.Max(0, remainingMs - 3000);
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delayMs);

                    if (generation != _generation || _phase != Phase.P4)
                        return;

                    lock (_p4Lock)
                    {
                        if (_p4ChainRound != round || _p4ChainStep == P4ChainStep.None)
                            return;

                        if (partyIndex < 0
                            || partyIndex >= 8
                            || !TryGetP4TrackedStatusIndex(5546, out var statusIndex)
                            || _p4StatusStateByPartyAndStatus[partyIndex, statusIndex] != expectedState)
                            return;

                        var currentRemaining = RemainingMs(_p4StatusExpiresAtByPartyAndStatus[partyIndex, statusIndex], NowMs());
                        if (currentRemaining <= 0 || currentRemaining > 4500)
                            return;
                    }

                    QTTS(text);
                }
                catch (Exception ex)
                {
                    DebugEcho(accessory, $"P4 5546 TTS延迟执行失败：{ex.Message}");
                }
            });
        }

        private void SendP4PartyCallouts(ScriptAccessory accessory, IReadOnlyList<string> messages, int generation, int round)
        {
            if (!EnableCommandMode || messages == null || messages.Count == 0)
                return;

            Task.Run(async () =>
            {
                try
                {
                    for (var i = 0; i < messages.Count; i++)
                    {
                        if (generation != _generation || _phase != Phase.P4)
                            return;

                        lock (_p4Lock)
                        {
                            if (_p4ChainRound != round || _p4ChainStep == P4ChainStep.None)
                                return;
                        }

                        accessory.Method.SendChat($"/p {messages[i]}");
                        if (i + 1 < messages.Count)
                            await Task.Delay(150);
                    }
                }
                catch (Exception ex)
                {
                    DebugEcho(accessory, $"P4小队频道提示发送失败：{ex.Message}");
                }
            });
        }

        private static bool TryConvertToUInt(object value, out uint result)
        {
            result = 0;
            if (value == null)
                return false;

            switch (value)
            {
                case byte v:
                    result = v;
                    return true;
                case ushort v:
                    result = v;
                    return true;
                case uint v:
                    result = v;
                    return true;
                case int v when v >= 0:
                    result = (uint)v;
                    return true;
                case long v when v >= 0 && v <= uint.MaxValue:
                    result = (uint)v;
                    return true;
                case string s:
                    return TryParseUInt(s, out result);
            }

            try
            {
                if (value is IConvertible convertible)
                {
                    result = convertible.ToUInt32(CultureInfo.InvariantCulture);
                    return true;
                }
            }
            catch
            {
                result = 0;
            }

            return false;
        }

        private static object ReadPropertyValue(object value, string propertyName)
        {
            if (value == null)
                return null;

            var property = value.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            return property?.GetValue(value);
        }

        private static bool TryReadUIntProperty(object value, string propertyName, out uint result)
        {
            result = 0;
            var propertyValue = ReadPropertyValue(value, propertyName);
            return TryConvertToUInt(propertyValue, out result);
        }

        private static bool TryExtractClassJobRowId(object value, out uint rowId)
        {
            rowId = 0;
            if (value == null)
                return false;

            if (TryConvertToUInt(value, out rowId))
                return true;

            if (TryReadUIntProperty(value, "RowId", out rowId)
                || TryReadUIntProperty(value, "Id", out rowId)
                || TryReadUIntProperty(value, "ID", out rowId))
                return true;

            var nestedValue = ReadPropertyValue(value, "Value");
            return nestedValue != null
                && !ReferenceEquals(nestedValue, value)
                && TryExtractClassJobRowId(nestedValue, out rowId);
        }

        private static bool TryGetPartyMemberClassJobRowId(ScriptAccessory accessory, int partyIndex, out uint rowId)
        {
            rowId = 0;
            if (partyIndex < 0 || partyIndex >= accessory.Data.PartyList.Count)
                return false;

            var obj = accessory.Data.Objects.SearchById(accessory.Data.PartyList[partyIndex]);
            if (obj == null)
                return false;

            if (TryReadUIntProperty(obj, "ClassJobId", out rowId)
                || TryReadUIntProperty(obj, "ClassJobID", out rowId))
                return true;

            return TryExtractClassJobRowId(ReadPropertyValue(obj, "ClassJob"), out rowId);
        }

        private static string PartyMemberJobName(ScriptAccessory accessory, int partyIndex)
        {
            return TryGetPartyMemberClassJobRowId(accessory, partyIndex, out var rowId)
                ? JobChineseName(rowId)
                : "未知职业";
        }

        private static string JobChineseName(uint classJobRowId)
        {
            switch (classJobRowId)
            {
                case 1: return "剑术师";
                case 2: return "格斗家";
                case 3: return "斧术师";
                case 4: return "枪术师";
                case 5: return "弓箭手";
                case 6: return "幻术师";
                case 7: return "咒术师";
                case 19: return "骑士";
                case 20: return "武僧";
                case 21: return "战士";
                case 22: return "龙骑士";
                case 23: return "吟游诗人";
                case 24: return "白魔法师";
                case 25: return "黑魔法师";
                case 26: return "秘术师";
                case 27: return "召唤师";
                case 28: return "学者";
                case 29: return "双剑师";
                case 30: return "忍者";
                case 31: return "机工士";
                case 32: return "暗黑骑士";
                case 33: return "占星术士";
                case 34: return "武士";
                case 35: return "赤魔法师";
                case 36: return "青魔法师";
                case 37: return "绝枪战士";
                case 38: return "舞者";
                case 39: return "钐镰客";
                case 40: return "贤者";
                case 41: return "蝰蛇剑士";
                case 42: return "绘灵法师";
                default: return "未知职业";
            }
        }

        private MarkType?[] BuildP4CommandMarks(IReadOnlyList<P4HalfSide> sides)
        {
            var marks = new MarkType?[8];
            var leftCount = 0;
            var rightCount = 0;

            for (var i = 0; i < marks.Length && i < sides.Count; i++)
            {
                if (sides[i] == P4HalfSide.Left && leftCount < P4LeftMarks.Length)
                {
                    marks[i] = P4LeftMarks[leftCount];
                    leftCount++;
                }
                else if (sides[i] == P4HalfSide.Right && rightCount < P4RightMarks.Length)
                {
                    marks[i] = P4RightMarks[rightCount];
                    rightCount++;
                }
            }

            return marks;
        }

        private void ApplyP4CommandMarks(ScriptAccessory accessory, IReadOnlyList<MarkType?> marks)
        {
            if (!EnableCommandMode) return;

            CommandMarkClear(accessory);

            var totalCount = Math.Min(marks.Count, accessory.Data.PartyList.Count);
            for (var i = 0; i < totalCount; i++)
            {
                if (marks[i].HasValue)
                    CommandMarkPartyMember(accessory, i, marks[i].Value);
            }

            ScheduleP4CommandMarkClear(accessory, _generation, P4GuideDurationMs);
        }

        private void DebugP4Resolve(
            ScriptAccessory accessory,
            int[,] states,
            IReadOnlyList<P4HalfSide> sides,
            IReadOnlyList<P4HalfColor> colors,
            IReadOnlyList<MarkType?> marks,
            int fourthParam,
            P4HalfSide sourceSide,
            P4HalfColor leftColor,
            P4HalfColor rightColor,
            Vector3 newTwelveDirection,
            Vector3 l1,
            Vector3 r1)
        {
            if (!DeveloperMode) return;

            DebugEcho(accessory, $"P4一运结算 fourthParam={fourthParam} sourceSide={FormatP4Side(sourceSide)} left={FormatP4Color(leftColor)} right={FormatP4Color(rightColor)} new12={FormatPosition(newTwelveDirection)} L1={FormatPosition(l1)} R1={FormatPosition(r1)}");

            for (var i = 0; i < 8; i++)
            {
                TryResolveP4PlayerColor(states, i, out _, out var trueShapeStatus, out var trueColorStatus);
                var objectId = i < accessory.Data.PartyList.Count ? accessory.Data.PartyList[i] : 0;
                DebugEcho(
                    accessory,
                    $"{PartyPriorityLabel(i)} {FormatObjectId(objectId)} {FormatP4StatusStates(states, i)} => {trueShapeStatus}/{trueColorStatus} {FormatP4Color(colors[i])} {FormatP4Side(sides[i])} mark={FormatP4Mark(marks[i])}");
            }
        }

        private void ExecuteP4ChainMove(ScriptAccessory accessory, int generation, int round)
        {
            var states = new int[8, 10];
            var expiresAt = new long[8, 10];
            Vector3 newTwelveDirection;
            bool directionReady;
            var now = NowMs();

            lock (_p4Lock)
            {
                for (var i = 0; i < 8; i++)
                    NormalizeP4ExclusivePairByTruth(_p4StatusStateByPartyAndStatus, _p4StatusExpiresAtByPartyAndStatus, i, 3, 4, now);

                Array.Copy(_p4StatusStateByPartyAndStatus, states, _p4StatusStateByPartyAndStatus.Length);
                Array.Copy(_p4StatusExpiresAtByPartyAndStatus, expiresAt, _p4StatusExpiresAtByPartyAndStatus.Length);
                newTwelveDirection = _p4FourthDirectionReady ? _p4FourthNewTwelveDirection : DefaultNorth;
                directionReady = _p4FourthDirectionReady;
            }

            var clocks = new P4ClockDirection[8];
            var targets = new Vector3[8];
            var moveCalls = new string[8];
            var moveCallStates = new int[8];
            var moveCallRemaining = new int[8];
            var partyMessages = new List<string>();

            for (var i = 0; i < 8; i++)
            {
                clocks[i] = ResolveP4MoveClock(states, expiresAt, i, now);
                targets[i] = P4ClockTarget(newTwelveDirection, clocks[i]);

                if (TryGetP4ActiveSoonState(states, expiresAt, i, 5546, now, out var state5546, out var remaining5546))
                {
                    var callText = P4MoveCallText(state5546);
                    if (!string.IsNullOrWhiteSpace(callText))
                    {
                        moveCalls[i] = callText;
                        moveCallStates[i] = state5546;
                        moveCallRemaining[i] = remaining5546;

                        if (i < accessory.Data.PartyList.Count)
                            partyMessages.Add($"{PartyMemberJobName(accessory, i)} {PartyPriorityLabel(i)} {callText}");
                    }
                }
            }

            var marks = BuildP4ChainMoveMarks(clocks);
            lock (_p4Lock)
                Array.Copy(marks, _p4CommandMarkByPartyIndex, marks.Length);

            ApplyP4CommandMarksNoTimer(accessory, marks);

            var myIndex = GetMyIndex(accessory);
            if (myIndex >= 0 && myIndex < 8 && clocks[myIndex] != P4ClockDirection.Unknown)
            {
                var target = targets[myIndex];
                DrawGuide(accessory, $"{DrawPrefix}_P4_Chain_Move_Guide", target, P4ChainGuideDurationMs);
                DrawStaticCircle(accessory, $"{DrawPrefix}_P4_Chain_Move_Target", target, 0.25f, P4ChainGuideDurationMs, GuideColor.V4);
                DebugEcho(accessory, $"P4循环移动指路已画：round={round} my={PartyPriorityLabel(myIndex)} clock={FormatP4Clock(clocks[myIndex])} target={FormatPosition(target)} duration={P4ChainGuideDurationMs}ms greenMove={EnableGreenMove}");
                GreenMoveToPoint(target, accessory, $"P4 chain move round {round}");

                if (!string.IsNullOrWhiteSpace(moveCalls[myIndex]))
                    ScheduleP4SelfTtsAtRemaining3(accessory, generation, round, myIndex, moveCallStates[myIndex], moveCallRemaining[myIndex], moveCalls[myIndex]);
            }
            else
            {
                DebugEcho(accessory, $"P4循环移动指路未画：round={round} myIndex={myIndex}");
            }

            SendP4PartyCallouts(accessory, partyMessages, generation, round);
            DebugP4ChainMove(accessory, round, states, expiresAt, clocks, targets, marks, moveCalls, moveCallRemaining, newTwelveDirection, directionReady, now);
        }

        private void ExecuteP4Petrify(ScriptAccessory accessory, int round)
        {
            var states = new int[8, 10];
            var expiresAt = new long[8, 10];
            var now = NowMs();

            lock (_p4Lock)
            {
                Array.Copy(_p4StatusStateByPartyAndStatus, states, _p4StatusStateByPartyAndStatus.Length);
                Array.Copy(_p4StatusExpiresAtByPartyAndStatus, expiresAt, _p4StatusExpiresAtByPartyAndStatus.Length);
            }

            var marks = BuildP4PetrifyMarks(states, expiresAt, now);
            lock (_p4Lock)
                Array.Copy(marks, _p4CommandMarkByPartyIndex, marks.Length);

            ApplyP4CommandMarksNoTimer(accessory, marks);

            var myIndex = GetMyIndex(accessory);
            string alertText = null;
            var alertPartyIndex = -1;
            var alertState = P4StateUnknown;
            var alertRemaining = 0;

            if (TryGetP4ActiveSoonState(states, expiresAt, myIndex, 5543, now, out var myState5543, out var myRemaining5543))
            {
                alertText = P4PetrifyCallText(myState5543, true);
                alertPartyIndex = myIndex;
                alertState = myState5543;
                alertRemaining = myRemaining5543;
            }
            else
            {
                for (var i = 0; i < 8; i++)
                {
                    if (!TryGetP4ActiveSoonState(states, expiresAt, i, 5543, now, out var state5543, out var remaining5543))
                        continue;

                    alertText = P4PetrifyCallText(state5543, false);
                    alertPartyIndex = i;
                    alertState = state5543;
                    alertRemaining = remaining5543;
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(alertText))
                Alert(alertText);

            DebugP4Petrify(accessory, round, states, expiresAt, marks, alertPartyIndex, alertState, alertRemaining, alertText, now);
        }

        private void ExecuteP4Element(ScriptAccessory accessory, int round)
        {
            var states = new int[8, 10];
            var expiresAt = new long[8, 10];
            var now = NowMs();

            lock (_p4Lock)
            {
                Array.Copy(_p4StatusStateByPartyAndStatus, states, _p4StatusStateByPartyAndStatus.Length);
                Array.Copy(_p4StatusExpiresAtByPartyAndStatus, expiresAt, _p4StatusExpiresAtByPartyAndStatus.Length);
            }

            TryResolveP4ElementCall(states, expiresAt, now, out var call, out var partyIndex, out var statusId, out var state, out var remainingMs);
            var text = P4ElementCallText(call);
            if (!string.IsNullOrWhiteSpace(text))
                QTTS(text);

            DebugP4Element(accessory, round, states, expiresAt, call, partyIndex, statusId, state, remainingMs, now);
        }

        private void DebugP4ChainMove(
            ScriptAccessory accessory,
            int round,
            int[,] states,
            long[,] expiresAt,
            IReadOnlyList<P4ClockDirection> clocks,
            IReadOnlyList<Vector3> targets,
            IReadOnlyList<MarkType?> marks,
            IReadOnlyList<string> moveCalls,
            IReadOnlyList<int> moveCallRemaining,
            Vector3 newTwelveDirection,
            bool directionReady,
            long now)
        {
            if (!DeveloperMode) return;

            DebugEcho(accessory, $"P4循环移动结算 round={round} new12={FormatPosition(newTwelveDirection)} ready={directionReady}");
            for (var i = 0; i < 8; i++)
            {
                var objectId = i < accessory.Data.PartyList.Count ? accessory.Data.PartyList[i] : 0;
                DebugEcho(
                    accessory,
                    $"{PartyPriorityLabel(i)} {FormatObjectId(objectId)} {FormatP4StatusStatesWithRemaining(states, expiresAt, i, now)} => clock={FormatP4Clock(clocks[i])} target={FormatPosition(targets[i])} 5546call={moveCalls[i] ?? "-"}({FormatRemainingMs(moveCallRemaining[i])}) mark={FormatP4Mark(marks[i])}");
            }
        }

        private void DebugP4Petrify(
            ScriptAccessory accessory,
            int round,
            int[,] states,
            long[,] expiresAt,
            IReadOnlyList<MarkType?> marks,
            int alertPartyIndex,
            int alertState,
            int alertRemaining,
            string alertText,
            long now)
        {
            if (!DeveloperMode) return;

            DebugEcho(accessory, $"P4石化观测 round={round} alertSource={PartyPriorityLabel(alertPartyIndex)} state={alertState} rem={FormatRemainingMs(alertRemaining)} text={alertText ?? "-"}");
            for (var i = 0; i < 8; i++)
            {
                TryGetP4ActiveSoonState(states, expiresAt, i, 5543, now, out var state5543, out var remaining5543);
                var call = P4PetrifyCallText(state5543, false) ?? "-";
                var objectId = i < accessory.Data.PartyList.Count ? accessory.Data.PartyList[i] : 0;
                DebugEcho(
                    accessory,
                    $"{PartyPriorityLabel(i)} {FormatObjectId(objectId)} {FormatP4StatusStatesWithRemaining(states, expiresAt, i, now)} => 5543={state5543}({FormatRemainingMs(remaining5543)}) call={call} mark={FormatP4Mark(marks[i])}");
            }
        }

        private void DebugP4Element(
            ScriptAccessory accessory,
            int round,
            int[,] states,
            long[,] expiresAt,
            P4ElementCall call,
            int partyIndex,
            uint statusId,
            int state,
            int remainingMs,
            long now)
        {
            if (!DeveloperMode) return;

            DebugEcho(accessory, $"P4火水观测 round={round} result={FormatP4ElementCall(call)} source={PartyPriorityLabel(partyIndex)} status={statusId} state={state} rem={FormatRemainingMs(remainingMs)}");
            for (var i = 0; i < 8; i++)
            {
                TryGetP4ActiveSoonState(states, expiresAt, i, 5547, now, out var state5547, out var remaining5547);
                TryGetP4ActiveSoonState(states, expiresAt, i, 5548, now, out var state5548, out var remaining5548);
                var objectId = i < accessory.Data.PartyList.Count ? accessory.Data.PartyList[i] : 0;
                DebugEcho(
                    accessory,
                    $"{PartyPriorityLabel(i)} {FormatObjectId(objectId)} {FormatP4StatusStatesWithRemaining(states, expiresAt, i, now)} => 5547={state5547}({FormatRemainingMs(remaining5547)}) 5548={state5548}({FormatRemainingMs(remaining5548)}) result={FormatP4ElementCall(call)}");
            }
        }

        #endregion

        #region GreenMove

        private void GreenMoveToPoint(Vector3 target, ScriptAccessory accessory, string reason)
        {
            if (!EnableGreenMove) return;

            try
            {
                if (GreenMoveClearQueueBeforeMove)
                    GreenMoveClearQueue();

                InvokeGreenMove6(
                    "MoveToPoint",
                    ref _greenMoveToPointSub,
                    ref _greenMoveToPointInvoke,
                    target,
                    GreenMoveTolerance > 0.0f ? GreenMoveTolerance : 0.1f,
                    GreenMoveSpeedMultiplier > 0.0f ? GreenMoveSpeedMultiplier : 1.0f,
                    GreenMoveIgnoreY,
                    Math.Max(0, GreenMoveMaxCastWaitMs),
                    GreenMoveWaitForQueuedCast);
            }
            catch (Exception ex)
            {
                WarnGreenMoveFailure(accessory, ex);
            }
        }

        private void GreenMoveStopAndClear(ScriptAccessory accessory, string reason)
        {
            if (!EnableGreenMove) return;

            try
            {
                GreenMoveClearQueue();
                GreenMoveStop();
            }
            catch (Exception ex)
            {
                WarnGreenMoveFailure(accessory, ex);
            }
        }

        private void GreenMoveClearQueue()
        {
            InvokeGreenMoveAction0("ClearQueue", ref _greenMoveClearQueueSub, ref _greenMoveClearQueueInvoke);
        }

        private void GreenMoveStop()
        {
            InvokeGreenMoveAction0("Stop", ref _greenMoveStopSub, ref _greenMoveStopInvoke);
        }

        private void WarnGreenMoveFailure(ScriptAccessory accessory, Exception ex)
        {
            if (_warnedGreenMove) return;

            _warnedGreenMove = true;
            accessory.Method.SendChat("/e GreenMove IPC调用失败，请确认PromeRotation已加载且GreenMove IPC已注册。");
        }

        private static void InvokeGreenMove6<T1>(
            string name,
            ref object sub,
            ref MethodInfo invoke,
            T1 arg1,
            float tolerance,
            float speed,
            bool ignoreY,
            int maxCastWaitMs,
            bool waitForQueuedCast)
        {
            var fullName = GreenMovePrefix + name;
            if (sub == null || invoke == null)
            {
                sub = CreateSubscriber<T1, float, float, bool, int, bool, object>(fullName);
                invoke = sub.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(m => m.Name == "InvokeAction" && m.GetParameters().Length == 6)
                    ?? throw new MissingMethodException($"{name} missing InvokeAction.");
            }

            try
            {
                invoke.Invoke(sub, new object[] { arg1, tolerance, speed, ignoreY, maxCastWaitMs, waitForQueuedCast });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        private static void InvokeGreenMoveAction0(string name, ref object sub, ref MethodInfo invoke)
        {
            var fullName = GreenMovePrefix + name;
            if (sub == null || invoke == null)
            {
                sub = CreateSubscriber<object>(fullName);
                invoke = sub.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(m => m.Name == "InvokeAction" && m.GetParameters().Length == 0)
                    ?? throw new MissingMethodException($"{name} missing InvokeAction.");
            }

            try
            {
                invoke.Invoke(sub, Array.Empty<object>());
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        private static object CreateSubscriber<TRet>(string fullName)
        {
            var pluginInterface = GetKodakkuPluginInterface();
            var method = pluginInterface.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m => m.Name == "GetIpcSubscriber" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1);

            if (method == null)
                throw new MissingMethodException("GetIpcSubscriber<TRet> not found.");

            return method.MakeGenericMethod(typeof(TRet)).Invoke(pluginInterface, new object[] { fullName })
                ?? throw new InvalidOperationException($"{fullName} subscriber is null.");
        }

        private static object CreateSubscriber<T1, T2, T3, T4, T5, T6, TRet>(string fullName)
        {
            var pluginInterface = GetKodakkuPluginInterface();
            var method = pluginInterface.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m => m.Name == "GetIpcSubscriber" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 7);

            if (method == null)
                throw new MissingMethodException("GetIpcSubscriber<T1,T2,T3,T4,T5,T6,TRet> not found.");

            return method.MakeGenericMethod(typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(TRet)).Invoke(pluginInterface, new object[] { fullName })
                ?? throw new InvalidOperationException($"{fullName} subscriber is null.");
        }

        private static object GetKodakkuPluginInterface()
        {
            var serviceType = typeof(ScriptAccessory).Assembly.GetType("KodakkuAssist.Data.Service")
                ?? throw new TypeLoadException("KodakkuAssist.Data.Service not found.");
            var prop = serviceType.GetProperty("PluginInterface", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new MissingMemberException("KodakkuAssist.Data.Service.PluginInterface not found.");

            return prop.GetValue(null)
                ?? throw new InvalidOperationException("KodakkuAssist PluginInterface is null.");
        }

        #endregion

        #region Draw Helpers

        private void DrawCircle(ScriptAccessory accessory, string name, Vector3 position, float radius, int duration, int delay = 0, Vector4? color = null, ScaleMode scaleMode = ScaleMode.ByTime)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = position;
            dp.Scale = new Vector2(radius);
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = duration;
            dp.ScaleMode = scaleMode;
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

        private void DrawCircleOnOwner(ScriptAccessory accessory, string name, uint ownerId, float radius, int duration, int delay = 0, Vector4? color = null, ScaleMode scaleMode = ScaleMode.ByTime)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Owner = ownerId;
            dp.Scale = new Vector2(radius);
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = duration;
            dp.ScaleMode = scaleMode;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        private void DrawRect(Event @event, ScriptAccessory accessory, string name, float width, float length, int duration, int delay = 0, Vector4? color = null, ScaleMode scaleMode = ScaleMode.YByTime)
        {
            DrawRect(accessory, name, @event.SourcePosition, @event.SourceRotation, width, length, duration, delay, color, scaleMode);
        }

        private void DrawRect(ScriptAccessory accessory, string name, Vector3 position, float rotation, float width, float length, int duration, int delay = 0, Vector4? color = null, ScaleMode scaleMode = ScaleMode.YByTime)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = position;
            dp.Rotation = rotation;
            dp.Scale = new Vector2(width, length);
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = duration;
            dp.ScaleMode = scaleMode;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        private void DrawFan(Event @event, ScriptAccessory accessory, string name, float radius, float radian, int duration, int delay = 0, Vector4? color = null, ScaleMode scaleMode = ScaleMode.ByTime)
        {
            DrawFan(accessory, name, @event.SourcePosition, @event.SourceRotation, radius, radian, duration, delay, color, scaleMode);
        }

        private void DrawFan(ScriptAccessory accessory, string name, Vector3 position, float rotation, float radius, float radian, int duration, int delay = 0, Vector4? color = null, ScaleMode scaleMode = ScaleMode.ByTime)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = position;
            dp.Rotation = rotation;
            dp.Scale = new Vector2(radius);
            dp.Radian = radian;
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = duration;
            dp.ScaleMode = scaleMode;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        private void DrawLineToTarget(ScriptAccessory accessory, string name, uint sourceId, uint targetId, float width, int duration, int delay = 0, Vector4? color = null)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Owner = sourceId;
            dp.TargetObject = targetId;
            dp.Scale = new Vector2(width, 1.0f);
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = duration;
            dp.ScaleMode = ScaleMode.YByDistance;
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
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }

        private void DrawGuideBetweenPositions(ScriptAccessory accessory, string name, Vector3 sourcePosition, Vector3 targetPosition, int duration, int delay = 0, Vector4? color = null)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = sourcePosition;
            dp.TargetPosition = targetPosition;
            dp.Scale = new Vector2(0.5f);
            dp.ScaleMode = ScaleMode.YByDistance;
            dp.Color = color ?? GuideColor.V4;
            dp.Delay = delay;
            dp.DestoryAt = duration;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }

        private void DrawInactiveGuideBetweenPositions(ScriptAccessory accessory, string name, Vector3 sourcePosition, Vector3 targetPosition, int duration, int delay = 0)
        {
            DrawGuideBetweenPositions(accessory, name, sourcePosition, targetPosition, duration, delay, SolidDangerRed);
        }

        #endregion

        #region Mechanisms

        [ScriptMethod(name: "P4开始", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49884"], userControl: false)]
        public void P4_Start(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            _phase = Phase.P4;
            ResetMechanic(accessory);
        }

        [ScriptMethod(name: "P4 2056真伪窗口", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2056"], userControl: false)]
        public void P4_StatusTruthWindow(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            if (_phase != Phase.P4) return;

            if (!int.TryParse(@event["Param"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var param))
                return;

            var now = NowMs();

            if (param == 1121 || param == 1122)
            {
                var targetPosition = ResolveEventTargetPosition(@event, accessory);
                var newTwelveDirection = DefaultNorth;
                var directionReady = IsLikelyArenaPosition(targetPosition)
                    && TryNormalizeFromCenter(targetPosition, out newTwelveDirection);

                lock (_p4Lock)
                {
                    _p4XParam = param;
                    _p4XUpdatedAt = now;
                    _p4XExpiresAt = now + P4StatusWindowMs;
                    _p4XEventCount++;

                    if (_p4XEventCount == 4)
                    {
                        _p4FourthXParam = param;
                        _p4FourthTargetPosition = targetPosition;
                        _p4FourthNewTwelveDirection = directionReady ? newTwelveDirection : DefaultNorth;
                        _p4FourthDirectionReady = directionReady;
                    }
                }
            }
            else if (param == 1119 || param == 1120)
            {
                lock (_p4Lock)
                {
                    _p4CParam = param;
                    _p4CUpdatedAt = now;
                    _p4CExpiresAt = now + P4StatusWindowMs;
                }
            }
        }

        [ScriptMethod(name: "P4 状态变量赋值", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(554[1-8]|454|1382|4887|4888|5464)$"], userControl: false)]
        public void P4_TrackedStatusAdd(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            if (_phase != Phase.P4) return;

            if (!TryGetStatusId(@event, out var rawStatusId))
                return;

            var directTrue = TryGetP4DirectTrueStatus(rawStatusId, out var directTrueStatusId);
            var statusId = directTrue ? directTrueStatusId : rawStatusId;

            if (!TryGetP4TrackedStatusIndex(statusId, out var statusIndex))
                return;

            if (!TryGetTargetId(@event, out var targetId))
                return;

            var partyIndex = GetPlayerIndex(accessory, targetId);
            if (partyIndex < 0 || partyIndex >= 8)
                return;

            var now = NowMs();
            var hasDuration = TryGetDurationMs(@event, out var durationMs);
            var assigned = false;
            var assignedState = P4StateUnknown;
            var xParam = 0;
            var cParam = 0;
            var xRemainingMs = 0;
            var cRemainingMs = 0;
            var xActive = false;
            var cActive = false;
            TryGetSourceId(@event, out var sourceId);

            lock (_p4Lock)
            {
                if (hasDuration)
                    _p4StatusExpiresAtByPartyAndStatus[partyIndex, statusIndex] = now + durationMs;

                if (directTrue)
                {
                    _p4StatusStateByPartyAndStatus[partyIndex, statusIndex] = P4StateTrue;
                    assigned = true;
                    assignedState = P4StateTrue;
                }
                else if (TryGetActiveP4StateValueLocked(now, out var stateValue))
                {
                    _p4StatusStateByPartyAndStatus[partyIndex, statusIndex] = stateValue;
                    assigned = true;
                    assignedState = stateValue;
                }

                xParam = _p4XParam;
                cParam = _p4CParam;
                xRemainingMs = RemainingMs(_p4XExpiresAt, now);
                cRemainingMs = RemainingMs(_p4CExpiresAt, now);
                xActive = _p4XParam != 0 && xRemainingMs > 0;
                cActive = _p4CParam != 0 && cRemainingMs > 0;
            }

            if (statusId == 5541 || statusId == 5542 || statusId == 454 || statusId == 1382)
            {
                DebugEcho(
                    accessory,
                    $"P4关键状态捕获 raw={rawStatusId} as={statusId} direct={directTrue} target={PartyPriorityLabel(partyIndex)} {FormatObjectId(targetId)} source={FormatObjectId(sourceId)} duration={(hasDuration ? durationMs.ToString(CultureInfo.InvariantCulture) : "-")}ms assigned={(assigned ? assignedState.ToString(CultureInfo.InvariantCulture) : "-")} x={xParam}/{FormatRemainingMs(xRemainingMs)}/{xActive} c={cParam}/{FormatRemainingMs(cRemainingMs)}/{cActive}");
            }
        }

        [ScriptMethod(name: "P4 50070后续循环开始", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:50070"], userControl: false)]
        public void P4_ChainStartActionEffect(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            if (_phase != Phase.P4) return;

            if (!TryStartP4Chain(out var round, out var stepGeneration))
                return;

            var generation = _generation;
            accessory.Method.RemoveDraw($"{DrawPrefix}_P4_First_.*");
            ClearP4CommandMarksNow(accessory);
            GreenMoveStopAndClear(accessory, "P4 chain start");

            ScheduleP4DelayedStep(
                accessory,
                generation,
                stepGeneration,
                round,
                P4ChainStep.Move,
                P4ChainDelayMs,
                () => ExecuteP4ChainMove(accessory, generation, round));
        }

        [ScriptMethod(name: "P4 后续状态移除推进", eventType: EventTypeEnum.StatusRemove, eventCondition: ["StatusID:regex:^554[3-8]$"], userControl: false)]
        public void P4_TrackedStatusRemove(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            if (_phase != Phase.P4) return;

            if (!TryGetStatusId(@event, out var statusId))
                return;

            if (!TryGetP4TrackedStatusIndex(statusId, out _))
                return;

            if (!TryGetTargetId(@event, out var targetId))
                return;

            var partyIndex = GetPlayerIndex(accessory, targetId);
            if (partyIndex < 0 || partyIndex >= 8)
                return;

            var generation = _generation;

            if (statusId == 5544 || statusId == 5545)
            {
                if (!TryAdvanceP4ChainStep(P4ChainStep.Move, P4ChainStep.Petrify, out var round, out var stepGeneration))
                    return;

                accessory.Method.RemoveDraw($"{DrawPrefix}_P4_Chain_Move_.*");
                ClearP4CommandMarksNow(accessory);
                GreenMoveStopAndClear(accessory, "P4 move status removed");

                ScheduleP4DelayedStep(
                    accessory,
                    generation,
                    stepGeneration,
                    round,
                    P4ChainStep.Petrify,
                    P4ChainDelayMs,
                    () => ExecuteP4Petrify(accessory, round));
            }
            else if (statusId == 5543)
            {
                if (!TryAdvanceP4ChainStep(P4ChainStep.Petrify, P4ChainStep.Element, out var round, out var stepGeneration))
                    return;

                accessory.Method.RemoveDraw($"{DrawPrefix}_P4_Chain_.*");
                ClearP4CommandMarksNow(accessory);
                GreenMoveStopAndClear(accessory, "P4 petrify status removed");

                ScheduleP4DelayedStep(
                    accessory,
                    generation,
                    stepGeneration,
                    round,
                    P4ChainStep.Element,
                    P4ChainDelayMs,
                    () => ExecuteP4Element(accessory, round));
            }
            else if (statusId == 5547 || statusId == 5548)
            {
                if (!TryAdvanceP4ChainRound(out var round, out var stepGeneration))
                    return;

                accessory.Method.RemoveDraw($"{DrawPrefix}_P4_Chain_.*");
                ClearP4CommandMarksNow(accessory);

                ScheduleP4DelayedStep(
                    accessory,
                    generation,
                    stepGeneration,
                    round,
                    P4ChainStep.Move,
                    P4ChainDelayMs,
                    () => ExecuteP4ChainMove(accessory, generation, round));
            }
        }

        [ScriptMethod(name: "P4 一运半场指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:50069"], userControl: false)]
        public void P4_FirstMechanicGuide(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            if (_phase != Phase.P4) return;

            var states = new int[8, 10];
            int fourthParam;
            Vector3 newTwelveDirection;

            lock (_p4Lock)
            {
                fourthParam = _p4FourthXParam;
                newTwelveDirection = _p4FourthNewTwelveDirection;

                if ((fourthParam != 1121 && fourthParam != 1122) || !_p4FourthDirectionReady)
                {
                    DebugEcho(accessory, $"P4一运结算跳过：第四次19510/2056未就绪 param={fourthParam} ready={_p4FourthDirectionReady} target={FormatPosition(_p4FourthTargetPosition)}");
                    return;
                }

                Array.Copy(_p4StatusStateByPartyAndStatus, states, _p4StatusStateByPartyAndStatus.Length);
            }

            var rightVector = RightVectorFromNewTwelve(newTwelveDirection);
            var l1 = P4ArenaCenter - rightVector * P4GuideOffset;
            var r1 = P4ArenaCenter + rightVector * P4GuideOffset;
            var sourceSide = SideOfPosition(@event.SourcePosition, rightVector);
            var sourceSideColor = fourthParam == 1122 ? P4HalfColor.Purple : P4HalfColor.Blue;
            var otherSideColor = OppositeColor(sourceSideColor);
            var leftColor = sourceSide == P4HalfSide.Left ? sourceSideColor : otherSideColor;
            var rightColor = sourceSide == P4HalfSide.Right ? sourceSideColor : otherSideColor;

            var sides = new P4HalfSide[8];
            var colors = new P4HalfColor[8];

            for (var i = 0; i < 8; i++)
            {
                if (!TryResolveP4PlayerColor(states, i, out var color, out _, out _))
                {
                    colors[i] = P4HalfColor.Unknown;
                    sides[i] = P4HalfSide.Unknown;
                    continue;
                }

                colors[i] = color;
                sides[i] = SideForColor(color, leftColor, rightColor);
            }

            var marks = BuildP4CommandMarks(sides);

            lock (_p4Lock)
            {
                Array.Copy(sides, _p4ResolvedSideByPartyIndex, sides.Length);
                Array.Copy(colors, _p4ResolvedColorByPartyIndex, colors.Length);
                Array.Copy(marks, _p4CommandMarkByPartyIndex, marks.Length);
            }

            ApplyP4CommandMarks(accessory, marks);

            var myIndex = GetMyIndex(accessory);
            if (myIndex >= 0 && myIndex < sides.Length)
            {
                var mySide = sides[myIndex];
                if (mySide == P4HalfSide.Left || mySide == P4HalfSide.Right)
                {
                    var targetPosition = mySide == P4HalfSide.Right ? r1 : l1;
                    DrawGuide(accessory, $"{DrawPrefix}_P4_First_Guide", targetPosition, P4GuideDurationMs);
                    DrawStaticCircle(accessory, $"{DrawPrefix}_P4_First_Target", targetPosition, 0.25f, P4GuideDurationMs, GuideColor.V4);
                    DebugEcho(accessory, $"P4一运指路已画：my={PartyPriorityLabel(myIndex)} side={FormatP4Side(mySide)} color={FormatP4Color(colors[myIndex])} target={FormatPosition(targetPosition)} duration={P4GuideDurationMs}ms greenMove={EnableGreenMove}");
                    GreenMoveToPoint(targetPosition, accessory, "P4 first mechanic");
                }
                else
                {
                    DebugEcho(accessory, $"P4一运指路未画：my={PartyPriorityLabel(myIndex)} side={FormatP4Side(mySide)} color={FormatP4Color(colors[myIndex])} states={FormatP4StatusStates(states, myIndex)}");
                }
            }
            else
            {
                DebugEcho(accessory, $"P4一运指路未画：myIndex={myIndex} partyCount={accessory.Data.PartyList.Count}");
            }

            DebugP4Resolve(accessory, states, sides, colors, marks, fourthParam, sourceSide, leftColor, rightColor, newTwelveDirection, l1, r1);
        }

        #endregion
    }
}
