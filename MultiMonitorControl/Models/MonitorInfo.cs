// Models/MonitorInfo.cs
using System;
using System.Drawing;

namespace MultiMonitorControl.Models
{
    public class MonitorInfo
    {
        public IntPtr Handle { get; set; }
        public IntPtr LogicalHandle { get; set; }
        public string Name { get; set; } = string.Empty;
        public Rectangle Bounds { get; set; }
        public bool IsPrimary { get; set; }
        public bool SupportsControlAPI { get; set; }

        public override string ToString()
        {
            var primary = IsPrimary ? " (Primary)" : "";
            var support = SupportsControlAPI ? "" : " [Limited Support]";
            return $"{Name}{primary} - {Bounds.Width}x{Bounds.Height}{support}";
        }
    }
}
