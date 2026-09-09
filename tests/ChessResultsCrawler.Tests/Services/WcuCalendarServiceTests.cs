using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Der Saisonkalender der Welsh Chess Union. Eine einzige Tabelle ohne Detailseiten und ohne
/// Nummer je Turnier — die schwierige Frage dieser Quelle ist die eigene Kennung, siehe
/// <see cref="WcuCalendarService.EventKeyOf"/>.
/// </summary>
public class WcuCalendarServiceTests
{
    private static string Fixture() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "wcu-calendar.html"));

    private static List<ParsedWcuEvent> ParsePage() => WcuCalendarService.ParsePage(Fixture());

    // ----- Die Tabelle ---------------------------------------------------------

    [Fact]
    public void ParsePage_ReadsEveryEvent()
    {
        var events = ParsePage();

        // 38 echte Turnierzeilen; die einspaltige "NOTE ..."-Hinweiszeile und die Jahres-/
        // Monatszwischenzeilen sind keine Turniere und duerfen nicht mitgezaehlt werden.
        Assert.Equal(38, events.Count);
        Assert.All(events, e => Assert.NotEqual("", e.Name));
        Assert.All(events, e => Assert.NotEqual("", e.EventId));
    }

    [Fact]
    public void ParsePage_IgnoresTheDecoyTableBeforeTheCalendar()
    {
        // Die Fixture traegt vor der eigentlichen Tabelle noch eine andere ("id=layout") --
        // das darf keine Zeilen liefern.
        Assert.DoesNotContain(ParsePage(), e => e.Name == "decoy");
    }

    /// <summary>
    /// Der Termin kommt aus Jahres- und Monatszwischenzeilen, die fuer alle folgenden Zeilen
    /// gelten, bis die naechste kommt.
    /// </summary>
    [Fact]
    public void ParsePage_CarriesYearAndMonthForwardAcrossRows()
    {
        var events = ParsePage();

        var august = events.Single(e => e.Name.StartsWith("First Pembrokeshire"));
        Assert.Equal(new DateOnly(2026, 8, 15), august.StartDate);
        Assert.Equal("First Pembrokeshire / Junior Rapidplay Championship", august.Name);

        var nextYear = events.Single(e => e.Name == "South Wales Winter Congress");
        Assert.Equal(new DateOnly(2027, 1, 15), nextYear.StartDate);
        Assert.Equal(new DateOnly(2027, 1, 17), nextYear.EndDate);
    }

    [Fact]
    public void ParsePage_ReadsAMultiDayRangeWithSpacesAroundTheHyphen()
    {
        var e = ParsePage().Single(x => x.Name.Contains("Newport Congress"));

        Assert.Equal(new DateOnly(2026, 8, 21), e.StartDate);
        Assert.Equal(new DateOnly(2026, 8, 23), e.EndDate);
    }

    /// <summary>„12th-14th" — ohne Leerzeichen um den Bindestrich.</summary>
    [Fact]
    public void ParsePage_ReadsAMultiDayRangeWithoutSpacesAroundTheHyphen()
    {
        var e = ParsePage().Single(x => x.Name.Contains("1st Pembrokeshire Open 5 Round Swiss Tournament"));

        Assert.Equal(new DateOnly(2027, 2, 12), e.StartDate);
        Assert.Equal(new DateOnly(2027, 2, 14), e.EndDate);
    }

    /// <summary>„3rd - 9th " — mit Leerzeichen am Ende der Zelle.</summary>
    [Fact]
    public void ParsePage_ReadsATrailingSpaceAfterTheDayRange()
    {
        var e = ParsePage().Single(x => x.Name == "South Wales International Open");

        Assert.Equal(new DateOnly(2027, 7, 3), e.StartDate);
        Assert.Equal(new DateOnly(2027, 7, 9), e.EndDate);
    }

    [Fact]
    public void ParsePage_SingleDayEvent_StartsAndEndsOnTheSameDay()
    {
        var e = ParsePage().Single(x => x.Name == "UK Open Blitz Champ Qualifier");

        Assert.Equal(new DateOnly(2026, 9, 19), e.StartDate);
        Assert.Equal(e.StartDate, e.EndDate);
    }

    // ----- Anschrift -------------------------------------------------------------

    /// <summary>
    /// Die Anschrift traegt bei den meisten Eintraegen die britische Postleitzahl im Fliesstext,
    /// direkt an den Ortsnamen angehaengt — genau wie die Quelle sie schreibt.
    /// </summary>
    [Fact]
    public void ParsePage_ReadsThePostcodeInTheAddress()
    {
        var e = ParsePage().Single(x => x.Name.Contains("First Pembrokeshire") && x.Name.Contains("Rapidplay"));

        Assert.Equal("The Western Hall, Pembroke Castle, Castle Terrace, Pembroke SA714LA", e.Place);
    }

    /// <summary>Steckt der "Entry Details"/"Info"-Link in einem echten Anker, landet er nicht in der Anschrift.</summary>
    [Fact]
    public void PlaceOf_ExcludesARealLinkAtTheEnd()
    {
        var place = WcuCalendarService.PlaceOf(
            "<b>The Western Hall,<br />Pembroke Castle,<br />Pembroke SA714LA<br />" +
            "<a href=\"http://example.com/\" target=\"_blank\">Entry Details</a>");

        Assert.Equal("The Western Hall, Pembroke Castle, Pembroke SA714LA", place);
    }

    /// <summary>
    /// EIN gemessener Eintrag traegt "Entry Details TBC" als reinen Fliesstext statt als Link
    /// (Zeile "row-33" der echten Seite) — ohne <c>&lt;a&gt;</c>-Tag gibt es fuer den Parser
    /// keinen Unterschied zu einer echten Adresszeile. Das Rauschen bleibt bewusst stehen statt
    /// ueber eine Woerterliste eigens weggefiltert zu werden (dieselbe Abwaegung wie bei der
    /// unstrukturierten Ausschreibung des deutschen Verbandskalenders).
    /// </summary>
    [Fact]
    public void ParsePage_AKnownEntryHasPlainTextLinkNoiseInThePlace()
    {
        var e = ParsePage().Single(x => x.Name == "1st Pembrokeshire Open 5 Round Swiss Tournament");
        Assert.Equal("Pembroke Castle, Castle Terrace, Pembroke SA714LA, Entry Details TBC", e.Place);
    }

    [Fact]
    public void ParsePage_EventsWithoutAVenue_HaveNoPlace()
    {
        var e = ParsePage().Single(x => x.Name == "National Youth Team Championship");
        Assert.Null(e.Place);
    }

    /// <summary>
    /// Zwei Turniere stehen in derselben Zelle uebereinander — sie werden zu EINEM Eintrag mit
    /// zusammengesetztem Namen, nicht zu zwei Eintraegen, denen dieselbe Kalenderzeile gehoert.
    /// </summary>
    [Fact]
    public void ParsePage_JoinsTwoTournamentsSharingOneCellIntoOneEntry()
    {
        var e = ParsePage().Single(x => x.StartDate == new DateOnly(2026, 10, 2));

        Assert.Equal("Welsh Seniors Champ (Over 50 and Over 65) / Welsh Open", e.Name);
    }

    /// <summary>
    /// Ein echter Tippfehler der Quelle: "&lt;b.&lt;" statt des sonst ueberall verwendeten
    /// "&lt;b/&gt;" — ein Tag ohne schliessendes "&gt;". Ohne Sonderbehandlung bliebe der Rest im
    /// Namen stehen.
    /// </summary>
    [Fact]
    public void ParsePage_StripsADanglingTagWithoutAClosingBracket()
    {
        var e = ParsePage().Single(x => x.Name == "Welsh Championship");
        Assert.DoesNotContain("<", e.Name);
    }

    // ----- Die eigene Kennung ------------------------------------------------------

    /// <summary>
    /// Am 1. November stehen zwei Turniere auf demselben Datum (WCPL- und WJCPL-Ligastart) — nur
    /// die unterschiedliche Anschrift trennt sie in der Kennung, der Name geht bewusst nicht ein.
    /// </summary>
    [Fact]
    public void ParsePage_TwoTournamentsOnTheSameDay_GetDifferentEventIds()
    {
        var sameDay = ParsePage().Where(e => e.StartDate == new DateOnly(2026, 11, 1)).ToList();

        Assert.Equal(2, sameDay.Count);
        Assert.NotEqual(sameDay[0].EventId, sameDay[1].EventId);
        Assert.NotEqual(sameDay[0].Place, sameDay[1].Place);
    }

    [Fact]
    public void ParsePage_AllEventIds_AreDistinct()
    {
        var events = ParsePage();
        Assert.Equal(events.Count, events.Select(e => e.EventId).Distinct().Count());
    }

    /// <summary>
    /// Die Kennung darf sich nicht aendern, wenn der Verband einen Tippfehler im NAMEN korrigiert
    /// — die Funktion nimmt den Namen deshalb gar nicht erst entgegen.
    /// </summary>
    [Fact]
    public void EventKeyOf_IsCaseAndWhitespaceInsensitiveForThePlace()
    {
        var d = new DateOnly(2026, 11, 1);

        var a = WcuCalendarService.EventKeyOf(d, d, "Best Western Heronston Hotel, Bridgend");
        var b = WcuCalendarService.EventKeyOf(d, d, "  best WESTERN heronston hotel, bridgend  ");

        Assert.Equal(a, b);
    }

    [Fact]
    public void EventKeyOf_DiffersWhenThePlaceDiffers()
    {
        var d = new DateOnly(2026, 11, 1);

        var a = WcuCalendarService.EventKeyOf(d, d, "Best Western Heronston Hotel, Bridgend");
        var b = WcuCalendarService.EventKeyOf(d, d, "Bridgend Ravens RFC, Tondu, Bridgend");

        Assert.NotEqual(a, b);
    }

    // ----- Rand- und Fehlerfaelle ----------------------------------------------------

    [Fact]
    public void ParsePage_EmptyPage_ReturnsNothing() =>
        Assert.Empty(WcuCalendarService.ParsePage("<html><body>nichts</body></html>"));

    [Fact]
    public void PlaceOf_OnlyALink_YieldsNothing() =>
        Assert.Null(WcuCalendarService.PlaceOf(
            "<a href=\"https://example.com\" target=\"_blank\">Info</a>"));

    [Fact]
    public void PlaceOf_Empty_YieldsNothing() =>
        Assert.Null(WcuCalendarService.PlaceOf(""));

    // ----- Ziel-Pruefung ---------------------------------------------------------

    [Fact]
    public void EnsureAllowedTarget_RefusesAnyOtherHostAndPlainHttp()
    {
        Assert.Throws<InvalidOperationException>(() =>
            WcuCalendarService.EnsureAllowedTarget(new Uri("https://example.com/calendar/")));
        Assert.Throws<InvalidOperationException>(() =>
            WcuCalendarService.EnsureAllowedTarget(new Uri("http://www.welshchessunion.uk/calendar/")));

        WcuCalendarService.EnsureAllowedTarget(
            new Uri("https://www.welshchessunion.uk/calendar/"));
    }
}
