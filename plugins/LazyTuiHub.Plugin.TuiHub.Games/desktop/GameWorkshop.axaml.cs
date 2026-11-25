using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace LazyTuiHub.Games.Desktop;

public sealed partial class GameWorkshop : UserControl
{
    private GamePageModel Model => (GamePageModel)DataContext!;
    private CancellationTokenSource? signIn;

    public GameWorkshop()
    {
        InitializeComponent();
        Unloaded += (_, _) =>
        {
            ClearSecret();
            signIn?.Cancel();
        };
    }

    internal void ClearSecret() => Secret.Text = null;

    internal void FocusInput()
    {
        Scroll.ScrollToHome();
        if (Model.InAdd)
            NewTitle.Focus();
        if (Model.Editing)
            EditTitle.Focus();
        if (Model.Moving)
        {
            TargetSearch.Text = null;
            TargetSearch.Focus();
        }
        if (Model.Identifying)
        {
            Query.Text = Model.Selected?.Entry.Title;
            Query.Focus();
        }
        if (Model.Scanning)
        {
            FolderHelp.Text = Model.Scanner?.Id is "steam"
                ? "Choose a Steam library or its steamapps folder."
                : "Each immediate subfolder is recorded as one installed game.";
            Folder.Focus();
        }
        if (Model.Connecting && Model.Platform is { } platform)
        {
            Secret.Text = null;
            SignInHelp.Text = platform.Id switch
            {
                "vndb" => "Use a VNDB token with listread permission to include private entries.",
                "steam" =>
                    "Verify your identity in Steam’s browser sign-in. Library sync uses the host’s Steam Web API key.",
                _ => $"Use a personal access token from your {LibraryText.Name(platform.Id)} account.",
            };
            Connect.Content = platform.UsesToken ? "Connect account" : "Verify in browser…";
            if (platform.UsesToken)
                Secret.Focus();
            else
                Connect.Focus();
        }
    }

    private void Back(object? s, RoutedEventArgs e) => Model.Back();

    private async void Add(object? s, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(NewTitle.Text))
            await Model.AddAsync();
    }

    private async void AddKey(object? s, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(NewTitle.Text))
        {
            e.Handled = true;
            await Model.AddAsync();
        }
    }

    private static void Browse(Uri uri) =>
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();

    private void GetToken(object? s, RoutedEventArgs e)
    {
        if (Model.Platform is { } p)
            Browse(p.SignInPage);
    }

    private async void BindAccount(object? s, RoutedEventArgs e)
    {
        if (signIn is not null)
            return;
        using var stop = new CancellationTokenSource();
        signIn = stop;
        var value = Secret.Text ?? string.Empty;
        Secret.Text = null;
        try
        {
            await Model.BindAsync(value, Browse, stop.Token);
        }
        finally
        {
            signIn = null;
        }
    }

    private void CancelSignIn(object? s, RoutedEventArgs e) => signIn?.Cancel();

    private async void ChooseFolder(object? s, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            return;
        var selected = await storage.OpenFolderPickerAsync(new() { Title = "Choose a game library" });
        if (selected.Count > 0 && selected[0].TryGetLocalPath() is { } path)
            Folder.Text = path;
    }

    private async void Scan(object? s, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(Folder.Text))
            await Model.ScanAsync();
    }

    private async Task SearchAsync()
    {
        if (Catalog.SelectedItem is GameAccountProvider p && !string.IsNullOrWhiteSpace(Query.Text))
            await Model.SearchAsync(p.Id, Query.Text);
    }

    private async void Search(object? s, RoutedEventArgs e) => await SearchAsync();

    private async void QueryKey(object? s, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await SearchAsync();
        }
    }

    private async void LinkRecord(object? s, RoutedEventArgs e)
    {
        if (s is Button { DataContext: GameMetadata m })
            await Model.LinkAsync(new(m.Provider, m.ExternalId));
    }

    private async void Unlink(object? s, RoutedEventArgs e)
    {
        if (s is Button { DataContext: GameMetadata m })
            await Model.UnlinkAsync(m);
    }

    private async void LinkSuggestion(object? s, RoutedEventArgs e)
    {
        if (s is Button { DataContext: CatalogKey k })
            await Model.LinkAsync(k);
    }

    private void ResetField(object? s, RoutedEventArgs e)
    {
        if (Model.Editing && s is Button { DataContext: GameFieldDraft field })
            field.Reset();
    }

    private void AddCategory(object? s, RoutedEventArgs e)
    {
        if (
            NewCategory.Text?.Trim() is { Length: > 0 } name
            && !Model.DraftCategories.Contains(name, StringComparer.Ordinal)
        )
            Model.DraftCategories.Add(name);
        NewCategory.Text = null;
    }

    private void RemoveCategory(object? s, RoutedEventArgs e)
    {
        if (s is Button { DataContext: string name })
            Model.DraftCategories.Remove(name);
    }

    private async void SaveInfo(object? s, RoutedEventArgs e) => await Model.SaveAsync();

    private void TargetChanged(object? s, TextChangedEventArgs e)
    {
        if (DataContext is GamePageModel { Moving: true })
            Model.FindTargets(TargetSearch.Text ?? string.Empty);
    }

    private async void Move(object? s, RoutedEventArgs e)
    {
        if (Target.SelectedItem is GameShelfItem item)
            await Model.MoveAsync(item.Id);
    }

    private async void SplitEntry(object? s, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(SplitTitle.Text))
            await Model.MoveAsync(null);
    }
}
