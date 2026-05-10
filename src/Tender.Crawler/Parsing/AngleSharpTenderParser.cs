using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Tender.Core.Models;

namespace Tender.Crawler.Parsing;

/// <summary>
/// 使用 AngleSharp 解析政府電子採購網標案列表頁面。
/// 解析 &lt;table id="tpam"&gt; 的每一列 &lt;tr&gt;。
/// 標案名稱從同列 &lt;script&gt; 內的 Geps3.CNS.pageCode2Img("...") 提取。
/// </summary>
public sealed class AngleSharpTenderParser : ITenderParser
{
    // 例：Geps3.CNS.pageCode2Img("智慧水利監測系統建置案");
    private static readonly Regex TenderNameRegex =
        new(@"Geps3\.CNS\.pageCode2Img\(""([^""]+)""\)", RegexOptions.Compiled);

    // 例：?pk=NzEyMTUxODc=&tenderType=...  或  ?pk=NzEyMTUxODc=
    private static readonly Regex PkRegex =
        new(@"[?&]pk=([^&""'\s]+)", RegexOptions.Compiled);

    private static readonly IBrowsingContext _context =
        BrowsingContext.New(Configuration.Default);

    private static readonly IHtmlParser _htmlParser =
        _context.GetService<IHtmlParser>()!;

    public IReadOnlyList<TenderItem> Parse(string html, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(html))
            throw new ParseException(0, "HTML is null or empty.");

        IDocument doc;
        try
        {
            doc = _htmlParser.ParseDocument(html);
        }
        catch (Exception ex)
        {
            throw new ParseException(0, $"AngleSharp failed to parse HTML: {ex.Message}", ex);
        }

        var table = doc.QuerySelector("table#tpam");
        if (table == null)
        {
            // 空表格也是合法結果（當天無標案）
            return Array.Empty<TenderItem>();
        }

        var rows = table.QuerySelectorAll("tbody tr");
        if (rows.Length == 0)
            return Array.Empty<TenderItem>();

        var results = new List<TenderItem>(rows.Length);

        foreach (var row in rows)
        {
            var item = TryParseRow(row, now);
            if (item != null)
                results.Add(item);
        }

        return results.AsReadOnly();
    }

    private static TenderItem? TryParseRow(IElement row, DateTimeOffset now)
    {
        var cells = row.QuerySelectorAll("td");
        if (cells.Length < 9)
            return null;

        // td[0] = 序號（忽略）
        // td[1] = 機關名稱
        var agencyName = cells[1].TextContent.Trim();

        // td[2] = 標案名稱（從 <script> 提取）
        var tenderCell = cells[2];
        var tenderName = ExtractTenderName(tenderCell);
        if (string.IsNullOrWhiteSpace(tenderName))
        {
            // fallback：嘗試從 <a> 文字取得
            tenderName = tenderCell.QuerySelector("a")?.TextContent.Trim()
                         ?? tenderCell.TextContent.Trim();
        }

        // detail URL 從 <a href="..."> 取得
        var detailUrl = tenderCell.QuerySelector("a")?.GetAttribute("href") ?? string.Empty;
        detailUrl = NormalizeUrl(detailUrl);

        // SourcePk 從 URL 解析 pk 參數
        var sourcePk = ExtractPk(detailUrl);

        // td[3] = 案號
        var tenderNo = cells[3].TextContent.Trim().NullIfEmpty();

        // td[4] = 招標方式
        var tenderMethod = cells[4].TextContent.Trim();

        // td[5] = 採購性質
        var procurementType = cells[5].TextContent.Trim().NullIfEmpty();

        // td[6] = 公告日期
        var announcementDate = cells[6].TextContent.Trim();

        // td[7] = 截止日期
        var bidDeadline = cells[7].TextContent.Trim().NullIfEmpty();

        // td[8] = 預算金額（可能含逗號，例 "1,000,000"）
        var budgetAmount = ParseBudget(cells[8].TextContent.Trim());

        // 必要欄位驗證
        if (string.IsNullOrWhiteSpace(agencyName) ||
            string.IsNullOrWhiteSpace(tenderName) ||
            string.IsNullOrWhiteSpace(tenderMethod) ||
            string.IsNullOrWhiteSpace(announcementDate))
        {
            return null;
        }

        // 若無法取得有效 pk，以 URL hash 作為 fallback
        if (string.IsNullOrWhiteSpace(sourcePk))
            sourcePk = $"NOPK-{Guid.NewGuid():N}";

        if (string.IsNullOrWhiteSpace(detailUrl))
            detailUrl = "https://web.pcc.gov.tw/pis/";

        return new TenderItem
        {
            SourcePk = sourcePk,
            AgencyName = agencyName,
            TenderName = tenderName,
            TenderNo = tenderNo,
            TenderMethod = tenderMethod,
            ProcurementType = procurementType,
            AnnouncementDate = announcementDate,
            BidDeadline = bidDeadline,
            BudgetAmount = budgetAmount,
            DetailUrl = detailUrl,
            CreatedAt = now,
            LastSeenAt = now,
        };
    }

    private static string ExtractTenderName(IElement cell)
    {
        // 找 <script> 標籤內容
        foreach (var script in cell.QuerySelectorAll("script"))
        {
            var scriptText = script.TextContent;
            var match = TenderNameRegex.Match(scriptText);
            if (match.Success)
                return match.Groups[1].Value.Trim();
        }

        // fallback：從整個 cell HTML 搜尋（某些頁面 script 在 td 外層）
        var htmlMatch = TenderNameRegex.Match(cell.InnerHtml);
        if (htmlMatch.Success)
            return htmlMatch.Groups[1].Value.Trim();

        return string.Empty;
    }

    private static string ExtractPk(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        var match = PkRegex.Match(url);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string NormalizeUrl(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
            return string.Empty;

        // 相對路徑補全為 absolute URL
        if (href.StartsWith("/"))
            return "https://web.pcc.gov.tw" + href;

        // 只接受 http/https；其他 scheme（file://, javascript:, cmd:…）視為無效，
        // 避免後續 ProcessStartBrowserLauncher 被當成 URL handler 觸發。
        if (Uri.TryCreate(href, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri.AbsoluteUri;
        }

        return string.Empty;
    }

    private static long? ParseBudget(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // 移除千分位逗號、空白
        var cleaned = text.Replace(",", "").Replace(" ", "").Trim();

        if (long.TryParse(cleaned, out var amount))
            return amount;

        return null;
    }
}

internal static class StringExtensions
{
    public static string? NullIfEmpty(this string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s;
}
