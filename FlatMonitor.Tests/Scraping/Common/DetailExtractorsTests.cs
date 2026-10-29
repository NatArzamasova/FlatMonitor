using FlatMonitor.Scraping.Common;
using FluentAssertions;
using HtmlAgilityPack;
using NUnit.Framework;

namespace FlatMonitor.Tests.Scraping.Common;

[TestFixture]
public class DetailExtractorsTests
{
    private static HtmlDocument Doc(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    [Test]
    public void ExtractText_ReturnsTheTrimmedText()
    {
        DetailExtractors.ExtractText(Doc("<dd class='x'>  Gas  </dd>"), "//dd[@class='x']").Should().Be("Gas");
    }

    [TestCase("<p>other</p>", TestName = "ExtractText_MissingNode_ReturnsNull")]
    [TestCase("<dd class='x'>   </dd>", TestName = "ExtractText_WhitespaceOnly_ReturnsNull")]
    public void ExtractText_WithoutAValue_ReturnsNull(string html)
    {
        DetailExtractors.ExtractText(Doc(html), "//dd[@class='x']").Should().BeNull();
    }

    [Test]
    public void ExtractAttribute_ReturnsTheTrimmedValueOrNull()
    {
        var doc = Doc("<img alt=' B ' /><img class='none' />");

        DetailExtractors.ExtractAttribute(doc, "//img[@alt]", "alt").Should().Be("B");
        DetailExtractors.ExtractAttribute(doc, "//img[@class='none']", "alt").Should().BeNull();
    }

    [Test]
    public void ExtractText_WithAnInvalidXPath_Throws()
    {
        // A programming error: it must fail the parser tests, not read as "field not on the page"
        var act = () => DetailExtractors.ExtractText(Doc("<p>x</p>"), "//dd[@class=");

        act.Should().Throw<Exception>();
    }

    [Test]
    public void ExtractAttribute_WithAnInvalidXPath_Throws()
    {
        var act = () => DetailExtractors.ExtractAttribute(Doc("<p>x</p>"), "//img[", "alt");

        act.Should().Throw<Exception>();
    }
}
