using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Der Kalender des niederlaendischen Verbands (KNSB) — 177 kuenftige Eintraege gegen 12 auf
/// chess-results, aber ohne Enddatum, Ort, Anschrift, Rundenzahl oder Teilnehmerzahl: die Liste
/// dieser Quelle (WordPress-Kern-REST, kein Plugin) liefert sie strukturell nicht.
/// </summary>
public class KnsbCalendarServiceTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static List<ParsedKnsbEvent> Events() =>
        KnsbCalendarService.ParseEvents(Fixture("knsb-events.json"));

    [Fact]
    public void ParseEvents_ReadsEveryRow()
    {
        var events = Events();

        Assert.Equal(13, events.Count);
        Assert.All(events, e => Assert.NotEqual("", e.Slug));
        Assert.All(events, e => Assert.NotEqual("", e.Name));
        Assert.Equal(events.Count, events.Select(e => e.Slug).Distinct().Count());
    }

    /// <summary>
    /// Titel kommen mit HTML-Entitaeten ("Maasstad &amp;#8217;87"). Ohne Aufloesung stuenden sie
    /// so im Kalender.
    /// </summary>
    [Fact]
    public void ParseEvents_DecodesTheHtmlEntitiesInTheTitle()
    {
        var names = Events().Select(e => e.Name).ToList();

        Assert.DoesNotContain(names, n => n.Contains("&#") || n.Contains("&amp;"));
        Assert.Contains(names, n => n.Contains("Maasstad ’87"));
    }

    /// <summary>Die Liste hat kein Datumsfeld — das Startdatum steckt als Suffix im Slug.</summary>
    [Fact]
    public void ParseEvents_ReadsTheStartDateFromTheSlugSuffix()
    {
        var e = Events().Single(x => x.Slug == "zomeravondcompetitie-2027-07-19");

        Assert.Equal(new DateOnly(2027, 7, 19), e.StartDate);
    }

    /// <summary>
    /// "onk-nsvg-2026-2026-11-26" nennt "2026" ZWEIMAL: einmal als Teil des Turniernamens, einmal
    /// als Jahr des Datumssuffix. Der Anker <c>$</c> muss den TATSAECHLICHEN Suffix treffen, nicht
    /// die erste Vierergruppe.
    /// </summary>
    [Fact]
    public void ParseEvents_DoesNotConfuseAYearInTheTitleWithTheDateSuffix()
    {
        var e = Events().Single(x => x.Slug == "onk-nsvg-2026-2026-11-26");

        Assert.Equal(new DateOnly(2026, 11, 26), e.StartDate);
        Assert.Equal("ONK NSVG 2026", e.Name);
    }

    [Fact]
    public void ParseEvents_KeepsTheUrl()
    {
        var e = Events().Single(x => x.Slug == "zomeravondcompetitie-2027-07-19");

        Assert.Equal("https://schaakbond.nl/event/zomeravondcompetitie-2027-07-19/", e.Url);
    }

    // ----- Bedenkzeit ----------------------------------------------------------

    /// <summary>
    /// Die "speed"-Taxonomie ist der eine Lichtblick dieser Quelle: sie steht STRUKTURIERT in der
    /// Liste, anders als bei den meisten Quellen des Projekts.
    /// </summary>
    [Fact]
    public void ParseEvents_ReadsTheSpeedFromTheTaxonomy()
    {
        var events = Events();

        Assert.Equal("Rapidschaak",
            events.Single(e => e.Slug == "zomeravondcompetitie-2027-07-19").Speed);
        Assert.Equal("Snelschaak",
            events.Single(e => e.Slug == "zomeravond-snelschaaktoernooi-2027-08-23").Speed);
        Assert.Equal("Normaalschaak",
            events.Single(e => e.Slug == "jeugdweekendtoernooi-leiden-2027-07-03").Speed);
        Assert.All(events, e => Assert.NotNull(e.Speed));
    }

    /// <summary>"Internetschaak" (Kategorie 61) markiert ein Turnier ohne Spielort.</summary>
    [Fact]
    public void ParseEvents_MarksInternetschaakAsOnline()
    {
        var events = Events();

        Assert.True(events.Single(e => e.Slug == "nk-online-schaken-voor-de-jeugd-fgh-2027-06-11").Online);
        Assert.False(events.Single(e => e.Slug == "zomeravondcompetitie-2027-07-19").Online);
        Assert.Equal(2, events.Count(e => e.Online));
    }

    // ----- Slugs ueber der PublicId-Laenge ------------------------------------

    /// <summary>
    /// Manche Slugs werden bis zu 81 Zeichen lang (gemessen: 5 von 177 ueber 60 Zeichen) — zu lang
    /// fuer eine direkte Ablage als <c>ExternalId</c>/<c>PublicId</c>. Dieser Test haelt nur fest,
    /// dass der PARSER selbst nichts kuerzt; das Kuerzen (per Hash) ist RookHub-seitige Aufgabe
    /// (<c>KnsbDirectorySweepService.PublicIdOf</c>).
    /// </summary>
    [Fact]
    public void ParseEvents_KeepsTheFullSlugEvenWhenLong()
    {
        var e = Events().Single(x => x.Slug ==
            "knsb-d-competitie-4e-ronde-tog-maasstad-87-voor-doven-en-slechthorende-2027-04-10");

        Assert.Equal(81, e.Slug.Length);
        Assert.Equal(new DateOnly(2027, 4, 10), e.StartDate);
    }

    // ----- Robustheit ----------------------------------------------------------

    [Fact]
    public void ParseEvents_SkipsRowsWithoutAParsableSlugInsteadOfThrowing()
    {
        var events = KnsbCalendarService.ParseEvents(
            """
            [{"slug":"kein-datum-hier","title":{"rendered":"A"}},
             {"slug":"","title":{"rendered":"B"}},
             {"slug":"c-2026-09-08","title":{"rendered":""}},
             {"slug":"d-2026-09-08","title":{"rendered":"D"}}]
            """);

        Assert.Equal("d-2026-09-08", Assert.Single(events).Slug);
    }

    [Fact]
    public void ParseEvents_ReturnsNothingForANonArrayBody() =>
        Assert.Empty(KnsbCalendarService.ParseEvents("""{"events":[]}"""));

    [Fact]
    public void StartDateFromSlug_RejectsAnUnplausibleDateInsteadOfThrowing() =>
        Assert.Null(KnsbCalendarService.StartDateFromSlug("toernooi-2026-13-40"));

    // ----- Ziel-Pruefung und Wartezeit ------------------------------------------

    [Fact]
    public void EnsureAllowedTarget_RefusesAnyOtherHostAndPlainHttp()
    {
        Assert.Throws<InvalidOperationException>(() =>
            KnsbCalendarService.EnsureAllowedTarget(new Uri("https://example.com/wp-json/")));
        Assert.Throws<InvalidOperationException>(() =>
            KnsbCalendarService.EnsureAllowedTarget(new Uri("http://schaakbond.nl/wp-json/")));

        KnsbCalendarService.EnsureAllowedTarget(
            new Uri("https://schaakbond.nl/wp-json/wp/v2/event"));
    }

    /// <summary>Die robots.txt der Quelle nennt "Crawl-delay: 15", und die gilt hier.</summary>
    [Fact]
    public void CrawlDelay_IsTheOneTheSourceAsksFor() =>
        Assert.Equal(TimeSpan.FromSeconds(15), KnsbCalendarService.CrawlDelay);
}
