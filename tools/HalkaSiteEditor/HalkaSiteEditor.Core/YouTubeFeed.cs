using System.Xml.Linq;

namespace HalkaSiteEditor.Core;

/// <summary>YouTubeの新着から拾った動画1本。</summary>
public sealed record FeedVideo(string VideoId, string Title, DateTimeOffset Published)
{
    public string Url => $"https://youtu.be/{VideoId}";
    public string PublishedAt => Published.ToLocalTime().ToString("yyyy-MM-dd");
}

/// <summary>
/// YouTubeの新着（RSS）を読みます。**APIキーは要りません。**
/// チャンネルごとに最新15本まで取れるので、「さっき出した動画を足す」用途には十分です。
/// </summary>
public static class YouTubeFeed
{
    /// <summary>HALKAのメインチャンネル（youtube.com/HALKAchan）。</summary>
    public const string DefaultChannelId = "UCEWHDzq3b6TdGRnlfqVzZqA";

    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace YouTube = "http://www.youtube.com/xml/schemas/2015";

    public static string FeedUrl(string channelId) =>
        $"https://www.youtube.com/feeds/videos.xml?channel_id={channelId}";

    /// <summary>チャンネルIDの形をざっと確かめます。</summary>
    public static bool LooksLikeChannelId(string? value)
    {
        var text = value?.Trim();
        return text is { Length: 24 } && text.StartsWith("UC", StringComparison.Ordinal) &&
               text.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
    }

    public static IReadOnlyList<FeedVideo> Parse(string xml)
    {
        var document = XDocument.Parse(xml);
        var videos = new List<FeedVideo>();

        foreach (var entry in document.Descendants(Atom + "entry"))
        {
            var id = entry.Element(YouTube + "videoId")?.Value;
            var title = entry.Element(Atom + "title")?.Value;
            var published = entry.Element(Atom + "published")?.Value;
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(title)) continue;

            var when = DateTimeOffset.TryParse(published, out var parsed) ? parsed : DateTimeOffset.Now;
            videos.Add(new FeedVideo(id, title.Trim(), when));
        }

        return videos;
    }

    public static async Task<IReadOnlyList<FeedVideo>> FetchAsync(string channelId, CancellationToken token = default)
    {
        if (!LooksLikeChannelId(channelId))
            throw new InvalidDataException("チャンネルIDの形が違います（UC で始まる24文字）。");

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HalkaSiteEditor/1.0");

        var response = await client.GetAsync(FeedUrl(channelId.Trim()), token);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidDataException(
                $"YouTubeから取れませんでした（{(int)response.StatusCode}）。チャンネルIDと、ネットにつながっているかを確かめてください。");
        }

        return Parse(await response.Content.ReadAsStringAsync(token));
    }

    /// <summary>作品一覧に入っている動画のIDを集めます（重複して足さないため）。</summary>
    public static HashSet<string> KnownVideoIds(WorksDocument works)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var category in works.Categories)
        foreach (var work in category.Works)
        {
            var id = YouTubeUrl.ExtractId(work.YouTubeUrl);
            if (id != null) known.Add(id);
        }
        return known;
    }
}
