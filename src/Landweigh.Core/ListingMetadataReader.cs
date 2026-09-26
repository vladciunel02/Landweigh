using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;

namespace Landweigh.Core;

public static class ListingMetadataReader
{
    // 1688 product URLs look like https://detail.1688.com/offer/894864039706.html
    private static readonly Regex OfferIdIn1688Path = new(@"^/offer/(\d+)\.html$", RegexOptions.Compiled);

    public static ListingMetadata Read(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var sourceUri = ReadCanonicalUri(doc);
        if (sourceUri is null)
        {
            return new ListingMetadata(null, null, null);
        }

        var platform = DetectPlatform(sourceUri.Host);
        var profileUrl = platform == "1688" ? Find1688ShopUrl(doc, sourceUri) : null;

        return new ListingMetadata(platform, sourceUri.GetLeftPart(UriPartial.Path), profileUrl);
    }

    // <link rel="canonical"> is the site's own clean URL for the page, without tracking parameters.
    private static Uri? ReadCanonicalUri(HtmlDocument doc)
    {
        var href = doc.DocumentNode
            .SelectSingleNode("//link[@rel='canonical']")
            ?.GetAttributeValue("href", null);

        return ToAbsoluteUri(href);
    }

    private static string? DetectPlatform(string host)
    {
        if (IsHostOf(host, "1688.com")) return "1688";
        if (IsHostOf(host, "alibaba.com")) return "alibaba";
        return null;
    }

    // The supplier's shop is the *.1688.com link that carries this listing's offerId,
    // e.g. https://dingpeiyuju.1688.com/?offerId=894864039706&td_page_id=...
    // Other *.1688.com links on the page (trade, rules, messages) never carry it.
    private static string? Find1688ShopUrl(HtmlDocument doc, Uri sourceUri)
    {
        var match = OfferIdIn1688Path.Match(sourceUri.AbsolutePath);
        if (!match.Success)
        {
            return null;
        }
        var offerId = match.Groups[1].Value;

        var links = doc.DocumentNode.SelectNodes("//a[@href]");
        if (links is null)
        {
            return null;
        }

        foreach (var link in links)
        {
            var uri = ToAbsoluteUri(link.GetAttributeValue("href", null));
            if (uri is null
                || !IsHostOf(uri.Host, "1688.com")
                || uri.Host is "1688.com" or "www.1688.com" or "detail.1688.com")
            {
                continue;
            }

            var query = HttpUtility.ParseQueryString(uri.Query);
            if (query["offerId"] == offerId)
            {
                return $"{uri.Scheme}://{uri.Host}/";
            }
        }

        return null;
    }

    private static bool IsHostOf(string host, string domain) =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase)
        || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);

    private static Uri? ToAbsoluteUri(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var decoded = WebUtility.HtmlDecode(href.Trim());
        if (decoded.StartsWith("//"))
        {
            decoded = "https:" + decoded;
        }

        return Uri.TryCreate(decoded, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri
            : null;
    }
}
