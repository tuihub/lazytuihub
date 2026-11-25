using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LazyTuiHub.Client;
using LazyTuiHub.Framework;
using LazyTuiHub.Interaction;

namespace LazyTuiHub.Desktop;

public sealed partial class OperationsPage : UserControl
{
    internal HashSet<string> ExecutedGroups { get; } = new(StringComparer.Ordinal);
    private readonly CapabilityCatalog catalog;
    private readonly DesktopSession session;
    private readonly Dictionary<CapabilityParameter, TextBox> inputs = [];
    private CancellationTokenSource? running;

    public OperationsPage(CapabilityCatalog catalog, DesktopSession session)
    {
        this.catalog = catalog;
        this.session = session;
        InitializeComponent();
        Find();
        DetachedFromVisualTree += (_, _) => running?.Cancel();
    }

    private void SearchChanged(object? sender, TextChangedEventArgs e) => Find();

    private void Find()
    {
        var query = Search.Text ?? string.Empty;
        try
        {
            Choices.ItemsSource = catalog
                .Groups.SelectMany(g => g.Actions)
                .Where(a =>
                    a.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)
                    || a.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                )
                .ToArray();
        }
        catch (AccessRequiredException error)
        {
            Choices.ItemsSource = null;
            Status.Text = error.Message;
            return;
        }
        Choices.SelectedIndex = Choices.ItemCount > 0 ? 0 : -1;
    }

    private void SelectOperation(object? sender, SelectionChangedEventArgs e)
    {
        Fields.Children.Clear();
        inputs.Clear();
        Result.Items.Clear();
        Result.IsVisible = false;
        Status.Text = null;
        var operation = Choices.SelectedItem as Capability;
        Execute.IsEnabled = operation is not null;
        Description.Text = operation?.Description;
        if (operation is null)
            return;
        foreach (var field in operation.Parameters.Where(p => p.IsInput))
        {
            var label = field.Name.Replace('-', ' ');
            var box = new TextBox { PasswordChar = field.IsSecret ? '●' : default };
            AutomationProperties.SetName(box, label);
            Fields.Children.Add(
                new TextBlock { Text = field.Optional ? label + " (optional)" : label, Classes = { "caption" } }
            );
            Fields.Children.Add(box);
            inputs.Add(field, box);
        }
    }

    private async void RunClick(object? sender, RoutedEventArgs e)
    {
        if (Choices.SelectedItem is not Capability operation || running is not null)
            return;
        var values = inputs
            .Where(p => !string.IsNullOrEmpty(p.Value.Text))
            .ToDictionary(p => p.Key.Name, p => p.Value.Text!, StringComparer.Ordinal);
        foreach (var field in inputs.Where(p => p.Key.IsSecret))
            field.Value.Clear();
        await session.RunAsync(async shutdown =>
        {
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
            running = cancel;
            Search.IsEnabled = Choices.IsEnabled = Fields.IsEnabled = Execute.IsEnabled = false;
            Stop.IsVisible = true;
            Result.Items.Clear();
            Result.IsVisible = false;
            Status.Text = "Running…";
            void Append(string line)
            {
                Result.Items.Add(line);
                Result.IsVisible = true;
            }
            try
            {
                var output = new InteractionOutput(
                    item => Dispatcher.UIThread.Invoke(() => Append(OperationFormatting.Json(item))),
                    notice => Dispatcher.UIThread.Invoke(() => Append(notice))
                );
                var result = await Task.Run(
                    () =>
                        operation.InvokeAsync(
                            values.GetValueOrDefault,
                            key => values.GetValueOrDefault(key) ?? string.Empty,
                            output,
                            cancel.Token
                        ),
                    cancel.Token
                );
                ExecutedGroups.Add(operation.Group.Name);
                if (result is IssuedSession issued)
                    Append($"Signed in to {issued.Account}. Session expires {issued.Expires.ToLocalTime():g}.");
                else if (result is not null)
                    Append(OperationFormatting.Json(result is UnsuccessfulResult failed ? failed.Detail : result));
                Status.Text = result is UnsuccessfulResult ? "Operation unsuccessful." : "Completed.";
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
                Status.Text = "Cancelled.";
            }
            catch (Exception error)
            {
                Status.Text = error.GetBaseException().Message;
            }
            finally
            {
                values.Clear();
                running = null;
                Search.IsEnabled = Choices.IsEnabled = Fields.IsEnabled = Execute.IsEnabled = true;
                Stop.IsVisible = false;
            }
        });
    }

    private void StopClick(object? sender, RoutedEventArgs e) => running?.Cancel();
}
