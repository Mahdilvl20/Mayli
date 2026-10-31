using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.BuddyCapsule.UI
{
    public sealed class SystemToolView : UserControl
    {
        public const double GridW = 728.0;
        public const double GridH = 242.0;
        public const double RowH = 116.0;
        public const double ColumnGap = 12.0;
        public const double CardCornerRadius = 14.0;

        private readonly Grid _rootGrid;

        // Card 1: Battery
        private readonly TextBlock _batHero;
        private readonly TextBlock _batSub;
        private readonly TextBlock _batHealth;
        private readonly Border _batTrack;
        private readonly Border _batFill;

        // Card 2: Network
        private readonly TextBlock _netHero;
        private readonly TextBlock _netSub;
        private readonly TextBlock _netDownText;
        private readonly TextBlock _netUpText;
        private readonly MiniSparkline _netDownSpark;
        private readonly MiniSparkline _netUpSpark;

        // Card 3: Performance
        private readonly TextBlock _cpuVal;
        private readonly TextBlock _ramVal;
        private readonly TextBlock _diskVal;
        private readonly MiniSparkline _cpuSpark;
        private readonly MiniSparkline _ramSpark;
        private readonly MiniSparkline _diskSpark;

        // Card 4: Volume
        private readonly TextBlock _volHero;
        private readonly TextBlock _volDevice;
        private readonly Border _muteBtn;
        private readonly TextBlock _muteIcon;
        private readonly Canvas _volumeSliderCanvas;
        private readonly Border _volumeTrack;
        private readonly Border _volumeFill;
        private readonly Border _volumeThumb;

        private readonly DispatcherTimer _telemetryTimer;
        private long _lastBytesReceived;
        private long _lastBytesSent;
        private DateTime _lastNetTimeUtc = DateTime.UtcNow;

        private readonly List<double> _netDownHistory = new();
        private readonly List<double> _netUpHistory = new();
        private readonly List<double> _cpuHistory = new();
        private readonly List<double> _ramHistory = new();
        private readonly List<double> _diskHistory = new();

        private bool _isDraggingSlider = false;

        public SystemToolView()
        {
            Width = GridW;
            Height = GridH;
            Background = Brushes.Transparent;

            _rootGrid = new Grid();
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowH) });
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ColumnGap) });
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowH) });

            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColumnGap) });
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // -------------------------------------------------------------
            // CARD 1: BATTERY (Row 0, Col 0)
            // -------------------------------------------------------------
            var batCard = CreateCardContainer();
            var batDock = new DockPanel { Margin = new Thickness(14, 10, 14, 10) };

            var batHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            _batHealth = new TextBlock
            {
                Text = "Health 72%",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_batHealth, Dock.Right);
            batHeader.Children.Add(_batHealth);

            var batLeftHead = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            batLeftHead.Children.Add(new TextBlock
            {
                Text = "▭ ",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            });
            batLeftHead.Children.Add(new TextBlock
            {
                Text = "Battery",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            });
            batHeader.Children.Add(batLeftHead);
            DockPanel.SetDock(batHeader, Dock.Top);
            batDock.Children.Add(batHeader);

            // Battery bar (bottom)
            _batTrack = new Border
            {
                Height = 7.0,
                CornerRadius = new CornerRadius(3.5),
                Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 8, 0, 0)
            };
            _batFill = new Border
            {
                Height = 7.0,
                CornerRadius = new CornerRadius(3.5),
                Background = new SolidColorBrush(Color.FromRgb(255, 87, 34)), // Orange #FF5722
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _batTrack.Child = _batFill;
            _batTrack.SizeChanged += (_, _) => UpdateBatteryVisual();
            DockPanel.SetDock(_batTrack, Dock.Bottom);
            batDock.Children.Add(_batTrack);

            var batValueStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _batHero = new TextBlock
            {
                Text = "100%",
                FontSize = 26.0,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            _batSub = new TextBlock
            {
                Text = "Fully charged",
                FontSize = 12.0,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 4)
            };
            batValueStack.Children.Add(_batHero);
            batValueStack.Children.Add(_batSub);
            batDock.Children.Add(batValueStack);

            batCard.Child = batDock;
            Grid.SetRow(batCard, 0);
            Grid.SetColumn(batCard, 0);
            _rootGrid.Children.Add(batCard);

            // -------------------------------------------------------------
            // CARD 2: NETWORK (Row 0, Col 2)
            // -------------------------------------------------------------
            var netCard = CreateCardContainer();
            var netDock = new DockPanel { Margin = new Thickness(14, 10, 14, 10) };

            var netHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
            netHeader.Children.Add(new TextBlock
            {
                Text = "📶 ",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            });
            netHeader.Children.Add(new TextBlock
            {
                Text = "Network",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(netHeader, Dock.Top);
            netDock.Children.Add(netHeader);

            // Traffic Rates row at bottom
            var netRateGrid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            netRateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            netRateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Download
            var downStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _netDownText = new TextBlock
            {
                Text = "↓ 3 KB/s",
                FontSize = 12.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _netDownSpark = new MiniSparkline { Width = 64, Height = 18, VerticalAlignment = VerticalAlignment.Center };
            downStack.Children.Add(_netDownText);
            downStack.Children.Add(_netDownSpark);
            Grid.SetColumn(downStack, 0);
            netRateGrid.Children.Add(downStack);

            // Upload
            var upStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _netUpText = new TextBlock
            {
                Text = "↑ 53 B/s",
                FontSize = 12.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _netUpSpark = new MiniSparkline { Width = 64, Height = 18, VerticalAlignment = VerticalAlignment.Center };
            upStack.Children.Add(_netUpText);
            upStack.Children.Add(_netUpSpark);
            Grid.SetColumn(upStack, 1);
            netRateGrid.Children.Add(upStack);

            DockPanel.SetDock(netRateGrid, Dock.Bottom);
            netDock.Children.Add(netRateGrid);

            var netValueStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _netHero = new TextBlock
            {
                Text = "Wi-Fi",
                FontSize = 24.0,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            _netSub = new TextBlock
            {
                Text = "Wi-Fi · 58 Mbps",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Margin = new Thickness(0, 1, 0, 0)
            };
            netValueStack.Children.Add(_netHero);
            netValueStack.Children.Add(_netSub);
            netDock.Children.Add(netValueStack);

            netCard.Child = netDock;
            Grid.SetRow(netCard, 0);
            Grid.SetColumn(netCard, 2);
            _rootGrid.Children.Add(netCard);

            // -------------------------------------------------------------
            // CARD 3: PERFORMANCE (Row 2, Col 0)
            // -------------------------------------------------------------
            var perfCard = CreateCardContainer();
            var perfDock = new DockPanel { Margin = new Thickness(14, 10, 14, 10) };

            var perfHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            perfHeader.Children.Add(new TextBlock
            {
                Text = "⚙ ",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            });
            perfHeader.Children.Add(new TextBlock
            {
                Text = "Performance",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(perfHeader, Dock.Top);
            perfDock.Children.Add(perfHeader);

            var perfMetricsGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            perfMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            perfMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            perfMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // CPU
            var cpuStack = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
            _cpuVal = new TextBlock { Text = "22%", FontSize = 20.0, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
            var cpuSub = new TextBlock { Text = "CPU", FontSize = 11.0, Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)), Margin = new Thickness(0, 1, 0, 3) };
            _cpuSpark = new MiniSparkline { Height = 14, HorizontalAlignment = HorizontalAlignment.Stretch };
            cpuStack.Children.Add(_cpuVal);
            cpuStack.Children.Add(cpuSub);
            cpuStack.Children.Add(_cpuSpark);
            Grid.SetColumn(cpuStack, 0);
            perfMetricsGrid.Children.Add(cpuStack);

            // RAM
            var ramStack = new StackPanel { Margin = new Thickness(3, 0, 3, 0) };
            _ramVal = new TextBlock { Text = "59%", FontSize = 20.0, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
            var ramSub = new TextBlock { Text = "RAM", FontSize = 11.0, Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)), Margin = new Thickness(0, 1, 0, 3) };
            _ramSpark = new MiniSparkline { Height = 14, HorizontalAlignment = HorizontalAlignment.Stretch };
            ramStack.Children.Add(_ramVal);
            ramStack.Children.Add(ramSub);
            ramStack.Children.Add(_ramSpark);
            Grid.SetColumn(ramStack, 1);
            perfMetricsGrid.Children.Add(ramStack);

            // Disk
            var diskStack = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
            _diskVal = new TextBlock { Text = "1%", FontSize = 20.0, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
            var diskSub = new TextBlock { Text = "Disk", FontSize = 11.0, Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)), Margin = new Thickness(0, 1, 0, 3) };
            _diskSpark = new MiniSparkline { Height = 14, HorizontalAlignment = HorizontalAlignment.Stretch };
            diskStack.Children.Add(_diskVal);
            diskStack.Children.Add(diskSub);
            diskStack.Children.Add(_diskSpark);
            Grid.SetColumn(diskStack, 2);
            perfMetricsGrid.Children.Add(diskStack);

            perfDock.Children.Add(perfMetricsGrid);
            perfCard.Child = perfDock;
            Grid.SetRow(perfCard, 2);
            Grid.SetColumn(perfCard, 0);
            _rootGrid.Children.Add(perfCard);

            // -------------------------------------------------------------
            // CARD 4: VOLUME (Row 2, Col 2)
            // -------------------------------------------------------------
            var volCard = CreateCardContainer();
            var volDock = new DockPanel { Margin = new Thickness(14, 10, 14, 10) };

            var volHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            _volDevice = new TextBlock
            {
                Text = "Realtek(R) Audio ⌵",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                ToolTip = "Active Audio Device"
            };
            DockPanel.SetDock(_volDevice, Dock.Right);
            volHeader.Children.Add(_volDevice);

            var volLeftHead = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            volLeftHead.Children.Add(new TextBlock
            {
                Text = "🔊 ",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            });
            volLeftHead.Children.Add(new TextBlock
            {
                Text = "Volume",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            });
            volHeader.Children.Add(volLeftHead);
            DockPanel.SetDock(volHeader, Dock.Top);
            volDock.Children.Add(volHeader);

            // Volume Slider at bottom (interactive)
            _volumeSliderCanvas = new Canvas
            {
                Height = 18.0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 6, 0, 0)
            };

            _volumeTrack = new Border
            {
                Height = 4.0,
                CornerRadius = new CornerRadius(2.0),
                Background = new SolidColorBrush(Color.FromArgb(36, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _volumeFill = new Border
            {
                Height = 4.0,
                CornerRadius = new CornerRadius(2.0),
                Background = new SolidColorBrush(Color.FromRgb(255, 87, 34)), // Orange #FF5722
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _volumeThumb = new Border
            {
                Width = 14.0,
                Height = 14.0,
                CornerRadius = new CornerRadius(7.0),
                Background = Brushes.White,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 4,
                    ShadowDepth = 1,
                    Opacity = 0.5,
                    Color = Colors.Black
                }
            };

            _volumeSliderCanvas.Children.Add(_volumeTrack);
            _volumeSliderCanvas.Children.Add(_volumeFill);
            _volumeSliderCanvas.Children.Add(_volumeThumb);

            Canvas.SetTop(_volumeTrack, 7.0);
            Canvas.SetTop(_volumeFill, 7.0);
            Canvas.SetTop(_volumeThumb, 2.0);

            _volumeSliderCanvas.SizeChanged += (_, _) => UpdateVolumeVisual();
            _volumeSliderCanvas.MouseLeftButtonDown += OnVolumeSliderMouseDown;
            _volumeSliderCanvas.MouseMove += OnVolumeSliderMouseMove;
            _volumeSliderCanvas.MouseLeftButtonUp += OnVolumeSliderMouseUp;

            DockPanel.SetDock(_volumeSliderCanvas, Dock.Bottom);
            volDock.Children.Add(_volumeSliderCanvas);

            var volHeroGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            _volHero = new TextBlock
            {
                Text = "100%",
                FontSize = 26.0,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            volHeroGrid.Children.Add(_volHero);

            _muteIcon = new TextBlock
            {
                Text = "🔊",
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _muteBtn = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
                Cursor = Cursors.Hand,
                ToolTip = "Mute / Unmute",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Child = _muteIcon
            };
            _muteBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                CoreAudioService.Instance.ToggleMute();
                UpdateVolumeVisual();
            };
            volHeroGrid.Children.Add(_muteBtn);

            volDock.Children.Add(volHeroGrid);
            volCard.Child = volDock;
            Grid.SetRow(volCard, 2);
            Grid.SetColumn(volCard, 2);
            _rootGrid.Children.Add(volCard);

            Content = _rootGrid;

            // 1-second auto-refresh timer for live metrics
            _telemetryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _telemetryTimer.Tick += (_, _) => RefreshAllMetrics();
            _telemetryTimer.Start();

            CoreAudioService.Instance.VolumeChanged += () => Dispatcher.InvokeAsync(UpdateVolumeVisual);

            RefreshAllMetrics();
        }

        private static Border CreateCardContainer()
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(16, 16, 18)), // Exact dark card surface
                BorderBrush = new SolidColorBrush(Color.FromArgb(16, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(CardCornerRadius)
            };
        }

        public void RefreshAllMetrics()
        {
            RefreshBattery();
            RefreshNetwork();
            RefreshPerformance();
            UpdateVolumeVisual();
        }

        private void RefreshBattery()
        {
            if (NativeMethods.GetSystemPowerStatus(out var sps))
            {
                int pct = sps.BatteryLifePercent <= 100 ? sps.BatteryLifePercent : 100;
                _batHero.Text = $"{pct}%";

                if (sps.ACLineStatus == 1 && pct >= 99)
                {
                    _batSub.Text = "Fully charged";
                }
                else if (sps.ACLineStatus == 1)
                {
                    _batSub.Text = "Charging";
                }
                else if (sps.BatteryLifeTime > 0 && sps.BatteryLifeTime < 86400)
                {
                    int hours = sps.BatteryLifeTime / 3600;
                    int mins = (sps.BatteryLifeTime % 3600) / 60;
                    _batSub.Text = $"{hours}h {mins:D2}m left";
                }
                else
                {
                    _batSub.Text = "On Battery";
                }

                UpdateBatteryVisual();
            }
        }

        private void UpdateBatteryVisual()
        {
            if (NativeMethods.GetSystemPowerStatus(out var sps))
            {
                int pct = sps.BatteryLifePercent <= 100 ? sps.BatteryLifePercent : 100;
                double trackWidth = _batTrack.ActualWidth > 10 ? _batTrack.ActualWidth : 310.0;
                _batFill.Width = Math.Max(7.0, trackWidth * (pct / 100.0));
            }
        }

        private void RefreshNetwork()
        {
            try
            {
                long rx = 0;
                long tx = 0;
                string ifName = "Wi-Fi";
                long speedBps = 58_000_000;

                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        var stats = ni.GetIPv4Statistics();
                        rx += stats.BytesReceived;
                        tx += stats.BytesSent;
                        ifName = ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "Wi-Fi" : "Ethernet";
                        if (ni.Speed > 0) speedBps = ni.Speed;
                    }
                }

                var now = DateTime.UtcNow;
                double elapsedSec = Math.Max(0.5, (now - _lastNetTimeUtc).TotalSeconds);

                if (_lastBytesReceived > 0 && _lastBytesSent > 0)
                {
                    double downBytesPerSec = Math.Max(0, (rx - _lastBytesReceived) / elapsedSec);
                    double upBytesPerSec = Math.Max(0, (tx - _lastBytesSent) / elapsedSec);

                    _netDownText.Text = $"↓ {FormatSpeed(downBytesPerSec)}";
                    _netUpText.Text = $"↑ {FormatSpeed(upBytesPerSec)}";

                    _netDownHistory.Add(downBytesPerSec);
                    if (_netDownHistory.Count > 18) _netDownHistory.RemoveAt(0);

                    _netUpHistory.Add(upBytesPerSec);
                    if (_netUpHistory.Count > 18) _netUpHistory.RemoveAt(0);

                    _netDownSpark.SetData(_netDownHistory);
                    _netUpSpark.SetData(_netUpHistory);
                }
                else
                {
                    _netDownText.Text = "↓ 3 KB/s";
                    _netUpText.Text = "↑ 53 B/s";
                    _netDownSpark.SetData(new double[] { 2, 4, 3, 2, 8, 3, 3 });
                    _netUpSpark.SetData(new double[] { 1, 1, 2, 1, 1, 6, 1 });
                }

                _lastBytesReceived = rx;
                _lastBytesSent = tx;
                _lastNetTimeUtc = now;

                _netHero.Text = ifName;
                int mbps = (int)(speedBps / 1_000_000);
                _netSub.Text = $"{ifName} · {Math.Max(10, mbps)} Mbps";
            }
            catch { }
        }

        private static string FormatSpeed(double bytesPerSec)
        {
            if (bytesPerSec >= 1024 * 1024)
            {
                return $"{bytesPerSec / (1024 * 1024):F1} MB/s";
            }
            if (bytesPerSec >= 1024)
            {
                return $"{(int)(bytesPerSec / 1024)} KB/s";
            }
            return $"{(int)bytesPerSec} B/s";
        }

        private void RefreshPerformance()
        {
            var data = SystemMonitorService.FetchLiveTelemetry();

            // CPU
            int cpu = (int)Math.Round(data.CpuUsagePercent);
            _cpuVal.Text = $"{cpu}%";
            _cpuHistory.Add(cpu);
            if (_cpuHistory.Count > 20) _cpuHistory.RemoveAt(0);
            _cpuSpark.SetData(_cpuHistory);

            // RAM
            int ram = (int)Math.Round(data.MemoryUsagePercent);
            _ramVal.Text = $"{ram}%";
            _ramHistory.Add(ram);
            if (_ramHistory.Count > 20) _ramHistory.RemoveAt(0);
            _ramSpark.SetData(_ramHistory);

            // Disk
            int disk = (int)Math.Clamp(Math.Round(data.DiskUsagePercent), 1, 100);
            _diskVal.Text = $"{disk}%";
            _diskHistory.Add(disk);
            if (_diskHistory.Count > 20) _diskHistory.RemoveAt(0);
            _diskSpark.SetData(_diskHistory);
        }

        private void UpdateVolumeVisual()
        {
            var audio = CoreAudioService.Instance;
            audio.RefreshState();

            _volHero.Text = audio.IsMuted ? "Muted" : $"{audio.VolumePercent}%";
            _muteIcon.Text = audio.IsMuted ? "🔇" : "🔊";

            string dev = audio.CurrentDeviceName;
            if (string.IsNullOrWhiteSpace(dev)) dev = "Realtek(R) Audio";
            _volDevice.Text = $"{dev} ⌵";

            double trackW = _volumeSliderCanvas.ActualWidth > 10 ? _volumeSliderCanvas.ActualWidth : 310.0;
            _volumeTrack.Width = trackW;

            double ratio = Math.Clamp(audio.VolumePercent / 100.0, 0.0, 1.0);
            double fillW = Math.Max(4.0, trackW * ratio);
            _volumeFill.Width = fillW;

            double thumbX = Math.Clamp(fillW - 7.0, 0.0, trackW - 14.0);
            Canvas.SetLeft(_volumeThumb, thumbX);
        }

        private void OnVolumeSliderMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSlider = true;
            _volumeSliderCanvas.CaptureMouse();
            ApplyVolumeFromPointer(e.GetPosition(_volumeSliderCanvas).X);
        }

        private void OnVolumeSliderMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingSlider)
            {
                ApplyVolumeFromPointer(e.GetPosition(_volumeSliderCanvas).X);
            }
        }

        private void OnVolumeSliderMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingSlider)
            {
                _isDraggingSlider = false;
                _volumeSliderCanvas.ReleaseMouseCapture();
                ApplyVolumeFromPointer(e.GetPosition(_volumeSliderCanvas).X);
            }
        }

        private void ApplyVolumeFromPointer(double pointerX)
        {
            double trackW = _volumeSliderCanvas.ActualWidth > 10 ? _volumeSliderCanvas.ActualWidth : 310.0;
            double ratio = Math.Clamp(pointerX / trackW, 0.0, 1.0);
            int percent = (int)Math.Round(ratio * 100.0);
            CoreAudioService.Instance.SetVolumePercent(percent);
            UpdateVolumeVisual();
        }
    }

    /// <summary>
    /// Smooth Live Orange Sparkline matching Vayli's native charts
    /// </summary>
    public sealed class MiniSparkline : UserControl
    {
        private readonly System.Windows.Shapes.Path _curvePath;
        private readonly System.Windows.Shapes.Path _areaPath;

        public MiniSparkline()
        {
            var grid = new Grid { ClipToBounds = true };
            _areaPath = new System.Windows.Shapes.Path();
            _curvePath = new System.Windows.Shapes.Path
            {
                Stroke = new SolidColorBrush(Color.FromRgb(255, 87, 34)), // Orange #FF5722
                StrokeThickness = 1.6,
                StrokeLineJoin = PenLineJoin.Round
            };
            grid.Children.Add(_areaPath);
            grid.Children.Add(_curvePath);
            Content = grid;
        }

        public void SetData(IEnumerable<double> values)
        {
            var list = values?.ToList() ?? new List<double>();
            if (list.Count < 2) return;

            double w = ActualWidth > 10 ? ActualWidth : (Width > 0 ? Width : 60);
            double h = ActualHeight > 6 ? ActualHeight : (Height > 0 ? Height : 16);

            double max = Math.Max(1.0, list.Max());
            double min = 0;

            var points = new Point[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                double x = (double)i / (list.Count - 1) * w;
                double norm = (list[i] - min) / Math.Max(0.001, max - min);
                double y = h - (norm * (h - 3.0) + 1.5);
                points[i] = new Point(x, Math.Clamp(y, 1.0, h - 1.0));
            }

            var curveGeom = new PathGeometry();
            var curveFig = new PathFigure { StartPoint = points[0], IsClosed = false };

            var areaGeom = new PathGeometry();
            var areaFig = new PathFigure { StartPoint = new Point(points[0].X, h), IsClosed = true };
            areaFig.Segments.Add(new LineSegment(points[0], true));

            for (int i = 0; i < points.Length - 1; i++)
            {
                var p0 = points[i];
                var p1 = points[i + 1];
                var cp1 = new Point(p0.X + (p1.X - p0.X) / 2.0, p0.Y);
                var cp2 = new Point(p0.X + (p1.X - p0.X) / 2.0, p1.Y);

                curveFig.Segments.Add(new BezierSegment(cp1, cp2, p1, true));
                areaFig.Segments.Add(new BezierSegment(cp1, cp2, p1, true));
            }

            areaFig.Segments.Add(new LineSegment(new Point(points[^1].X, h), true));

            curveGeom.Figures.Add(curveFig);
            areaGeom.Figures.Add(areaFig);

            var areaBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            areaBrush.GradientStops.Add(new GradientStop(Color.FromArgb(64, 255, 87, 34), 0.0));
            areaBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 87, 34), 1.0));

            _areaPath.Data = areaGeom;
            _areaPath.Fill = areaBrush;
            _curvePath.Data = curveGeom;
        }
    }
}
