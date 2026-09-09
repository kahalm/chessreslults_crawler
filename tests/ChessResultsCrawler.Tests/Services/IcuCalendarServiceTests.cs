using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Der Kalender der Irish Chess Union — 81 kuenftige Turniere, rund 94 % davon nicht auf
/// chess-results, und die Koordinaten stehen im Kartenblock DERSELBEN Seite.
///
/// <para>Die Vorlage ist eine gekuerzte echte Trefferseite: zehn Zeilen, ausgesucht nach den
/// Faellen, die schiefgehen koennen — jahreslose und jahresbehaftete Termine, Zeitraeume ueber
/// eine Monatsgrenze, zwei Bedenkzeiten in EINEM Feld, „TBA" als Ort, ein Auslandsturnier, eine
/// nordirische Anschrift und drei Eintraege, die gar keine Turniere sind.</para>
/// </summary>
public class IcuCalendarServiceTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    /// <summary>
    /// 2026 ist das Jahr, das die echte Seite am 2026-09-09 als „From" gesetzt hatte — und damit
    /// das Jahr jeder Zeile, die keines nennt.
    /// </summary>
    private static async Task<List<ParsedIcuEvent>> ListAsync() =>
        (await IcuCalendarService.ParseListAsync(Fixture("icu-events-page1.html"), 2026)).Events;

    private static async Task<ParsedIcuEvent> RowAsync(string eventId) =>
        (await ListAsync()).Single(e => e.EventId == eventId);

    // ----- Die Trefferliste --------------------------------------------------

    [Fact]
    public async Task ParseList_ReadsEveryRow()
    {
        var events = await ListAsync();

        Assert.Equal(10, events.Count);
        Assert.All(events, e => Assert.Matches(@"^\d+$", e.EventId));
        Assert.All(events, e => Assert.NotEqual("", e.Name));
        Assert.All(events, e => Assert.Equal($"https://www.icu.ie/events/{e.EventId}", e.Url));
        Assert.Equal(events.Count, events.Select(e => e.EventId).Distinct().Count());
    }

    /// <summary>
    /// Die Fusszeile („1-20 of 81 ∙ next ∙ last") ist das einzige Blaetter-Signal; auf der
    /// letzten Seite fehlt sie ganz. Ohne diese Auskunft holte der Durchgang entweder zu wenig
    /// oder liefe gegen den Deckel.
    /// </summary>
    [Fact]
    public async Task ParseList_ReportsThatAFurtherPageFollows() =>
        Assert.True((await IcuCalendarService.ParseListAsync(
            Fixture("icu-events-page1.html"), 2026)).HasNext);

    [Fact]
    public async Task ParseList_KeepsNameAndSingleDayDate()
    {
        var row = await RowAsync("2305");

        Assert.Equal("3rd Raharney Rapid FIDE Open", row.Name);
        Assert.Equal(new DateOnly(2026, 9, 12), row.StartDate);
        Assert.Equal(row.StartDate, row.EndDate);
        Assert.Equal("Raharney Chess Club", row.Place);
    }

    /// <summary>
    /// „12–13 Sep" nennt den Monat nur EINMAL, und zwar hinten — der Anfang haette sonst gar
    /// keinen.
    /// </summary>
    [Fact]
    public async Task ParseList_ARangeInsideOneMonth_TakesTheMonthFromTheEnd()
    {
        var row = await RowAsync("2181");

        Assert.Equal(new DateOnly(2026, 9, 12), row.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 13), row.EndDate);
    }

    /// <summary>„16 Sep – 2 Dec" — beide Seiten nennen ihren Monat, das Jahr keine von beiden.</summary>
    [Fact]
    public async Task ParseList_ARangeAcrossMonths_KeepsBothMonths()
    {
        var row = await RowAsync("2328");

        Assert.Equal(new DateOnly(2026, 9, 16), row.StartDate);
        Assert.Equal(new DateOnly(2026, 12, 2), row.EndDate);
    }

    /// <summary>
    /// Steht ein Jahr da, gehoert es ans ENDE des Zeitraums — und gilt fuer beide Seiten, solange
    /// der Zeitraum keinen Jahreswechsel enthaelt.
    /// </summary>
    [Fact]
    public async Task ParseList_AnExplicitYearAppliesToBothEnds()
    {
        var row = await RowAsync("2205");

        Assert.Equal(new DateOnly(2027, 1, 29), row.StartDate);
        Assert.Equal(new DateOnly(2027, 2, 7), row.EndDate);
        Assert.Equal(new DateOnly(2030, 10, 25), (await RowAsync("2199")).StartDate);
    }

    /// <summary>
    /// Mehrere Bedenkzeiten stehen in EINEM Feld, durch Komma getrennt („Rapid, Blitz") — als ein
    /// Schlagwort gelesen passte keines mehr auf eine Klasse.
    /// </summary>
    [Fact]
    public async Task ParseList_SplitsTheTimeControlsInsideOneField()
    {
        var row = await RowAsync("2189");

        Assert.Contains("Rapid", row.Categories);
        Assert.Contains("Blitz", row.Categories);
        Assert.Contains("FIDE-rated", row.Categories);
        Assert.DoesNotContain(row.Categories, c => c.Contains(','));
    }

    /// <summary>
    /// Der Ortstext dieser Quelle ist oft die VOLLE Anschrift — bei nordirischen Spielorten mit
    /// britischer Postleitzahl, was fuer die Verortung wichtig ist (siehe der Sweep).
    /// </summary>
    [Fact]
    public async Task ParseList_KeepsTheFullAddressIncludingThePostcode()
    {
        var place = (await RowAsync("2259")).Place;

        Assert.Contains("Groomsport", place);
        Assert.Contains("BT19 6JR", place);
    }

    /// <summary>
    /// „TBA" ist kein Ort, sondern die Ansage, dass es noch keinen gibt — 20 der 81 Zeilen stehen
    /// so da. Steht daneben doch etwas („TBA - Dublin"), ist DAS die Auskunft.
    /// </summary>
    [Fact]
    public async Task ParseList_TbaIsNoPlace_ButWhatStandsNextToItIs()
    {
        Assert.Null((await RowAsync("2190")).Place);
        Assert.Null((await RowAsync("2199")).Place);
        Assert.Equal("Dublin", (await RowAsync("2189")).Place);
    }

    /// <summary>
    /// Unterricht, Lehrgang und Jahreshauptversammlung stehen in derselben Liste wie die
    /// Turniere. Die beiden Unterrichtsreihen laufen ueber 78 bzw. 84 Tage und verdeckten im
    /// Kalender ein Vierteljahr.
    /// </summary>
    [Fact]
    public async Task ParseList_MarksWhatIsNotATournament()
    {
        var events = await ListAsync();

        Assert.Equal(
            ["2190", "2315", "2328"],
            events.Where(e => e.NonTournament).Select(e => e.EventId).Order().ToList());
        Assert.False((await RowAsync("2305")).NonTournament);
        Assert.False((await RowAsync("2221")).NonTournament);
    }

    [Fact]
    public async Task ParseList_SkipsRowsWithoutAUsableDateInsteadOfThrowing()
    {
        var (events, hasNext) = await IcuCalendarService.ParseListAsync(
            """
            <table id="results"><tbody>
            <tr><td>irgendwann</td><td><a href="/events/1">A</a></td><td>Cork</td></tr>
            <tr><td>3 Oct</td><td><a href="/events/2">B</a></td><td>Cork</td></tr>
            <tr><td>4 Oct</td><td>ohne Verweis</td><td>Cork</td></tr>
            </tbody></table>
            """, 2026);

        Assert.Equal("2", Assert.Single(events).EventId);
        Assert.False(hasNext);
    }

    // ----- Der Termin fuer sich ---------------------------------------------

    /// <summary>
    /// Der Jahreswechsel INNERHALB eines Zeitraums, in beiden Schreibweisen. Ohne diese
    /// Unterscheidung landete eine Seite zwoelf Monate daneben — und die Quelle schreibt das
    /// Jahr grundsaetzlich nur ans Ende, wenn ueberhaupt.
    /// </summary>
    [Theory]
    [InlineData("28 Dec – 3 Jan", 2026, "2026-12-28", "2027-01-03")]
    [InlineData("28 Dec – 3 Jan 2027", 2026, "2026-12-28", "2027-01-03")]
    [InlineData("12 Sep", 2026, "2026-09-12", "2026-09-12")]
    [InlineData("31 Jul – 8 Aug 2027", 2026, "2027-07-31", "2027-08-08")]
    [InlineData("2–5 Jan 2027", 2026, "2027-01-02", "2027-01-05")]
    public void ParseDateRange_ReadsTheSourcesShorthand(
        string text, int defaultYear, string start, string end)
    {
        var range = IcuCalendarService.ParseDateRange(text, defaultYear);

        Assert.NotNull(range);
        Assert.Equal(DateOnly.Parse(start), range!.Value.Start);
        Assert.Equal(DateOnly.Parse(end), range.Value.End);
    }

    [Theory]
    [InlineData("")]
    [InlineData("demnaechst")]
    [InlineData("31 Feb")]
    [InlineData("12 Foo")]
    public void ParseDateRange_RefusesWhatItCannotRead(string text) =>
        Assert.Null(IcuCalendarService.ParseDateRange(text, 2026));

    // ----- Die Koordinaten aus derselben Seite -------------------------------

    /// <summary>
    /// Der Sonderwert dieser Quelle: die Kartenmarken liegen als JSON in der Trefferseite, ein
    /// Zusatzabruf entfaellt. Zugeordnet wird ueber den Turnier-Verweis im Popup-Text — die Marken
    /// stehen NICHT parallel zu den Zeilen (hoechstens 20 je Seite).
    /// </summary>
    [Fact]
    public void ParseMapData_JoinsTheMarkersByTournamentNumber()
    {
        var points = IcuCalendarService.ParseMapData(Fixture("icu-events-page1.html"));

        Assert.Equal(5, points.Count);
        Assert.Equal(53.5242079, points["2305"].Lat, 5);
        Assert.Equal(-7.0968796, points["2305"].Lon, 5);
        Assert.DoesNotContain("2221", points.Keys);   // Samarkand: von der Quelle nicht verortet
    }

    /// <summary>
    /// Die Quelle schickt die Koordinaten als ZEICHENKETTE. 0/0 liegt im Golf von Guinea — dort
    /// landet ein leeres Feld, nicht ein Turnier; und eine Marke ohne Turnier-Verweis ist nicht
    /// zuordenbar.
    /// </summary>
    [Fact]
    public void ParseMapData_RejectsTheNullIslandAndUnassignableMarkers()
    {
        var points = IcuCalendarService.ParseMapData(
            """
            <script id="map-data" type="application/json">{"markers":[
              {"lat":"0","lng":"0","popupHtml":"<a href=\"/events/1\">A</a>"},
              {"lat":"999","lng":"10","popupHtml":"<a href=\"/events/2\">B</a>"},
              {"lat":"53.5","lng":"-8.1","popupHtml":"kein Verweis"},
              {"lat":"51.9","lng":"-8.47","popupHtml":"<a href=\"/events/4\">D</a>"}]}</script>
            """);

        Assert.Equal("4", Assert.Single(points).Key);
        Assert.Equal(51.9, points["4"].Lat, 5);
    }

    [Fact]
    public void ParseMapData_WithoutAMapBlock_IsEmptyInsteadOfThrowing()
    {
        Assert.Empty(IcuCalendarService.ParseMapData("<html><body>nichts</body></html>"));
        Assert.Empty(IcuCalendarService.ParseMapData(
            """<script id="map-data" type="application/json">{kaputt</script>"""));
    }

    // ----- Die Detailseite ---------------------------------------------------

    /// <summary>
    /// Der einzige EXAKTE Zuordnungsschluessel dieser Quelle — und er steht mal auf einem Spiegel
    /// („s2.chess-results.com") und mal mit grossem „Tnr". Selten (2 von 28 Seiten), aber wo er
    /// steht, muss er gelesen werden.
    /// </summary>
    [Fact]
    public async Task ParseDetail_ReadsTheChessResultsNumberEntriesAndWebsite()
    {
        var detail = await IcuCalendarService.ParseDetailAsync(Fixture("icu-event-2305.html"));

        Assert.Equal("1491223", detail.ChessResultsId);
        Assert.Equal(28, detail.PlayerCount);
        Assert.Equal("https://n91chess.com/3rdRapid", detail.Website);
    }

    /// <summary>
    /// Der haeufige Fall: eine Seite ohne Paarungs-Verweis und ohne Meldeliste. Sie ist kein
    /// Fehler, sie hat nur nichts, was die Trefferliste nicht schon sagt.
    /// </summary>
    [Fact]
    public async Task ParseDetail_APageWithoutThoseFields_IsEmptyNotBroken()
    {
        var detail = await IcuCalendarService.ParseDetailAsync(Fixture("icu-event-2266.html"));

        Assert.Null(detail.ChessResultsId);
        Assert.Null(detail.PlayerCount);
        Assert.Null(detail.Website);
    }

    // ----- Ziel-Pruefung -----------------------------------------------------

    [Fact]
    public void EnsureAllowedTarget_RefusesAnyOtherHostAndPlainHttp()
    {
        Assert.Throws<InvalidOperationException>(() =>
            IcuCalendarService.EnsureAllowedTarget(new Uri("https://example.com/events")));
        Assert.Throws<InvalidOperationException>(() =>
            IcuCalendarService.EnsureAllowedTarget(new Uri("http://www.icu.ie/events")));

        IcuCalendarService.EnsureAllowedTarget(new Uri("https://www.icu.ie/events?page=2"));
    }

    /// <summary>
    /// Die Quelle nennt in ihrer robots.txt keine Wartezeit — dies ist die Selbstbeschraenkung
    /// gegenueber einer kleinen Verbandsseite, und sie soll nicht versehentlich wegfallen.
    /// </summary>
    [Fact]
    public void CrawlDelay_StaysAPoliteFiveSeconds() =>
        Assert.Equal(TimeSpan.FromSeconds(5), IcuCalendarService.CrawlDelay);
}
