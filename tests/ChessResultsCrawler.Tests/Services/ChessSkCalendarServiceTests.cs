using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Der Kalender des slowakischen Verbands (chess.sk) — die einzige gepruefte Quelle mit einer
/// AUSDRUECKLICH angebotenen Schnittstelle. Ihr Sonderwert: 27 von 79 Eintraegen nennen die
/// chess-results-Nummer, die Zuordnung zum bestehenden Bestand ist dort also exakt statt geraten.
/// </summary>
public class ChessSkCalendarServiceTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static List<ParsedChessSkEvent> ParseList() =>
        ChessSkCalendarService.ParseList(Fixture("chess-sk-tournaments.json"));

    // ----- Die Liste ---------------------------------------------------------

    [Fact]
    public void ParseList_ReadsEveryRow()
    {
        var events = ParseList();

        Assert.Equal(12, events.Count);
        Assert.All(events, e => Assert.Matches(@"^\d+$", e.EventId));
        Assert.All(events, e => Assert.NotEqual("", e.Name));
        Assert.Equal(events.Count, events.Select(e => e.EventId).Distinct().Count());
    }

    /// <summary>
    /// Die Schnittstelle schickt Zahlen als ZEICHENKETTE (<c>"tournamentId":"5956"</c>), ihre
    /// eigene Spezifikation nennt sie <c>integer</c>. Beides muss gelesen werden — sonst faellt
    /// der Bestand komplett aus, wenn die Quelle das eines Tages richtigstellt.
    /// </summary>
    [Fact]
    public void ParseList_ReadsTheIdWhetherItIsAStringOrANumber()
    {
        var asString = ChessSkCalendarService.ParseList(
            """[{"tournamentId":"5956","nazov":"A","termin_od":"2026-09-08","termin_do":"2026-09-08"}]""");
        var asNumber = ChessSkCalendarService.ParseList(
            """[{"tournamentId":5956,"nazov":"A","termin_od":"2026-09-08","termin_do":"2026-09-08"}]""");

        Assert.Equal("5956", Assert.Single(asString).EventId);
        Assert.Equal("5956", Assert.Single(asNumber).EventId);
    }

    /// <summary>
    /// Der eigentliche Gewinn dieser Quelle: das Feld „Swiss manager URL" nennt die
    /// chess-results-Nummer. Damit ist die Zuordnung zum bestehenden Verzeichnis ein EXAKTER
    /// Schluessel und kein Namensvergleich, der zwei Turniere derselben Woche verwechseln kann.
    /// </summary>
    [Fact]
    public void ParseList_PullsTheChessResultsNumberOutOfTheSwissManagerLink()
    {
        var events = ParseList();

        var withNumber = events.Where(e => e.ChessResultsId is not null).ToList();
        Assert.NotEmpty(withNumber);
        Assert.All(withNumber, e => Assert.Matches(@"^\d+$", e.ChessResultsId!));
        Assert.Equal("1482875", events.Single(e => e.EventId == "5909").ChessResultsId);
    }

    [Theory]
    [InlineData("https://s3.chess-results.com/tnr1482875.aspx?lan=4&SNode=S0", "1482875")]
    [InlineData("http://chess-results.com/tnr999.aspx", "999")]
    [InlineData("https://sites.google.com/view/turnaj", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ChessResultsIdOf_TakesOnlyARealTournamentLink(string? url, string? expected) =>
        Assert.Equal(expected, ChessSkCalendarService.ChessResultsIdOf(url));

    /// <summary>
    /// Der Kalender fuehrt Schulungen und Seminare in DERSELBEN Liste wie Turniere und hat kein
    /// Feld, das sie trennt. Sie gehoeren nicht in einen Turnierkalender — erkannt wird das am
    /// Namen, und zwar gefaltet: die Quelle schreibt „Skolenie" und „Skolenie" nebeneinander.
    /// </summary>
    [Fact]
    public void ParseList_FlagsTrainingCoursesAsNoTournament()
    {
        var events = ParseList();

        Assert.True(events.Single(e => e.EventId == "6016").NonTournament);  // Skolenie a seminar
        Assert.True(events.Single(e => e.EventId == "6019" || e.EventId == "6007").NonTournament);
        Assert.False(events.Single(e => e.EventId == "5956").NonTournament);
    }

    [Fact]
    public void ParseList_KeepsTheEndDateAndFallsBackToTheStart()
    {
        var events = ParseList();

        var multiDay = events.Single(e => e.EventId == "5909");
        Assert.Equal(new DateOnly(2026, 9, 10), multiDay.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 13), multiDay.EndDate);

        var oneDay = events.Single(e => e.EventId == "5956");
        Assert.Equal(oneDay.StartDate, oneDay.EndDate);
    }

    /// <summary>Eine Zeile ohne Nummer, Namen oder Startdatum ist keine — und darf nicht werfen.</summary>
    [Fact]
    public void ParseList_SkipsUnusableRowsInsteadOfThrowing()
    {
        var events = ChessSkCalendarService.ParseList(
            """
            [{"tournamentId":"1","nazov":"","termin_od":"2026-09-08"},
             {"tournamentId":"","nazov":"A","termin_od":"2026-09-08"},
             {"tournamentId":"3","nazov":"C","termin_od":"kein Datum"},
             {"tournamentId":"4","nazov":"D","termin_od":"2026-09-08"}]
            """);

        Assert.Equal("4", Assert.Single(events).EventId);
    }

    // ----- Die Detailseite ---------------------------------------------------

    /// <summary>
    /// Gelesen wird ueber die CSS-KLASSE (<c>datagridphp_detail_td_field_system</c>), nicht ueber
    /// die slowakische Beschriftung daneben — eine uebersetzte Oberflaeche braecht diesen Parser
    /// sonst.
    /// </summary>
    [Fact]
    public async Task ParseDetailAsync_ReadsTheFieldsByTheirCssKey()
    {
        var fields = await ChessSkCalendarService.ParseDetailAsync(Fixture("chess-sk-detail-5933.html"));

        Assert.Equal("Bratislava Norm Week - GM - september", fields["nazov"]);
        Assert.Equal("Bratislava - mestská časť Ružinov", fields["mesto"]);
        Assert.Equal("Slovnaft Business Center, Vlčie hrdlo 1/A, 824 12 Bratislava", fields["miesto"]);
        Assert.Equal("Uzavretý turnaj na 9 kôl, 90+30´30", fields["system"]);
        Assert.Equal("Standard", fields["trn_type"]);
    }

    /// <summary>
    /// Die Anschrift ist der Grund, warum sich der Abruf je Turnier lohnt: sie traegt die
    /// Postleitzahl, und die ist der genaueste Weg der Verortung. Die Liste kennt nur den Ort.
    /// </summary>
    [Fact]
    public async Task ApplyDetail_BringsTheAddressWithItsPostalCode()
    {
        var e = new ParsedChessSkEvent { EventId = "5933", Name = "x" };
        ChessSkCalendarService.ApplyDetail(
            e, await ChessSkCalendarService.ParseDetailAsync(Fixture("chess-sk-detail-5933.html")));

        Assert.Contains("824 12", e.Address);
        Assert.Equal("Standard", e.Type);
    }

    /// <summary>
    /// Das Feld „System" traegt DREI Angaben in einem Satz. Auseinandergenommen werden alle drei:
    /// „Uzavretý turnaj" (geschlossenes Turnier) ist ein Rundenturnier, „na 9 kôl" die
    /// Rundenzahl, „90+30´30" die Bedenkzeit.
    /// </summary>
    [Fact]
    public async Task ApplyDetail_SplitsTheSystemFieldIntoItsThreeAnswers()
    {
        var e = new ParsedChessSkEvent { EventId = "5933", Name = "x" };
        ChessSkCalendarService.ApplyDetail(
            e, await ChessSkCalendarService.ParseDetailAsync(Fixture("chess-sk-detail-5933.html")));

        Assert.Equal("roundRobin", e.System);
        Assert.Equal(9, e.Rounds);
        Assert.Equal("90+30´30", e.TimeControl);
        Assert.Equal("Uzavretý turnaj na 9 kôl, 90+30´30", e.SystemText);
    }

    /// <summary>Ein Teil der Eintraege ist auf ENGLISCH geschrieben — auch die muessen lesbar sein.</summary>
    [Fact]
    public async Task ApplyDetail_UnderstandsTheEnglishSpellingToo()
    {
        var e = new ParsedChessSkEvent { EventId = "5966", Name = "x" };
        ChessSkCalendarService.ApplyDetail(
            e, await ChessSkCalendarService.ParseDetailAsync(Fixture("chess-sk-detail-5966.html")));

        Assert.Equal("swiss", e.System);
        Assert.Equal(7, e.Rounds);
        Assert.Equal("90 min +30 sec/move", e.TimeControl);
    }

    /// <summary>
    /// Ohne Trennzeichen traegt der EINE Abschnitt System und Rundenzahl mit — beide stehen schon
    /// in ihrer eigenen Spalte und gehoeren nicht noch einmal in die Bedenkzeit.
    /// </summary>
    [Fact]
    public async Task ApplyDetail_StripsTheSystemAndRoundsFromTheTimeControl()
    {
        var e = new ParsedChessSkEvent { EventId = "5955", Name = "x" };
        ChessSkCalendarService.ApplyDetail(
            e, await ChessSkCalendarService.ParseDetailAsync(Fixture("chess-sk-detail-5955.html")));

        Assert.Equal("7 min/partiu + 3 sek/ťah", e.TimeControl);
        Assert.Equal(6, e.Rounds);
    }

    /// <summary>Eine leere Seite darf nicht werfen und darf nichts erfinden.</summary>
    [Fact]
    public async Task ParseDetailAsync_ReturnsNothingForAPageWithoutTheTable()
    {
        var fields = await ChessSkCalendarService.ParseDetailAsync("<html><body>nichts</body></html>");

        Assert.Empty(fields);
    }

    // ----- Die Freitext-Zerlegung -------------------------------------------

    /// <summary>
    /// Das Wort „Schweizer System" steht in der Quelle in mindestens vier Schreibweisen, eine
    /// davon mit Tippfehler („Šviačiarský"). Deshalb ein nachsichtiger Ausdruck und keine feste
    /// Zeichenkette.
    /// </summary>
    [Theory]
    [InlineData("Švajčiarsky systém na 7 kôl", "swiss")]
    [InlineData("švajčiar 5 kôl", "swiss")]
    [InlineData("pre hráčov od 18 rokov, Šviačiarský systém, 9. kôl", "swiss")]
    [InlineData("Tournament type: Swiss", "swiss")]
    [InlineData("Uzavretý turnaj na 9 kôl", "roundRobin")]
    [InlineData("A turnaj kruhový", "roundRobin")]
    [InlineData("Open turnaj, 8. kôl", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void SystemOf_ReadsTheSystemThroughItsSpellings(string? text, string? expected) =>
        Assert.Equal(expected, ChessSkCalendarService.SystemOf(text));

    /// <summary>
    /// „A turnaj kruhový, B turnaj švajčiarsky" beschreibt ZWEI Turniere in einem Eintrag. Sich
    /// fuer eines zu entscheiden waere geraten — dann lieber nichts sagen.
    /// </summary>
    [Fact]
    public void SystemOf_SaysNothingWhenBothSystemsAreNamed() =>
        Assert.Null(ChessSkCalendarService.SystemOf(
            "A turnaj kruhový ,B turnaj švajčiarsky , C turnaj švajčiarsky."));

    [Theory]
    [InlineData("Švajčiarsky systém na 7 kôl ; 60min/partia+30s/ťah", 7)]
    [InlineData("7kôl, 2x 10min.", 7)]
    [InlineData("2 x 10 minút + 2s./ťah 8.kôl", 8)]
    [InlineData("Švajčiarsky systém na 11 kôl, tempo hry 3 min + 2s", 11)]
    [InlineData("90 min +30 sec/move; Number of rounds: 7; Tournament type: Swiss", 7)]
    [InlineData("Švajčiarsky systém", null)]
    [InlineData("Aréna", null)]
    public void RoundsOf_ReadsTheRoundCount(string text, int? expected) =>
        Assert.Equal(expected, ChessSkCalendarService.RoundsOf(text));

    /// <summary>
    /// „8 dvojkôl" sind acht DOPPELrunden. Die als acht Runden zu lesen waere falsch, und die
    /// Ziffer steht nicht unmittelbar vor „kôl" — deshalb bleibt es unbekannt statt geraten.
    /// </summary>
    [Fact]
    public void RoundsOf_LeavesDoubleRoundsUnknownRatherThanGuessing() =>
        Assert.Null(ChessSkCalendarService.RoundsOf("švajčiar 8 dvojkôl tempom 30 min/partiu"));

    [Theory]
    // Eigener Abschnitt hinter Semikolon, Klammer oder Komma.
    [InlineData("Švajčiarsky systém na 7 kôl ; 60min/partia+30s/ťah", "60min/partia+30s/ťah")]
    [InlineData("švajčiar 5 kôl (10 min/partiu + 2 sek/ťah)", "10 min/partiu + 2 sek/ťah")]
    [InlineData("Švajčiarsky systém, 9 kôl, 3 min/partia + 2s/ťah", "3 min/partia + 2s/ťah")]
    // Kein Trennzeichen: die fuehrende System- und Rundenangabe faellt weg.
    [InlineData("švajčiar 6 kôl 7 min/partiu + 3 sek/ťah", "7 min/partiu + 3 sek/ťah")]
    [InlineData("Švajčiarsky 7 kôl 2 x 60 minút + 30 sekúnd na ťah", "2 x 60 minút + 30 sekúnd na ťah")]
    // Fuellwoerter stapeln sich: „tempo hry :".
    [InlineData("Švajčiarsky na 9 kôl, tempo hry: 2 x 15 min", "2 x 15 min")]
    [InlineData("Švajčiarsky systém na 7 kôl s hracím tempom 2 x 10 min + 3 s", "2 x 10 min + 3 s")]
    // Die Kurzform ohne Einheit.
    [InlineData("Uzavretý turnaj na 9 kôl, 90+30´30", "90+30´30")]
    // Ein Satzende trennt, ein „min." und ein „9. kôl" duerfen dabei nicht zerfallen.
    [InlineData("Švajčiarsky systém na 7 kôl, tempo 2 × 15 min + 5 sek/ťah. Pravidlá FIDE.",
        "2 × 15 min + 5 sek/ťah")]
    [InlineData("švajčiarsky na 7 kôl, tempo 2x15 min. + 5 sek./ťah", "2x15 min. + 5 sek./ťah")]
    // Gar keine Zeitangabe im Text.
    [InlineData("švajčiarsky na 7 kôl", null)]
    [InlineData("Aréna", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void TimeControlOf_KeepsOnlyTheTimePart(string? text, string? expected) =>
        Assert.Equal(expected, ChessSkCalendarService.TimeControlOf(text));

    // ----- Ziel-Pruefung ----------------------------------------------------

    [Fact]
    public void EnsureAllowedTarget_RefusesAnyOtherHostAndPlainHttp()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ChessSkCalendarService.EnsureAllowedTarget(new Uri("https://example.com/api")));
        Assert.Throws<InvalidOperationException>(() =>
            ChessSkCalendarService.EnsureAllowedTarget(new Uri("http://www.chess.sk/api")));

        ChessSkCalendarService.EnsureAllowedTarget(
            new Uri("https://www.chess.sk/api/turnaje.php/v1/tournaments"));
    }
}
