using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Labs.Panels;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace UiComparison.AvaloniaApp;

public sealed class ReaderWindow : Window
{
    private const string ReaderPage = "Reader";
    private const string WorkflowPage = "Workflow";
    private readonly ReaderSession session = new();
    private readonly ContentControl page = new();
    private readonly ListBox articles = new();
    private readonly TextBlock title = Text("", 30);
    private readonly TextBlock body = Text("", 16);
    private readonly TextBlock footer = Text("Ready", 12);
    private readonly Button read = new() { Content = "Mark read" };
    private readonly TextBox search = new() { PlaceholderText = "Search · 搜索", Width = 240 };
    private readonly PluginResult<Control> plugin;
    private readonly Control reader;
    private readonly Control? pluginView;
    private readonly WorkflowView workflow = new();
    private Article[] visible = [];
    private Article? selected;

    public ReaderWindow()
    {
        Title = "LazyTuiHub · Avalonia prototype";
        Width = 1180;
        Height = 800;
        plugin = PluginLoader.Load<Control>(
            PrototypeApp.WithoutPlugin,
            "UiComparison.AvaloniaPlugin.ReaderToolsPlugin"
        );
        pluginView = plugin.Page?.CreateView();
        reader = BuildReader();
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        layout.Children.Add(BuildHeader());
        Grid.SetRow(page, 1);
        layout.Children.Add(page);
        var status = new Border { Padding = new Thickness(20, 10), Child = footer };
        Grid.SetRow(status, 2);
        layout.Children.Add(status);
        Content = layout;
        page.Transitions =
        [
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(180) },
        ];
        ShowPage(reader, ReaderPage);
        search.TextChanged += (_, _) =>
        {
            session.Search = search.Text ?? "";
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
        KeyDown += (_, e) =>
        {
            if (
                e.Key == Key.F
                && e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control)
            )
            {
                search.Focus();
                e.Handled = true;
            }
        };
        Loaded += (_, _) =>
            Measurements.Save("avalonia", "loaded", new { plugin.Status, Articles = session.Articles.Count });
        InstallMenu();
    }

    private FlexPanel BuildHeader()
    {
        var row = new FlexPanel
        {
            ColumnSpacing = 10,
            RowSpacing = 10,
            Wrap = FlexWrap.Wrap,
            Margin = new Thickness(20, 16),
        };
        row.Children.Add(Text("L / LazyTuiHub", 22));
        row.Children.Add(Text("AVALONIA", 12));
        row.Children.Add(Action(ReaderPage, () => ShowPage(reader, ReaderPage)));
        row.Children.Add(Action(WorkflowPage, () => ShowPage(workflow, WorkflowPage)));
        row.Children.Add(Action("Plugin", () => ShowPage(pluginView ?? Text(plugin.Status, 16), "Plugin")));
        row.Children.Add(
            Action("Light / dark", () =>
                Application.Current!.RequestedThemeVariant =
                    Application.Current.ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark
            )
        );
        return row;
    }

    private Grid BuildReader()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("200,320,*") };
        var feeds = new ListBox
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
                    Margin = new Thickness(12, 20),
                },
            }
        );
        var listing = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
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
        note.TextChanged += (_, _) => session.Note = note.Text ?? "";
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

    private void ShowPage(Control view, string name)
    {
        page.Opacity = 0;
        page.Content = view;
        Dispatcher.UIThread.Post(() => page.Opacity = 1);
        footer.Text = $"{name} · {plugin.Status}";
    }

    private void InstallMenu()
    {
        var menu = new NativeMenu();
        var view = new NativeMenuItem("View") { Menu = new NativeMenu() };
        var readerItem = new NativeMenuItem(ReaderPage);
        readerItem.Click += (_, _) => ShowPage(reader, ReaderPage);
        var workflowItem = new NativeMenuItem(WorkflowPage);
        workflowItem.Click += (_, _) => ShowPage(workflow, WorkflowPage);
        view.Menu.Add(readerItem);
        view.Menu.Add(workflowItem);
        menu.Add(view);
        NativeMenu.SetMenu(this, menu);
    }

    public void Verify()
    {
        if (
            visible.Length != 600
            || !string.Equals(title.Text, visible[0].Title, StringComparison.Ordinal)
            || Bounds.Width <= 0
        )
            throw new InvalidOperationException("Reader layout/selection failed.");
        var realized = articles.GetRealizedContainers().Count();
        if (realized <= 0 || realized >= session.Articles.Count)
            throw new InvalidOperationException("Viewport list realization failed.");
        search.Text = "workflow";
        Dispatcher.UIThread.RunJobs();
        if (articles.Items.Count == 0 || articles.Items.Count >= 600)
            throw new InvalidOperationException("Search UI failed.");
        if (!PrototypeApp.WithoutPlugin && plugin.Page is null)
            throw new InvalidOperationException(plugin.Status);
        var view = plugin.Page?.CreateView();
        view?.Measure(new Size(800, 600));
        var missing = PluginLoader.Load<Control>(
            false,
            "unused",
            System.IO.Path.Combine(AppContext.BaseDirectory, "absent-plugin.dll")
        );
        if (
            missing.Page is not null
            || !missing.Status.Contains(nameof(FileNotFoundException), StringComparison.Ordinal)
        )
            throw new InvalidOperationException("Missing plugin diagnostics failed.");
        workflow.Verify();
        Measurements.Save(
            "avalonia",
            "verified",
            new
            {
                Matches = visible.Length,
                RealizedBeforeSearch = realized,
                Plugin = plugin.Status,
                PluginView = view?.GetType().FullName,
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
