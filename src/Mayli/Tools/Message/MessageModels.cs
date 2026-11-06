using System;
using System.Text.Json.Serialization;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Message
{
    public enum MessageAnimationMode
    {
        Slide,
        TypeOn,
        Fade,
        Scroll,
        Blur,
        Pulse
    }

    public enum MessageFinish
    {
        None,   // "Solid"
        Silver, // Metallic grey
        Ocean,  // Cyan / deep blue
        Aurora  // Purple / pink / cyan
    }

    public enum MessageDurationMode
    {
        Sec3,
        Sec5,
        Sec10,
        Custom
    }

    public sealed class MessageSettings
    {
        public string Text { get; set; } = "";
        public MessageAnimationMode Animation { get; set; } = MessageAnimationMode.Slide;
        public double Speed { get; set; } = 1.0; // 0.5 (Slow), 1.0 (Normal), 1.5 (Fast)
        public string ColorHex { get; set; } = "#FFFFFF";
        public string CustomColorHex { get; set; } = "#FFFFFF";
        public MessageFinish Finish { get; set; } = MessageFinish.None;
        public MessageDurationMode Duration { get; set; } = MessageDurationMode.Sec5;
        public int CustomDurationSeconds { get; set; } = 15;
        public bool RepeatEnabled { get; set; } = false;

        [JsonIgnore]
        public TimeSpan DurationSpan => Duration switch
        {
            MessageDurationMode.Sec3 => TimeSpan.FromSeconds(3),
            MessageDurationMode.Sec5 => TimeSpan.FromSeconds(5),
            MessageDurationMode.Sec10 => TimeSpan.FromSeconds(10),
            MessageDurationMode.Custom => TimeSpan.FromSeconds(Math.Clamp(CustomDurationSeconds, 1, 300)),
            _ => TimeSpan.FromSeconds(5)
        };

        [JsonIgnore]
        public string DurationLabel => RepeatEnabled
            ? LocalizationService.T("message.pinned", "Pinned")
            : Duration switch
            {
                MessageDurationMode.Sec3 => string.Format(LocalizationService.T("message.shows_fmt", "Shows for {0}s"), 3),
                MessageDurationMode.Sec5 => string.Format(LocalizationService.T("message.shows_fmt", "Shows for {0}s"), 5),
                MessageDurationMode.Sec10 => string.Format(LocalizationService.T("message.shows_fmt", "Shows for {0}s"), 10),
                MessageDurationMode.Custom => string.Format(LocalizationService.T("message.shows_fmt", "Shows for {0}s"), Math.Clamp(CustomDurationSeconds, 1, 300)),
                _ => string.Format(LocalizationService.T("message.shows_fmt", "Shows for {0}s"), 5)
            };

        public MessageSettings Clone()
        {
            return new MessageSettings
            {
                Text = Text,
                Animation = Animation,
                Speed = Speed,
                ColorHex = ColorHex,
                CustomColorHex = CustomColorHex,
                Finish = Finish,
                Duration = Duration,
                CustomDurationSeconds = CustomDurationSeconds,
                RepeatEnabled = RepeatEnabled
            };
        }
    }
}
