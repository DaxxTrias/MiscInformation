using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;
using System.Drawing;
using System.Numerics;

namespace MiscInformation
{
    public class MiscInformationSettings : ISettings
    {
        public MiscInformationSettings()
        {
            ShowInTown = new ToggleNode(false);
            PersistData = new ToggleNode(false);
        }

        public ToggleNode Enable { get; set; } = new ToggleNode(true);
        [Menu("Show in Town and Hideout")]
        public ToggleNode ShowInTown { get; set; }
        [Menu("Display Position", "X and Y coordinates to draw on screen")]
        public RangeNode<Vector2> DisplayPosition { get; set; } = new(new Vector2(160, 160), new Vector2(-4000, -4000), Vector2.One * 4000);
        public ToggleNode UseBuiltInAreaColor { get; set; } = new ToggleNode(true);
        [Menu("Persist Data", "If enabled data will not reset on area change")]
        public ToggleNode PersistData { get; set; }
        public ColorNode BackgroundColor { get; set; } = new ColorNode(Color.FromArgb(255, 0, 0, 0));
        public ColorNode AreaTextColor { get; set; } = new ColorNode(Color.FromArgb(255, 200, 255, 140));
        public ColorNode XphTextColor { get; set; } = new ColorNode(Color.FromArgb(220, 190, 130, 255));
        public ColorNode XphGetLeft { get; set; } = new ColorNode(Color.FromArgb(220, 190, 130, 255));
        public ColorNode TimeLeftColor { get; set; } = new ColorNode(Color.FromArgb(220, 190, 130, 255));
        public ColorNode TimerTextColor { get; set; } = new ColorNode(Color.FromArgb(220, 190, 130, 255));
        public ColorNode LatencyTextColor { get; set; } = new ColorNode(Color.FromArgb(220, 190, 130, 255));
        [Menu("High Ping Handling", "Pause game if ping exceeds threshhold while outside town")] public ToggleNode EnableHighPingHandler { get; set; } = new ToggleNode(false);
        [Menu("High Ping Threshold (ms)")] public RangeNode<int> HighPingThresholdMs { get; set; } = new RangeNode<int>(1000, 100, 5000);
        [Menu("High Ping Cooldown (ms)", "Minimum time between actions to avoid spam")] public RangeNode<int> HighPingCooldownMs { get; set; } = new RangeNode<int>(3000, 0, 30000);
        [Menu("Clone Frame / Ability Mirror", "Capture a small client-area region and redraw it elsewhere.")]
        public CloneFrameSettings CloneFrame { get; set; } = new CloneFrameSettings();
    }

    [Submenu(CollapsedByDefault = true)]
    public class CloneFrameSettings
    {
        [Menu("Enable Clone Frame")]
        public ToggleNode Enable { get; set; } = new ToggleNode(false);

        [Menu("Show in Town and Hideout")]
        public ToggleNode ShowInTown { get; set; } = new ToggleNode(true);

        [Menu("Source Position", "Top-left corner of the captured region, relative to the game client.")]
        public RangeNode<Vector2> SourcePosition { get; set; } = new RangeNode<Vector2>(new Vector2(720, 950), new Vector2(-4000, -4000), Vector2.One * 4000);

        [Menu("Source Size", "Width and height of the captured region.")]
        public RangeNode<Vector2> SourceSize { get; set; } = new RangeNode<Vector2>(new Vector2(480, 96), Vector2.One, Vector2.One * 2000);

        [Menu("Target Position", "Top-left corner where the captured region is drawn.")]
        public RangeNode<Vector2> TargetPosition { get; set; } = new RangeNode<Vector2>(new Vector2(820, 560), new Vector2(-4000, -4000), Vector2.One * 4000);

        [Menu("Target Size", "Width and height of the mirrored image.")]
        public RangeNode<Vector2> TargetSize { get; set; } = new RangeNode<Vector2>(new Vector2(280, 56), Vector2.One, Vector2.One * 2000);

        [Menu("Opacity", "0 is invisible, 255 is fully opaque.")]
        public RangeNode<int> Opacity { get; set; } = new RangeNode<int>(230, 0, 255);

        [Menu("Refresh Interval (ms)", "Higher values reduce capture cost. 66ms is about 15 fps.")]
        public RangeNode<int> RefreshIntervalMs { get; set; } = new RangeNode<int>(66, 16, 1000);

        [Menu("Draw Source Outline")]
        public ToggleNode DrawSourceOutline { get; set; } = new ToggleNode(false);

        [Menu("Draw Target Outline")]
        public ToggleNode DrawTargetOutline { get; set; } = new ToggleNode(true);
    }
}
