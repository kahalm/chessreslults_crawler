using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Der Kalender des ungarischen Verbands (chess.hu). Sein Ertrag ist vor allem VORLAUF: ab
/// November 2026 fuehrt er 41 Turniere, wo chess-results 5 kennt.
/// </summary>
public class ChessHuCalendarServiceTests
{
    private static string Fixture() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "chess-hu-calendar.json"));

    private static List<ParsedChessHuEvent> Parse() => ChessHuCalendarService.Parse(Fixture());

    [Fact]
    public void Parse_ReadsEveryRow()
    {
        var events = Parse();

        Assert.Equal(16, events.Count);
        Assert.All(events, e => Assert.Matches(@"^\d+$", e.EventId));
        Assert.All(events, e => Assert.NotEqual("", e.Name));
        Assert.Equal(events.Count, events.Select(e => e.EventId).Distinct().Count());
    }

    /// <summary>
    /// Die Falle dieser Quelle: <c>from_date</c> ist <c>MM.DD</c> OHNE Jahr, das steht daneben in
    /// <c>from_year</c>. Wer nur das Datum liest, legt den halben Kalender ins laufende Jahr — und
    /// er reicht bis Juni 2027.
    /// </summary>
    [Fact]
    public void Parse_BuildsTheDateFromTheSeparateYearField()
    {
        var e = Parse().Single(x => x.EventId == "74262");

        Assert.Equal(new DateOnly(2026, 9, 7), e.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 13), e.EndDate);
    }

    [Theory]
    [InlineData("2026", "09.07", "2026-09-07")]
    [InlineData("2027", "06.11", "2027-06-11")]
    [InlineData("2026", "9.7", null)]      // die Quelle schreibt zweistellig; alles andere ist Raten
    [InlineData(null, "09.07", null)]
    [InlineData("2026", null, null)]
    [InlineData("26", "09.07", null)]
    public void ParseDate_NeedsBothHalves(string? year, string? monthDay, string? expected) =>
        Assert.Equal(expected is null ? null : DateOnly.Parse(expected),
            ChessHuCalendarService.ParseDate(year, monthDay));

    /// <summary>Ein Ende VOR dem Anfang ist ein Tippfehler der Quelle, kein Zeitraum.</summary>
    [Fact]
    public void Parse_IgnoresAnEndDateBeforeTheStart()
    {
        var events = ChessHuCalendarService.Parse(
            """
            [{"id":"1","name":"A","from_year":"2026","from_date":"09.10",
              "to_year":"2026","to_date":"09.02"}]
            """);

        var e = Assert.Single(events);
        Assert.Equal(e.StartDate, e.EndDate);
    }

    /// <summary>
    /// „Online" und „Helyszín később" („Ort spaeter") stehen im ORTS-Feld, sind aber keine Orte.
    /// Ohne diese Unterscheidung sucht die Verortung nach einem Ort namens „Online".
    /// </summary>
    [Theory]
    [InlineData("Siklós", true)]
    [InlineData("Budapest", true)]
    [InlineData("Online", false)]
    [InlineData("ONLINE (lichess)", false)]
    [InlineData("Helyszín később", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsVenue_SeparatesRealPlacesFromPlaceholders(string? place, bool expected) =>
        Assert.Equal(expected, ChessHuCalendarService.IsVenue(place));

    [Fact]
    public void Parse_MarksTheOnlineEntryAsHavingNoVenue()
    {
        var online = Parse().Single(e => e.Place == "Online");

        Assert.False(online.HasVenue);
        Assert.All(Parse().Where(e => e.Place is not null and not "Online"),
            e => Assert.True(e.HasVenue));
    }

    [Fact]
    public void Parse_ReadsTheFideFlag()
    {
        var events = Parse();

        Assert.Contains(events, e => e.FideRated);
        Assert.Contains(events, e => !e.FideRated);
    }

    /// <summary>Eine Zeile ohne Nummer, Namen oder Startdatum ist keine — und darf nicht werfen.</summary>
    [Fact]
    public void Parse_SkipsUnusableRowsInsteadOfThrowing()
    {
        var events = ChessHuCalendarService.Parse(
            """
            [{"id":"1","name":"","from_year":"2026","from_date":"09.10"},
             {"id":"","name":"A","from_year":"2026","from_date":"09.10"},
             {"id":"3","name":"C","from_date":"09.10"},
             {"id":"4","name":"D","from_year":"2026","from_date":"09.10"}]
            """);

        Assert.Equal("4", Assert.Single(events).EventId);
    }

    [Fact]
    public void EnsureAllowedTarget_RefusesAnyOtherHostAndPlainHttp()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ChessHuCalendarService.EnsureAllowedTarget(new Uri("https://example.com/app/x.json")));
        Assert.Throws<InvalidOperationException>(() =>
            ChessHuCalendarService.EnsureAllowedTarget(new Uri("http://chess.hu/app/x.json")));

        ChessHuCalendarService.EnsureAllowedTarget(new Uri("https://chess.hu/app/versenynaptar.json"));
    }
}
