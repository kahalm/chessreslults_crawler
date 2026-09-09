using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Der Kalender des slowakischen Verbands (SSZ) — die einzige gepruefte Quelle, die ausdruecklich
/// zur Nutzung einlaedt: dokumentierte REST-Schnittstelle, im Footer als „Free api specification"
/// verlinkt, und in der Beschreibung steht woertlich „If you need more, just ask for it".
///
/// <para>78 kuenftige Turniere, 20 Monate Vorlauf, 53 davon nicht auf chess-results.</para>
/// </summary>
public class SszCalendarServiceTests
{
    private static string Fixture() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sszz-tournaments.json"));

    private static List<ParsedSszEvent> Parse() => SszCalendarService.Parse(Fixture());

    [Fact]
    public void Parse_ReadsEveryTournament()
    {
        var events = Parse();

        Assert.Equal(78, events.Count);
        Assert.All(events, e => Assert.Matches(@"^\d+$", e.EventId));
        Assert.All(events, e => Assert.NotEqual("", e.Name));
        Assert.Equal(78, events.Select(e => e.EventId).Distinct().Count());
        Assert.All(events, e => Assert.True(e.EndDate >= e.StartDate, $"Ende vor Beginn bei {e.Name}"));
    }

    /// <summary>
    /// Alle Werte kommen als ZEICHENKETTE aus dieser Schnittstelle — auch die Nummer
    /// (<c>"tournamentId": "5909"</c>). Wer eine Zahl erwartet, bekommt einen
    /// Deserialisierungsfehler und keine Turniere.
    /// </summary>
    [Fact]
    public void Parse_HandlesTheStringTypedId()
    {
        var first = Assert.Single(Parse(), e => e.EventId == "5909");

        Assert.Equal("Majstrovstvá SR amatérov 2026", first.Name);
        Assert.Equal(new DateOnly(2026, 9, 10), first.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 13), first.EndDate);
        Assert.Equal("Prešov", first.Place);
        Assert.Equal("SVK", first.Country);
        Assert.Equal("1482875", first.ChessResultsId);
    }

    /// <summary>
    /// Der praktische Gewinn: ein Teil der Eintraege liefert die chess-results-Nummer mit — ein
    /// EXAKTER Zuordnungsschluessel statt Namensraterei. Bei den uebrigen gibt es sie nicht, weil
    /// es die Turniere dort (noch) nicht gibt.
    /// </summary>
    [Fact]
    public void Parse_ReadsTheTournamentNumberWhereItExists()
    {
        var events = Parse();

        var withTnr = events.Count(e => e.ChessResultsId is not null);
        Assert.InRange(withTnr, 10, events.Count - 10);
        Assert.All(events.Where(e => e.ChessResultsId is not null),
            e => Assert.Matches(@"^\d+$", e.ChessResultsId!));
    }

    /// <summary>
    /// Der Wirt des Verweises ist eine der Server-Unterdomaenen (s1…s3), nicht chess-results.com
    /// selbst — geprueft wird deshalb nicht auf den Anfang der URL.
    /// </summary>
    [Theory]
    [InlineData("https://s3.chess-results.com/tnr1482875.aspx?lan=4&SNode=S0", "1482875")]
    [InlineData("https://s1.chess-results.com/Tnr999.aspx", "999")]
    [InlineData("http://chess-results.com/tnr1.aspx", "1")]
    [InlineData("https://example.com/tnr1.aspx", null)]
    [InlineData("https://s3.chess-results.com/spieler.aspx", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void TournamentIdFrom_ReadsAnyServerSubdomain(string? url, string? expected) =>
        Assert.Equal(expected, SszCalendarService.TournamentIdFrom(url));

    /// <summary>
    /// Der Kalender fuehrt vereinzelt Auslandstermine — der Aufrufer muss also nach Land filtern
    /// koennen, statt alles als slowakisch einzutragen.
    /// </summary>
    [Fact]
    public void Parse_KeepsTheCountrySoForeignEventsCanBeTold()
    {
        var events = Parse();

        Assert.Contains(events, e => e.Country == "SVK");
        Assert.All(events, e => Assert.False(string.IsNullOrWhiteSpace(e.Country)));
    }

    /// <summary>Ort ist bei jedem Eintrag da — er ist die Grundlage der Verortung.</summary>
    [Fact]
    public void Parse_PlaceIsAlwaysPresent() =>
        Assert.All(Parse(), e => Assert.False(string.IsNullOrWhiteSpace(e.Place), $"kein Ort bei {e.Name}"));

    [Fact]
    public void Parse_EmptyOrBrokenJson_YieldsNothing()
    {
        Assert.Empty(SszCalendarService.Parse("[]"));
        Assert.Empty(SszCalendarService.Parse("""[{"nazov":"ohne Nummer","termin_od":"2026-09-10"}]"""));
        Assert.Empty(SszCalendarService.Parse("""[{"tournamentId":"1","nazov":"ohne Datum"}]"""));
    }

    [Theory]
    [InlineData("http://www.chess.sk/api/x")]
    [InlineData("https://chess.sk/api/x")]      // ohne www ist ein anderer Host
    [InlineData("https://example.com/x")]
    public void EnsureAllowedTarget_RefusesAnythingElse(string url) =>
        Assert.Throws<InvalidOperationException>(() =>
            SszCalendarService.EnsureAllowedTarget(new Uri(url)));
}
