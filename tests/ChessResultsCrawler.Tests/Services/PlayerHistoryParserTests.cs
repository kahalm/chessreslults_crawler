using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Die Turnierhistorie eines Spielers und seine Spielerkarte — gegen echtes, getrimmtes
/// chess-results-Markup.
///
/// <para>Beide Seiten sind die einzige Quelle fuer Daten, die es sonst nirgends gibt: die
/// Spielersuche liefert ALLE Teilnahmen eines Spielers (vergangene und kuenftige) samt der
/// STARTNUMMER, die nur im Link steht; die Spielerkarte liefert Punkte, Platz,
/// Performance-Rating und Elo-Aenderung. Faellt hier etwas um, hat chess-results das Markup
/// umgebaut — sonst wuerde es nur als still leere Historie auffallen.</para>
/// </summary>
public class PlayerHistoryParserTests
{
    private readonly HtmlParserService _parser = new();

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public async Task ParsePlayerTournamentsAsync_RealSearchPage_ReadsEveryColumn()
    {
        var rows = await _parser.ParsePlayerTournamentsAsync(Fixture("player-search.html"));

        Assert.Equal(4, rows.Count);

        var open = rows.Single(r => r.TournamentName.StartsWith("Schach Tirol Open"));
        Assert.Equal("1107064", open.TournamentId);
        Assert.Equal("2025/08/30", open.EndDate);
        Assert.Equal(56, open.Rank);
        Assert.Equal(9, open.Rounds);
        Assert.Equal(56, open.PlayerCount);
        Assert.Equal("144749", open.IdentNumber);
        Assert.Equal("1693034", open.FideId);
        Assert.Equal("Schwaz", open.Club);
        Assert.Equal("AUT", open.Federation);
    }

    /// <summary>
    /// Die Startnummer steht in KEINER Spalte — nur im Link auf den Spielernamen
    /// (<c>tnr…?lan=1&amp;art=9&amp;snr=44</c>). Ohne sie ist die Spielerkarte dieses Turniers
    /// nicht adressierbar, und damit gaebe es weder Punkte noch Performance.
    /// </summary>
    [Fact]
    public async Task ParsePlayerTournamentsAsync_StartingNumber_ComesFromTheNameLink()
    {
        var rows = await _parser.ParsePlayerTournamentsAsync(Fixture("player-search.html"));

        Assert.Equal(44, rows.Single(r => r.TournamentId == "1107064").Snr);
        Assert.All(rows, r => Assert.NotNull(r.Snr));
    }

    /// <summary>
    /// „-" in der Platz-Spalte heisst „noch nicht gespielt". Das ist die Unterscheidung, an der
    /// haengt, ob ein Kartenabruf ueberhaupt lohnt — und sie darf nicht als Platz 0 ankommen.
    /// </summary>
    [Fact]
    public async Task ParsePlayerTournamentsAsync_FutureTournament_HasNoRank()
    {
        var rows = await _parser.ParsePlayerTournamentsAsync(Fixture("player-search.html"));

        var league = rows.Single(r => r.TournamentName.StartsWith("TMM 1.Klasse"));
        Assert.Null(league.Rank);
        Assert.Equal(11, league.Rounds);
        Assert.NotNull(league.Snr);
    }

    /// <summary>
    /// Bei einem AUSLANDS-Turnier ist die Ident-Nummer „0" — dort traegt allein die Fide-ID die
    /// Identitaet. Wer die Zeilen nur ueber die Ident-Nummer dem Konto zuordnet, verliert genau
    /// diese Turniere.
    /// </summary>
    [Fact]
    public async Task ParsePlayerTournamentsAsync_ForeignTournament_KeepsTheFideId()
    {
        var rows = await _parser.ParsePlayerTournamentsAsync(Fixture("player-search.html"));

        var abroad = rows.Single(r => r.TournamentName.Contains("Tegernsee"));
        Assert.Equal("0", abroad.IdentNumber);
        Assert.Equal("1693034", abroad.FideId);
    }

    [Fact]
    public async Task ParsePlayerCardAsync_RealCard_ReadsPointsRankAndPerformance()
    {
        var card = await _parser.ParsePlayerCardAsync(Fixture("player-card.html"));

        Assert.NotNull(card);
        Assert.True(card!.HasResult);
        Assert.Equal(1.5m, card.Points);
        Assert.Equal(56, card.Rank);
        Assert.Equal(1740, card.PerformanceRating);
        Assert.Equal(44, card.StartingRank);
        Assert.Equal(1854, card.RatingNational);
        Assert.Equal(1923, card.RatingInternational);
        Assert.Equal("144749", card.IdentNumber);
        Assert.Equal("1693034", card.FideId);
        Assert.Equal(1983, card.YearOfBirth);
    }

    /// <summary>
    /// chess-results schreibt Kommazahlen mit KOMMA, auch auf der englischen Seite: „1,5" und
    /// „-51,6". Mit invarianter Kultur allein gelesen wuerde aus 1,5 Punkten eine 15 und aus
    /// -51,6 Elo eine -516.
    /// </summary>
    [Fact]
    public async Task ParsePlayerCardAsync_DecimalComma_IsReadCorrectly()
    {
        var card = await _parser.ParsePlayerCardAsync(Fixture("player-card.html"));

        Assert.Equal(-51.6m, card!.RatingChange);
    }

    [Fact]
    public async Task ParsePlayerCardAsync_PageWithoutTheBlock_ReturnsNull()
    {
        Assert.Null(await _parser.ParsePlayerCardAsync("<html><body><p>nichts</p></body></html>"));
    }

    /// <summary>
    /// Der Fund, der diese Datei ausgeloest hat: auf der art=9-Seite tragen ZWEI Tabellen die
    /// Klasse CRs1, und die erste ist der zweispaltige Player-info-Block. Die frueher zuerst
    /// versuchte Klassen-Auswahl griff also den falschen Block — dessen Zeilen haben zwei
    /// Zellen, fielen durch die Mindestzellen-Pruefung, und die Rundenliste kam LEER zurueck.
    /// Gegen die alte Fassung faellt dieser Test um.
    /// </summary>
    [Fact]
    public async Task ParsePlayerDetailPageAsync_PageWithPlayerInfoBlock_StillFindsTheRounds()
    {
        var rounds = await _parser.ParsePlayerDetailPageAsync(Fixture("player-card.html"));

        var round = Assert.Single(rounds);
        Assert.Equal(1, round.RoundNumber);
        Assert.Equal("Bodrov Timofey", round.OpponentName);
        Assert.Equal(2129, round.OpponentElo);
    }

    // ----- Rundenplan -------------------------------------------------------

    /// <summary>
    /// Start und Ende einer LIGA sagen nicht, wann gespielt wird: elf Runden von September bis
    /// April liegen zwei bis fuenf Wochen auseinander. Im Kalender stand die Liga damit an rund
    /// 200 Tagen, an denen nichts stattfindet.
    /// </summary>
    [Fact]
    public async Task ParseRoundPlanAsync_RealLeaguePlan_ReadsEveryRound()
    {
        var rounds = await _parser.ParseRoundPlanAsync(Fixture("round-plan.html"));

        Assert.Equal(11, rounds.Count);
        Assert.Equal(1, rounds[0].Number);
        Assert.Equal(new DateOnly(2026, 9, 26), rounds[0].Date);
        Assert.Equal(new DateOnly(2027, 4, 17), rounds[^1].Date);
        Assert.Equal(11, rounds[^1].Number);
        Assert.Contains("14:00", rounds[0].TimeText);

        // Und der Beweis, dass Start bis Ende die falsche Auskunft ist: zwischen der ersten und
        // der zweiten Runde liegen zwei Wochen ohne Schach.
        Assert.Equal(14, rounds[1].Date.DayNumber - rounds[0].Date.DayNumber);
    }

    /// <summary>
    /// Kein hinterlegter Plan ist der Normalfall bei den meisten Turnieren — eine leere Liste,
    /// kein Fehler. Der Kalender faellt dann auf Start bis Ende zurueck, was bei einem
    /// Wochenend-Open auch richtig ist.
    /// </summary>
    [Fact]
    public async Task ParseRoundPlanAsync_PageWithoutAPlan_ReturnsEmpty()
    {
        Assert.Empty(await _parser.ParseRoundPlanAsync("<html><body><p>nichts</p></body></html>"));
    }

    /// <summary>
    /// Beide Datumsformate: „yyyy/MM/dd" liefert die englische Seite (lan=1), „dd.MM.yyyy" die
    /// deutsche. Ein Sprachwechsel darf den Rundenplan nicht still leer laufen lassen.
    /// </summary>
    [Theory]
    [InlineData("2026/09/26")]
    [InlineData("26.09.2026")]
    public async Task ParseRoundPlanAsync_BothDateFormats_AreRead(string date)
    {
        var html = $"""
            <table class="CRs1"><tr><th>Round</th><th>Date</th><th>Time</th></tr>
            <tr><td>1</td><td>{date}</td><td>14:00 Uhr</td></tr></table>
            """;

        var round = Assert.Single(await _parser.ParseRoundPlanAsync(html));
        Assert.Equal(new DateOnly(2026, 9, 26), round.Date);
    }
}
