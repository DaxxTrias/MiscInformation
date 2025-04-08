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
        public RangeNode<Vector2> DisplayPosition { get; set; } = new(new Vector2(160, 160), Vector2.Zero, Vector2.One * 4000);
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
    }
}
