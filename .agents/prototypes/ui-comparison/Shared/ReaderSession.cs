namespace UiComparison;

public sealed class ReaderSession
{
    public static readonly string[] Feeds = ["All feeds", "Engineering", "Design", "Field notes"];
    public IReadOnlyList<Article> Articles { get; } = CreateArticles();
    public ISet<int> Read { get; } = new HashSet<int>();
    public string Feed { get; set; } = Feeds[0];
    public string Search { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;

    public Article[] Visible() =>
        Articles
            .Where(article =>
                (
                    string.Equals(Feed, Feeds[0], StringComparison.Ordinal)
                    || string.Equals(article.Feed, Feed, StringComparison.Ordinal)
                )
                && (
                    article.Title.Contains(Search, StringComparison.OrdinalIgnoreCase)
                    || article.Body.Contains(Search, StringComparison.OrdinalIgnoreCase)
                )
            )
            .ToArray();

    public bool ToggleRead(int id) => !Read.Remove(id) && Read.Add(id);

    private static Article[] CreateArticles()
    {
        string[] titles =
        [
            "Make room for the next idea",
            "A workflow that explains itself",
            "Small tools, connected",
            "阅读是一种缓慢的发现",
            "Designing for unreliable networks",
            "What a plugin should own",
        ];
        string[] paragraphs =
        [
            "A useful workspace gives each activity enough room. The feed list, article list and reading pane should stay understandable as the window changes size.",
            "The framework plans the work, connects typed ports and explains unavailable capabilities. Business modules supply the behavior. A graph should make these relationships visible.",
            "Streaming values can keep moving while synchronized inputs update a node's configuration. Connections remain explicit; execution details should not obscure the user's intent.",
            "界面需要容纳中文、键盘操作与长篇阅读。请在下方输入框中输入中文，检查候选框位置、组合输入、选区与撤销。示例数据保留英文和中文，便于比较字体与换行。",
            "Devices on the public Internet have different resources and connectivity. Availability and failure explanations matter more than assuming every participant stays online.",
            "A missing plugin makes its features unavailable. The host still starts and keeps its descriptions and data. A plugin page in this prototype comes from a separate managed DLL.",
        ];
        return Enumerable
            .Range(0, 600)
            .Select(index => new Article(
                index,
                Feeds[1 + index % 3],
                $"{titles[index % titles.Length]} · {index + 1:000}",
                string.Join(
                    "\n\n",
                    Enumerable.Range(0, 4).Select(offset => paragraphs[(index + offset) % paragraphs.Length])
                )
            ))
            .ToArray();
    }
}
