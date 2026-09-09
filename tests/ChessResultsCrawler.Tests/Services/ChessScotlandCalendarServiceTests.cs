using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Der Kalender von Chess Scotland: 43-44 kuenftige Turniere in EINEM Abruf, gegen 8 auf
/// chess-results fuer SCO — und die Bedenkzeit-KLASSE steht als Schlagwort strukturiert dabei,
/// anders als bei jeder anderen Quelle des Projekts.
/// </summary>
public class ChessScotlandCalendarServiceTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static Task<List<ParsedChessScotlandEvent>> Events() =>
        ChessScotlandCalendarService.ParseListAsync(Fixture("chessscotland-upcoming.html"));

    // ----- Die Liste -----------------------------------------------------------

    [Fact]
    public async Task ParseListAsync_ReadsEveryRow()
    {
        var events = await Events();

        Assert.Equal(6, events.Count);
        Assert.All(events, e => Assert.NotEqual("", e.Slug));
        Assert.All(events, e => Assert.NotEqual("", e.Name));
        Assert.Equal(events.Count, events.Select(e => e.Slug).Distinct().Count());
    }

    [Fact]
    public async Task ParseListAsync_KeepsSingleDayAndRangeDates()
    {
        var events = await Events();

        var singleDay = events.Single(e => e.Slug == "nejca-albyn-trophy-2026");
        Assert.Equal(new DateOnly(2026, 9, 13), singleDay.StartDate);
        Assert.Equal(singleDay.StartDate, singleDay.EndDate);

        var range = events.Single(e => e.Slug == "ayr-congress-2026");
        Assert.Equal(new DateOnly(2026, 9, 11), range.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 13), range.EndDate);

        // Ueber einen Monats- UND Jahreswechsel hinweg ("Fri 28th April 2028 - Mon 1st May 2028") —
        // beide Seiten nennen ihr eigenes Jahr, nichts wird aus dem Kontext ergaenzt.
        var crossMonth = events.Single(e => e.Slug == "sct-perth-congress-2028");
        Assert.Equal(new DateOnly(2028, 4, 28), crossMonth.StartDate);
        Assert.Equal(new DateOnly(2028, 5, 1), crossMonth.EndDate);
    }

    /// <summary>
    /// Der Sonderwert dieser Quelle: die Bedenkzeit-KLASSE steht strukturiert dabei. Ein Kongress
    /// kann mehrere zugleich tragen (Hauptturnier + Blitz-Abend).
    /// </summary>
    [Fact]
    public async Task ParseListAsync_KeepsCategoriesAndTimeControls()
    {
        var events = await Events();

        var ayr = events.Single(e => e.Slug == "ayr-congress-2026");
        Assert.Equal(["Adult", "Junior"], ayr.Categories);
        Assert.Equal(["Standard", "Blitz", "Fide"], ayr.TimeControls);

        var hamilton = events.Single(e => e.Slug == "hamilton-allegro");
        Assert.Equal(["Adult", "Junior", "International"], hamilton.Categories);
        Assert.Equal(["Allegro", "Fide"], hamilton.TimeControls);
    }

    /// <summary>
    /// „Main 4NCL Online" hat KEIN Time-Controls-Absatz ueberhaupt (nicht bloss einen leeren) —
    /// ein Online-Event hat keine Bedenkzeit-Klasse notiert.
    /// </summary>
    [Fact]
    public async Task ParseListAsync_OnlineEvent_HasNoTimeControlParagraphAtAll()
    {
        var main4Ncl = (await Events()).Single(e =>
            e.Slug == "main-4ncl-online-s14-team-event-played-on-lichess-org");

        Assert.Equal(["Online"], main4Ncl.Categories);
        Assert.Empty(main4Ncl.TimeControls);
    }

    /// <summary>
    /// „Chess Scotland SGM" hat ueberhaupt keine Kategorie-/Bedenkzeit-Zeile (leeres Meta-Div) —
    /// beide Listen bleiben leer statt zu werfen.
    /// </summary>
    [Fact]
    public async Task ParseListAsync_RowWithoutAnyMetadata_YieldsEmptyLists()
    {
        var sgm = (await Events()).Single(e => e.Slug == "chess-scotland-sgm-scio-incorporation");

        Assert.Empty(sgm.Categories);
        Assert.Empty(sgm.TimeControls);
    }

    [Fact]
    public async Task ParseListAsync_SkipsUnusableRowsInsteadOfThrowing()
    {
        var events = await ChessScotlandCalendarService.ParseListAsync(
            """
            <div class="published">
             <a href="/calendar/no-date">No Date</a>
             <p>not a date</p>
            </div>
            <div class="published">
             <span>no link at all</span>
             <p>Mon 1st January 2027</p>
            </div>
            <div class="published">
             <a href="/calendar/valid-one">Valid One</a>
             <p>Mon 1st January 2027</p>
            </div>
            """);

        Assert.Equal("valid-one", Assert.Single(events).Slug);
    }

    // ----- Termin-Freitext -----------------------------------------------------

    [Theory]
    [InlineData("Tue 8th September 2026", "2026-09-08", "2026-09-08")]
    [InlineData("Fri 11th September 2026 - Sun 13th September 2026", "2026-09-11", "2026-09-13")]
    [InlineData("Fri 28th April 2028 - Mon 1st May 2028", "2028-04-28", "2028-05-01")]
    public void ParseDateRange_ParsesTheSourceFormat(string text, string start, string end)
    {
        var range = ChessScotlandCalendarService.ParseDateRange(text);

        Assert.NotNull(range);
        Assert.Equal(DateOnly.Parse(start), range!.Value.Start);
        Assert.Equal(DateOnly.Parse(end), range.Value.End);
    }

    [Theory]
    [InlineData("")]
    [InlineData("8th September 2026")]         // kein Wochentag
    [InlineData("Tue 8th September")]          // kein Jahr
    [InlineData("irgendein Text")]
    public void ParseDateRange_RejectsUnrecognisedText(string text) =>
        Assert.Null(ChessScotlandCalendarService.ParseDateRange(text));

    // ----- Die Detailseite -------------------------------------------------------

    /// <summary>
    /// Der haeufigste erfolgreiche Fall: eine kommaseparierte Zeile mit dem Ortsnamen VOR der
    /// Postleitzahl, ein Klammerzusatz dahinter wird abgeschnitten.
    /// </summary>
    [Fact]
    public void ParseDetail_ExtractsTheVenueLine_WhenAPostcodeIsPresent()
    {
        var detail = ChessScotlandCalendarService.ParseDetail(Fixture("chessscotland-detail-ayr.html"));

        Assert.NotNull(detail);
        Assert.Equal("Dalblair Road, Ayr. KA7 1UG", detail!.Venue);
    }

    /// <summary>Der haeufigere Fall (35 von 43 gemessen): „More details to follow" — kein Ort.</summary>
    [Fact]
    public void ParseDetail_ReturnsNullVenue_WhenTheAnnouncementNamesNoAddress()
    {
        var detail = ChessScotlandCalendarService.ParseDetail(Fixture("chessscotland-detail-hamilton.html"));

        Assert.NotNull(detail);
        Assert.Null(detail!.Venue);
    }

    /// <summary>
    /// Ein per Copy-Paste eingefuegtes Bild liegt als Base64-Daten-URI MITTEN im selben JSON — ein
    /// naiver Volltext-Regex-Treffer darauf fand hier zwei falsche „Postleitzahlen" aus reinen
    /// Zufallszeichen (gemessen an „glasgow-congress-2027"). Der echte Text nennt nur einen
    /// Gebaeudenamen ohne Postleitzahl, das Ergebnis muss also <c>null</c> bleiben.
    /// </summary>
    [Fact]
    public void ParseDetail_IgnoresEmbeddedImageData_AndDoesNotFabricateAPostcode()
    {
        var detail = ChessScotlandCalendarService.ParseDetail(Fixture("chessscotland-detail-glasgow.html"));

        Assert.NotNull(detail);
        Assert.Null(detail!.Venue);
    }

    [Fact]
    public void ParseDetail_ReturnsNull_WhenThereIsNoContentInput() =>
        Assert.Null(ChessScotlandCalendarService.ParseDetail("<html><body>nichts hier</body></html>"));

    // ----- Textbausteine -------------------------------------------------------

    /// <summary>
    /// Nur STRING-„insert"-Werte zaehlen; ein Bild-Embed (<c>{"image": "data:..."}</c>) traegt ein
    /// OBJEKT als „insert" und wird uebersprungen.
    /// </summary>
    [Fact]
    public void PlainTextOf_SkipsImageEmbeds()
    {
        var text = ChessScotlandCalendarService.PlainTextOf(
            """{"ops":[{"insert":"Vorher "},{"insert":{"image":"data:image/jpeg;base64,PI70VZAAAA"}},{"insert":" Nachher"}]}""");

        Assert.Equal("Vorher  Nachher", text);
        Assert.DoesNotContain("PI70VZ", text);
        Assert.DoesNotContain("data:image", text);
    }

    [Fact]
    public void VenueOf_StripsALabelPrefixAndATrailingParenthetical()
    {
        Assert.Equal(
            "Dalblair Road, Ayr. KA7 1UG",
            ChessScotlandCalendarService.VenueOf(
                "Dalblair Road, Ayr. KA7 1UG (see map on the Venue tab above)"));

        Assert.Equal(
            "Hazlehead Primary School, Provost Graham Ave, Aberdeen AB15 8HB",
            ChessScotlandCalendarService.VenueOf(
                "Venue: Hazlehead Primary School, Provost Graham Ave, Aberdeen AB15 8HB"));
    }

    [Fact]
    public void VenueOf_ReturnsNull_WithoutAnyRecognisablePostcode() =>
        Assert.Null(ChessScotlandCalendarService.VenueOf(
            "Some venue\nwill be announced soon\nno address yet"));

    // ----- Kennung -------------------------------------------------------------

    [Fact]
    public void PublicIdOf_IsStableShortAndDistinctPerSlug()
    {
        var a = ChessScotlandCalendarService.PublicIdOf("ayr-congress-2026");
        var b = ChessScotlandCalendarService.PublicIdOf("ayr-congress-2026");
        var c = ChessScotlandCalendarService.PublicIdOf("murrayfield-stadium-blitz-edinburgh-uk-blitz-qualifier");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.StartsWith("sc", a);
        Assert.True(a.Length <= 24, $"PublicId {a} ueberschreitet die 24-Zeichen-Spalte");
    }

    // ----- Ziel-Pruefung -----------------------------------------------------

    [Fact]
    public void EnsureAllowedTarget_RefusesAnyOtherHostAndPlainHttp()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ChessScotlandCalendarService.EnsureAllowedTarget(new Uri("https://example.com/calendar/upcoming")));
        Assert.Throws<InvalidOperationException>(() =>
            ChessScotlandCalendarService.EnsureAllowedTarget(new Uri("http://www.chessscotland.com/calendar/upcoming")));

        ChessScotlandCalendarService.EnsureAllowedTarget(
            new Uri("https://www.chessscotland.com/calendar/upcoming"));
    }
}
