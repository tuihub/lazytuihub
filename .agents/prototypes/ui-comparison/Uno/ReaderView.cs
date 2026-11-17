using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Uno.Toolkit.UI;
using Windows.Foundation;

namespace UiComparison.UnoApp;

public sealed class ReaderView : Grid
{
    private readonly ReaderSession session = new();
    private readonly ContentControl page = new();
    private readonly ListView articles = new();
    private readonly TextBlock title = Text("", 30);
    private readonly TextBlock body = Text("", 16);
    private readonly TextBlock footer = Text("Ready", 12);
    private readonly Button read = new() { Content = "Mark read" };
    private readonly TextBox search = new() { PlaceholderText = "Search · 搜索", Width = 240 };
    private readonly PluginResult<FrameworkElement> plugin;
    private readonly FrameworkElement? pluginView;
    private readonly FrameworkElement reader;
    private readonly WorkflowView workflow = new();
    private Article[] visible = [];
    private Article? selected;

    public ReaderView()
    {
        RequestedTheme = ElementTheme.Light;
        Background = new SolidColorBrush(Colors.White);
        plugin = PluginLoader.Load<FrameworkElement>(App.WithoutPlugin, "UiComparison.UnoPlugin.ReaderToolsPlugin");
        pluginView = plugin.Page?.CreateView();
        reader = BuildReader();
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Children.Add(BuildHeader());
        Grid.SetRow(page, 1);
        Children.Add(page);
        var status = new Border { Padding = new Thickness(20, 10, 20, 10), Child = footer };
        Grid.SetRow(status, 2);
        Children.Add(status);
        ShowPage(reader, "Reader");
        search.TextChanged += (_, _) =>
        {
            session.Search = search.Text;
            RefreshArticles();
        };
        read.Click += (_, _) =>
        {
            if (selected is not null)
            {
                session.ToggleRead(selected.Id);
                UpdateRead();
            }
        };
        var accelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = Windows.System.VirtualKey.F,
            Modifiers = OperatingSystem.IsMacOS()
                ? Windows.System.VirtualKeyModifiers.Windows
                : Windows.System.VirtualKeyModifiers.Control,
        };
        accelerator.Invoked += (_, e) =>
        {
            search.Focus(FocusState.Keyboard);
            e.Handled = true;
        };
        KeyboardAccelerators.Add(accelerator);
    }

    private FlexPanel BuildHeader()
    {
        var row = new FlexPanel
        {
            ColumnGap = 10,
            RowGap = 10,
            Wrap = FlexWrap.Wrap,
            Margin = new Thickness(20, 16, 20, 16),
        };
        row.Children.Add(Text("L / LazyTuiHub", 22));
        row.Children.Add(Text("UNO", 12));
        row.Children.Add(Action("Reader", () => ShowPage(reader, "Reader")));
        row.Children.Add(Action("Workflow", () => ShowPage(workflow, "Workflow")));
        row.Children.Add(Action("Plugin", () => ShowPage(pluginView ?? Text(plugin.Status, 16), "Plugin")));
        row.Children.Add(Action("Light / dark", () =>
            {
                RequestedTheme = RequestedTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
                Background = new SolidColorBrush(RequestedTheme == ElementTheme.Dark ? Colors.Black : Colors.White);
            }));
        return row;
    }

    private Grid BuildReader()
    {
        var grid = new Grid();
        foreach (var width in new[] { new GridLength(200), new GridLength(320), new GridLength(1, GridUnitType.Star) })
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        var feeds = new ListView
        {
            ItemsSource = ReaderSession.Feeds,
            SelectedIndex = 0,
            Margin = new Thickness(8),
        };
        feeds.SelectionChanged += (_, _) =>
        {
            session.Feed = (string?)feeds.SelectedItem ?? ReaderSession.Feeds[0];
            RefreshArticles();
        };
        grid.Children.Add(
            new Border
            {
                Child = new StackPanel
                {
                    Spacing = 16,
                    Children = { Text("LIBRARY", 12), feeds },
                    Margin = new Thickness(12, 20, 12, 20),
                },
            }
        );
        var listing = new Grid();
        listing.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        listing.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        search.Margin = new Thickness(12);
        listing.Children.Add(search);
        articles.Margin = new Thickness(6);
        articles.SelectionChanged += (_, _) => SelectArticle(articles.SelectedIndex);
        Grid.SetRow(articles, 1);
        listing.Children.Add(articles);
        Grid.SetColumn(listing, 1);
        grid.Children.Add(listing);
        body.TextWrapping = TextWrapping.Wrap;
        title.TextWrapping = TextWrapping.Wrap;
        var note = new TextBox
        {
            PlaceholderText = "Reading notes · 在这里测试中文输入",
            AcceptsReturn = true,
            MinHeight = 100,
        };
        note.TextChanged += (_, _) => session.Note = note.Text;
        var reading = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(30),
                Spacing = 22,
                Children = { Text("TODAY / YOUR READING SPACE", 12), title, read, body, Text("NOTES", 12), note },
            },
        };
        Grid.SetColumn(reading, 2);
        grid.Children.Add(reading);
        RefreshArticles();
        return grid;
    }

    private void RefreshArticles()
    {
        visible = session.Visible();
        articles.ItemsSource = visible.Select(article => article.Caption).ToArray();
        articles.SelectedIndex = visible.Length > 0 ? 0 : -1;
        SelectArticle(articles.SelectedIndex);
        footer.Text = $"{visible.Length} articles · {plugin.Status}";
    }

    private void SelectArticle(int index)
    {
        selected = index >= 0 && index < visible.Length ? visible[index] : null;
        title.Text = selected?.Title ?? "No matching articles";
        body.Text = selected?.Body ?? "Try another feed or search.";
        UpdateRead();
    }

    private void UpdateRead()
    {
        read.IsEnabled = selected is not null;
        read.Content = selected is not null && session.Read.Contains(selected.Id) ? "Mark unread" : "Mark read";
    }

    private void ShowPage(FrameworkElement view, string name)
    {
        page.Content = view;
        var storyboard = new Storyboard();
        var fade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(fade, page);
        Storyboard.SetTargetProperty(fade, "Opacity");
        storyboard.Children.Add(fade);
        storyboard.Begin();
        footer.Text = $"{name} · {plugin.Status}";
    }

    public async Task VerifyAsync()
    {
        await Task.Yield();
        UpdateLayout();
        if (
            visible.Length != 600
            || !string.Equals(title.Text, visible[0].Title, StringComparison.Ordinal)
            || ActualWidth <= 0
        )
            throw new InvalidOperationException("Reader layout/selection failed.");
        var realized = Enumerable
            .Range(0, visible.Length)
            .Count(index => articles.ContainerFromIndex(index) is not null);
        if (realized <= 0 || realized >= session.Articles.Count)
            throw new InvalidOperationException("Viewport list realization failed.");
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object sender, TextChangedEventArgs args) => changed.TrySetResult();
        search.TextChanged += OnChanged;
        try
        {
            search.Text = "workflow";
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            search.TextChanged -= OnChanged;
        }
        if (articles.Items.Count == 0 || articles.Items.Count >= 600)
            throw new InvalidOperationException("Search UI failed.");
        if (!App.WithoutPlugin && plugin.Page is null)
            throw new InvalidOperationException(plugin.Status);
        pluginView?.Measure(new Size(800, 600));
        var missing = PluginLoader.Load<FrameworkElement>(
            false,
            "unused",
            System.IO.Path.Combine(AppContext.BaseDirectory, "absent-plugin.dll")
        );
        if (
            missing.Page is not null
            || !missing.Status.Contains(nameof(FileNotFoundException), StringComparison.Ordinal)
        )
            throw new InvalidOperationException("Missing plugin diagnostics failed.");
        if (
            App.Cupertino
            && !ReferenceEquals(
                Application.Current.Resources[typeof(Button)],
                Application.Current.Resources["CupertinoButtonStyle"]
            )
        )
            throw new InvalidOperationException("Cupertino implicit button style was not installed.");
        workflow.Verify();
        Measurements.Save(
            "uno",
            "verified",
            new
            {
                Matches = visible.Length,
                RealizedBeforeSearch = realized,
                ListWidth = articles.ActualWidth,
                ListHeight = articles.ActualHeight,
                Plugin = plugin.Status,
                PluginView = pluginView?.GetType().FullName,
                Workflow = "5 nodes, 4 edges; wrap/grow checked",
            }
        );
    }

    internal static TextBlock Text(string value, double size) =>
        new()
        {
            Text = value,
            FontSize = size,
            VerticalAlignment = VerticalAlignment.Center,
        };

    internal static Button Action(string label, Action action)
    {
        var button = new Button { Content = label };
        button.Click += (_, _) => action();
        return button;
    }
}
