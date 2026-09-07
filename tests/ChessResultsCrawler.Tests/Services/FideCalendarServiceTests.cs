using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Der FIDE-Kalender als zweite Turnierquelle.
///
/// <para>Zwei Dinge machen ihn eigen: der richtige Endpunkt ist NICHT der naheliegende
/// (<c>show=table</c>/<c>apilist</c> liefern saubere JSON-Zeilen aus einer 2025 stehen
/// gebliebenen Tabelle; gepflegt wird die HTML-Jahresansicht <c>show=showYear</c>), und die
/// Jahresansicht nennt nur Tag und Monat — das Jahr kommt aus der Anfrage.</para>
/// </summary>
public class FideCalendarServiceTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public async Task ParseYearAsync_RealYearView_ReadsEveryEvent()
    {
        var events = await FideCalendarService.ParseYearAsync(Fixture("fide-year.html"), 2026);

        Assert.Equal(24, events.Count);

        var tata = events.Single(e => e.EventId == "12684");
        Assert.Equal("Tata Steel Chess 2026 – Masters", tata.Name);
        Assert.Equal(new DateOnly(2026, 1, 16), tata.StartDate);
        Assert.Equal(new DateOnly(2026, 2, 1), tata.EndDate);
        Assert.Equal("Wijk can Zee", tata.City);
        Assert.Equal("NED", tata.Country);
    }

    /// <summary>
    /// Der Fall, an dem die Jahres-Zuordnung haengt: „27 Dec - 05 Jan" laeuft ueber den
    /// Jahreswechsel, und das Ereignis erscheint mit DERSELBEN Zeichenkette in zwei
    /// Jahresansichten. Aus der 2026er-Ansicht allein waere es 27.12.2026 bis 05.01.2027 — falsch.
    /// Der Startmonat entscheidet: zweite Jahreshaelfte heisst „beginnt im abgefragten Jahr".
    /// </summary>
    [Fact]
    public async Task ParseYearAsync_EventAcrossTheTurnOfTheYear_GetsBothYearsRight()
    {
        var from2025 = await FideCalendarService.ParseYearAsync(Fixture("fide-year.html"), 2025);

        var rilton = from2025.Single(e => e.EventId == "12683");
        Assert.Equal(new DateOnly(2025, 12, 27), rilton.StartDate);
        Assert.Equal(new DateOnly(2026, 1, 5), rilton.EndDate);
    }

    [Theory]
    // Innerhalb eines Jahres: beide Jahre gleich.
    [InlineData(1, 2, 2026, 2026, 2026)]
    [InlineData(5, 5, 2026, 2026, 2026)]
    // Ueber den Jahreswechsel, Start in der zweiten Jahreshaelfte: Start im abgefragten Jahr.
    [InlineData(12, 1, 2026, 2026, 2027)]
    // Ueber den Jahreswechsel, Start in der ERSTEN Jahreshaelfte gibt es real nicht — dann ist
    // das abgefragte Jahr das ENDjahr.
    [InlineData(2, 1, 2026, 2025, 2026)]
    public void ResolveYears_AssignsTheYears(
        int startMonth, int endMonth, int year, int expectedStart, int expectedEnd)
    {
        var (start, end) = FideCalendarService.ResolveYears(startMonth, endMonth, year);

        Assert.Equal(expectedStart, start);
        Assert.Equal(expectedEnd, end);
    }

    /// <summary>Online-Ereignisse haben keine Stadt — nur das Kuerzel „ONL".</summary>
    [Fact]
    public async Task ParseYearAsync_OnlineEvent_HasNoCity()
    {
        var events = await FideCalendarService.ParseYearAsync(Fixture("fide-year.html"), 2026);

        var online = events.Single(e => e.EventId == "12795");
        Assert.Null(online.City);
        Assert.Equal("ONL", online.Country);
        Assert.Equal(new DateOnly(2026, 1, 31), online.StartDate);
        Assert.Equal(online.StartDate, online.EndDate);
    }

    [Theory]
    [InlineData("Malmo (SWE)", "Malmo", "SWE")]
    [InlineData("Wijk aan Zee (NED)", "Wijk aan Zee", "NED")]
    [InlineData("Weissenhaus  (GER)", "Weissenhaus", "GER")]
    [InlineData(" (ONL)", null, "ONL")]
    [InlineData("Saint Louis, Missouri (USA)", "Saint Louis, Missouri", "USA")]
    // Ein Stadtname darf Klammern enthalten — genommen wird die LETZTE.
    [InlineData("Frankfurt (Oder) (GER)", "Frankfurt (Oder)", "GER")]
    [InlineData("", null, null)]
    public void SplitPlace_SeparatesCityAndCountry(string place, string? city, string? country)
    {
        var (parsedCity, parsedCountry) = FideCalendarService.SplitPlace(place);

        Assert.Equal(city, parsedCity);
        Assert.Equal(country, parsedCountry);
    }

    /// <summary>Ein Datum, das es nicht gibt, wird verworfen statt geraten.</summary>
    [Theory]
    [InlineData("31 Feb - 01 Mar / Wien (AUT)")]
    [InlineData("01 Xyz - 02 Mar / Wien (AUT)")]
    [InlineData("kein Termin")]
    public void ParseWhen_Nonsense_IsRejected(string when)
    {
        Assert.Null(FideCalendarService.ParseWhen(when, 2026));
    }

    /// <summary>
    /// Derselbe SSRF-Schutz wie im CrawlerService, nur fuer den anderen Host: https und ein
    /// EXAKTER Hostvergleich — „calendar.fide.com.attacker.tld" muss durchfallen.
    /// </summary>
    [Theory]
    [InlineData("http://calendar.fide.com/calendar_server.php")]
    [InlineData("https://calendar.fide.com.attacker.tld/x")]
    [InlineData("https://evil-calendar.fide.com.example/x")]
    [InlineData("https://127.0.0.1/x")]
    [InlineData("https://chess-results.com/x")]
    public void EnsureAllowedTarget_ForeignOrInsecureTarget_IsRefused(string url)
    {
        Assert.Throws<InvalidOperationException>(
            () => FideCalendarService.EnsureAllowedTarget(new Uri(url)));
    }

    [Fact]
    public void EnsureAllowedTarget_TheRealTarget_IsAllowed()
    {
        FideCalendarService.EnsureAllowedTarget(new Uri("https://calendar.fide.com/calendar_server.php"));
    }

    /// <summary>
    /// Der VERTRAG fuer den Aufrufer, festgenagelt: dieselbe Zeichenkette wird je Jahresansicht
    /// anders gelesen, und nur eine Lesart kann stimmen. Wer aufsteigend abfragt und je
    /// Ereignis-Nummer den ersten Treffer behaelt, bekommt die richtigen Daten — die 2025er
    /// Ansicht liefert 2025-12-27, die 2026er liest dasselbe Ereignis ein Jahr zu spaet.
    /// </summary>
    [Fact]
    public async Task ParseYearAsync_AscendingYears_FirstHitIsTheCorrectOne()
    {
        var html = Fixture("fide-year.html");
        var from2025 = await FideCalendarService.ParseYearAsync(html, 2025);
        var from2026 = await FideCalendarService.ParseYearAsync(html, 2026);

        Assert.Equal(new DateOnly(2025, 12, 27), from2025.Single(e => e.EventId == "12683").StartDate);
        Assert.Equal(new DateOnly(2026, 12, 27), from2026.Single(e => e.EventId == "12683").StartDate);
    }

    [Fact]
    public async Task ParseYearAsync_PageWithoutEvents_ReturnsEmpty()
    {
        Assert.Empty(await FideCalendarService.ParseYearAsync("<html><body>leer</body></html>", 2026));
    }
}
