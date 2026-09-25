using AngleSharp;
using AngleSharp.Dom;
using System.Globalization;
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

        Assert.Equal(66445, series!.ThiefGuildSeriesId);
        Assert.Equal("The Book of Prophecy", series.Name);
        Assert.Equal(3, series.Position);
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

        Assert.Equal(66445, result.Series!.ThiefGuildSeriesId);
        Assert.Equal("The Book of Prophecy", result.Series.Name);
        Assert.Equal(2, result.Series.Position);
    }

    private const string MetadataPage = "<html><head><title>Endless Rain - Fan Mission for Thief Gold -  Thief Guild</title>\n" +
        "<script type=\"application/ld+json\">\n" +
        "{ \"@context\": \"http://schema.org\", \"@type\": \"CreativeWork\", \"name\": \"Endless Rain\",\n" +
        "  \"description\": \"\\\"There was a time\\\"\\r\\nThe high plazas of Stonemarket call me tonight.   \" }\n" +
        "</script></head>\n" +
        "<body>\n" +
        "  <h3>Endless Rain <span class=\"text-muted\">(2014)</span></h3>\n" +
        "  <div class=\"panel-body\"><div class=\"row\"><div class=\"col-lg-6 col-xs-12\"><div class=\"row\">\n" +
        "    <p style=\"font-size: xx-large\">\n" +
        "      <i class=\"material-icons text-primary\">star</i>\n" +
        "      9.<small>02</small>\n" +
        "    </p></div></div>\n" +
        "    <div class=\"col\"><div style=\"font-size: x-large\">\n" +
        "      <a href=\"/fanmissions/rating_list/2535\" class=\"label label-default\">\n" +
        "        229 ratings\n" +
        "      </a></div></div></div></div>\n" +
        "  <ul class=\"list-group\">\n" +
        "    <li class=\"list-group-item\">Game: Thief Gold</li>\n" +
        "    <li class=\"list-group-item\">Released: Sept. 8, 2014</li>\n" +
        "    <li class=\"list-group-item\">\n" +
        "      Single mission\n" +
        "    </li>\n" +
        "    <li class=\"list-group-item\">\n" +
        "      Sequel of:\n" +
        "      <br/>\n" +
        "      <a href=\"/works/bc79e112-64cb-4cdf-9ac5-bce4a4b82592\">Between These Dark Walls</a>\n" +
        "    </li>\n" +
        "    <li class=\"list-group-item text-muted\">\n" +
        "      <small>FM has a sequel:\n" +
        "      <br/>\n" +
        "      <a href=\"/works/242559be-45c6-41c5-8d59-34fe374f77b9\">The Chalice of Souls</a>\n" +
        "      </small>\n" +
        "    </li>\n" +
        "  </ul>\n" +
        "</body></html>";

    [Fact]
    public async Task ExtractRating_ReadsRatingAndCount()
    {
        var document = await ParseAsync(MetadataPage);

        var (rating, count) = ThiefGuildPageParser.ExtractRating(document);

        Assert.Equal(9.02, rating);
        Assert.Equal(229, count);
    }

    [Fact]
    public async Task ExtractRating_UnderCommaDecimalCulture_ParsesInvariantly()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var document = await ParseAsync(MetadataPage);

            Assert.Equal(9.02, ThiefGuildPageParser.ExtractRating(document).Rating);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task ExtractRating_WithoutRatingCountLink_ReturnsNulls()
    {
        var document = await ParseAsync("""<p><i class="material-icons">star</i> 9.<small>02</small></p>""");

        var (rating, count) = ThiefGuildPageParser.ExtractRating(document);

        Assert.Null(rating);
        Assert.Null(count);
    }

    [Fact]
    public async Task ExtractCampaignMissionCount_SingleMission_Returns1()
    {
        var document = await ParseAsync(MetadataPage);

        Assert.Equal(1, ThiefGuildPageParser.ExtractCampaignMissionCount(document));
    }

    [Fact]
    public async Task ExtractCampaignMissionCount_Campaign_ReturnsMissionCount()
    {
        var document = await ParseAsync("""
            <ul class="list-group"><li class="list-group-item">
                Campaign of 10 missions
            </li></ul>
            """);

        Assert.Equal(10, ThiefGuildPageParser.ExtractCampaignMissionCount(document));
    }

    [Fact]
    public async Task ExtractCampaignMissionCount_Missing_ReturnsNull()
    {
        var document = await ParseAsync("""<ul class="list-group"><li class="list-group-item">Game: Thief Gold</li></ul>""");

        Assert.Null(ThiefGuildPageParser.ExtractCampaignMissionCount(document));
    }

    [Fact]
    public async Task ExtractDescription_ReadsLdJsonDescription()
    {
        var document = await ParseAsync(MetadataPage);

        var description = ThiefGuildPageParser.ExtractDescription(document);

        Assert.Equal("\"There was a time\"\r\nThe high plazas of Stonemarket call me tonight.", description);
    }

    [Fact]
    public async Task ExtractDescription_SkipsLdJsonBlocksWithoutDescription()
    {
        const string html = """
            <html><head>
            <script type="application/ld+json">{"@type":"BreadcrumbList"}</script>
            <script type="application/ld+json">{"description": "Second block."}</script>
            </head><body></body></html>
            """;
        var document = await ParseAsync(html);

        Assert.Equal("Second block.", ThiefGuildPageParser.ExtractDescription(document));
    }

    [Fact]
    public async Task ExtractDescription_InvalidJson_ReturnsNull()
    {
        var document = await ParseAsync("""<html><head><script type="application/ld+json">{ not json</script></head><body></body></html>""");

        Assert.Null(ThiefGuildPageParser.ExtractDescription(document));
    }

    [Fact]
    public async Task ExtractSidebarLink_ReadsSequelLinksAsAbsoluteUrls()
    {
        var document = await ParseAsync(MetadataPage);

        Assert.Equal(
            new ThiefGuildLink("Between These Dark Walls", "https://www.thiefguild.com/works/bc79e112-64cb-4cdf-9ac5-bce4a4b82592"),
            ThiefGuildPageParser.ExtractSidebarLink(document, "Sequel of:"));
        Assert.Equal(
            new ThiefGuildLink("The Chalice of Souls", "https://www.thiefguild.com/works/242559be-45c6-41c5-8d59-34fe374f77b9"),
            ThiefGuildPageParser.ExtractSidebarLink(document, "FM has a sequel:"));
    }

    [Fact]
    public async Task BuildResult_OnDetailPage_IncludesAllMetadata()
    {
        var document = await ParseAsync(MetadataPage);

        var result = ThiefGuildPageParser.BuildResult(document, document.Body!.TextContent, "https://www.thiefguild.com/fanmissions/2535/endless-rain");

        Assert.Equal(9.02, result.Rating);
        Assert.Equal(229, result.RatingCount);
        Assert.Equal(1, result.CampaignMissionCount);
        Assert.StartsWith("\"There was a time\"", result.Description);
        Assert.Equal("Between These Dark Walls", result.SequelOf?.Title);
        Assert.Equal("The Chalice of Souls", result.HasSequel?.Title);
        Assert.Null(result.Series);
    }

    [Fact]
    public async Task BuildResult_InSeries_ListsEveryPartWithTitlesAndUrls()
    {
        var document = await ParseAsync(SeriesHeaderPage(Part1Link + "TBOPP2THC" + Part3Link));

        var result = ThiefGuildPageParser.BuildResult(document, document.Body!.TextContent, "https://www.thiefguild.com/fanmissions/2682/x");

        Assert.Equal(new[]
        {
            new ThiefGuildSeriesPartInfo(1, "The Book of Prophecy Part 1: Dead Letter Box", "https://www.thiefguild.com/fanmissions/2684/the-book-of-prophecy-part-1-dead-letter-box"),
            new ThiefGuildSeriesPartInfo(2, "Some Mission", "https://www.thiefguild.com/fanmissions/2682/x"),
            new ThiefGuildSeriesPartInfo(3, "The Book of Prophecy Part 3: In the Lion's Den", "https://www.thiefguild.com/fanmissions/66450/the-book-of-prophecy-part-3-in-the-lions-den")
        }, result.Series!.Parts);
    }

    [Fact]
    public async Task ExtractSeries_WithoutCurrentTitle_UsesTheUnlinkedTextForTheCurrentPart()
    {
        var document = await ParseAsync(SeriesHeaderPage(Part1Link + "TBOPP2THC"));

        var series = ThiefGuildPageParser.ExtractSeries(document);

        Assert.Equal("TBOPP2THC", series!.Parts![1].Title);
    }

    [Fact]
    public async Task LooksLikeMissionDetailPage_OnDetailPage_IsTrue()
    {
        var document = await ParseAsync(MetadataPage);

        Assert.True(ThiefGuildPageParser.LooksLikeMissionDetailPage(document));
    }

    [Fact]
    public async Task LooksLikeMissionDetailPage_OnMaintenancePage_IsFalse()
    {
        var document = await ParseAsync("<html><body><h1>Down for maintenance</h1></body></html>");

        Assert.False(ThiefGuildPageParser.LooksLikeMissionDetailPage(document));
    }
}
