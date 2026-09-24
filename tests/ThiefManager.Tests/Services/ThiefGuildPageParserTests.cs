using AngleSharp;
using AngleSharp.Dom;
using ThiefManager.Services;
using Xunit;

namespace ThiefManager.Tests.Services;

public class ThiefGuildPageParserTests
{
    private static readonly IBrowsingContext Browser = BrowsingContext.New(Configuration.Default);

    private static async Task<IDocument> ParseAsync(string html) =>
        await Browser.OpenAsync(req => req.Content(html));

    [Fact]
    public async Task BuildResult_OnDetailPage_PicksAuthorFromAuthorHeading_NotEarlierScreenshotUploaderLink()
    {
        const string html = """
            <html><head><title>Endless Rain - Fan Mission for Thief Gold -  Thief Guild - Thief Series and Related Games Fan Mission Database</title></head>
            <body>
              <div class="row">
                <a href="/user/2239/morente" style="font-size: small; text-decoration: none">Morente</a>
              </div>
              <h3 style="margin-bottom: 1px">
                Endless Rain
                <span class="text-muted" style="font-size: small">(2014)</span>
              </h3>
              <div class="row well fmroundbox text-justify">
                <h4>Author</h4>
                <div class="row">
                  <div class="col-lg-11">
                    <p>
                      <a href="/user/77/skacky">skacky</a>
                      <a class="btn btn-default btn-xs" href="/fanmissions?author=5239">Missions</a>
                    </p>
                  </div>
                </div>
              </div>
            </body></html>
            """;
        var document = await ParseAsync(html);

        var result = ThiefGuildPageParser.BuildResult(document, document.Body!.TextContent, "https://www.thiefguild.com/fanmissions/2535/endless-rain");

        Assert.Equal("skacky", result.Author);
        Assert.Equal(2014, result.ReleaseYear);
    }

    [Fact]
    public async Task BuildResult_OnSearchCard_UsesAuthorQueryLink()
    {
        const string html = """
            <div class="panel panel-default">
              <div class="caption">
                <h4><a href="/fanmissions/34342/the-black-mage">The Black Mage</a></h4>
                <div class="row"><div class="col-xs-12"><p class="text-muted">
                  by <span><a href="/fanmissions?author=6007">grayman</a> and <a href="/fanmissions?author=7842">JackFarmer</a></span>
                </p></div></div>
              </div>
            </div>
            """;
        var document = await ParseAsync(html);
        var card = document.QuerySelector("div.panel")!;

        var result = ThiefGuildPageParser.BuildResult(card, card.TextContent, "https://www.thiefguild.com/fanmissions/34342/the-black-mage");

        Assert.Equal("grayman, JackFarmer", result.Author);
    }

    [Fact]
    public async Task BuildResult_OnDetailPage_WithCoAuthoredMission_CreditsAllAuthors_NotTheMissionsButtonLink()
    {
        // Real-world markup shape from thiefguild.com/fanmissions/104893/rotlock-prison: the
        // heading reads "Authors" (plural) and each author is followed by a "Missions" button
        // linking to /fanmissions?author=..., which must not be picked up as an author name.
        const string html = """
            <html><head><title>Rotlock Prison - Fan Mission for Thief 2 -  Thief Guild - Thief Series and Related Games Fan Mission Database</title></head>
            <body>
              <div class="row well fmroundbox text-justify">
                <h4>Authors</h4>
                <div class="row">
                  <div class="col-lg-11">
                    <p>
                      <a href="/user/159/lord-taffer">Lord Taffer</a>
                      <a class="btn btn-default btn-xs" href="/fanmissions?author=16870">Missions</a>
                      <br/>Co-Author
                    </p>
                  </div>
                </div>
                <div class="row">
                  <div class="col-lg-11">
                    <p>
                      <a href="/user/167/aemanyl">Aemanyl</a>
                      <a class="btn btn-default btn-xs" href="/fanmissions?author=6934">Missions</a>
                      <br/>Co-Author
                    </p>
                  </div>
                </div>
              </div>
            </body></html>
            """;
        var document = await ParseAsync(html);

        var result = ThiefGuildPageParser.BuildResult(document, document.Body!.TextContent, "https://www.thiefguild.com/fanmissions/104893/rotlock-prison");

        Assert.Equal("Lord Taffer, Aemanyl", result.Author);
    }

    [Fact]
    public async Task BuildResult_OnSearchCard_WithMultipleUncreditedAuthors_ReturnsNullAuthor()
    {
        const string html = """
            <div class="panel panel-default">
              <div class="caption">
                <h4><a href="/fanmissions/26275/thief-the-black-parade">Thief: The Black Parade</a></h4>
                <div class="row"><div class="col-xs-12"><p class="text-muted">
                  by <span>7 authors</span>
                </p></div></div>
              </div>
            </div>
            """;
        var document = await ParseAsync(html);
        var card = document.QuerySelector("div.panel")!;

        var result = ThiefGuildPageParser.BuildResult(card, card.TextContent, "https://www.thiefguild.com/fanmissions/26275/thief-the-black-parade");

        Assert.Null(result.Author);
    }

    [Fact]
    public async Task ExtractDetailPageTitle_StripsTrailingSiteSuffix()
    {
        const string html = "<html><head><title>Endless Rain - Fan Mission for Thief Gold -  Thief Guild - Thief Series and Related Games Fan Mission Database</title></head><body></body></html>";
        var document = await ParseAsync(html);

        var title = ThiefGuildPageParser.ExtractDetailPageTitle(document);

        Assert.Equal("Endless Rain", title);
    }

    private static string SeriesHeaderPage(string membersHtml, string seriesHref = "/fanmissions?series=66445") => $$"""
        <html><head><title>Some Mission - Fan Mission for Thief II: The Metal Age -  Thief Guild</title></head>
        <body>
          <h3 style="margin-bottom: 1px">Some Mission</h3>
          <h6 style="margin: 2px">
              <a href="{{seriesHref}}"
              >
                  The Book of Prophecy:
              </a>
                  <br/>
                  {{membersHtml}}
              <br/>
          </h6>
          <ul class="list-group">
            <li class="list-group-item">Series: <a href="{{seriesHref}}">The Book of Prophecy</a></li>
          </ul>
        </body></html>
        """;

    private const string Part1Link = """
        <a title="The Book of Prophecy Part 1: Dead Letter Box (2007)" class="text-muted" href="/fanmissions/2684/the-book-of-prophecy-part-1-dead-letter-box">
            TBOPP1DLB
        </a>
        """;
    private const string Part2Link = """
        <a title="The Book of Prophecy Part 2: The Hidden City (2009)" class="text-muted" href="/fanmissions/2682/the-book-of-prophecy-part-2-the-hidden-city">
            TBOPP2THC
        </a>
        """;
    private const string Part3Link = """
        <a title="The Book of Prophecy Part 3: In the Lion&#39;s Den (2026)" class="text-muted" href="/fanmissions/66450/the-book-of-prophecy-part-3-in-the-lions-den">
            TBOPP3ITLD
        </a>
        """;

    [Fact]
    public async Task ExtractSeries_OnLastPartPage_ReturnsSeriesAndPosition()
    {
        var document = await ParseAsync(SeriesHeaderPage(Part1Link + Part2Link + "TBOPP3ITLD"));

        var series = ThiefGuildPageParser.ExtractSeries(document);

        Assert.Equal(new ThiefGuildSeriesInfo(66445, "The Book of Prophecy", 3), series);
    }

    [Fact]
    public async Task ExtractSeries_OnMiddlePartPage_ReturnsMiddlePosition()
    {
        var document = await ParseAsync(SeriesHeaderPage(Part1Link + "TBOPP2THC" + Part3Link));

        Assert.Equal(2, ThiefGuildPageParser.ExtractSeries(document)?.Position);
    }

    [Fact]
    public async Task ExtractSeries_OnPageWithoutSeries_ReturnsNull()
    {
        var document = await ParseAsync("<html><body><h3>Lonely Mission</h3><h6></h6></body></html>");

        Assert.Null(ThiefGuildPageParser.ExtractSeries(document));
    }

    [Fact]
    public async Task ExtractSeries_WithTwoUnlinkedEntries_ReturnsNull()
    {
        var document = await ParseAsync(SeriesHeaderPage("AAA" + Part2Link + "BBB"));

        Assert.Null(ThiefGuildPageParser.ExtractSeries(document));
    }

    [Fact]
    public async Task ExtractSeries_WithNoUnlinkedEntry_ReturnsNull()
    {
        var document = await ParseAsync(SeriesHeaderPage(Part1Link + Part2Link));

        Assert.Null(ThiefGuildPageParser.ExtractSeries(document));
    }

    [Fact]
    public async Task ExtractSeries_WithNonNumericSeriesId_ReturnsNull()
    {
        var document = await ParseAsync(SeriesHeaderPage(Part1Link + "TBOPP2THC", "/fanmissions?series=abc"));

        Assert.Null(ThiefGuildPageParser.ExtractSeries(document));
    }

    [Fact]
    public async Task BuildResult_OnDetailPageInSeries_IncludesSeries()
    {
        var document = await ParseAsync(SeriesHeaderPage(Part1Link + "TBOPP2THC"));

        var result = ThiefGuildPageParser.BuildResult(document, document.Body!.TextContent, "https://www.thiefguild.com/fanmissions/2682/x");

        Assert.Equal(new ThiefGuildSeriesInfo(66445, "The Book of Prophecy", 2), result.Series);
    }
}
