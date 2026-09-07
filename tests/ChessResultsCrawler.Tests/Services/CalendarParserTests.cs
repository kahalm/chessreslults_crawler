using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Der ANKUENDIGUNGS-Kalender von chess-results (<c>Kalender.aspx</c>) — der zweite Datenbestand
/// derselben Seite.
///
/// <para>Der Anlass: die Turniersuche fuellt sich erst beim Swiss-Manager-Upload, typisch Tage bis
/// Wochen vor dem Turnier. Am 2026-09-07 fuer AUT gemessen kannte sie 8 im November beginnende
/// Turniere und 7 im Dezember — der Kalender 23 und 16. Von 143 kuenftigen Kalendereintraegen
/// fehlten 93 in der Turniersuche.</para>
/// </summary>
public class CalendarParserTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static async Task<List<ParsedCalendarEntry>> ParseAsync() =>
        await new HtmlParserService().ParseCalendarAsync(Fixture("tournament-calendar.html"));

    [Fact]
    public async Task ParseCalendarAsync_ReadsEveryDataRow()
    {
        var entries = await ParseAsync();

        // Die Vorlage ist der echte Abzug mit „All entries" vom 2026-09-07.
        Assert.Equal(245, entries.Count);
        Assert.All(entries, e => Assert.NotEqual("", e.Name));
        Assert.All(entries, e => Assert.NotNull(e.StartDate));
    }

    /// <summary>
    /// Die Tabelle mischt Monatsueberschriften („Start-Date | End-Date | Feb. 2026 | …") unter die
    /// Datenzeilen, und zwar je Monat neu. Sie duerfen nicht als Turniere durchgehen — erkennbar
    /// daran, dass in der zweiten Zelle KEIN Datum steht.
    /// </summary>
    [Fact]
    public async Task ParseCalendarAsync_SkipsTheRepeatedMonthHeaders()
    {
        var entries = await ParseAsync();

        Assert.DoesNotContain(entries, e => e.Name.Contains("End-Date", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, e => e.Name.Contains("Tournament-Calendar", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, e => e.Name is "FED" or "URL" or "DOC");
    }

    /// <summary>
    /// EIN Abruf deckt alle Foederationen ab, die den Kalender benutzen — das ist der Grund, warum
    /// diese Quelle so billig ist. Die Turniersuche kennt 261 Foederationen, der Kalender 16.
    /// </summary>
    [Fact]
    public async Task ParseCalendarAsync_CoversSeveralFederationsInOneFetch()
    {
        var entries = await ParseAsync();

        var feds = entries.Select(e => e.Federation).Where(f => f is not null).Distinct().ToList();
        Assert.Contains("AUT", feds);
        Assert.Contains("GER", feds);
        Assert.Contains("SUI", feds);
        Assert.Contains("CZE", feds);
        Assert.True(feds.Count >= 10, $"Nur {feds.Count} Foederationen erkannt: {string.Join(",", feds)}");
    }

    /// <summary>
    /// Ein mehrtaegiges Turnier traegt zwei Daten; ein eintaegiges bekommt Beginn = Ende, damit
    /// der Aufrufer nicht zwischen „kein Ende" und „endet am selben Tag" unterscheiden muss.
    /// </summary>
    [Fact]
    public async Task ParseCalendarAsync_ReadsBothDates()
    {
        var entries = await ParseAsync();

        var scd = Assert.Single(entries, e => e.Name.StartsWith("58. Meisterschaft des SCD", StringComparison.Ordinal));
        Assert.Equal(new DateOnly(2026, 2, 13), scd.StartDate);
        Assert.Equal(new DateOnly(2026, 11, 27), scd.EndDate);
        Assert.All(entries, e => Assert.True(e.EndDate >= e.StartDate,
            $"Ende vor Beginn bei {e.Name}: {e.StartDate} bis {e.EndDate}"));
    }

    /// <summary>
    /// Die Kalender-Nummer aus dem Ausschreibungs-Verweis ist die einzige stabile Kennung, die der
    /// Kalender hergibt — und sie fehlt bei einem guten Fuenftel der Eintraege. Wer sie als
    /// Pflichtfeld behandelt, verliert genau die.
    /// </summary>
    [Fact]
    public async Task ParseCalendarAsync_ReadsTheCalendarIdWhereItExists()
    {
        var entries = await ParseAsync();

        var scd = Assert.Single(entries, e => e.Name.StartsWith("58. Meisterschaft des SCD", StringComparison.Ordinal));
        Assert.Equal("10814", scd.CalendarId);
        Assert.Equal("http://s-c-d.at", scd.Url);

        var withId = entries.Count(e => e.CalendarId is not null);
        Assert.InRange(withId, entries.Count / 2, entries.Count - 1);
    }

    /// <summary>
    /// Nur wenige Eintraege verweisen auf eine chess-results-Turnierseite (an AUT gemessen: 6 von
    /// 146). Die Zuordnung zu bestehenden Verzeichniseintraegen muss deshalb ueber Termin und Namen
    /// laufen — wer auf diese Nummer baut, ordnet 96 % nicht zu.
    /// </summary>
    [Fact]
    public async Task ParseCalendarAsync_TournamentNumberIsTheExceptionNotTheRule()
    {
        var entries = await ParseAsync();

        var withTnr = entries.Count(e => e.ChessResultsId is not null);
        Assert.True(withTnr < entries.Count / 5,
            $"{withTnr} von {entries.Count} mit Turniernummer — die Annahme 'selten' stimmt nicht mehr.");
        Assert.All(entries.Where(e => e.ChessResultsId is not null),
            e => Assert.Matches(@"^\d+$", e.ChessResultsId!));
    }
}
