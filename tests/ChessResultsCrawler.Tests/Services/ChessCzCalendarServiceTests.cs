using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Der Terminkalender des tschechischen Verbands (chess.cz). Die billigste Quelle der Reihe — ein
/// Abruf ohne Formular, ohne Paginierung — und die mit dem kleinsten Volumen. Ihr eigentlicher
/// Wert sind die LIGARUNDEN: Spieltermine, fuer die sonst je Turnier eine eigene
/// chess-results-Seite geholt werden muesste.
/// </summary>
public class ChessCzCalendarServiceTests
{
    private static string Fixture() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "chess-cz-calendar.html"));

    private static Task<List<ParsedChessCzEvent>> ParseAsync() =>
        ChessCzCalendarService.ParseAsync(Fixture());

    /// <summary>
    /// Die Seite fuehrt dieselbe Zeile in mehreren Laschen. Ohne Entdopplung ueber den Slug
    /// bekaeme jeder zweite Eintrag ein Duplikat.
    /// </summary>
    [Fact]
    public async Task ParseAsync_DeduplicatesTheRowsThatAppearInSeveralTabs()
    {
        var events = await ParseAsync();

        Assert.NotEmpty(events);
        Assert.Equal(events.Count, events.Select(e => e.EventId).Distinct().Count());
        Assert.All(events, e => Assert.NotEqual("", e.Name));
    }

    [Fact]
    public async Task ParseAsync_ReadsNamePlaceAndCountry()
    {
        var e = (await ParseAsync()).Single(x => x.EventId == "bratislava-norm-week");

        Assert.Equal("Bratislava Norm Week", e.Name);
        Assert.Equal("Bratislava", e.Place);
        Assert.Equal("SK", e.Country);
        Assert.Equal("1453538", e.ChessResultsId);
        Assert.Equal(new DateOnly(2026, 9, 5), e.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 11), e.EndDate);
    }

    /// <summary>
    /// Der Grund, warum diese Quelle sich lohnt: „šachy.cz Extraliga – 3. kolo" ist Runde 3
    /// derselben Meisterschaft, und alle elf Runden nennen dieselbe chess-results-Nummer. Das ist
    /// ein fertiger Rundenplan — sonst kostet er einen Seitenabruf je Turnier.
    /// </summary>
    [Fact]
    public async Task ParseAsync_RecognisesLeagueRoundsAndBindsThemToTheirSeries()
    {
        var rounds = (await ParseAsync())
            .Where(e => e.SeriesName == "šachy.cz Extraliga")
            .OrderBy(e => e.RoundNumber)
            .ToList();

        Assert.True(rounds.Count >= 8, $"Nur {rounds.Count} Runden erkannt");
        Assert.Equal(Enumerable.Range(1, rounds.Count).ToList(),
            rounds.Select(r => r.RoundNumber!.Value).ToList());
        Assert.All(rounds, r => Assert.Equal("1464172", r.ChessResultsId));
        // Eine Ligarunde wird dezentral gespielt — sie nennt keinen Ort.
        Assert.All(rounds, r => Assert.Null(r.Place));
    }

    /// <summary>Und was keine Runde ist, bekommt auch keine Rundennummer.</summary>
    [Fact]
    public async Task ParseAsync_LeavesOrdinaryTournamentsWithoutARoundNumber()
    {
        var e = (await ParseAsync()).Single(x => x.EventId == "bratislava-norm-week");

        Assert.Null(e.RoundNumber);
        Assert.Null(e.SeriesName);
    }

    /// <summary>
    /// Jeder fuenfte Eintrag ist gar kein Turnier: Schiedsrichter- und Trainerschulungen,
    /// Trainingslager, Arbeitstreffen.
    /// </summary>
    [Fact]
    public async Task ParseAsync_FlagsTheEntriesThatAreNoTournaments()
    {
        var events = await ParseAsync();

        Assert.Contains(events, e => e.NonTournament && e.Name.Contains("Školení"));
        Assert.DoesNotContain(events, e => e.NonTournament && e.EventId == "bratislava-norm-week");
    }

    /// <summary>
    /// Die Jugend-Lasche ist verlaesslich — und sie traegt einen Fall, den kein Namensmuster
    /// faengt: „Mistrovství Čech 8 – 10 let" nennt seine Altersklasse als Spanne, ohne das Wort
    /// „mládež".
    /// </summary>
    [Fact]
    public async Task ParseAsync_TakesTheYouthMarkFromTheYouthTab()
    {
        var events = await ParseAsync();

        var youth = events.Where(e => e.Youth).ToList();
        Assert.NotEmpty(youth);
        Assert.Contains(youth, e => e.Name.Contains("8 – 10 let"));
        Assert.False(events.Single(e => e.EventId == "bratislava-norm-week").Youth);
    }

    /// <summary>
    /// Die drei Formen, in denen diese Quelle einen Zeitraum schreibt. Das Jahr steht EINMAL am
    /// Ende und zweistellig.
    /// </summary>
    [Theory]
    [InlineData("12. 9. 26", "2026-09-12", "2026-09-12")]
    [InlineData("5. - 11. 9. 26", "2026-09-05", "2026-09-11")]
    [InlineData("31. 10. - 7. 11. 26", "2026-10-31", "2026-11-07")]
    [InlineData("16. - 23. 1. 27", "2027-01-16", "2027-01-23")]
    // Ueber den JAHRESwechsel gehoert das genannte Jahr zum Ende.
    [InlineData("28. 12. - 4. 1. 27", "2026-12-28", "2027-01-04")]
    public void ParseSpan_ReadsAllThreeShapes(string text, string start, string end)
    {
        var span = ChessCzCalendarService.ParseSpan(text);

        Assert.NotNull(span);
        Assert.Equal(DateOnly.Parse(start), span!.Value.Start);
        Assert.Equal(DateOnly.Parse(end), span.Value.End);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("demnaechst")]
    [InlineData("31. 2. 26")]     // den gibt es nicht
    [InlineData("12. 13. 26")]    // Monat 13
    public void ParseSpan_RefusesWhatIsNoDate(string? text) =>
        Assert.Null(ChessCzCalendarService.ParseSpan(text));

    [Fact]
    public async Task ParseAsync_EmptyPage_ReturnsNothing() =>
        Assert.Empty(await ChessCzCalendarService.ParseAsync("<html><body>nichts</body></html>"));

    [Fact]
    public void EnsureAllowedTarget_RefusesAnyOtherHostAndPlainHttp()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ChessCzCalendarService.EnsureAllowedTarget(new Uri("https://example.com/vypis/")));
        Assert.Throws<InvalidOperationException>(() =>
            ChessCzCalendarService.EnsureAllowedTarget(new Uri("http://www.chess.cz/vypis/")));

        ChessCzCalendarService.EnsureAllowedTarget(
            new Uri("https://www.chess.cz/vypis-vsech-udalosti/"));
    }
}
