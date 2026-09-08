using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Der Kalender des italienischen Verbands (FSI).
///
/// <para>Warum die Quelle zaehlt: Italien faehrt sein Turnierwesen auf Vega/vesus, nicht auf
/// chess-results — von 285 Eintraegen verlinkt KEIN EINZIGER dorthin, und eine Namensstichprobe
/// von 15 fand 3 auf chess-results. Rund vier Fuenftel der italienischen Turniere fehlen dort
/// also.</para>
///
/// <para>Die Vorlage ist ein Ausschnitt der echten Seite mit neun Turnieren, ausgewaehlt nach
/// VARIANTEN und nicht nach Reihenfolge: zwei Termine und ein Termin, mit und ohne
/// Schiedsrichter, mit und ohne Notiz, Rapid, Blitz, Jugend und ein Eintrag aus Suedtirol.</para>
/// </summary>
public class FsiCalendarServiceTests
{
    private static string Fixture() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "fsi-calendar.html"));

    private static Task<List<ParsedFsiEvent>> ParseAsync() => FsiCalendarService.ParseAsync(Fixture());

    [Fact]
    public async Task ParseAsync_ReadsEveryEvent()
    {
        var events = await ParseAsync();

        Assert.Equal(9, events.Count);
        Assert.All(events, e => Assert.NotEqual("", e.Name));
        Assert.All(events, e => Assert.NotEqual("", e.EventId));
        Assert.All(events, e => Assert.True(e.EndDate >= e.StartDate,
            $"Ende vor Beginn bei {e.Name}"));
    }

    /// <summary>
    /// Die FSI-Nummer aus „Immissione" ist die Identitaet des Eintrags — an der echten Seite
    /// tragen sie 283 von 283 Turnieren, und alle 283 sind verschieden. Ein Eintrag ohne sie wird
    /// uebersprungen, statt beim naechsten Durchgang als neues Turnier zu erscheinen.
    /// </summary>
    [Fact]
    public async Task ParseAsync_EveryEventCarriesAUniqueId()
    {
        var events = await ParseAsync();

        Assert.All(events, e => Assert.Matches(@"^\d+$", e.EventId));
        Assert.Equal(events.Count, events.Select(e => e.EventId).Distinct().Count());
    }

    /// <summary>
    /// Alle Felder stehen INLINE in der Trefferliste — das ist der Grund, warum diese Quelle mit
    /// einem Abruf auskommt. Geprueft am ersten Eintrag der Vorlage.
    /// </summary>
    [Fact]
    public async Task ParseAsync_ReadsTheInlineFields()
    {
        var events = await ParseAsync();

        var superstar = Assert.Single(events, e => e.Name.Contains("Superstar", StringComparison.Ordinal));
        Assert.Equal("21750", superstar.EventId);
        Assert.Equal(new DateOnly(2026, 9, 8), superstar.StartDate);
        Assert.Equal(new DateOnly(2026, 10, 20), superstar.EndDate);
        Assert.Equal("LAZIO", superstar.Region);
        Assert.Equal("Torneo Elo Italia/FIDE", superstar.EventType);
        Assert.Equal("Roma", superstar.Province);
        Assert.Equal("Roma", superstar.Place);
        Assert.Equal("90 minuti + 30 secondi di incremento a mossa", superstar.TimeControl);
        Assert.Equal(7, superstar.Rounds);
        Assert.Contains("Oratorio San Gioacchino", superstar.Note);
    }

    /// <summary>
    /// Ort und Provinz sind bei jedem Eintrag da (283/283 an der echten Seite). Die Provinz
    /// loest italienische Namensgleichheit auf — „Marino" gibt es mehrfach, „Marino" plus
    /// Provinz Roma nicht.
    /// </summary>
    [Fact]
    public async Task ParseAsync_PlaceAndProvinceAreAlwaysPresent()
    {
        var events = await ParseAsync();

        Assert.All(events, e => Assert.False(string.IsNullOrWhiteSpace(e.Place), $"kein Ort bei {e.Name}"));
        Assert.All(events, e => Assert.False(string.IsNullOrWhiteSpace(e.Province), $"keine Provinz bei {e.Name}"));
    }

    /// <summary>
    /// Ein eintaegiges Turnier nennt nur EIN Datum. Beginn und Ende muessen dann gleich sein —
    /// nicht „Ende fehlt", sonst muesste jeder Aufrufer den Sonderfall selbst behandeln.
    /// </summary>
    [Theory]
    [InlineData("11-09-2026   -   13-09-2026", "2026-09-11", "2026-09-13")]
    [InlineData("15-11-2026", "2026-11-15", "2026-11-15")]
    [InlineData("28-12-2026 - 04-01-2027", "2026-12-28", "2027-01-04")]
    public void ParseDates_HandlesBothForms(string text, string start, string end)
    {
        var (s, e) = FsiCalendarService.ParseDates(text);

        Assert.Equal(DateOnly.Parse(start), s);
        Assert.Equal(DateOnly.Parse(end), e);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("kein Datum", null)]
    public void ParseDates_WithoutADate_ReturnsNull(string text, object? _)
    {
        var (s, e) = FsiCalendarService.ParseDates(text);

        Assert.Null(s);
        Assert.Null(e);
    }

    /// <summary>
    /// „9 (8)" heisst neun Runden im Haupt- und acht im Nebenturnier — gezaehlt wird die ERSTE.
    /// „variabile" ist keine Zahl und darf keine erfinden.
    /// </summary>
    [Theory]
    [InlineData("7", 7)]
    [InlineData("9 (8)", 9)]
    [InlineData("variabile", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseRounds_ReadsTheMainTournament(string? text, int? expected) =>
        Assert.Equal(expected, FsiCalendarService.ParseRounds(text));

    /// <summary>
    /// Derselbe Schutz wie bei den anderen Hosts: kein anderer Host, kein http. Die URL wird aus
    /// Datumsangaben gebaut, ist also nicht nutzergesteuert — aber die Regel gilt trotzdem, damit
    /// eine spaetere Umleitung nicht irgendwohin laeuft.
    /// </summary>
    [Theory]
    [InlineData("http://www.federscacchi.com/x")]
    [InlineData("https://example.com/x")]
    public void EnsureAllowedTarget_RefusesAnythingElse(string url) =>
        Assert.Throws<InvalidOperationException>(() =>
            FsiCalendarService.EnsureAllowedTarget(new Uri(url)));

    [Fact]
    public void EnsureAllowedTarget_AcceptsTheCalendar() =>
        FsiCalendarService.EnsureAllowedTarget(
            new Uri("https://www.federscacchi.com/fsi/index.php/calendario/calendario?ric=1"));
}
