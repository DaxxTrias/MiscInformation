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
        [Menu("Clone Frame / Ability Mirror", "Mirror selected skill-bar child rects in a single horizontal line.")]
        public CloneFrameSettings CloneFrame { get; set; } = new CloneFrameSettings();
    }

    [Submenu(CollapsedByDefault = true)]
    public class CloneFrameSettings
    {
        // TODO: Investigate direct skill icon/texture data from ExileCore memory to replace screen capture.
        [Menu("Enable Clone Frame", "Incredibly performance intensive. Capture only the skills you actually want to see. Less capture slots = better performance.")]
        public ToggleNode Enable { get; set; } = new ToggleNode(false);

        [Menu("Show in Town and Hideout")]
        public ToggleNode ShowInTown { get; set; } = new ToggleNode(true);

        [Menu("Capture Child 0")]
        public ToggleNode CaptureChild0 { get; set; } = new ToggleNode(false);

        [Menu("Capture Child 1")]
        public ToggleNode CaptureChild1 { get; set; } = new ToggleNode(false);

        [Menu("Capture Child 2")]
        public ToggleNode CaptureChild2 { get; set; } = new ToggleNode(false);

        [Menu("Capture Child 3")]
        public ToggleNode CaptureChild3 { get; set; } = new ToggleNode(true);

        [Menu("Capture Child 4")]
        public ToggleNode CaptureChild4 { get; set; } = new ToggleNode(true);

        [Menu("Capture Child 5")]
        public ToggleNode CaptureChild5 { get; set; } = new ToggleNode(true);

        [Menu("Capture Child 6")]
        public ToggleNode CaptureChild6 { get; set; } = new ToggleNode(true);

        [Menu("Capture Child 7")]
        public ToggleNode CaptureChild7 { get; set; } = new ToggleNode(true);

        [Menu("Target Position", "Top-left corner where the captured region is drawn.")]
        public RangeNode<Vector2> TargetPosition { get; set; } = new RangeNode<Vector2>(new Vector2(850, 315), new Vector2(-4000, -4000), Vector2.One * 4000);

        [Menu("Target Size", "Width and height of each mirrored skill tile.")]
        public RangeNode<Vector2> TargetSize { get; set; } = new RangeNode<Vector2>(new Vector2(40, 40), Vector2.One, Vector2.One * 2000);

        [Menu("Opacity", "0 is invisible, 255 is fully opaque.")]
        public RangeNode<int> Opacity { get; set; } = new RangeNode<int>(150, 0, 255);

        [Menu("Refresh Interval (ms)", "How often to sample selected slots. Unchanged snapshots reuse cached textures. Higher values = better performance.")]
        public RangeNode<int> RefreshIntervalMs { get; set; } = new RangeNode<int>(250, 16, 1000);

        [Menu("Draw Source Outline")]
        public ToggleNode DrawSourceOutline { get; set; } = new ToggleNode(false);

        [Menu("Draw Target Outline")]
        public ToggleNode DrawTargetOutline { get; set; } = new ToggleNode(true);
    }
}
