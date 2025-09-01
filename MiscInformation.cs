using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared;
using ExileCore2.Shared.Cache;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Helpers;
using JM.LinqFaster;
using Input = ExileCore2.Input;
using Vector2N = System.Numerics.Vector2;
using System.Runtime.InteropServices;

namespace MiscInformation
{
    public class MiscInformation : BaseSettingsPlugin<MiscInformationSettings>
    {
        private string areaName = "";

        private Dictionary<int, float> ArenaEffectiveLevels = new Dictionary<int, float>
        {
            {71, 70.94f},
            {72, 71.82f},
            {73, 72.64f},
            {74, 73.4f},
            {75, 74.1f},
            {76, 74.74f},
            {77, 75.32f},
            {78, 75.84f},
            {79, 76.3f},
            {80, 76.7f},
            {81, 77.04f},
            {82, 77.32f},
            {83, 77.54f},
            {84, 77.7f}
        };

        private TimeCache<bool> CalcXp;
        private bool CanRender;
        private DebugInformation debugInformation;
        private Vector2N drawTextVector2;
        private string latency = "";
        private RectangleF leftPanelStartDrawRect = RectangleF.Empty;
        private TimeCache<bool> LevelPenalty;
        private double levelXpPenalty, partyXpPenalty;
        private float percentGot;
        private double partytime = 4000;
        private string ping = "";
        private DateTime startTime, lastTime;
        private long startXp, getXp, xpLeftQ;
        private float startY;
        private double time;
        private string Time = "";
        private string timeLeft = "";
        private TimeSpan timeSpan;
        private string xpGetLeft = "";
        private string xpRate = "";
        private string xpReceivingText = "";
        private DateTime _lastHighPingActionUtc = DateTime.MinValue;
        private float? _initialLeftPanelY;

        private const int KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const int KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        private static extern uint keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        private static void PressKey(Keys key)
        {
            keybd_event((byte)key, 0, KEYEVENTF_EXTENDEDKEY | 0, 0);
            keybd_event((byte)key, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
        }

        private bool TryInvokePauseOrMenuViaPluginBridge()
        {
            // Try a few conventional method names; succeed on the first found
            var candidateMethods = new[]
            {
                "ReAgent.TriggerPause",
                "ReAgent.TriggerMainMenu",
                "ReAgent.Pause",
                "ReAgent.MainMenu"
            };

            foreach (var methodName in candidateMethods)
            {
                try
                {
                    var method = GameController.PluginBridge.GetMethod<Action>(methodName);
                    if (method != null)
                    {
                        method();
                        return true;
                    }
                }
                catch
                {
                    // Ignore and try next candidate
                }
            }

            return false;
        }

        public float GetEffectiveLevel(int monsterLevel)
        {
            return Convert.ToSingle(-0.03 * Math.Pow(monsterLevel, 2) + 5.17 * monsterLevel - 144.9);
        }

        public override void OnLoad()
        {
            Order = -50;
            Graphics.InitImage("menu-background.png");
        }

        public override bool Initialise()
        {
            Input.RegisterKey(Keys.F10);

            Input.ReleaseKey += (sender, keys) =>
            {
                if (keys == Keys.F10) Settings.Enable.Value = !Settings.Enable;
            };

            GameController.LeftPanel.WantUse(() => Settings.Enable);
            CalcXp = new TimeCache<bool>(() =>
            {
                partytime += time;
                time = 0;
                CalculateXp();
                var areaCurrentArea = GameController.Area.CurrentArea;

                if (areaCurrentArea == null)
                    return false;

                timeSpan = DateTime.UtcNow - areaCurrentArea.TimeEntered;

                // Time = $"{timeSpan.TotalMinutes:00}:{timeSpan.Seconds:00}";
                Time = AreaInstance.GetTimeString(timeSpan);
                xpReceivingText = $"{xpRate}  *{levelXpPenalty * partyXpPenalty:p0}";

                xpGetLeft =
                    $"Got: {ConvertHelper.ToShorten(getXp, "0.00")} ({percentGot:P3})  Left: {ConvertHelper.ToShorten(xpLeftQ, "0.00")}";

                if (partytime > 4900)
                {
                    var levelPenaltyValue = LevelPenalty.Value;
                }

                return true;
            }, 1000);

            LevelPenalty = new TimeCache<bool>(() =>
            {
                partyXpPenalty = PartyXpPenalty();
                levelXpPenalty = LevelXpPenalty();
                return true;
            }, 5000);

            GameController.EntityListWrapper.PlayerUpdate += OnEntityListWrapperOnPlayerUpdate;
            // Only invoke the handler if the player entity is available during init
            var playerEntity = GameController.Player;
            if (playerEntity != null)
                OnEntityListWrapperOnPlayerUpdate(this, playerEntity);

            debugInformation = new DebugInformation("Game FPS", "Collect game fps", false);
            return true;
        }

        private void OnEntityListWrapperOnPlayerUpdate(object sender, Entity entity)
        {
            var playerComp = entity?.GetComponent<Player>();
            if (playerComp == null)
                return;

            if (!Settings.PersistData.Value || startXp == 0)
            {
                percentGot = 0;
                xpRate = "0.00 xp/h";
                timeLeft = "-h -m -s  to level up";
                getXp = 0;
                xpLeftQ = 0;

                startTime = lastTime = DateTime.UtcNow;
                startXp = playerComp.XP;
                levelXpPenalty = LevelXpPenalty();
            }
        }

        public override void AreaChange(AreaInstance area)
        {
            LevelPenalty.ForceUpdate();
            _initialLeftPanelY = null;
        }

        public override void Tick()
        {
            TickLogic();
        }

        private void TickLogic()
        {
            time += GameController.DeltaTime;
            var gameUi = GameController.Game.IngameState.IngameUi;

            if (GameController.Area.CurrentArea == null || gameUi.InventoryPanel.IsVisible)
            {
                CanRender = false;
                return;
            }

            var UIHover = GameController.Game.IngameState.UIHover;

            if (UIHover.Tooltip != null && UIHover.Tooltip.IsVisibleLocal &&
                UIHover.Tooltip.GetClientRectCache.Intersects(leftPanelStartDrawRect))
            {
                CanRender = false;
                return;
            }

            CanRender = true;

            var calcXpValue = CalcXp.Value;
            //var ingameStateCurFps = GameController?.Game?.IngameState?.CurFps ?? 1.0f;
            //debugInformation.Tick = ingameStateCurFps;
            var areaSuffix = (GameController.Area.CurrentArea.RealLevel >= 68)
                ? $" - T{GameController.Area.CurrentArea.RealLevel - 67}"
                : "";

            areaName = $"{GameController.Area.CurrentArea.DisplayName}{areaSuffix}";
            latency = $"({GameController.Game.IngameState.ServerData.Latency})";
            ping = $"ping:({GameController.Game.IngameState.ServerData.Latency})";

            // High ping handler
            if (Settings.EnableHighPingHandler.Value)
            {
                try
                {
                    var area = GameController.Area.CurrentArea;
                    if (area is { IsTown: false, IsHideout: false })
                    {
                        var latencyMs = GameController.Game.IngameState.ServerData.Latency;
                        if (latencyMs >= Settings.HighPingThresholdMs.Value)
                        {
                            var now = DateTime.UtcNow;
                            var minNext = _lastHighPingActionUtc.AddMilliseconds(Settings.HighPingCooldownMs.Value);
                            if (now >= minNext)
                            {
                                var invoked = TryInvokePauseOrMenuViaPluginBridge();
                                if (!invoked)
                                {
                                    PressKey(Keys.Escape);
                                }

                                _lastHighPingActionUtc = now;
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore errors during high-ping handling to avoid disrupting normal flow
                }
            }
        }

        private void CalculateXp()
        {
            var level = GameController.Player.GetComponent<Player>()?.Level ?? 100;

            if (level >= 100)
            {
                // player can't level up, just show fillers
                xpRate = "0.00 xp/h";
                timeLeft = "--h--m--s";
                return;
            }

            long currentXp = GameController.Player.GetComponent<Player>().XP;
            getXp = currentXp - startXp;
            var rate = (currentXp - startXp) / (DateTime.UtcNow - startTime).TotalHours;
            xpRate = $"{ConvertHelper.ToShorten(rate, "0.00")} xp/h";

            if (level >= 0 && level + 1 < Constants.PlayerXpLevels.Length && rate > 1)
            {
                var xpLeft = Constants.PlayerXpLevels[level + 1] - currentXp;
                xpLeftQ = xpLeft;
                var time = TimeSpan.FromHours(xpLeft / rate);
                timeLeft = $"{time.Hours:0}h {time.Minutes:00}m {time.Seconds:00}s to level up";

                if (getXp == 0)
                    percentGot = 0;
                else
                {
                    percentGot = getXp / ((float)Constants.PlayerXpLevels[level + 1] - (float)Constants.PlayerXpLevels[level]);
                    if (percentGot < -100) percentGot = 0;
                }
            }
        }

        private double LevelXpPenalty()
        {
            var area = GameController.Area.CurrentArea;
            if (area == null)
                return 1d;

            var arenaLevel = area.RealLevel;
            var characterLevel = GameController.Player.GetComponent<Player>()?.Level ?? 100;


            if (arenaLevel > 70 && !ArenaEffectiveLevels.ContainsKey(arenaLevel))
            {
                // calculate the effective level and add it to dictionary
                ArenaEffectiveLevels.Add(arenaLevel, GetEffectiveLevel(arenaLevel));
            }
            var effectiveArenaLevel = arenaLevel < 71 ? arenaLevel : ArenaEffectiveLevels[arenaLevel];
            var safeZone = Math.Floor(Convert.ToDouble(characterLevel) / 16) + 3;
            var effectiveDifference = Math.Max(Math.Abs(characterLevel - effectiveArenaLevel) - safeZone, 0);
            double xpMultiplier;

            xpMultiplier = Math.Pow((characterLevel + 5) / (characterLevel + 5 + Math.Pow(effectiveDifference, 2.5)), 1.5);

            if (characterLevel >= 95) //For player levels equal to or higher than 95:
                xpMultiplier *= 1d / (1 + 0.1 * (characterLevel - 94));

            xpMultiplier = Math.Max(xpMultiplier, 0.01);

            return xpMultiplier;
        }

        private double PartyXpPenalty()
        {
            var entities = GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Player];

            if (entities.Count == 0)
                return 1;

            var levels = entities.Select(y => y.GetComponent<Player>()?.Level ?? 100).ToList();
            var characterLevel = GameController.Player.GetComponent<Player>()?.Level ?? 100;
            var partyXpPenalty = Math.Pow(characterLevel + 10, 2.71) / levels.SumF(level => Math.Pow(level + 10, 2.71));
            return partyXpPenalty * levels.Count;
        }

        public override void Render()
        {
            if (!CanRender)
                return;

            if (GameController.Area.CurrentArea == null || !Settings.ShowInTown && GameController.Area.CurrentArea.IsTown ||
                !Settings.ShowInTown && GameController.Area.CurrentArea.IsHideout)
                return;

            var origStartPoint = GameController.LeftPanel.StartDrawPoint;

            // Apply X offset
            var pos = Settings.DisplayPosition.Value;
            var rightHalfDrawPoint = origStartPoint.Translate(
                pos.X - GameController.IngameState.IngameUi.MapSideUI.Width);

            // Establish a stable Y anchor (first observed left panel Y per area)
            var anchorY = _initialLeftPanelY ?? origStartPoint.Y;
            if (_initialLeftPanelY == null) _initialLeftPanelY = anchorY;

            // Add Y offset to the starting Y position for background and text rendering.
            leftPanelStartDrawRect = new RectangleF(rightHalfDrawPoint.X, anchorY + pos.Y, 1, 1);

            var leftSideItems = new[]
            {
            (Time, Settings.TimerTextColor),
            (ping, Settings.LatencyTextColor)
        };

            var rightSideItems = new[]
            {
            (areaName, Settings.UseBuiltInAreaColor ? GameController.Area.CurrentArea.AreaColorName : Settings.AreaTextColor.Value),
            (timeLeft, Settings.TimeLeftColor.Value),
            (xpReceivingText, Settings.XphTextColor.Value),
            (xpGetLeft, Settings.XphTextColor.Value)
        };

            var rightTextBounds = leftSideItems.Select(x => Graphics.MeasureText(x.Item1)).ToList()
                switch
            {
                var s => new Vector2N(s.DefaultIfEmpty(Vector2N.Zero).Max(x => x.X), s.Sum(x => x.Y))
            };

            var leftTextBounds = rightSideItems.Select(x => Graphics.MeasureText(x.Item1)).ToList()
                switch
            {
                var s => new Vector2N(s.DefaultIfEmpty(Vector2N.Zero).Max(x => x.X), s.Sum(x => x.Y))
            };

            var sumX = rightTextBounds.X + leftTextBounds.X + 5;
            var maxY = Math.Max(rightTextBounds.Y, leftTextBounds.Y);

            // Apply the Y offset for the background
            var leftHalfDrawPoint = rightHalfDrawPoint with { X = rightHalfDrawPoint.X - sumX };

            // Apply the DrawYOffset to the startY for text drawing
            startY = anchorY + pos.Y;

            var bounds = new RectangleF(leftHalfDrawPoint.X, startY - 2, sumX, maxY);
            Graphics.DrawImage("menu-background.png", bounds, Settings.BackgroundColor);

            // Adjust the text rendering positions to account for the DrawYOffset
            Vector2N leftTextPosition = new Vector2N(leftHalfDrawPoint.X, startY);
            Vector2N rightTextPosition = new Vector2N(rightHalfDrawPoint.X, startY);

            // Render the left side text items
            foreach (var (text, color) in leftSideItems)
            {
                drawTextVector2 = Graphics.DrawText(text, leftTextPosition, color);
                leftTextPosition.Y += drawTextVector2.Y;
            }

            // Render the right side text items
            foreach (var (text, color) in rightSideItems)
            {
                drawTextVector2 = Graphics.DrawText(text, rightTextPosition, color, FontAlign.Right);
                rightTextPosition.Y += drawTextVector2.Y;
            }

            // Update the StartDrawPoint with the new Y position after the render
            GameController.LeftPanel.StartDrawPoint = new System.Numerics.Vector2(origStartPoint.X, origStartPoint.Y + maxY + 10);
        }
    }
}
