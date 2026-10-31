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
        private static bool _applied = false;

        public static void ApplyToApplication(Application app)
        {
            if (_applied || app == null) return;

            try
            {
                const string xaml = @"
<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <Style x:Key=""ModernScrollBarThumb"" TargetType=""{x:Type Thumb}"">
        <Setter Property=""OverridesDefaultStyle"" Value=""true"" />
        <Setter Property=""IsTabStop"" Value=""false"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type Thumb}"">
                    <Border x:Name=""ThumbBorder""
                            Background=""#38FFFFFF""
                            CornerRadius=""2.5""
                            Margin=""0"" />
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""true"">
                            <Setter TargetName=""ThumbBorder"" Property=""Background"" Value=""#70FFFFFF"" />
                        </Trigger>
                        <Trigger Property=""IsDragging"" Value=""true"">
                            <Setter TargetName=""ThumbBorder"" Property=""Background"" Value=""#A0FFFFFF"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType=""{x:Type ScrollBar}"">
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
                        <ControlTemplate TargetType=""{x:Type ScrollBar}"">
                            <Grid Background=""Transparent"" SnapsToDevicePixels=""true"">
                                <Track x:Name=""PART_Track"" IsDirectionReversed=""true"" EnableWindowlessSupport=""true"">
                                    <Track.Thumb>
                                        <Thumb Style=""{StaticResource ModernScrollBarThumb}"" />
                                    </Track.Thumb>
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
                        <ControlTemplate TargetType=""{x:Type ScrollBar}"">
                            <Grid Background=""Transparent"" SnapsToDevicePixels=""true"">
                                <Track x:Name=""PART_Track"" IsDirectionReversed=""false"" EnableWindowlessSupport=""true"">
                                    <Track.Thumb>
                                        <Thumb Style=""{StaticResource ModernScrollBarThumb}"" />
                                    </Track.Thumb>
                                </Track>
                            </Grid>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Trigger>
        </Style.Triggers>
    </Style>

    <Style TargetType=""{x:Type ScrollViewer}"">
        <Setter Property=""OverridesDefaultStyle"" Value=""true"" />
        <Setter Property=""PanningMode"" Value=""Both"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type ScrollViewer}"">
                    <Grid Background=""{TemplateBinding Background}"">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width=""*"" />
                            <ColumnDefinition Width=""Auto"" />
                        </Grid.ColumnDefinitions>
                        <Grid.RowDefinitions>
                            <RowDefinition Height=""*"" />
                            <RowDefinition Height=""Auto"" />
                        </Grid.RowDefinitions>

                        <ScrollContentPresenter Grid.Column=""0"" Grid.Row=""0""
                                                Margin=""{TemplateBinding Padding}""
                                                Content=""{TemplateBinding Content}""
                                                ContentTemplate=""{TemplateBinding ContentTemplate}""
                                                CanContentScroll=""{TemplateBinding CanContentScroll}"" />

                        <ScrollBar x:Name=""PART_VerticalScrollBar""
                                   Grid.Column=""1"" Grid.Row=""0""
                                   Orientation=""Vertical""
                                   ViewportSize=""{TemplateBinding ViewportHeight}""
                                   Maximum=""{TemplateBinding ScrollableHeight}""
                                   Minimum=""0""
                                   Value=""{Binding VerticalOffset, Mode=OneWay, RelativeSource={RelativeSource TemplatedParent}}""
                                   Visibility=""{TemplateBinding ComputedVerticalScrollBarVisibility}""
                                   Margin=""2,0,1,0"" />

                        <ScrollBar x:Name=""PART_HorizontalScrollBar""
                                   Grid.Column=""0"" Grid.Row=""1""
                                   Orientation=""Horizontal""
                                   ViewportSize=""{TemplateBinding ViewportWidth}""
                                   Maximum=""{TemplateBinding ScrollableWidth}""
                                   Minimum=""0""
                                   Value=""{Binding HorizontalOffset, Mode=OneWay, RelativeSource={RelativeSource TemplatedParent}}""
                                   Visibility=""{TemplateBinding ComputedHorizontalScrollBarVisibility}""
                                   Margin=""0,2,0,1"" />
                    </Grid>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</ResourceDictionary>";

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xaml));
                if (XamlReader.Load(stream) is ResourceDictionary dict)
                {
                    app.Resources.MergedDictionaries.Add(dict);
                    _applied = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModernScrollbarStyles] Failed to load: {ex.Message}");
            }
        }
    }
}
