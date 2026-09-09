using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Der Aktivitaeten-Feed des norwegischen Verbands. chess-results fuehrt fuer NOR null kuenftige
/// Turniere — der Zugewinn ist hier rechnerisch vollstaendig, und genau deshalb muessen die zwei
/// Formfehler des Feeds gehalten werden: er beginnt mit einem Zeilenumbruch VOR der
/// XML-Deklaration und benutzt einen nirgends deklarierten Namensraum <c>ev:</c>. Jeder von
/// beiden allein macht ihn fuer einen strengen Leser zu Nicht-XML.
/// </summary>
public class SjakkCalendarServiceTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static List<ParsedSjakkEvent> ParseFeed() =>
        SjakkCalendarService.ParseFeed(Fixture("sjakk-feed.rss"));

    // ----- Der Feed ----------------------------------------------------------

    /// <summary>
    /// Die Probe aufs Ganze: die Fixture ist der ECHTE Feed (gekuerzt), also mit beiden
    /// Formfehlern. Kommt hier eine leere Liste zurueck, ist die Reparatur kaputt — und das faellt
    /// sonst nirgends auf, weil ein unlesbarer Feed sich wie ein leerer verhaelt.
    /// </summary>
    [Fact]
    public void ParseFeed_ReadsTheRealFeedDespiteItsTwoDefects()
    {
        var events = ParseFeed();

        Assert.Equal(9, events.Count);
        Assert.All(events, e => Assert.NotEqual("", e.EventId));
        Assert.All(events, e => Assert.NotEqual("", e.Name));
        Assert.Equal(events.Count, events.Select(e => e.EventId).Distinct().Count());
    }

    /// <summary>
    /// Der Feed beginnt mit einem Zeilenumbruch vor <c>&lt;?xml …?&gt;</c>. Ohne das Abschneiden
    /// wirft <c>XDocument.Parse</c>, bevor es ein einziges Item gesehen hat.
    /// </summary>
    [Fact]
    public void RepairFeedXml_DropsTheNewlineBeforeTheDeclaration()
    {
        Assert.StartsWith("\n<?xml", Fixture("sjakk-feed.rss"));
        Assert.StartsWith("<?xml", SjakkCalendarService.RepairFeedXml(Fixture("sjakk-feed.rss")));
    }

    /// <summary>
    /// Der zweite Formfehler: <c>ev:startdate</c> ohne <c>xmlns:ev</c>. Ausgerechnet die beiden
    /// Felder, wegen derer man den Feed liest, machen ihn unlesbar.
    /// </summary>
    [Fact]
    public void RepairFeedXml_DeclaresTheMissingEventNamespace()
    {
        var raw = Fixture("sjakk-feed.rss");
        Assert.DoesNotContain("xmlns:ev", raw);
        Assert.Contains("<ev:startdate>", raw);

        Assert.Contains("xmlns:ev=", SjakkCalendarService.RepairFeedXml(raw));
    }

    /// <summary>
    /// Richtet die Quelle ihren Feed eines Tages, darf hier kein ZWEITES <c>xmlns:ev</c>
    /// entstehen — das waere wieder ein Formfehler, nur ein anderer.
    /// </summary>
    [Fact]
    public void RepairFeedXml_AlreadyDeclared_LeavesItAlone()
    {
        const string sound = """
            <?xml version="1.0"?>
            <rss version="2.0" xmlns:ev="http://purl.org/rss/1.0/modules/event/"><channel/></rss>
            """;

        var repaired = SjakkCalendarService.RepairFeedXml(sound);

        Assert.Equal(1, repaired.Split("xmlns:ev").Length - 1);
    }

    [Fact]
    public void ParseFeed_SingleDayEvent_StartsAndEndsOnTheSameDay()
    {
        var horten = ParseFeed().First(e => e.Name == "Horten BGP høst 2027");

        Assert.Equal(new DateOnly(2026, 10, 31), horten.StartDate);
        Assert.Equal(horten.StartDate, horten.EndDate);
    }

    [Fact]
    public void ParseFeed_MultiDayEvent_KeepsTheWholeSpan()
    {
        var nordisk = ParseFeed().First(e => e.Name == "Nordisk for skolelag");

        Assert.Equal(new DateOnly(2026, 9, 11), nordisk.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 13), nordisk.EndDate);
    }

    /// <summary>
    /// Der Feed fuehrt einzelne Eintraege ohne <c>ev:startdate</c> (gemessen: einen von 1000).
    /// Ein Turnier ohne Datum hat im Kalender keinen Platz — es faellt weg statt mit einem
    /// geratenen Termin dazustehen.
    /// </summary>
    [Fact]
    public void ParseFeed_ItemWithoutDates_IsSkipped()
    {
        Assert.Contains("Seriesjakkens 4. helg 26/27", Fixture("sjakk-feed.rss"));
        Assert.DoesNotContain(ParseFeed(), e => e.Name == "Seriesjakkens 4. helg 26/27");
    }

    /// <summary>
    /// Die Kennung ist der Adressbestandteil — und der traegt norwegische Buchstaben. Wer nur
    /// ASCII zulaesst, verliert genau die Termine, die nach einem Ort benannt sind.
    /// </summary>
    [Fact]
    public void ParseFeed_KeepsTheNorwegianLettersInTheSlug()
    {
        var horten = ParseFeed().First(e => e.Name == "Horten BGP høst 2027");

        Assert.Equal("horten-bgp-høst-2027", horten.EventId);
        Assert.Equal("https://www.sjakk.no/aktiviteter/horten-bgp-høst-2027", horten.Url);
    }

    /// <summary>
    /// Der Feed ist zu <b>98 %</b> Archiv (1000 Items zurueck bis 2022, davon 81 kuenftig). Ohne
    /// den Stichtag im Aufruf traegt jeder Durchgang das gesamte Archiv nach RookHub.
    /// </summary>
    [Fact]
    public void ParseFeed_ReadsTheArchiveToo_TheCallerPicksTheCutoff()
    {
        var events = ParseFeed();

        Assert.Contains(events, e => e.StartDate.Year == 2023);
        Assert.Equal(7, events.Count(e => e.EndDate >= new DateOnly(2026, 9, 9)));
    }

    [Fact]
    public void ParseFeed_NotXmlAtAll_ReturnsNothing() =>
        Assert.Empty(SjakkCalendarService.ParseFeed("<html><body>nichts</body></html>"));

    // ----- Die Kennung -------------------------------------------------------

    /// <summary>
    /// Der Adressbestandteil geht spaeter ungeprueft in eine Abruf-Adresse. Alles mit
    /// Schraegstrich, Fragezeichen oder Doppelpunkt darin ist keiner, sondern ein Weg, den
    /// Detailabruf woanders hin zu schicken.
    /// </summary>
    [Theory]
    [InlineData("https://www.sjakk.no/aktiviteter/horten-bgp-høst-2027", "horten-bgp-høst-2027")]
    [InlineData("https://www.sjakk.no/aktiviteter/nordisk-for-skolelag-3/", "nordisk-for-skolelag-3")]
    // Eine Termin-Adresse traegt keine Abfrage. Etwas anderes als die erwartete Form ist kein
    // Grund zu raten — der Termin faellt weg, statt einen Abruf auf eine geratene Adresse zu
    // schicken.
    [InlineData("https://www.sjakk.no/aktiviteter/x?a=b", null)]
    [InlineData("https://www.sjakk.no/aktuelt/etwas-anderes", null)]
    [InlineData("https://www.sjakk.no/aktiviteter/", null)]
    [InlineData("", null)]
    public void SlugOf_OnlyAcceptsAnAddressSegment(string link, string? expected) =>
        Assert.Equal(expected, SjakkCalendarService.SlugOf(link));

    // ----- Die Detailseite ---------------------------------------------------

    /// <summary>
    /// Der Grund, die Detailseite ueberhaupt zu holen: der Feed hat KEIN Ortsfeld. Nur 17 von 80
    /// kuenftigen Terminen nennen ein „Spillsted" — dieser hier ist einer davon.
    /// </summary>
    [Fact]
    public void ParseDetail_ReadsVenueTimeControlAndSystem()
    {
        var detail = SjakkCalendarService.ParseDetail(Fixture("sjakk-detail-fagernes.html"));

        Assert.NotNull(detail);
        Assert.Equal("Scandic Valdres Hotell Fagernes", detail.Venue);
        Assert.Equal("3 min / parti, 2 sekunder tillegg pr trekk", detail.TimeControl);
        Assert.Equal(9, detail.Rounds);
        Assert.Equal("swiss", detail.System);
        Assert.Equal("https://fagerneschessautumn2026.blogspot.com/", detail.Website);
    }

    /// <summary>
    /// Der HAEUFIGERE Fall (52 von 80): kein Spielort, aber ein ausrichtender Verein. Er ist der
    /// beste Ortshinweis, den diese Quelle hat — ein norwegischer Vereinsname traegt meist seinen
    /// Ort („Kongsvinger Sjakklubb"), und der Titel „Kongsvingerlyn NGP 2026" allein tut das nicht.
    /// </summary>
    [Fact]
    public void ParseDetail_WithoutVenue_StillNamesTheOrganizingClub()
    {
        var detail = SjakkCalendarService.ParseDetail(Fixture("sjakk-detail-kongsvingerlyn.html"));

        Assert.NotNull(detail);
        Assert.Null(detail.Venue);
        Assert.Equal("Kongsvinger Sjakklubb", detail.Organizer);
        Assert.Equal(12, detail.Rounds);
    }

    /// <summary>
    /// Gelesen wird ueber die BESCHRIFTUNG, nicht ueber die Reihenfolge: die Zeilen fehlen
    /// einzeln, und die Tabelle ist bei jedem Termin anders lang.
    /// </summary>
    [Fact]
    public void ParseDetail_NoTableAtAll_ReturnsNull() =>
        Assert.Null(SjakkCalendarService.ParseDetail("<html><body>Siden finnes ikke</body></html>"));

    /// <summary>
    /// „10 runder med 5 runder mandag og 5 runder tirsdag" sind zehn Runden, nicht fuenf — die
    /// ERSTE Zahl vor dem Wort zaehlt. „6, FIDE Dutch Swiss" und das nackte „5" kommen ebenfalls
    /// so in der Quelle vor.
    /// </summary>
    [Theory]
    [InlineData("9 runder sveitser", 9)]
    [InlineData("11 runder Fide Dutch swiss", 11)]
    [InlineData("Det spilles 10 runder med 5 runder mandag og 5 runder tirsdag", 10)]
    [InlineData("6, FIDE Dutch Swiss", 6)]
    [InlineData("5", 5)]
    [InlineData("Swiss system, 7 rounds", 7)]
    [InlineData("FIDE Dutch Swiss", null)]
    [InlineData("", null)]
    public void RoundsOf_TakesTheNumberBeforeTheWord(string text, int? expected) =>
        Assert.Equal(expected, SjakkCalendarService.RoundsOf(text));

    /// <summary>
    /// <b>„monrad" ist der skandinavische Name des Schweizer Systems.</b> Wer nur nach „swiss"
    /// sucht, laesst diese Turniere als „unbekannt" stehen.
    /// </summary>
    [Theory]
    [InlineData("9 runder sveitser", "swiss")]
    [InlineData("7 runder hurtigsjakk(Sveiser)", "swiss")]
    [InlineData("9 runder monrad", "swiss")]
    [InlineData("11 runder Dutch Swiss", "swiss")]
    [InlineData("5 runder Berger", "roundRobin")]
    [InlineData("Turneringen går over TO dager!", null)]
    [InlineData(null, null)]
    public void SystemOf_KnowsTheScandinavianNameForSwiss(string? text, string? expected) =>
        Assert.Equal(expected, SjakkCalendarService.SystemOf(text));

    // ----- Ziel-Pruefung -----------------------------------------------------

    [Fact]
    public void EnsureAllowedTarget_ForeignHost_Throws() =>
        Assert.Throws<InvalidOperationException>(() =>
            SjakkCalendarService.EnsureAllowedTarget(new Uri("https://example.com/aktiviteter/x")));

    [Fact]
    public void EnsureAllowedTarget_PlainHttp_Throws() =>
        Assert.Throws<InvalidOperationException>(() =>
            SjakkCalendarService.EnsureAllowedTarget(new Uri("http://www.sjakk.no/aktiviteter/x")));

    [Fact]
    public void EnsureAllowedTarget_TheFeed_IsFine() =>
        SjakkCalendarService.EnsureAllowedTarget(new Uri("https://www.sjakk.no/aktiviteter-feed.rss"));
}
