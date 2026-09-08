using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Der Kalender des slowenischen Verbands (SZS) — das krasseste Verhaeltnis aller geprueften
/// Quellen: 78 kuenftige Turniere gegen 7 auf chess-results. Und nicht bloss Vorlauf: im
/// Rueckblick auf einen abgeschlossenen Monat erscheinen 36 von 88 Eintraegen dort NIE.
/// </summary>
public class SzsCalendarServiceTests
{
    private static string Fixture() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "szs-page1.html"));

    private static Task<List<ParsedSzsEvent>> ParseAsync() => SzsCalendarService.ParseAsync(Fixture());

    [Fact]
    public async Task ParseAsync_ReadsEveryRow()
    {
        var events = await ParseAsync();

        Assert.Equal(30, events.Count);
        Assert.All(events, e => Assert.Matches(@"^\d+$", e.EventId));
        Assert.All(events, e => Assert.NotEqual("", e.Name));
        Assert.Equal(30, events.Select(e => e.EventId).Distinct().Count());
    }

    /// <summary>Die Kopfzeile traegt nur <c>th</c> und darf kein Turnier werden.</summary>
    [Fact]
    public async Task ParseAsync_SkipsTheHeaderRow()
    {
        var events = await ParseAsync();

        Assert.DoesNotContain(events, e => e.Name is "Naziv" or "TID");
        Assert.DoesNotContain(events, e => e.Place is "Kraj");
    }

    /// <summary>
    /// Der eigentliche Wert dieser Quelle: die Postleitzahl steht in der TREFFERLISTE, nicht erst
    /// auf der Detailseite. Die Verortung braucht also keinen Abruf je Turnier.
    /// </summary>
    [Fact]
    public async Task ParseAsync_ReadsThePostalCodeFromTheList()
    {
        var events = await ParseAsync();

        var withZip = events.Count(e => e.PostalCode is not null);
        Assert.True(withZip >= 25, $"Nur {withZip} von {events.Count} mit Postleitzahl");
        Assert.All(events.Where(e => e.PostalCode is not null),
            e => Assert.Matches(@"^\d{4}$", e.PostalCode!));
    }

    /// <summary>
    /// In der echten Liste stehen auch Bruchstuecke in der Postleitzahl-Spalte („4" statt „1410").
    /// Eine einstellige Zahl ist keine Postleitzahl, sondern ein Tippfehler des Einstellers — sie
    /// darf nicht als Ort durchgehen, sonst verortet der Geocoder irgendwohin.
    /// </summary>
    [Fact]
    public async Task ParseAsync_RejectsATruncatedPostalCode()
    {
        var events = await ParseAsync();

        var broken = events.Where(e => e.PostalCode is null).ToList();
        Assert.NotEmpty(broken);
        Assert.All(broken, e => Assert.NotNull(e.Place));   // Ort bleibt, nur die PLZ faellt weg
    }

    /// <summary>
    /// Absagen stehen im NAMEN, nicht in einem Statusfeld — die Quelle hat keines. Wer das nicht
    /// liest, zeigt abgesagte Turniere als stattfindende.
    /// </summary>
    [Theory]
    [InlineData("ODPADE; Gurman 2026 - 36", true)]
    [InlineData("ODPOVEDANO Litija šahira", true)]
    [InlineData("Prestavljeno iz 7.9.; Gurman 2026 - 36", true)]
    [InlineData("odpade Zagorje ob Savi 10+5", true)]
    [InlineData("Zagorje ob Savi 10+5", false)]
    [InlineData("Odprti turnir Buče 2026", false)]
    public async Task ParseAsync_DetectsCancellationsInTheName(string name, bool expected)
    {
        var html = Fixture().Replace("Zagorje ob Savi 10+5", name);
        var events = await SzsCalendarService.ParseAsync(html);

        Assert.Contains(events, e => e.Name == name && e.Cancelled == expected);
    }

    /// <summary>
    /// Der Monat steht als slowenisches WORT da. Ein Parser, der ein Zahlenformat erwartet, liest
    /// hier gar nichts.
    /// </summary>
    [Theory]
    [InlineData("23. februar 2027", "2027-02-23")]
    [InlineData("04. avgust 2026", "2026-08-04")]
    [InlineData("1. januar 2027", "2027-01-01")]
    [InlineData("15. december 2026", "2026-12-15")]
    [InlineData("31. marec 2027", "2027-03-31")]
    public void ParseSlovenianDate_ReadsTheMonthWord(string text, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), SzsCalendarService.ParseSlovenianDate(text));

    [Theory]
    [InlineData("")]
    [InlineData("23. Februar 2027 irgendwas")]   // Gross/klein ist egal, das muss durchgehen
    [InlineData("32. januar 2027")]              // Tag gibt es nicht
    [InlineData("23. nichtmonat 2027")]
    [InlineData("23.02.2027")]                   // Zahlenformat kommt hier nicht vor
    public void ParseSlovenianDate_RefusesWhatItCannotRead(string text)
    {
        var result = SzsCalendarService.ParseSlovenianDate(text);

        // Der zweite Fall MUSS gelesen werden, die uebrigen nicht.
        if (text.StartsWith("23. Februar", StringComparison.Ordinal))
            Assert.Equal(new DateOnly(2027, 2, 23), result);
        else
            Assert.Null(result);
    }

    [Theory]
    [InlineData("http://www.sah-zveza.si/x")]
    [InlineData("https://example.com/x")]
    public void EnsureAllowedTarget_RefusesAnythingElse(string url) =>
        Assert.Throws<InvalidOperationException>(() =>
            SzsCalendarService.EnsureAllowedTarget(new Uri(url)));
}
