using System.Text.Json;
using Landweigh.Core;

namespace Landweigh.Tests;

public class ListingMetadataReaderTests
{
    private const string FishCanonical =
        "<link rel=\"canonical\" href=\"https://detail.1688.com/offer/894864039706.html\">";

    [Test]
    public void Read_1688Page_ReadsAllThreeValues()
    {
        var html = FishCanonical
                 + "<a href=\"https://dingpeiyuju.1688.com/?offerId=894864039706&amp;td_page_id=X\">Shop</a>";

        var result = ListingMetadataReader.Read(html);

        Assert.That(result.Platform, Is.EqualTo("1688"));
        Assert.That(result.SourceUrl, Is.EqualTo("https://detail.1688.com/offer/894864039706.html"));
        Assert.That(result.SupplierProfileUrl, Is.EqualTo("https://dingpeiyuju.1688.com/"));
    }

    [Test]
    public void Read_NoCanonicalLink_ReturnsAllNull()
    {
        var result = ListingMetadataReader.Read("<p>Hello</p><a href=\"https://dingpeiyuju.1688.com/?offerId=1\">Shop</a>");

        Assert.That(result, Is.EqualTo(new ListingMetadata(null, null, null)));
    }

    [Test]
    public void Read_EmptyInput_ReturnsAllNull()
    {
        var result = ListingMetadataReader.Read("");

        Assert.That(result, Is.EqualTo(new ListingMetadata(null, null, null)));
    }

    [Test]
    public void Read_CanonicalWithQueryAndFragment_StripsThem()
    {
        var html = "<link rel=\"canonical\" href=\"https://detail.1688.com/offer/1.html?spm=abc#top\">";

        var result = ListingMetadataReader.Read(html);

        Assert.That(result.SourceUrl, Is.EqualTo("https://detail.1688.com/offer/1.html"));
    }

    [Test]
    public void Read_ShopLinkForAnotherOffer_IsIgnored()
    {
        // Recommended products link to other shops with their own offerId.
        var html = FishCanonical
                 + "<a href=\"https://othershop.1688.com/?offerId=111111\">Other shop</a>";

        var result = ListingMetadataReader.Read(html);

        Assert.That(result.SupplierProfileUrl, Is.Null);
    }

    [Test]
    public void Read_DetailPageLinkWithSameOfferId_IsNotTakenAsShop()
    {
        var html = FishCanonical
                 + "<a href=\"https://detail.1688.com/offer/894864039706.html?offerId=894864039706\">This page</a>";

        var result = ListingMetadataReader.Read(html);

        Assert.That(result.SupplierProfileUrl, Is.Null);
    }

    [Test]
    public void Read_ProtocolRelativeShopLink_IsFound()
    {
        var html = FishCanonical
                 + "<a href=\"//dingpeiyuju.1688.com/page/offerlist.htm?offerId=894864039706\">Shop</a>";

        var result = ListingMetadataReader.Read(html);

        Assert.That(result.SupplierProfileUrl, Is.EqualTo("https://dingpeiyuju.1688.com/"));
    }

    [Test]
    public void Read_AlibabaPage_DetectsPlatformButNoShopRuleYet()
    {
        var html = "<link rel=\"canonical\" href=\"https://www.alibaba.com/product-detail/Solar-Light_1600000000000.html\">";

        var result = ListingMetadataReader.Read(html);

        Assert.That(result.Platform, Is.EqualTo("alibaba"));
        Assert.That(result.SupplierProfileUrl, Is.Null);
    }

    [Test]
    public void Read_UnknownSite_PlatformIsNull()
    {
        var result = ListingMetadataReader.Read("<link rel=\"canonical\" href=\"https://example.com/item/1\">");

        Assert.That(result.Platform, Is.Null);
        Assert.That(result.SourceUrl, Is.EqualTo("https://example.com/item/1"));
    }

    // The answer keys already hold the correct values, so the fixtures double as test data.
    [TestCase("001-1688-fish-bucket")]
    [TestCase("002-1688-solar-garden-light")]
    public void Read_Fixture_MatchesExpectedJson(string fixture)
    {
        var folder = Path.Combine(TestContext.CurrentContext.TestDirectory, "fixtures", fixture);
        var html = File.ReadAllText(Path.Combine(folder, "listing.html"));
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "expected.json")));
        var key = expected.RootElement;

        var result = ListingMetadataReader.Read(html);

        Assert.That(result.Platform, Is.EqualTo(key.GetProperty("platform").GetString()));
        Assert.That(result.SourceUrl, Is.EqualTo(key.GetProperty("source_url").GetString()));
        Assert.That(result.SupplierProfileUrl,
            Is.EqualTo(key.GetProperty("supplier").GetProperty("profile_url").GetString()));
    }
}
