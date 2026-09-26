using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Landweigh.Core;

public static class ListingPruner
{
    public static string Prune(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var unwantedNodes = doc.DocumentNode.SelectNodes("//script|//style|//noscript|//svg|//comment()");
        if (unwantedNodes != null)
        {
            foreach (var node in unwantedNodes)
            {
                node.Remove();
            }
        }

        var textNodes = doc.DocumentNode.SelectNodes("//text()");
        if(textNodes == null)
        {
            return string.Empty;
        }
        string combinedText = string.Join(" ", textNodes.Select(node => node.InnerText));
        string decodedText = WebUtility.HtmlDecode(combinedText);
        string cleanedText = Regex.Replace(decodedText, @"\s+", " ").Trim();
        return cleanedText;
    }
}