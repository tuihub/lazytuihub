using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using LazyTuiHub.Desktop;
using LazyTuiHub.Games;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed partial class DesktopTests
{
    private static async Task ExerciseGameLibraryAsync(DesktopUiDriver driver, GameLibrary library)
    {
        await driver.ResizeAsync(1280, 800);
        var pages = driver.Window.FindControl<ListBox>("Pages")!;
        await driver.ClickAsync(
            pages.ContainerFromItem(
                pages.Items.OfType<LazyTuiHub.Desktop.NavigationPage>().Single(page => page.Title == "Games")
            )!
        );
        foreach (var design in new[] { DesignSystem.Fluent, DesignSystem.MacOS, DesignSystem.Material })
        {
            driver.Window = ((ShellWindow)driver.Window).ChangeAppearance(new(design, ColorMode.Light));
            await driver.SettleAsync();
            var card = GameCard(driver, "CLANNAD");
            var title = card.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "CLANNAD");
            var colorRole = card.Classes.Contains("chosen") ? "SelectedText" : "PrimaryText";
            Assert.True(Application.Current!.TryGetResource(colorRole, card.ActualThemeVariant, out var expected));
            Assert.Equal(
                Assert.IsAssignableFrom<ISolidColorBrush>(expected).Color,
                Assert.IsAssignableFrom<ISolidColorBrush>(title.Foreground).Color
            );
            var poster = card.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "Poster");
            Assert.InRange(poster.Bounds.Height / poster.Bounds.Width, 1.49, 1.51);
            var top = card.TranslatePoint(default, driver.Window)!.Value.Y;
            Assert.InRange(top, 0, driver.Window.ClientSize.Height - card.Bounds.Height);
            Assert.DoesNotContain(VisibleGame<TextBlock>(driver), label => label.Text == "Installed on devices");
            await driver.CaptureAsync(design + "-games-shelf");
            await CheckShelfLayoutAsync(driver, design);
        }
        var states = GameControl<ListBox>(driver, "States");
        await driver.ClickAsync(states.ContainerFromIndex(3)!);
        Assert.Single(GameControl<ItemsRepeater>(driver, "Shelf").ItemsSource!.Cast<object>());
        Assert.NotNull(GameCard(driver, "Untitled prototype"));
        await driver.ClickAsync(states.ContainerFromIndex(0)!);
        var search = GameControl<TextBox>(driver, "Search");
        await driver.TypeAsync(search, "クラナド");
        Assert.Equal("クラナド", search.Text);
        Assert.DoesNotContain(VisibleGame<TextBlock>(driver), item => item.Name == "SearchHint");
        Assert.Single(GameControl<ItemsRepeater>(driver, "Shelf").ItemsSource!.Cast<object>());
        await driver.ClickAsync(GameControl<Button>(driver, "ViewMode"));
        await driver.ClickAsync(GameCard(driver, "CLANNAD"));
        foreach (var label in new[] { "Playing", "Installed", "Key", "2004-04-28", "A story" })
            Assert.Contains(VisibleGame<TextBlock>(driver), t => t.Text == label);
        await driver.CaptureAsync("games-library");
        await driver.ResizeAsync(390, 740);
        Assert.DoesNotContain(VisibleGame<ItemsRepeater>(driver), r => r.Name == "Rows");
        await driver.CaptureAsync("games-narrow-detail");
        await driver.ClickAsync(GameControl<Button>(driver, "Back"));
        Assert.True(GameCard(driver, "CLANNAD").IsFocused);
        var count = GameControl<TextBlock>(driver, "GameCount");
        var countRight = count.TranslatePoint(new Point(count.Bounds.Width, 0), driver.Window)!.Value.X;
        var actionsLeft = GameControl<Button>(driver, "SourceButton").TranslatePoint(default, driver.Window)!.Value.X;
        Assert.True(actionsLeft - countRight >= 12);
        Assert.Equal("クラナド", GameControl<TextBox>(driver, "Search").Text);
        await driver.KeyAsync(driver.Window, Key.Enter);
        await driver.ResizeAsync(1280, 800);
        await driver.ClickAsync(GameControl<Button>(driver, "Identify"));
        Assert.Contains(VisibleGame<TextBlock>(driver), t => t.Text == "Linked sources");
        await driver.KeyAsync(driver.Window, Key.Escape);
        await ExerciseGameEditsAsync(driver, library);
        await driver.ClickAsync(GameControl<Button>(driver, "CloseDetail"));
        await driver.ClickAsync(GameControl<Button>(driver, "SourceButton"));
        var platform = VisibleGame<Button>(driver).First(b => Equals(b.Content, "Connect"));
        await driver.ClickAsync(platform);
        var secret = GameControl<TextBox>(driver, "Secret");
        Assert.NotEqual(default, secret.PasswordChar);
        await driver.TypeAsync(secret, "unused-test-token");
        await driver.KeyAsync(driver.Window, Key.Escape);
        Assert.True(string.IsNullOrEmpty(secret.Text));
        await driver.CaptureAsync("games-sources");
        await driver.KeyAsync(driver.Window, Key.Escape);
        await driver.ClickAsync(GameControl<Button>(driver, "AddButton"));
        await driver.TypeAsync(GameControl<TextBox>(driver, "NewTitle"), "A new game");
        await driver.ClickAsync(GameControl<Button>(driver, "Create"));
        Assert.Contains(library.Shelf(), g => g.Title == "A new game");
    }

    private static async Task CheckShelfLayoutAsync(DesktopUiDriver driver, DesignSystem design)
    {
        Rect Bounds(Control control) => new(control.TranslatePoint(default, driver.Window)!.Value, control.Bounds.Size);
        var scopes = GameControl<ListBox>(driver, "States");
        Rect[] Tabs() =>
            Enumerable.Range(0, scopes.ItemCount).Select(i => Bounds(scopes.ContainerFromIndex(i)!)).ToArray();
        var tabs = Tabs();
        if (design == DesignSystem.Material)
        {
            var chip = scopes.ContainerFromIndex(1)!;
            var label = chip.GetVisualDescendants()
                .OfType<Avalonia.Controls.Presenters.ContentPresenter>()
                .Single(c => c.Name == "PART_ContentPresenter");
            Assert.InRange(Math.Abs(Bounds(label).Center.X - Bounds(chip).Center.X), 0, 1);
        }
        var filters = GameControl<Button>(driver, "MoreFilters");
        var tools = Bounds(filters);
        await driver.ClickAsync(scopes.ContainerFromIndex(1)!);
        Assert.Equal(tabs, Tabs());
        Assert.Equal(tools, Bounds(filters));
        await driver.ClickAsync(scopes.ContainerFromIndex(0)!);
        var shelf = GameControl<ScrollViewer>(driver, "ShelfScroll");
        var viewport = Bounds(shelf);
        await driver.ClickAsync(filters);
        var popup = Assert.IsType<Flyout>(filters.Flyout);
        Assert.True(popup.IsOpen);
        Assert.Equal(viewport, Bounds(shelf));
        popup.Hide();
        await driver.SettleAsync();
        var search = GameControl<TextBox>(driver, "Search");
        var before = Bounds(search);
        await driver.ClickAsync(GameCard(driver, "CLANNAD"));
        Assert.Equal(before, Bounds(search));
        Assert.Equal(tools, Bounds(filters));
        await driver.ClickAsync(GameControl<Button>(driver, "CloseDetail"));
        Assert.Equal(viewport, Bounds(shelf));
    }

    private static async Task ExerciseGameEditsAsync(DesktopUiDriver driver, GameLibrary library)
    {
        var id = library.Shelf().Single(g => g.Title == "CLANNAD").Id;
        await driver.ClickAsync(GameControl<Button>(driver, "Edit"));
        Assert.Equal("CLANNAD", GameControl<TextBox>(driver, "EditTitle").Text);
        var field = VisibleGame<TextBox>(driver).Single(b => AutomationProperties.GetName(b) == "Released");
        Assert.Equal("2004-04-28", field.Text);
        await driver.TypeAsync(field, "2004");
        await ClickIntoViewAsync(driver, GameControl<Button>(driver, "Save"));
        Assert.Equal("2004", library.Show(id).Overrides["Released"]);
        await driver.ClickAsync(GameControl<Button>(driver, "Edit"));
        var reset = VisibleGame<Button>(driver).Single(b => Equals(b.Content, "Reset to source"));
        await ClickIntoViewAsync(driver, reset);
        await ClickIntoViewAsync(driver, GameControl<Button>(driver, "Save"));
        Assert.False(library.Show(id).Overrides.ContainsKey("Released"));
        var move = VisibleGame<Button>(driver).First(b => Equals(b.Content, "Reassign"));
        await ClickIntoViewAsync(driver, move);
        await driver.TypeAsync(GameControl<TextBox>(driver, "SplitTitle"), "Separate edition");
        await ClickIntoViewAsync(driver, GameControl<Button>(driver, "Split"));
        Assert.Equal(1, library.Shelf().Single(g => g.Title == "Separate edition").Installations);
        Assert.Equal(
            (1, 0),
            (library.Shelf().Single(g => g.Id == id).Accounts, library.Shelf().Single(g => g.Id == id).Installations)
        );
    }

    private static IEnumerable<T> VisibleGame<T>(DesktopUiDriver driver)
        where T : Control => driver.Window.GetVisualDescendants().OfType<T>().Where(c => c.IsEffectivelyVisible);

    private static T GameControl<T>(DesktopUiDriver driver, string name)
        where T : Control => VisibleGame<T>(driver).Single(c => c.Name == name);

    private static Button GameCard(DesktopUiDriver driver, string name) =>
        VisibleGame<Button>(driver)
            .Single(b => b.Classes.Contains("coverCard") && AutomationProperties.GetName(b) == name);

    private static async Task ClickIntoViewAsync(DesktopUiDriver driver, Control control)
    {
        control.BringIntoView();
        await driver.SettleAsync();
        await driver.ClickAsync(control);
    }
}
