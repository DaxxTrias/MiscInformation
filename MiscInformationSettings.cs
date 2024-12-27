using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;
using System.Drawing;
using System.Windows.Forms;

namespace MiscInformation
{
    public class MiscInformationSettings : ISettings
    {
        public MiscInformationSettings()
        {
            BackgroundColor = new (Color.FromArgb(255, 0, 0, 0)); // Does nothing.
            XphTextColor = new (Color.FromArgb(220, 190, 130, 255));
            XphGetLeft = new(Color.FromArgb(220, 190, 130, 255));
            TimeLeftColor = new(Color.FromArgb(220, 190, 130, 255));
            TimerTextColor = new(Color.FromArgb(220, 190, 130, 255));
            LatencyTextColor = new(Color.FromArgb(220, 190, 130, 255));
        }

        public ToggleNode Enable { get; set; } = new ToggleNode(true);
        public RangeNode<int> DrawXOffset { get; set; } = new RangeNode<int>(0, -150, 150);
        public ColorNode BackgroundColor { get; set; }
        public ColorNode XphTextColor { get; set; }
        public ColorNode XphGetLeft { get; set; }
        public ColorNode TimeLeftColor { get; set; }
        public ColorNode TimerTextColor { get; set; }
        public ColorNode LatencyTextColor { get; set; }
    }
}
