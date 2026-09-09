using ChessResultsCrawler.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Der Kalender des polnischen Verbands (chessarbiter.com) — die ergiebigste Einzelquelle des
/// Projekts: 611 kuenftige Turniere in EINER Antwort, mehr als alle uebrigen Verbandskalender
/// zusammen.
/// </summary>
public class ChessArbiterCalendarServiceTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static readonly DateOnly Today = new(2026, 9, 8);

    private static List<ParsedChessArbiterEvent> ParseList() =>
        ChessArbiterCalendarService.ParseList(Fixture("chessarbiter-list.html"), Today);

    // ----- Die Liste ---------------------------------------------------------

    [Fact]
    public void ParseList_ReadsEveryRow()
    {
        var events = ParseList();

        Assert.Equal(20, events.Count);
        Assert.All(events, e => Assert.Matches(@"^\d+$", e.EventId));
        Assert.All(events, e => Assert.Matches(@"^\d{4}$", e.Year));
        Assert.All(events, e => Assert.NotEqual("", e.Name));
        Assert.Equal(events.Count, events.Select(e => $"{e.Year}/{e.EventId}").Distinct().Count());
    }

    /// <summary>
    /// Die Falle dieser Quelle: die Liste nennt nur Tag und Monat. Das Jahr im Adresspfad taugt
    /// NICHT als Ersatz — es ist das Jahr, in dem der EINTRAG angelegt wurde, und bei neun der 611
    /// steht dort 2024 oder 2025 fuer ein Turnier von 2026. Verlaesslich ist die Reihenfolge.
    /// </summary>
    [Fact]
    public void ParseList_TakesTheYearFromTheOrderNotFromThePath()
    {
        var events = ParseList();

        Assert.All(events, e => Assert.Equal(2026, e.StartDate.Year));
        Assert.Equal(new DateOnly(2026, 9, 9), events[0].StartDate);
        Assert.Equal(new DateOnly(2026, 12, 30), events[^1].StartDate);
        // Die Termine steigen — daran haengt die ganze Jahresableitung.
        Assert.Equal(events.Select(e => e.StartDate).Order().ToList(),
            events.Select(e => e.StartDate).ToList());
    }

    /// <summary>
    /// Springt der Monat zurueck, hat das Jahr gewechselt. Ohne diesen Schritt saessen die
    /// Januar-Turniere elf Monate in der Vergangenheit.
    /// </summary>
    [Fact]
    public void ParseList_IncrementsTheYearWhenTheMonthGoesBackwards()
    {
        var html = Row("2026", "1", "Dezember-Turnier", "20-12")
                   + Row("2026", "2", "Januar-Turnier", "10-01")
                   + Row("2026", "3", "Februar-Turnier", "07-02");

        var events = ChessArbiterCalendarService.ParseList(Wrap(html), new DateOnly(2026, 12, 1));

        Assert.Equal(new DateOnly(2026, 12, 20), events[0].StartDate);
        Assert.Equal(new DateOnly(2027, 1, 10), events[1].StartDate);
        Assert.Equal(new DateOnly(2027, 2, 7), events[2].StartDate);
    }

    [Fact]
    public void ParseList_ReadsPlaceRegionAndSpeed()
    {
        var e = ParseList().First(x => x.EventId == "287");

        Assert.Equal("Sosnowica", e.Place);
        Assert.Equal("Lubelskie", e.Region);
        Assert.Equal("klasyczne", e.SpeedText);
        Assert.Equal("https://www.chessarbiter.com/turnieje/2026/ti_287", e.Url);
    }

    /// <summary>
    /// Die Woiwodschaft steht als Kuerzel in der Zeile („Poland,SL") und ausgeschrieben in der
    /// Auswahlliste DERSELBEN Antwort. Aufgeloest wird von dort — keine zweite Anfrage, und keine
    /// Tabelle im Quelltext, die veralten koennte.
    /// </summary>
    [Fact]
    public void ParseRegions_ReadsTheNamesFromTheDropdownOnTheSamePage()
    {
        var regions = ChessArbiterCalendarService.ParseRegions(Fixture("chessarbiter-list.html"));

        Assert.Equal(16, regions.Count);
        Assert.Equal("Śląskie", regions["SL"]);
        Assert.Equal("Lubelskie", regions["LU"]);
        Assert.Equal("Warmińsko-Mazurskie", regions["WM"]);
    }

    /// <summary>Der Aktualisierungsvermerk der Quelle gehoert nicht zum Ortsnamen.</summary>
    [Theory]
    [InlineData("Sosnowica  [aktualizacja:04-09-2026]", "Sosnowica")]
    [InlineData("Warszawa", "Warszawa")]
    [InlineData("[aktualizacja:04-09-2026]", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void StripUpdateNote_RemovesOnlyTheNote(string? text, string? expected) =>
        Assert.Equal(expected, ChessArbiterCalendarService.StripUpdateNote(text));

    // ----- Die Detailseite ---------------------------------------------------

    /// <summary>
    /// Die Recherche hatte hier eine JavaScript-Datei erwartet (<c>capro_tournament.js</c>) — die
    /// gibt es nicht (404). Die Detailseite ist server-gerendertes HTML mit ENGLISCHEN
    /// Beschriftungen, also unabhaengig von der Oberflaechensprache lesbar.
    /// </summary>
    [Fact]
    public void ParseDetail_ReadsEveryFieldTheListDoesNotHave()
    {
        var detail = ChessArbiterCalendarService.ParseDetail(Fixture("chessarbiter-detail.html"));

        Assert.NotNull(detail);
        Assert.Equal(new DateOnly(2026, 9, 9), detail!.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 13), detail.EndDate);
        Assert.StartsWith("Sosnowica,", detail.Place);
        Assert.Equal("90' + 30'' na ruch", detail.TimeControl);
        Assert.Equal(9, detail.Rounds);
        Assert.Equal("swiss", detail.System);
        Assert.Equal("LWZSzach", detail.Organizer);
    }

    /// <summary>
    /// Bemerkenswert und einmalig unter allen Quellen: die Teilnehmerzahl steht auch bei einem
    /// GEPLANTEN Turnier da. chess-results und FIDE nennen sie fuer die Zukunft grundsaetzlich
    /// nicht.
    /// </summary>
    [Fact]
    public void ParseDetail_EvenKnowsHowManyAreEnteredForAPlannedTournament() =>
        Assert.Equal(12,
            ChessArbiterCalendarService.ParseDetail(Fixture("chessarbiter-detail.html"))!.PlayerCount);

    /// <summary>Der Wert des Feldes „System" ist selbst wieder ein Uebersetzungsaufruf.</summary>
    [Theory]
    [InlineData("Tr(\"Swiss\",\"\");", "swiss")]
    [InlineData("Swiss", "swiss")]
    [InlineData("Tr(\"Round robin\",\"\");", "roundRobin")]
    [InlineData("Tr(\"Knock-out\",\"\");", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void SystemOf_ReadsThroughTheTranslationCall(string? text, string? expected) =>
        Assert.Equal(expected, ChessArbiterCalendarService.SystemOf(text));

    [Fact]
    public void ParseDetail_APageWithoutTheTable_ReturnsNothing() =>
        Assert.Null(ChessArbiterCalendarService.ParseDetail("<html><body>nichts</body></html>"));

    // ----- Ziel-Pruefung -----------------------------------------------------

    [Fact]
    public void EnsureAllowedTarget_RefusesAnyOtherHostAndPlainHttp()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ChessArbiterCalendarService.EnsureAllowedTarget(new Uri("https://example.com/turnieje")));
        Assert.Throws<InvalidOperationException>(() =>
            ChessArbiterCalendarService.EnsureAllowedTarget(new Uri("http://www.chessarbiter.com/x")));

        ChessArbiterCalendarService.EnsureAllowedTarget(
            new Uri("https://www.chessarbiter.com/turnieje/2026/ti_291"));
    }

    /// <summary>
    /// Die Kennung wandert in einen PFAD. Alles, was dort nicht wie Jahr und Nummer aussieht, wird
    /// abgewiesen, bevor eine Anfrage daraus wird.
    /// </summary>
    [Theory]
    [InlineData("2026", "../../etc")]
    [InlineData("2026", "291/../..")]
    [InlineData("../2026", "291")]
    [InlineData("26", "291")]
    public async Task FetchDetailAsync_RefusesAMalformedKey(string year, string id)
    {
        var service = new ChessArbiterCalendarService(
            new HttpClient(new ThrowingHandler()), Mock.Of<ILogger<ChessArbiterCalendarService>>());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.FetchDetailAsync(year, id));
    }

    [Fact]
    public async Task FetchDetailAsync_HoltDieAdresseMitSchraegstrich()
    {
        // Ohne Schraegstrich antwortet chessarbiter 301 auf genau dieselbe Adresse MIT — und der
        // Crawl-Handler folgt Umleitungen bewusst nicht. Am 2026-09-09 endeten dadurch ALLE 150
        // Detailabrufe eines Durchgangs ohne Ergebnis, und die Turniere blieben Kandidaten: jeder
        // weitere Durchgang holte dieselben Seiten erneut.
        var recorder = new RecordingHandler(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "chessarbiter-detail.html")));
        var service = new ChessArbiterCalendarService(
            new HttpClient(recorder), Mock.Of<ILogger<ChessArbiterCalendarService>>());

        var detail = await service.FetchDetailAsync("2026", "291");

        Assert.NotNull(recorder.LastUrl);
        Assert.Equal("https://www.chessarbiter.com/turnieje/2026/ti_291/", recorder.LastUrl);
        Assert.NotNull(detail);
    }

    [Fact]
    public async Task FetchDetailAsync_EineUmleitungIstKeinErgebnis()
    {
        // Der Handler folgt nicht — eine 301 muss deshalb als „nichts geholt" ankommen und nicht
        // als leeres, aber gueltiges Ergebnis (das waere ein Turnier, das nie wieder gefragt wird).
        var service = new ChessArbiterCalendarService(
            new HttpClient(new RecordingHandler("", HttpStatusCode.MovedPermanently)),
            Mock.Of<ILogger<ChessArbiterCalendarService>>());

        Assert.Null(await service.FetchDetailAsync("2026", "291"));
    }

    // ----- Hilfen ------------------------------------------------------------

    private static string Row(string year, string id, string name, string date) =>
        $"""
         <tr class="tbl1"><td style="text-align:center" width="10%">{date}<div class="szary">
         <div style="color:red;">planowany</div></td></div>
         <td><a href = "https://www.chessarbiter.com/turnieje/open.php?turn={year}/ti_{id}&n=" target="_blank">{name}</a>
         <div class="szary">Warszawa</div></td><td width="12%">Poland,MA <br><div class="szary">szybkie</div></td></tr>
         """;

    private static string Wrap(string rows) => $"<html><body><table>{rows}</table></body></html>";

    /// <summary>Merkt sich die angefragte Adresse und antwortet mit vorgegebenem Inhalt.</summary>
    private sealed class RecordingHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public string? LastUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            LastUrl = r.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) =>
            throw new InvalidOperationException("Es haette gar keine Anfrage geben duerfen");
    }

}
