using Landweigh.Core;

namespace Landweigh.Tests;

public class ListingPrunerTests
{
    [Test]
    public void Prune_RemovesScripts()
    {
        var result = ListingPruner.Prune("<p>Hello</p><script>var x = 1;</script>");

        Assert.That(result, Is.EqualTo("Hello"));
    }

    [Test]
    public void Prune_RemovesStylesNoscriptSvgAndComments()
    {
        var html = "<style>p { color: red; }</style>"
                 + "<noscript>Please enable JavaScript</noscript>"
                 + "<svg><text>icon</text></svg>"
                 + "<!-- hidden comment -->"
                 + "<p>Hello</p>";

        var result = ListingPruner.Prune(html);

        Assert.That(result, Is.EqualTo("Hello"));
    }

    [Test]
    public void Prune_PageWithNothingToRemove_DoesNotCrash()
    {
        var result = ListingPruner.Prune("<p>Hello</p>");

        Assert.That(result, Is.EqualTo("Hello"));
    }

    [Test]
    public void Prune_EmptyInput_ReturnsEmptyString()
    {
        var result = ListingPruner.Prune("");

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Prune_CollapsesWhitespace()
    {
        var result = ListingPruner.Prune("<p>a</p>\n\n   <p>b</p>");

        Assert.That(result, Is.EqualTo("a b"));
    }

    [Test]
    public void Prune_TrimsLeadingAndTrailingWhitespace()
    {
        var result = ListingPruner.Prune("   <p>  Hello  </p>   ");

        Assert.That(result, Is.EqualTo("Hello"));
    }

    [Test]
    public void Prune_DecodesHtmlEntities()
    {
        var result = ListingPruner.Prune("<p>Fish &amp; tackle &lt;wholesale&gt;</p>");

        Assert.That(result, Is.EqualTo("Fish & tackle <wholesale>"));
    }

    [Test]
    public void Prune_KeepsWordsFromAdjacentElementsApart()
    {
        var result = ListingPruner.Prune("<div>Price</div><div>10.5</div><span>Khaki</span><span>12L</span>");

        Assert.That(result, Is.EqualTo("Price 10.5 Khaki 12L"));
    }

    [TestCase("001-1688-fish-bucket", "10.5")]
    [TestCase("002-1688-solar-garden-light", "3.9")]
    public void Prune_RealListing_KeepsPriceAndShrinksBelowFivePercent(string fixture, string expectedPrice)
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "fixtures", fixture, "listing.html");
        var html = File.ReadAllText(path);

        var pruned = ListingPruner.Prune(html);

        Assert.Multiple(() =>
        {
            Assert.That(pruned, Does.Contain(expectedPrice));
            Assert.That(pruned.Length, Is.LessThan(html.Length * 0.05),
                $"Pruned text kept {pruned.Length:N0} of {html.Length:N0} characters");
        });
    }
}
