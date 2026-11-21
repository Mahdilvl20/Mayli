using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Tools;
using WindowsBuddy.UI.AnimateIcons;
using WindowsBuddy.UI.Settings;

namespace WindowsBuddy.BuddyCapsule.Core
{
    public sealed partial class CapsuleWindow : Window
    {
        private static void LogNotif(string msg)
        {
            try
            {
                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mayli", "logs");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "notifications.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [CapsuleWindow] {msg}\r\n");
            }
            catch { }
        }

        private void OnWindowsNotificationReceived(WindowsNotificationPayload winNotif)
        {
            LogNotif($"OnWindowsNotificationReceived: App='{winNotif.AppName}', Title='{winNotif.Title}', Msg='{winNotif.Message}', HasIcon={winNotif.AppIcon != null}");
            Dispatcher.InvokeAsync(() =>
            {
                var payload = new NotchNotificationPayload
                {
                    AppName = winNotif.AppName,
                    Title = winNotif.Title,
                    Subtitle = winNotif.Message,
                    AppIcon = winNotif.AppIcon,
                    Icon = winNotif.AppGlyph,
                    DurationMs = 5000 // 5 seconds display on the notch
                };
                OnRealNotificationRequested(payload);
            });
        }

        private void OnRealNotificationRequested(NotchNotificationPayload payload)
        {
            LogNotif($"OnRealNotificationRequested: App='{payload.AppName}', Title='{payload.Title}', Icon='{payload.Icon}'");
            Dispatcher.InvokeAsync(() =>
            {
                // Live Activity filter checks
                if ((payload.Icon == "⚡" || payload.Icon == "🔋") && !AppPreferences.Instance.ShowBatteryActivity)
                    return;

                if (payload.Icon == "🔊" && !AppPreferences.Instance.VolumeActivity)
                    return;

                if ((payload.Icon == "📋" || payload.Icon == "📁" || payload.Icon == "✂️") && !AppPreferences.Instance.ClipboardCopiedActivity)
                    return;

                if ((payload.Icon == "🌐" || payload.Icon == "⌨️") && !AppPreferences.Instance.KeyboardLanguageActivity)
                    return;

                if (payload.Icon == "⏱" && !AppPreferences.Instance.TimerFinishAlerts && !AppPreferences.Instance.RunningTimersActivity)
                    return;

                if (payload.Icon == "📅" && !AppPreferences.Instance.CalendarAlerts)
                    return;

                _isBannerActive = true;
                string titleText = (payload.Title ?? "").Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();
                string subText = (payload.Subtitle ?? "").Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
                string appText = (payload.AppName ?? "").Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();

                var bannerPalette = CapsuleTheme.Current;
                _bannerTitle.Foreground = CapsuleTheme.Brush(bannerPalette.Text);
                _bannerSubtitle.Foreground = CapsuleTheme.Brush(bannerPalette.Text2);
                _bannerAppName.Foreground = CapsuleTheme.Brush(bannerPalette.Text2);
                _bannerIcon.Foreground = CapsuleTheme.Brush(bannerPalette.Text);
                _bannerAppIconBorder.Background = CapsuleTheme.Brush(bannerPalette.Surface2);

                _bannerTitle.Text = titleText;
                _bannerSubtitle.Text = subText;
                _bannerSubtitle.Visibility = string.IsNullOrWhiteSpace(subText) ? Visibility.Collapsed : Visibility.Visible;
                _bannerSubtitle.Margin = (!string.IsNullOrWhiteSpace(titleText) && !string.IsNullOrWhiteSpace(subText) && !char.IsWhiteSpace(subText[0]) && subText[0] != '·')
                    ? new Thickness(5, 0, 0, 0)
                    : new Thickness(0);

                if (!string.IsNullOrWhiteSpace(appText) && !string.Equals(appText, titleText, StringComparison.OrdinalIgnoreCase))
                {
                    _bannerAppName.Text = appText + " · ";
                    _bannerAppName.Visibility = Visibility.Visible;
                }
                else
                {
                    _bannerAppName.Text = "";
                    _bannerAppName.Visibility = Visibility.Collapsed;
                }

                // Ensure collapsed content root is fully visible and opaque
                _collapsedContent.Visibility = Visibility.Visible;
                _collapsedContent.Opacity = 1.0;

                // Display banner view before icon play so elements are in the visual presentation tree
                _collapsedMinimalView.Visibility = Visibility.Collapsed;
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedTimerView.Visibility = Visibility.Collapsed;
                _collapsedHudView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Collapsed;
                _collapsedMessageView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Visible;
                AnimateBannerEntrance();

                if (payload.AppIcon != null)
                {
                    _bannerAppImage.Source = payload.AppIcon;
                    _bannerAppIconBorder.Visibility = Visibility.Visible;
                    _bannerAnimateIcon.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                }
                else if (payload.Title == "Cut" || payload.Icon == "✂️")
                {
                    _bannerAppIconBorder.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.StrokeBrush = CapsuleTheme.Brush(CapsuleTheme.Current.Text);
                    _bannerAnimateIcon.IconType = AnimateIconType.CutScissors;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerAnimateIcon.Play(forceRestart: true);
                }
                else if (payload.Title == "Copied" || payload.Icon == "📋" || payload.Icon == "📎" || payload.Icon == "📁")
                {
                    _bannerAppIconBorder.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.StrokeBrush = CapsuleTheme.Brush(CapsuleTheme.Current.Text);
                    _bannerAnimateIcon.IconType = (payload.Title == "Cut") ? AnimateIconType.CutScissors : AnimateIconType.CopyCheck;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerAnimateIcon.Play(forceRestart: true);
                }
                else if (payload.Icon == "🎧" || (payload.Title != null && payload.Title.Contains("Connected", StringComparison.OrdinalIgnoreCase)))
                {
                    _bannerAppIconBorder.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.StrokeBrush = CapsuleTheme.Brush(Color.FromRgb(10, 132, 255));
                    _bannerAnimateIcon.IconType = AnimateIconType.Bluetooth;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerAnimateIcon.Play(forceRestart: true);
                }
                else if (payload.Icon == "🔊" || payload.Icon == "🔈")
                {
                    _bannerAppIconBorder.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.StrokeBrush = CapsuleTheme.Brush(CapsuleTheme.Current.Text);
                    _bannerAnimateIcon.IconType = AnimateIconType.Volume;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerAnimateIcon.Play(forceRestart: true);
                }
                else if (payload.Icon == "🌐" || payload.Icon == "⌨️")
                {
                    _bannerAppIconBorder.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.StrokeBrush = CapsuleTheme.Brush(CapsuleTheme.Current.Text);
                    _bannerAnimateIcon.IconType = AnimateIconType.Globe;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerAnimateIcon.Play(forceRestart: true);
                }
                else if (payload.Icon == "⏱" || payload.Icon == "⏰")
                {
                    _bannerAppIconBorder.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.StrokeBrush = CapsuleTheme.Brush(CapsuleTheme.Current.Text);
                    _bannerAnimateIcon.IconType = AnimateIconType.Timer;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerAnimateIcon.Play(forceRestart: true);
                }
                else
                {
                    _bannerAppIconBorder.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Visible;
                    _bannerIcon.Foreground = CapsuleTheme.Brush(CapsuleTheme.Current.Text);
                    _bannerIcon.Text = !string.IsNullOrEmpty(payload.Icon) ? payload.Icon : "🔔";
                }

                // PRIORITY HANDLING: Notification (5s) > Timer > Music
                // 1. Music Satellite Island: if music notch is currently active, album art detaches smoothly to the left!
                bool isMusicPlaying = (_isMusicActive || _isMediaRetained) && AppPreferences.Instance.ShowClosedNowPlaying;
                if (isMusicPlaying)
                {
                    UpdateMusicSatelliteArtwork();
                    _musicSatelliteTarget = 1.0;
                }
                else
                {
                    _musicSatelliteTarget = 0.0;
                }

                // 2. Timer Priority: if timer is active, timer digits detach to the right island so timer is never lost!
                var timerSnap = WindowsBuddy.Tools.Timer.TimerService.Instance.GetActiveNotchTimer();
                bool isTimerActive = timerSnap.IsActive && AppPreferences.Instance.RunningTimersActivity;
                if (isTimerActive)
                {
                    UpdateTimerNotchVisuals(timerSnap);
                    _splitIslandsTarget = 1.0;
                }
                else
                {
                    _splitIslandsTarget = 0.0;
                }

                // Size ALL notification/Bluetooth/copy/cut banners dynamically to match whichever notch is active right now
                ApplyBannerDimensionsAndLayout(isMusicPlaying, isTimerActive, timerSnap);

                _edgeLight.TriggerBurst(4.2);
                EvaluateDynamicVisibility();

                if (_morphProgress <= 0.1)
                {
                    _targetProgress = 0.0;
                    _lastMorphTickTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                    _morphTimer.Start();
                }

                int durationMs = payload.DurationMs > 0 ? payload.DurationMs : 5000;
                _bannerDismissTimer.Interval = TimeSpan.FromMilliseconds(durationMs);
                _bannerDismissTimer.Stop();
                _bannerDismissTimer.Start();
            });
        }

        private void UpdateMusicSatelliteArtwork()
        {
            if (_currentArtworkSource != null)
            {
                _musicSatelliteArtworkImage.Source = _currentArtworkSource;
                _musicSatelliteArtworkImage.Visibility = Visibility.Visible;
                _musicSatelliteGlyph.Visibility = Visibility.Collapsed;
            }
            else
            {
                _musicSatelliteArtworkImage.Source = null;
                _musicSatelliteArtworkImage.Visibility = Visibility.Collapsed;
                _musicSatelliteGlyph.Visibility = Visibility.Visible;
            }

            var pSat = CapsuleTheme.Current;
            Color beatColor = _currentSongBeatColor.A > 0 ? _currentSongBeatColor : Color.FromRgb(255, 60, 90);
            if (pSat.IsLight)
            {
                double lum = (0.299 * beatColor.R + 0.587 * beatColor.G + 0.114 * beatColor.B) / 255.0;
                double maxC = Math.Max(beatColor.R, Math.Max(beatColor.G, beatColor.B));
                double minC = Math.Min(beatColor.R, Math.Min(beatColor.G, beatColor.B));
                double sat = maxC > 0 ? (maxC - minC) / maxC : 0;
                if (lum > 0.72 || sat < 0.22) beatColor = pSat.Accent;
            }
            _musicSatelliteBeatRing.Stroke = new SolidColorBrush(Color.FromArgb(160, beatColor.R, beatColor.G, beatColor.B));
            _musicSatelliteEqualizer?.SetBeatColor(beatColor);
        }

        private void OnRealMediaSessionChanged(MediaSessionSnapshot snapshot)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (!AppPreferences.Instance.ShowMediaActivity || (snapshot.IsPlaying && !string.IsNullOrWhiteSpace(snapshot.SourceApp) && AppPreferences.Instance.IsAudioAppExcluded(snapshot.SourceApp)))
                {
                    _isMusicActive = false;
                    _isMediaRetained = false;
                    _mediaSettleExitTimer.Stop();
                    _spectrumTimer.Stop();
                    SyncAudioSpectrumServiceState();
                    _miniEqualizer.IsPlaying = false;
                    if (_musicSatelliteEqualizer != null) _musicSatelliteEqualizer.IsPlaying = false;
                    _isMediaWideActive = false;
                    _mediaWideDismissTimer.Stop();
                    UpdateEdgeLightEffectiveColor();
                    UpdateCollapsedViewState();
                    return;
                }

                bool wasPlaying = _isMusicActive;
                _isMusicActive = snapshot.IsPlaying;
                if (snapshot.BeatColor.A > 0)
                {
                    _currentSongBeatColor = snapshot.BeatColor;
                }
                UpdateEdgeLightEffectiveColor();

                if (_isMusicActive)
                {
                    _isMediaRetained = true;
                    _mediaSettleExitTimer.Stop();
                    SyncAudioSpectrumServiceState();
                    _spectrumTimer.Start();
                }
                else
                {
                    if (wasPlaying && _isMediaRetained)
                    {
                        // Music paused/stopped: smoothly decay equalizer and keep media card alive for 3.5s before morphing
                        SyncAudioSpectrumServiceState();
                        _spectrumTimer.Start();
                        _mediaSettleExitTimer.Stop();
                        _mediaSettleExitTimer.Interval = TimeSpan.FromSeconds(3.5);
                        _mediaSettleExitTimer.Start();
                    }
                }
                string track = !string.IsNullOrWhiteSpace(snapshot.Title)
                    ? snapshot.Title
                    : (!string.IsNullOrWhiteSpace(_lastObservedTrack) ? _lastObservedTrack : "");
                string artist = !string.IsNullOrWhiteSpace(snapshot.Artist)
                    ? snapshot.Artist
                    : (!string.IsNullOrWhiteSpace(_lastObservedArtist) ? _lastObservedArtist : "");

                // Determine beat color based on user's Music color setting
                Color beatColor = snapshot.BeatColor;
                bool isCustomBeatColor = !string.Equals(AppPreferences.Instance.MusicBeatColor, "Artwork", StringComparison.OrdinalIgnoreCase);
                if (isCustomBeatColor)
                {
                    try
                    {
                        beatColor = (Color)ColorConverter.ConvertFromString(AppPreferences.Instance.MusicBeatColor);
                    }
                    catch { isCustomBeatColor = false; }
                }

                // Compact media view (312 x 50)
                var mediaPalette = CapsuleTheme.Current;
                _mediaTrackRun.Foreground = CapsuleTheme.Brush(mediaPalette.Text);
                _mediaSepRun.Foreground = CapsuleTheme.Brush(mediaPalette.Text3);
                _mediaArtistRun.Foreground = CapsuleTheme.Brush(mediaPalette.Text2);
                _wideMediaTrackText.Foreground = CapsuleTheme.Brush(mediaPalette.Text);
                _wideRemainingText.Foreground = CapsuleTheme.Brush(mediaPalette.Text2);
                _wideMediaArtistText.Foreground = CapsuleTheme.Brush(mediaPalette.Text3);

                _mediaTrackRun.Text = track;
                bool hasArtist = !string.IsNullOrWhiteSpace(artist);
                _mediaSepRun.Text = hasArtist ? " – " : string.Empty;
                _mediaArtistRun.Text = hasArtist ? artist : string.Empty;

                _miniEqualizer.IsPlaying = snapshot.IsPlaying;
                _miniEqualizer.SetBeatColor(beatColor);
                if (_musicSatelliteEqualizer != null)
                {
                    _musicSatelliteEqualizer.IsPlaying = snapshot.IsPlaying;
                    _musicSatelliteEqualizer.SetBeatColor(beatColor);
                }
                _waveformProgress.SetBeatColor(beatColor, isCustomBeatColor);
                _currentArtworkSource = snapshot.Artwork;
                _albumArtThumb.SetArtwork(snapshot.Artwork, artist);

                // Wide media card view (392 x 84)
                _wideMediaTrackText.Text = track;
                _wideMediaArtistText.Text = hasArtist ? artist : "Playing Now";
                _wideWaveformProgress.SetBeatColor(beatColor, isCustomBeatColor);
                _wideAlbumArtThumb.SetArtwork(snapshot.Artwork, artist);
                UpdateWideScrubberTheme(beatColor, isCustomBeatColor);

                if (_musicSatelliteTarget > 0.005)
                {
                    UpdateMusicSatelliteArtwork();
                }

                // Timeline sync for live song time and progress scrubber
                if (snapshot.DurationSeconds > 1.0)
                {
                    _mediaDurSeconds = snapshot.DurationSeconds;
                    _mediaPosSeconds = Math.Clamp(snapshot.PositionSeconds, 0.0, _mediaDurSeconds);
                    _lastMediaTimelineSyncUtc = DateTime.UtcNow;
                }
                else
                {
                    _mediaDurSeconds = 0.0;
                    _mediaPosSeconds = Math.Max(0.0, snapshot.PositionSeconds);
                    _lastMediaTimelineSyncUtc = DateTime.UtcNow;
                }
                UpdateMediaTimelineVisuals();

                bool trackChanged = !string.Equals(track, _lastObservedTrack, StringComparison.Ordinal) ||
                                    !string.Equals(artist, _lastObservedArtist, StringComparison.Ordinal);
                _lastObservedTrack = track;
                _lastObservedArtist = artist;

                if (!_isMusicActive)
                {
                    bool wasWide = _isMediaWideActive;
                    _isMediaWideActive = false;
                    _mediaWideDismissTimer.Stop();
                    if ((wasWide || !_isMediaRetained) && !_isBannerActive)
                    {
                        UpdateCollapsedViewState(animateCompactEntrance: wasWide && _isMediaRetained);
                    }
                }
                else
                {
                    // Whenever a song starts playing or track switches, expand the notch to show track & progress bar, then settle
                    bool shouldExpandWide = (!wasPlaying && _isMusicActive) || (snapshot.IsTrackSwitch && trackChanged);
                    if (shouldExpandWide)
                    {
                        ActivateMediaWideCard();
                        return;
                    }

                    if (!_isBannerActive)
                    {
                        UpdateCollapsedViewState();
                    }
                }
            });
        }

        private void ActivateMediaWideCard()
        {
            if (!_isMusicActive && !_isMediaRetained) return;
            _isMediaWideActive = true;
            _edgeLight.TriggerBurst(4.5);
            ResetMediaWideTimer();
            UpdateMediaTimelineVisuals();

            if (!_isBannerActive)
            {
                UpdateCollapsedViewState();
                AnimateViewEntrance(_collapsedMediaWideView, 0.0);
            }
        }

        private void ResetMediaWideTimer()
        {
            _mediaWideDismissTimer.Stop();
            int durationSec = AppPreferences.Instance.LiveActivityEventDuration switch
            {
                "2 s" => 2,
                "5 s" => 5,
                _ => (int)MediaWideDurationSeconds
            };
            _mediaWideDismissTimer.Interval = TimeSpan.FromSeconds(durationSec);
            _mediaWideDismissTimer.Start();
        }

        private void UpdateMediaTimelineVisuals()
        {
            double elapsedSinceSync = _isMusicActive
                ? Math.Max(0.0, (DateTime.UtcNow - _lastMediaTimelineSyncUtc).TotalSeconds)
                : 0.0;

            double currentPos = Math.Max(0.0, _mediaPosSeconds + elapsedSinceSync);
            string remStr;
            double ratio;

            if (_mediaDurSeconds > 1.0)
            {
                currentPos = Math.Clamp(currentPos, 0.0, _mediaDurSeconds);
                double remaining = Math.Max(0.0, _mediaDurSeconds - currentPos);
                remStr = FormatRemaining(remaining);
                ratio = Math.Clamp(currentPos / _mediaDurSeconds, 0.0, 1.0);
            }
            else
            {
                remStr = FormatRemaining(currentPos);
                ratio = 0.0;
            }

            if (!string.Equals(_wideRemainingText.Text, remStr, StringComparison.Ordinal))
            {
                _wideRemainingText.Text = remStr;
            }

            double trackWidth = _wideProgressTrack.ActualWidth > 10.0 ? _wideProgressTrack.ActualWidth : 200.0;
            double fillW = _mediaDurSeconds > 1.0 ? Math.Clamp(trackWidth * ratio, 3.0, trackWidth) : 0.0;
            _wideProgressFill.Width = fillW;

            double thumbX = Math.Clamp(fillW - 3.5, 0.0, Math.Max(0.0, trackWidth - 7.0));
            _wideProgressThumbTranslate.X = thumbX;
        }

        private void UpdateWideScrubberTheme(Color beatColor, bool isCustom)
        {
            bool isLight = CapsuleTheme.Current.IsLight;
            Color cStart, cMid, cEnd;
            if (isLight)
            {
                cStart = Color.FromRgb(
                    (byte)Math.Clamp(beatColor.R * 0.85, 0, 255),
                    (byte)Math.Clamp(beatColor.G * 0.85, 0, 255),
                    (byte)Math.Clamp(beatColor.B * 0.85, 0, 255));
                cMid = beatColor;
                cEnd = Color.FromRgb(
                    (byte)Math.Clamp(beatColor.R * 0.65, 0, 255),
                    (byte)Math.Clamp(beatColor.G * 0.65, 0, 255),
                    (byte)Math.Clamp(beatColor.B * 0.65, 0, 255));
            }
            else if (isCustom)
            {
                cStart = Color.FromRgb(
                    (byte)Math.Clamp(beatColor.R * 0.72 + 255 * 0.28, 0, 255),
                    (byte)Math.Clamp(beatColor.G * 0.72 + 255 * 0.28, 0, 255),
                    (byte)Math.Clamp(beatColor.B * 0.72 + 255 * 0.28, 0, 255));
                cMid = beatColor;
                cEnd = Color.FromRgb(
                    (byte)Math.Clamp(beatColor.R * 0.50 + 255 * 0.50, 0, 255),
                    (byte)Math.Clamp(beatColor.G * 0.55 + 255 * 0.45, 0, 255),
                    (byte)Math.Clamp(beatColor.B * 0.60 + 255 * 0.40, 0, 255));
            }
            else
            {
                cStart = Color.FromRgb(
                    (byte)Math.Clamp(214 * 0.68 + beatColor.R * 0.32, 0, 255),
                    (byte)Math.Clamp(146 * 0.68 + beatColor.G * 0.32, 0, 255),
                    (byte)Math.Clamp(255 * 0.72 + beatColor.B * 0.28, 0, 255));
                cMid = Color.FromRgb(
                    (byte)Math.Clamp(156 * 0.68 + beatColor.R * 0.32, 0, 255),
                    (byte)Math.Clamp(132 * 0.68 + beatColor.G * 0.32, 0, 255),
                    (byte)Math.Clamp(255 * 0.75 + beatColor.B * 0.25, 0, 255));
                cEnd = Color.FromRgb(
                    (byte)Math.Clamp(136 * 0.68 + beatColor.R * 0.32, 0, 255),
                    (byte)Math.Clamp(198 * 0.68 + beatColor.G * 0.32, 0, 255),
                    (byte)Math.Clamp(255 * 0.78 + beatColor.B * 0.22, 0, 255));
            }

            var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            grad.GradientStops.Add(new GradientStop(cStart, 0.0));
            grad.GradientStops.Add(new GradientStop(cMid, 0.52));
            grad.GradientStops.Add(new GradientStop(cEnd, 1.0));
            grad.Freeze();
            _wideProgressFill.Background = grad;
        }

        private static string FormatRemaining(double remainingSeconds)
        {
            int totalSec = Math.Max(0, (int)Math.Round(remainingSeconds));
            int mins = totalSec / 60;
            int secs = totalSec % 60;
            return $"{mins}:{secs:D2}";
        }

        private static void AnimateViewEntrance(FrameworkElement view, double fromY = 0.0)
        {
            view.Opacity = 0.0;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var fadeAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = ease
            };

            if (view.RenderTransform is TranslateTransform tt && Math.Abs(fromY) > 0.01)
            {
                double safeY = Math.Clamp(fromY, -1.5, 1.5);
                var slideAnim = new DoubleAnimation(safeY, 0.0, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = ease
                };
                tt.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            }
            view.BeginAnimation(OpacityProperty, fadeAnim);
        }

        private void AnimateBannerEntrance()
        {
            _bannerSequenceId++;
            _bannerTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            _bannerScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            _bannerScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            _collapsedBannerView.BeginAnimation(OpacityProperty, null);

            var cubicEase = new CubicEase { EasingMode = EasingMode.EaseOut };
            var backEase = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 };

            var slideAnim = new DoubleAnimation(-8.0, 0.0, TimeSpan.FromMilliseconds(260)) { EasingFunction = cubicEase };
            var scaleAnim = new DoubleAnimation(0.92, 1.0, TimeSpan.FromMilliseconds(280)) { EasingFunction = backEase };
            var fadeAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = cubicEase };

            _bannerTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _bannerScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            _bannerScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            _collapsedBannerView.BeginAnimation(OpacityProperty, fadeAnim);
        }

        private void AnimateBannerExit()
        {
            if (!_isBannerActive) return;

            int currentSeq = _bannerSequenceId;
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

            var slideAnim = new DoubleAnimation(0.0, -7.0, TimeSpan.FromMilliseconds(190)) { EasingFunction = ease };
            var scaleAnim = new DoubleAnimation(1.0, 0.93, TimeSpan.FromMilliseconds(190)) { EasingFunction = ease };
            var fadeAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(170)) { EasingFunction = ease };

            fadeAnim.Completed += (_, _) =>
            {
                if (_bannerSequenceId == currentSeq && _isBannerActive)
                {
                    _isBannerActive = false;
                    _musicSatelliteTarget = 0.0;
                    if (_isMessagePinned) _isMessageActive = true;

                    _bannerTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                    _bannerScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    _bannerScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    _collapsedBannerView.BeginAnimation(OpacityProperty, null);
                    _bannerTranslate.Y = 0;
                    _bannerScale.ScaleX = 1.0;
                    _bannerScale.ScaleY = 1.0;
                    _collapsedBannerView.Opacity = 1.0;

                    UpdateCollapsedViewState();

                    if (_collapsedNormalView.Visibility == Visibility.Visible)
                    {
                        AnimateViewEntrance(_collapsedNormalView, 5.0);
                    }
                    else if (_collapsedMediaView.Visibility == Visibility.Visible)
                    {
                        AnimateViewEntrance(_collapsedMediaView, 5.0);
                    }
                    else if (_collapsedHudView.Visibility == Visibility.Visible)
                    {
                        AnimateViewEntrance(_collapsedHudView, 5.0);
                        _collapsedHudIcon?.Play(forceRestart: true);
                    }
                    else if (_collapsedTimerView.Visibility == Visibility.Visible)
                    {
                        AnimateViewEntrance(_collapsedTimerView, 5.0);
                    }

                    _morphTimer.Start();
                }
            };

            _bannerTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _bannerScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            _bannerScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            _collapsedBannerView.BeginAnimation(OpacityProperty, fadeAnim);
        }

        private Color GetEffectiveTimerColor()
        {
            bool syncWithMusic = AppPreferences.Instance.EdgeLightSyncWithMusic ||
                                 string.Equals(AppPreferences.Instance.EdgeLightColor, "Artwork", StringComparison.OrdinalIgnoreCase);

            if (syncWithMusic && (_isMusicActive || _isMediaRetained || RealtimeNotchActionService.Instance.IsMusicPlaying))
            {
                if (!string.Equals(AppPreferences.Instance.MusicBeatColor, "Artwork", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        return (Color)ColorConverter.ConvertFromString(AppPreferences.Instance.MusicBeatColor);
                    }
                    catch { }
                }

                Color songColor = _currentSongBeatColor;
                if (songColor.A == 0 || (songColor.R == 48 && songColor.G == 209 && songColor.B == 88 && RealtimeNotchActionService.Instance.CurrentBeatColor.A > 0))
                {
                    songColor = RealtimeNotchActionService.Instance.CurrentBeatColor;
                }
                if (songColor.A > 0)
                {
                    double lum = (0.299 * songColor.R + 0.587 * songColor.G + 0.114 * songColor.B) / 255.0;
                    double maxC = Math.Max(songColor.R, Math.Max(songColor.G, songColor.B));
                    double minC = Math.Min(songColor.R, Math.Min(songColor.G, songColor.B));
                    double sat = maxC > 0 ? (maxC - minC) / maxC : 0;
                    if (lum > 0.80 || sat < 0.15)
                    {
                        return Color.FromRgb(255, 60, 90);
                    }
                    return songColor;
                }
            }

            return CapsuleTheme.Current.Accent;
        }

        private void OnTimerServiceUpdated()
        {
            var snap = WindowsBuddy.Tools.Timer.TimerService.Instance.GetActiveNotchTimer();
            bool isActive = snap.IsActive && AppPreferences.Instance.RunningTimersActivity;

            UpdateTimerNotchVisuals(snap);

            if (isActive != _lastTimerActiveState)
            {
                _lastTimerActiveState = isActive;
                UpdateCollapsedViewState();
            }
            else if (isActive && _collapsedTimerView.Visibility == Visibility.Visible)
            {
                // Adjust width if title appeared/disappeared while timer is active
                GetConfiguredRestingDimensions(out double rw, out double rh);
                double desiredW = !string.IsNullOrWhiteSpace(snap.Title) ? Math.Max(rw, 248.0) : rw;
                if (Math.Abs(_currentBaseCollapsedWidth - desiredW) > 1.0)
                {
                    _currentBaseCollapsedWidth = desiredW;
                    _currentBaseCollapsedHeight = rh;
                    if (_morphProgress <= 0.1)
                    {
                        _lastMorphTickTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                        _morphTimer.Start();
                    }
                }
            }
        }

        private void UpdateTimerNotchVisuals(WindowsBuddy.Tools.Timer.NotchTimerSnapshot snap)
        {
            if (!snap.IsActive)
            {
                // Completely reset progress bars when timer stops or finishes so no stale arc remains
                _collapsedTimerLeftRingArc.Data = null;
                _collapsedTimerLeftRingArc.Visibility = Visibility.Collapsed;
                _leftIslandRingArc.Data = null;
                _leftIslandRingArc.Visibility = Visibility.Collapsed;
                return;
            }

            Color effectiveColor = GetEffectiveTimerColor();
            if (_cachedTimerAccentBrush == null || _cachedTimerAccentColor != effectiveColor)
            {
                _cachedTimerAccentColor = effectiveColor;
                _cachedTimerAccentBrush = new SolidColorBrush(effectiveColor);
                _cachedTimerAccentBrush.Freeze();

                _collapsedTimerClockIcon.Stroke = _cachedTimerAccentBrush;
                _collapsedTimerLeftRingArc.Stroke = _cachedTimerAccentBrush;
                _collapsedTimerDigitsText.Foreground = _cachedTimerAccentBrush;
                _leftIslandRingArc.Stroke = _cachedTimerAccentBrush;
                _leftIslandClockIcon.Stroke = _cachedTimerAccentBrush;
                _rightIslandTimeText.Foreground = _cachedTimerAccentBrush;
            }

            string displayDigits = snap.IsRunning ? snap.DisplayTime : $"❚❚ {snap.DisplayTime}";
            double ratio = snap.IsRunning ? snap.RemainingRatio : 0.0;

            // 1. Update Standalone Notch Timer View (when NO music is playing)
            DrawTimerRingArc(_collapsedTimerLeftRingArc, new Point(11, 11), 9.5, ratio);

            string wantTitle = !string.IsNullOrWhiteSpace(snap.Title) ? snap.Title : "";
            if (_collapsedTimerTitleText.Text != wantTitle)
            {
                _collapsedTimerTitleText.Text = wantTitle;
                _collapsedTimerTitleText.Visibility = string.IsNullOrEmpty(wantTitle) ? Visibility.Collapsed : Visibility.Visible;
            }

            if (_collapsedTimerDigitsText.Text != displayDigits)
            {
                _collapsedTimerDigitsText.Text = displayDigits;
            }

            // 2. Update Detached Split Dynamic Island Capsules (when Music IS playing)
            DrawTimerRingArc(_leftIslandRingArc, new Point(18, 18), 16.2, ratio);

            if (_rightIslandTimeText.Text != displayDigits)
            {
                _rightIslandTimeText.Text = displayDigits;
                double stableWidth = displayDigits.Length <= 5 ? 66.0 : (displayDigits.Length <= 7 ? 76.0 : 88.0);
                if (Math.Abs(_rightTimerIsland.Width - stableWidth) > 0.5)
                {
                    _rightTimerIsland.Width = stableWidth;
                }
            }
        }

        private static void DrawTimerRingArc(Path targetPath, Point center, double radius, double ratio)
        {
            ratio = Math.Clamp(ratio, 0.0, 1.0);
            if (ratio <= 0.002)
            {
                targetPath.Data = null;
                targetPath.Visibility = Visibility.Collapsed;
                return;
            }

            if (targetPath.Visibility != Visibility.Visible)
            {
                targetPath.Visibility = Visibility.Visible;
            }

            double angle = Math.Min(ratio * 359.99, 359.92);
            double startAngle = -90.0;
            double endAngle = startAngle + angle;

            double startRad = startAngle * Math.PI / 180.0;
            double endRad = endAngle * Math.PI / 180.0;

            var startPt = new Point(center.X + radius * Math.Cos(startRad), center.Y + radius * Math.Sin(startRad));
            var endPt = new Point(center.X + radius * Math.Cos(endRad), center.Y + radius * Math.Sin(endRad));
            bool isLargeArc = angle > 180.0;

            if (targetPath.Data is PathGeometry existingPg &&
                existingPg.Figures.Count == 1 &&
                existingPg.Figures[0].Segments.Count == 1 &&
                existingPg.Figures[0].Segments[0] is ArcSegment existingArc)
            {
                existingPg.Figures[0].StartPoint = startPt;
                existingArc.Point = endPt;
                existingArc.Size = new Size(radius, radius);
                existingArc.IsLargeArc = isLargeArc;
                return;
            }

            var fig = new PathFigure
            {
                StartPoint = startPt,
                IsClosed = false
            };
            fig.Segments.Add(new ArcSegment(
                endPt,
                new Size(radius, radius),
                0,
                isLargeArc,
                SweepDirection.Clockwise,
                true));

            targetPath.Data = new PathGeometry(new[] { fig });
        }

        private void OnSystemVolumeChanged(int percent, bool isMuted)
        {
            if (!AppPreferences.Instance.VolumeActivity) return;
            TriggerHudNotification(NotchHudKind.Volume, percent, isMuted);
        }

        private void OnScreenBrightnessChanged(int percent)
        {
            if (!AppPreferences.Instance.BrightnessActivity) return;
            TriggerHudNotification(NotchHudKind.Brightness, percent, false);
        }

        private void TriggerHudNotification(NotchHudKind kind, int percent, bool isMuted)
        {
            if (_isTucked || _morphProgress > 0.15 || _machine.IsOpenOrOpening)
                return;

            bool wasActiveSameKind = _isHudActive && _activeHudKind == kind;
            bool prevMuted = _hudIsMuted;

            _isHudActive = true;
            _activeHudKind = kind;
            _hudIsMuted = (kind == NotchHudKind.Volume) && isMuted;
            _hudTargetRatio = _hudIsMuted ? 0.0 : Math.Clamp(percent / 100.0, 0.0, 1.0);

            if (!wasActiveSameKind)
            {
                _hudDisplayedRatio = _hudTargetRatio;
            }

            UpdateHudStylingAndIcons();
            UpdateHudProgressGeometry();

            bool showMedia = (_isMusicActive || _isMediaRetained) && AppPreferences.Instance.ShowClosedNowPlaying;
            UpdateCollapsedViewState();

            if (!_isBannerActive && !_isMessageActive)
            {
                if (showMedia)
                {
                    _rightHudIcon?.Play(forceRestart: !wasActiveSameKind || prevMuted != _hudIsMuted);
                }
                else
                {
                    if (!wasActiveSameKind)
                    {
                        AnimateViewEntrance(_collapsedHudView, -3.0);
                    }
                    _collapsedHudIcon?.Play(forceRestart: !wasActiveSameKind || prevMuted != _hudIsMuted);
                }
            }

            _edgeLight.TriggerBurst(3.2);

            if (_morphProgress <= 0.1 && !_machine.IsOpenOrOpening)
            {
                _targetProgress = 0.0;
                _lastMorphTickTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                _morphTimer.Start();
            }

            int durationMs = AppPreferences.Instance.LiveActivityEventDuration switch
            {
                "2 s" => 1800,
                "5 s" => 3500,
                _ => 2200
            };
            _hudDismissTimer.Interval = TimeSpan.FromMilliseconds(durationMs);
            _hudDismissTimer.Stop();
            _hudDismissTimer.Start();
        }

        public void SetCapsLockStateForTest(bool isOn, bool expandPill = true, bool immediate = false, bool rightHudOccupied = false, bool rightTimerOccupied = false)
        {
            if (rightHudOccupied)
            {
                _rightHudTarget = 1.0;
                _rightHudProgress = 1.0;
            }
            else
            {
                _rightHudTarget = 0.0;
                _rightHudProgress = 0.0;
            }

            if (rightTimerOccupied)
            {
                _splitIslandsTarget = 1.0;
                _splitIslandsProgress = 1.0;
            }
            else
            {
                _splitIslandsTarget = 0.0;
                _splitIslandsProgress = 0.0;
            }

            OnCapsLockStateChanged(isOn, expandPill, ignorePreference: true);
            if (immediate)
            {
                _capsLockTarget = _isCapsLockOn ? 1.0 : 0.0;
                _capsLockProgress = _capsLockTarget;
                _capsLockPillTarget = (isOn && expandPill) ? 1.0 : 0.0;
                _capsLockPillProgress = _capsLockPillTarget;
                if (!expandPill) _capsLockShrinkTimer?.Stop();
                UpdateMorphVisuals(_morphProgress);
            }
        }

        private void SyncCapsLockState()
        {
            bool enabled = AppPreferences.Instance.CapsLockActivity;
            bool liveCaps = enabled && RealtimeNotchActionService.ReadCapsLockState();
            if (liveCaps != _isCapsLockOn)
            {
                OnCapsLockStateChanged(liveCaps);
            }
        }

        private void ExpandCapsLockPillTemporarily()
        {
            _capsLockPillTarget = 1.0;
            _capsLockShrinkTimer.Stop();
            _capsLockShrinkTimer.Start();
            _lastMorphTickTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            _morphTimer.Start();
        }

        private void OnCapsLockStateChanged(bool isOn, bool startExpandedPill = true, bool ignorePreference = false)
        {
            if (!ignorePreference && !AppPreferences.Instance.CapsLockActivity)
            {
                isOn = false;
            }
            bool stateChanged = _isCapsLockOn != isOn;
            _isCapsLockOn = isOn;

            _capsLockTarget = isOn ? 1.0 : 0.0;

            if (isOn)
            {
                if (stateChanged || startExpandedPill)
                {
                    if (startExpandedPill)
                    {
                        _capsLockPillTarget = 1.0;
                        if (stateChanged)
                        {
                            _capsLockPillProgress = 1.0;
                        }
                        _capsLockShrinkTimer.Stop();
                        _capsLockShrinkTimer.Start();
                    }
                    else
                    {
                        _capsLockShrinkTimer.Stop();
                        _capsLockPillTarget = 0.0;
                        _capsLockPillProgress = 0.0;
                    }
                }
            }
            else
            {
                _capsLockShrinkTimer.Stop();
                _capsLockPillTarget = 0.0;
            }

            _lastMorphTickTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            _morphTimer.Start();
        }

        private void DismissHudNotification()
        {
            if (!_isHudActive) return;
            bool wasCenterHud = _collapsedHudView.Visibility == Visibility.Visible;

            _isHudActive = false;
            _rightHudTarget = 0.0;
            UpdateCollapsedViewState();

            if (wasCenterHud)
            {
                if (_collapsedTimerView.Visibility == Visibility.Visible)
                {
                    AnimateViewEntrance(_collapsedTimerView, 4.0);
                }
                else if (_collapsedNormalView.Visibility == Visibility.Visible)
                {
                    AnimateViewEntrance(_collapsedNormalView, 4.0);
                }
                else if (_collapsedMinimalView.Visibility == Visibility.Visible)
                {
                    AnimateViewEntrance(_collapsedMinimalView, 4.0);
                }
            }

            if (_morphProgress <= 0.1 && !_machine.IsOpenOrOpening)
            {
                _lastMorphTickTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                _morphTimer.Start();
            }
        }

        private void UpdateHudStylingAndIcons()
        {
            var p = CapsuleTheme.Current;
            Color iconColor;
            Color fillColor;
            Color trackColor = p.IsLight
                ? Color.FromArgb(40, 0, 0, 0)
                : Color.FromArgb(42, 255, 255, 255);
            Color ringTrackColor = p.IsLight
                ? Color.FromArgb(34, 0, 0, 0)
                : Color.FromArgb(36, 255, 255, 255);

            AnimateIconType desiredIconType;
            if (_activeHudKind == NotchHudKind.Brightness)
            {
                desiredIconType = AnimateIconType.Brightness;
                iconColor = p.IsLight ? Color.FromRgb(218, 138, 0) : Color.FromRgb(255, 214, 10);
                fillColor = p.IsLight ? Color.FromRgb(230, 149, 0) : Color.FromRgb(255, 214, 10);
            }
            else
            {
                desiredIconType = (_hudIsMuted || _hudTargetRatio <= 0.002)
                    ? AnimateIconType.VolumeMute
                    : AnimateIconType.Volume;

                if (_hudIsMuted)
                {
                    iconColor = Color.FromRgb(255, 69, 58);
                    fillColor = Color.FromRgb(255, 69, 58);
                }
                else
                {
                    iconColor = p.IsLight ? Color.FromRgb(28, 28, 34) : Color.FromRgb(250, 250, 254);
                    fillColor = p.IsLight ? Color.FromRgb(28, 28, 34) : Color.FromRgb(250, 250, 254);
                }
            }

            var iconBrush = CapsuleTheme.Brush(iconColor);
            var fillBrush = CapsuleTheme.Brush(fillColor);
            var trackBrush = CapsuleTheme.Brush(trackColor);
            var ringTrackBrush = CapsuleTheme.Brush(ringTrackColor);

            if (_collapsedHudIcon != null)
            {
                if (_collapsedHudIcon.IconType != desiredIconType)
                    _collapsedHudIcon.IconType = desiredIconType;
                _collapsedHudIcon.StrokeBrush = iconBrush;
            }
            if (_rightHudIcon != null)
            {
                if (_rightHudIcon.IconType != desiredIconType)
                    _rightHudIcon.IconType = desiredIconType;
                _rightHudIcon.StrokeBrush = iconBrush;
            }

            if (_collapsedHudBarTrack != null) _collapsedHudBarTrack.Background = trackBrush;
            if (_collapsedHudBarFill != null) _collapsedHudBarFill.Background = fillBrush;
            if (_rightHudRingTrack != null) _rightHudRingTrack.Stroke = ringTrackBrush;
            if (_rightHudRingArc != null) _rightHudRingArc.Stroke = fillBrush;
        }

        private void UpdateHudProgressGeometry()
        {
            _lastRenderedHudRatio = _hudDisplayedRatio;
            double ratio = Math.Clamp(_hudDisplayedRatio, 0.0, 1.0);

            if (_rightHudRingArc != null)
            {
                DrawTimerRingArc(_rightHudRingArc, new Point(18, 18), 16.2, ratio);
            }

            if (_collapsedHudBarHost != null && _collapsedHudBarFill != null)
            {
                double trackW = _collapsedHudBarHost.ActualWidth;
                if (trackW > 1.0)
                {
                    double fillW = ratio <= 0.002 ? 0.0 : Math.Clamp(trackW * ratio, 3.5, trackW);
                    _collapsedHudBarFill.Width = fillW;
                    _collapsedHudBarFill.Visibility = fillW <= 0.1 ? Visibility.Collapsed : Visibility.Visible;
                }
            }
        }

    }
}
