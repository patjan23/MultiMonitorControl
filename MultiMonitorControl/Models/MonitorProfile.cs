// Models/MonitorProfile.cs
using System;

namespace MultiMonitorControl.Models
{
    public class MonitorProfile
    {
        public string MonitorName { get; set; } = string.Empty;
        public int Brightness { get; set; } = 50;
        public int Contrast { get; set; } = 50;
        public int RedGain { get; set; } = 50;
        public int GreenGain { get; set; } = 50;
        public int BlueGain { get; set; } = 50;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0";
    }
}