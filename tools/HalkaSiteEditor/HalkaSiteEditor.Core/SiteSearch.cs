namespace HalkaSiteEditor.Core;

/// <summary>探した結果の1件。</summary>
/// <param name="Tab">どのタブにあるか。</param>
/// <param name="Where">そのタブの中での名前。</param>
/// <param name="Excerpt">見つかったあたりの文字。</param>
/// <param name="Target">タブの中でさらに選ぶもの（ページ・分類・ゲームなど）。</param>
public sealed record SearchHit(string Tab, string Where, string Excerpt, object? Target);

/// <summary>
/// 直せるところを、タブをまたいで探します。
/// タブが増えて「あの文言どこだっけ」になったときのためのものです。何も書き換えません。
/// </summary>
public static class SiteSearch
{
    /// <summary>HALKA WORLD を指す目印（ゲームタブの中でさらに選ぶため）。</summary>
    public const string WorldTarget = "halka-world";

    /// <summary>これより多い結果は返しません。</summary>
    public const int Limit = 200;

    public static IReadOnlyList<SearchHit> Find(SiteSession session, string query)
    {
        var needle = (query ?? "").Trim();
        if (needle.Length == 0) return Array.Empty<SearchHit>();

        var hits = new List<SearchHit>();

        void Add(string tab, string where, string? text, object? target = null)
        {
            if (text == null || text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0) return;
            hits.Add(new SearchHit(tab, where, Excerpt(text, needle), target));
        }

        // 1つずつの入力欄（依頼ページの料金・リンク・色・見出しなど）。
        foreach (var group in session.Groups)
        foreach (var field in group.Fields)
            Add(group.Title, field.Label, field.Value);

        // 文章のかたまり。
        foreach (var document in new[] { session.CommissionText, session.UtamazeText, session.ClubText, session.LegalText })
        foreach (var page in document?.Pages ?? Array.Empty<PageTextPage>())
        foreach (var block in page.Blocks)
            Add("ページの文章", $"{page.Title}／{block.Label}", block.Text, page);

        // 作品一覧。
        foreach (var category in session.Works.Categories)
        {
            Add("作品一覧", $"{category.Name}：分類の名前", category.Name, category);
            Add("作品一覧", $"{category.Name}：分類の説明", category.Description, category);
            foreach (var work in category.Works)
                Add("作品一覧", $"{category.Name}：{work.Title}", work.Title, category);
        }

        // うれしいこと。
        foreach (var item in session.News?.Items ?? Enumerable.Empty<NewsItem>())
        {
            Add("うれしいこと", item.Title, item.Title);
            Add("うれしいこと", $"{item.Title}：説明", item.Description);
            Add("うれしいこと", $"{item.Title}：受賞など", item.Achievements);
        }

        // リンク集。
        foreach (var card in session.Links?.Cards ?? Enumerable.Empty<LinkCard>())
        {
            Add("リンク", card.Name, card.Name);
            Add("リンク", $"{card.Name}：説明", card.Note);
            Add("リンク", $"{card.Name}：リンク先", card.Href);
        }

        // くらぶの更新内容。
        if (session.ClubUpdate != null) Add("はるかくらぶ", "更新内容", session.ClubUpdate.Text);

        // ゲーム。
        if (session.Games != null)
        {
            foreach (var card in session.Games.Collection.Cards)
            {
                Add("ゲーム", $"一覧：{card.Title}", card.Title);
                Add("ゲーム", $"一覧：{card.Title}の説明", card.Description);
            }
            foreach (var page in session.Games.Pages)
            {
                Add("ゲーム", $"{page.Title.Value}：説明", page.Description?.Text, page);
                foreach (var entry in page.Changelog)
                    Add("ゲーム", $"{page.Title.Value}：{entry.Version}", entry.Items, page);
            }
        }

        // HALKA WORLD。
        if (session.World != null)
        {
            foreach (var field in new[] { session.World.CurrentVersion, session.World.Controls }
                         .OfType<EditField>())
                Add("ゲーム", $"HALKA WORLD：{field.Label}", field.Value, WorldTarget);
        }

        foreach (var archive in session.World?.Archives ?? Enumerable.Empty<WorldArchive>())
        foreach (var entry in archive.Entries)
            Add("ゲーム", $"HALKA WORLD：{entry.Version}", entry.Items, WorldTarget);

        // ページの顔。
        foreach (var page in session.Meta?.Pages ?? Array.Empty<MetaPage>())
        foreach (var field in page.Fields)
            Add("ページの顔", field.Label, field.Value, page);

        return hits.Take(Limit).ToArray();
    }

    /// <summary>見つかったところの前後を切り出します。</summary>
    private static string Excerpt(string text, string needle)
    {
        var single = text.Replace("\r\n", " ").Replace("\n", " / ");
        var at = single.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (at < 0) return single.Length <= 60 ? single : single[..60] + "…";

        var from = Math.Max(0, at - 20);
        var length = Math.Min(single.Length - from, needle.Length + 50);
        var cut = single.Substring(from, length);
        return (from > 0 ? "…" : "") + cut + (from + length < single.Length ? "…" : "");
    }
}
