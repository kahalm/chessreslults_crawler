using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Die Turnierdatenbank des Deutschen Schachbunds. Ihr Ertrag ist klein, aber es ist eine ANDERE
/// Art Turnier: ein reines Meldesystem ohne Ergebnismeldung, in dem Vereins-Abendturniere,
/// Jugend-Cups, Fernschach, Problemschach und Schach960 stehen — Kategorien, die chess-results
/// praktisch nie fuehrt.
/// </summary>
public class SchachbundCalendarServiceTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static List<ParsedSchachbundEvent> ParsePage() =>
        SchachbundCalendarService.ParsePage(Fixture("schachbund-bayern.html"), "bayern");

    // ----- Die Regionsseite --------------------------------------------------

    [Fact]
    public void ParsePage_ReadsEveryRow()
    {
        var events = ParsePage();

        Assert.Equal(19, events.Count);
        Assert.All(events, e => Assert.NotEqual("", e.EventId));
        Assert.All(events, e => Assert.NotEqual("", e.Name));
        Assert.All(events, e => Assert.Equal("bayern", e.Region));
        Assert.Equal(events.Count, events.Select(e => e.EventId).Distinct().Count());
    }

    /// <summary>
    /// Die ANSCHRIFT ist der Grund, die Seite zu lesen statt nur den Feed: sie steht je Zeile da,
    /// oft mit Postleitzahl, und der Feed nennt sie fast nie.
    /// </summary>
    [Fact]
    public void ParsePage_ReadsTheAddressFromEachRow()
    {
        var events = ParsePage();

        var withPlace = events.Count(e => e.Place is { Length: > 0 });
        Assert.True(withPlace >= 17, $"Nur {withPlace} von {events.Count} mit Ort");
        Assert.Equal("Caissa Center München, Frankfurter Ring 193a, 80807 München",
            events.First(e => e.Name.StartsWith("CCM Monatliches")).Place);
    }

    /// <summary>
    /// Der Termin kommt aus dem <c>title</c>-Attribut, nicht aus dem Datumsblock: dort steht er
    /// vollstaendig („12.09.2026 10:00–13.09.2026 16:30"), waehrend der Block bei mehrtaegigen
    /// Turnieren abkuerzt („12. - 13.09.2026") und ueber den Monatswechsel raten liesse.
    /// </summary>
    [Fact]
    public void ParsePage_TakesTheFullDateRangeFromTheTitleAttribute()
    {
        var multiDay = ParsePage().First(e => e.Name == "CCM Sprint-Cup VIII");

        Assert.Equal(new DateOnly(2026, 9, 12), multiDay.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 13), multiDay.EndDate);
    }

    [Fact]
    public void ParsePage_SingleDayEvent_StartsAndEndsOnTheSameDay()
    {
        var oneDay = ParsePage().First(e => e.Name.StartsWith("CCM Monatliches"));

        Assert.Equal(new DateOnly(2026, 9, 10), oneDay.StartDate);
        Assert.Equal(oneDay.StartDate, oneDay.EndDate);
    }

    [Fact]
    public void ParsePage_BuildsTheDetailUrlFromTheSlug()
    {
        var e = ParsePage().First(x => x.Name.StartsWith("CCM Monatliches"));

        Assert.Equal("ccm-monatliches-rapidturnier-10-september-2026-12-3", e.EventId);
        Assert.Equal(
            "https://www.schachbund.de/turnierdetails/ccm-monatliches-rapidturnier-10-september-2026-12-3.html",
            e.Url);
    }

    [Fact]
    public void ParsePage_EmptyPage_ReturnsNothing() =>
        Assert.Empty(SchachbundCalendarService.ParsePage("<html><body>nichts</body></html>", "bayern"));

    // ----- Die Regionen ------------------------------------------------------

    /// <summary>
    /// Die Regionen werden GELESEN, nicht geraten: gerade in den Sonderkategorien (Fernschach,
    /// Problemschach, Schach960) liegt der Wert dieser Quelle, und eine fest eingebaute Liste
    /// uebersaehe eine neue stillschweigend.
    /// </summary>
    [Fact]
    public void ParseRegions_ReadsThemFromTheNavigation()
    {
        var regions = SchachbundCalendarService.ParseRegions(Fixture("schachbund-bayern.html"));

        Assert.True(regions.Count >= 20, $"Nur {regions.Count} Regionen");
        Assert.Contains("bayern", regions);
        Assert.Contains("fernschachbund", regions);
        Assert.Contains("schach960", regions);
    }

    /// <summary>
    /// Die Falle dieser Quelle: der FEED-Schluessel hat keine Bindestriche, der SEITEN-Schluessel
    /// schon. Mit Bindestrich antwortet der Feed 404 — betrifft fuenf der 25 Regionen.
    /// </summary>
    [Theory]
    [InlineData("nordrhein-westfalen", "nordrhein-westfalen", "nordrheinwestfalen")]
    [InlineData("rheinland-pfalz", "rheinland-pfalz", "rheinlandpfalz")]
    [InlineData("bayern", "bayern", "bayern")]
    public void PageKeyAndFeedKey_DifferWhereTheNameIsCompound(
        string region, string page, string feed)
    {
        Assert.Equal(page, SchachbundCalendarService.PageKey(region));
        Assert.Equal(feed, SchachbundCalendarService.FeedKey(region));
    }

    // ----- Die Ausschreibung -------------------------------------------------

    [Fact]
    public void ParseFeed_KeysTheAnnouncementsBySlug()
    {
        var feed = SchachbundCalendarService.ParseFeed(Fixture("schachbund-bayern-feed.xml"));

        Assert.NotEmpty(feed);
        Assert.Contains("ccm-monatliches-rapidturnier-10-september-2026-12-3", feed.Keys);
    }

    /// <summary>
    /// Rundenzahl, Bedenkzeit und System stehen nur in der Ausschreibung — und die ist Freitext
    /// des Veranstalters. Wo sie eine Gliederung hat, wird sie gelesen; wo nicht, bleibt das Feld
    /// leer statt geraten.
    /// </summary>
    [Fact]
    public void ParseFeed_ReadsRoundsTimeControlAndSystemFromTheAnnouncement()
    {
        var lines = SchachbundCalendarService.ParseFeed(Fixture("schachbund-bayern-feed.xml"))
            ["ccm-monatliches-rapidturnier-10-september-2026-12-3"];

        Assert.Equal(5, SchachbundCalendarService.RoundsOf(lines));
        Assert.Equal("12 Minuten + 3 Sekunden/Zug", SchachbundCalendarService.TimeControlOf(lines));
        Assert.Equal("swiss", SchachbundCalendarService.SystemOf(lines));
        // Der Feed gliedert die Anschrift in drei Zeilen — die Seite zieht sie zu einer zusammen.
        Assert.Equal("Caissa Center München, Frankfurter Ring 193a, 2. Stock West, 80807 München",
            SchachbundCalendarService.PlaceOf(lines));
    }

    /// <summary>
    /// Nur vier von 96 Ausschreibungen sind ueberhaupt gegliedert — der Rest ist Werbetext. Er
    /// darf nichts erfinden.
    /// </summary>
    [Fact]
    public void PlaceOf_AnUnstructuredAnnouncement_YieldsNothing()
    {
        var prose = SchachbundCalendarService.Describe(
            "<p>Das Turnier findet im Kulturhaus statt. Es gibt Kuchen.</p>");

        Assert.Null(SchachbundCalendarService.PlaceOf(prose));
    }

    [Theory]
    [InlineData("5 Runden Beschleunigtes Schweizer System", 5)]
    [InlineData("7-rundiges Open", null)]
    [InlineData("Es werden 9 Runden gespielt", 9)]
    [InlineData("Ein Turnier ohne Angabe", null)]
    public void RoundsOf_ReadsTheNumberBeforeTheWord(string line, int? expected) =>
        Assert.Equal(expected, SchachbundCalendarService.RoundsOf([line]));

    [Theory]
    [InlineData("Bedenkzeit: 90 Minuten für 40 Züge + 30 Minuten", "90 Minuten für 40 Züge + 30 Minuten")]
    [InlineData("bedenkzeit 15 min", "15 min")]
    [InlineData("Ein Turnier ohne Angabe", null)]
    public void TimeControlOf_ReadsWhatFollowsTheLabel(string line, string? expected) =>
        Assert.Equal(expected, SchachbundCalendarService.TimeControlOf([line]));

    [Theory]
    [InlineData("5 Runden Schweizer System", "swiss")]
    [InlineData("Vollrundig, jeder gegen jeden", "roundRobin")]
    [InlineData("Ein Turnier ohne Angabe", null)]
    public void SystemOf_ReadsTheSystem(string line, string? expected) =>
        Assert.Equal(expected, SchachbundCalendarService.SystemOf([line]));

    // ----- Ziel-Pruefung -----------------------------------------------------

    [Fact]
    public void EnsureAllowedTarget_RefusesAnyOtherHostAndPlainHttp()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SchachbundCalendarService.EnsureAllowedTarget(new Uri("https://example.com/x.xml")));
        Assert.Throws<InvalidOperationException>(() =>
            SchachbundCalendarService.EnsureAllowedTarget(new Uri("http://www.schachbund.de/x.xml")));

        SchachbundCalendarService.EnsureAllowedTarget(
            new Uri("https://www.schachbund.de/turnierdatenbank-bayern.html"));
    }

    /// <summary>Die Quelle nennt in ihrer robots.txt eine Wartezeit, und die gilt hier.</summary>
    [Fact]
    public void CrawlDelay_IsTheOneTheSourceAsksFor() =>
        Assert.Equal(TimeSpan.FromSeconds(5), SchachbundCalendarService.CrawlDelay);
}
