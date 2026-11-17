using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UiComparison.UnoPlugin;

public sealed class ReaderToolsPlugin : IPrototypePage<FrameworkElement>
{
    public string Title => "Reader tools";

    public FrameworkElement CreateView()
    {
        var count = 0;
        var result = new TextBlock { Text = "No clips yet.", FontSize = 18 };
        var clip = new Button { Content = "Clip a passage" };
        clip.Click += (_, _) => result.Text = $"Clipped passages: {++count}";
        return new StackPanel
        {
            Margin = new Thickness(36),
            Spacing = 20,
            Children =
            {
                new TextBlock { Text = "Reader tools", FontSize = 30 },
                new TextBlock { Text = "This page and its event handler live in a dynamically loaded DLL." },
                new TextBox
                {
                    PlaceholderText = "A passage worth keeping · 值得保留的一段话",
                    AcceptsReturn = true,
                    MinHeight = 120,
                },
                clip,
                result,
            },
        };
    }
}
