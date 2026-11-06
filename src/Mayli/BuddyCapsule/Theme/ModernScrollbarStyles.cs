using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

namespace WindowsBuddy.BuddyCapsule.Theme
{
    public static class ModernScrollbarStyles
    {
        private static ResourceDictionary _currentDict = null;

        public static void ApplyToApplication(Application app)
        {
            if (app == null) return;
            UpdateStyles(app);
            CapsuleTheme.ThemeChanged += () =>
            {
                app.Dispatcher.InvokeAsync(() => UpdateStyles(app));
            };
        }

        private static void UpdateStyles(Application app)
        {
            try
            {
                bool isLight = CapsuleTheme.Current.IsLight;
                string thumbBase = isLight ? "#38000000" : "#38FFFFFF";
                string thumbHover = isLight ? "#70000000" : "#70FFFFFF";
                string thumbDrag = isLight ? "#A0000000" : "#A0FFFFFF";

                string xaml = $@"
<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <Style x:Key=""ModernScrollBarPageButton"" TargetType=""{{x:Type RepeatButton}}"">
        <Setter Property=""OverridesDefaultStyle"" Value=""true"" />
        <Setter Property=""Background"" Value=""Transparent"" />
        <Setter Property=""Focusable"" Value=""false"" />
        <Setter Property=""IsTabStop"" Value=""false"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{{x:Type RepeatButton}}"">
                    <Rectangle Fill=""{{TemplateBinding Background}}"" Height=""{{TemplateBinding Height}}"" Width=""{{TemplateBinding Width}}"" />
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style x:Key=""ModernScrollBarThumb"" TargetType=""{{x:Type Thumb}}"">
        <Setter Property=""OverridesDefaultStyle"" Value=""true"" />
        <Setter Property=""IsTabStop"" Value=""false"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{{x:Type Thumb}}"">
                    <Border x:Name=""ThumbBorder""
                            Background=""{thumbBase}""
                            CornerRadius=""2.5""
                            Margin=""0"" />
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""true"">
                            <Setter TargetName=""ThumbBorder"" Property=""Background"" Value=""{thumbHover}"" />
                        </Trigger>
                        <Trigger Property=""IsDragging"" Value=""true"">
                            <Setter TargetName=""ThumbBorder"" Property=""Background"" Value=""{thumbDrag}"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType=""{{x:Type ScrollBar}}"">
        <Setter Property=""OverridesDefaultStyle"" Value=""true"" />
        <Setter Property=""SnapsToDevicePixels"" Value=""true"" />
        <Setter Property=""Background"" Value=""Transparent"" />
        <Style.Triggers>
            <Trigger Property=""Orientation"" Value=""Vertical"">
                <Setter Property=""Width"" Value=""5"" />
                <Setter Property=""MinWidth"" Value=""5"" />
                <Setter Property=""MaxWidth"" Value=""5"" />
                <Setter Property=""Template"">
                    <Setter.Value>
                        <ControlTemplate TargetType=""{{x:Type ScrollBar}}"">
                            <Grid Background=""Transparent"" SnapsToDevicePixels=""true"">
                                <Track x:Name=""PART_Track"" IsDirectionReversed=""true"">
                                    <Track.DecreaseRepeatButton>
                                        <RepeatButton Style=""{{StaticResource ModernScrollBarPageButton}}"" Command=""{{x:Static ScrollBar.PageUpCommand}}"" />
                                    </Track.DecreaseRepeatButton>
                                    <Track.Thumb>
                                        <Thumb Style=""{{StaticResource ModernScrollBarThumb}}"" />
                                    </Track.Thumb>
                                    <Track.IncreaseRepeatButton>
                                        <RepeatButton Style=""{{StaticResource ModernScrollBarPageButton}}"" Command=""{{x:Static ScrollBar.PageDownCommand}}"" />
                                    </Track.IncreaseRepeatButton>
                                </Track>
                            </Grid>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Trigger>
            <Trigger Property=""Orientation"" Value=""Horizontal"">
                <Setter Property=""Height"" Value=""5"" />
                <Setter Property=""MinHeight"" Value=""5"" />
                <Setter Property=""MaxHeight"" Value=""5"" />
                <Setter Property=""Template"">
                    <Setter.Value>
                        <ControlTemplate TargetType=""{{x:Type ScrollBar}}"">
                            <Grid Background=""Transparent"" SnapsToDevicePixels=""true"">
                                <Track x:Name=""PART_Track"" IsDirectionReversed=""false"">
                                    <Track.DecreaseRepeatButton>
                                        <RepeatButton Style=""{{StaticResource ModernScrollBarPageButton}}"" Command=""{{x:Static ScrollBar.PageLeftCommand}}"" />
                                    </Track.DecreaseRepeatButton>
                                    <Track.Thumb>
                                        <Thumb Style=""{{StaticResource ModernScrollBarThumb}}"" />
                                    </Track.Thumb>
                                    <Track.IncreaseRepeatButton>
                                        <RepeatButton Style=""{{StaticResource ModernScrollBarPageButton}}"" Command=""{{x:Static ScrollBar.PageRightCommand}}"" />
                                    </Track.IncreaseRepeatButton>
                                </Track>
                            </Grid>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Trigger>
        </Style.Triggers>
    </Style>
</ResourceDictionary>";

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xaml));
                if (XamlReader.Load(stream) is ResourceDictionary dict)
                {
                    if (_currentDict != null)
                    {
                        app.Resources.MergedDictionaries.Remove(_currentDict);
                    }
                    _currentDict = dict;
                    app.Resources.MergedDictionaries.Add(dict);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModernScrollbarStyles] Failed to load: {ex.Message}");
            }
        }
    }
}
