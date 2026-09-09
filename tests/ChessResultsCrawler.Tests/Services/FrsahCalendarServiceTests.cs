using ChessResultsCrawler.Services;
using System.Text.Json;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Der Kalender des rumaenischen Verbands (FRSah) — 31 kuenftige Turniere, rund ein Drittel
/// davon nicht auf chess-results (darunter die kompletten nationalen Mannschaftsligen). Anders
/// als beim englischen Verband (dasselbe Plugin) steht die Spielstaette schon VOLLSTAENDIG im
/// Termin selbst — es gibt keinen zweiten Endpunkt zu holen.
/// </summary>
public class FrsahCalendarServiceTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static List<ParsedFrsahEvent> Events() =>
        FrsahCalendarService.ParseEvents(Fixture("frsah-events.json")).Events;

    // ----- Die Termine ---------------------------------------------------------

    [Fact]
    public void ParseEvents_ReadsEveryRow()
    {
        var events = Events();

        Assert.Equal(8, events.Count);
        Assert.All(events, e => Assert.Matches(@"^\d+$", e.EventId));
        Assert.All(events, e => Assert.NotEqual("", e.Name));
        Assert.Equal(events.Count, events.Select(e => e.EventId).Distinct().Count());
    }

    /// <summary>
    /// Die Titel kommen mit HTML-Entitaeten („Ediția a IV-a &amp;#8211; Etapa",
    /// „CHECK &amp;#038; MATE"). Ohne Aufloesung stuenden sie so im Kalender.
    /// </summary>
    [Fact]
    public void ParseEvents_DecodesTheHtmlEntitiesInTheTitleAndVenue()
    {
        var events = Events();
        var names = events.Select(e => e.Name).ToList();

        Assert.DoesNotContain(names, n => n.Contains("&#") || n.Contains("&amp;"));
        Assert.Contains(names, n => n.Contains("Grand Prix MegaChess la Mega Mall 2026 – Etapa a IV-a"));
        Assert.Contains(names, n => n.Contains("CHECK & MATE"));

        // Die Entitaet steckt auch im VENUE-Namen, nicht nur im Titel.
        var places = events.Select(e => e.Place).Where(p => p is not null).ToList();
        Assert.DoesNotContain(places, p => p!.Contains("&#"));
        Assert.Contains(places, p => p!.Contains("Mega Mall Bucuresti – B-dul Pierre de Coubertin"));
    }

    [Fact]
    public void ParseEvents_KeepsStartAndEndDate()
    {
        var e = Events().Single(x => x.EventId == "41501");

        Assert.Equal(new DateOnly(2026, 9, 12), e.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 13), e.EndDate);
        Assert.Contains(Events(), x => x.EndDate > x.StartDate);
    }

    [Fact]
    public void ParseEvents_ReportsTheTotalPageCount() =>
        Assert.Equal(1, FrsahCalendarService.ParseEvents(Fixture("frsah-events.json")).TotalPages);

    [Fact]
    public void ParseEvents_SkipsUnusableRowsInsteadOfThrowing()
    {
        var (events, _) = FrsahCalendarService.ParseEvents(
            """
            {"events":[{"id":1,"title":"","start_date":"2026-09-12 00:00:00"},
                       {"title":"A","start_date":"2026-09-12 00:00:00"},
                       {"id":3,"title":"C","start_date":"kein Datum"},
                       {"id":4,"title":"D","start_date":"2026-09-12 00:00:00"}],"total_pages":1}
            """);

        Assert.Equal("4", Assert.Single(events).EventId);
    }

    // ----- Die eingebettete Spielstaette ---------------------------------------

    /// <summary>
    /// Der Sonderfall gegenueber England: die Spielstaette steckt schon VOLLSTAENDIG im Termin —
    /// bei 25 von 26 gemessenen Faellen aber nur als NAME, ohne strukturierte Adresse.
    /// </summary>
    [Fact]
    public void ParseEvents_MostVenuesCarryOnlyTheNameNoStructuredFields()
    {
        var e = Events().Single(x => x.EventId == "41669");

        Assert.Equal("Tg-Mureș, Piața Republicii nr. 47, sala de la etaj, intrarea prin curte", e.Place);
        Assert.Null(e.City);
        Assert.Null(e.PostalCode);
    }

    /// <summary>
    /// Der eine von 26 Faellen mit strukturierter Anschrift: Adresse, Ort und Provinz kommen als
    /// eigene Felder — der Ortsname steht im zusammengesetzten Text bewusst HINTEN.
    /// </summary>
    [Fact]
    public void ParseEvents_BuildsAPlaceTextWithTheTownAtTheEndWhenFieldsAreStructured()
    {
        var e = Events().Single(x => x.EventId == "41501");

        Assert.Equal("Sala Polivalentă – strada Știrbei Vodă nr.32, Craiova, Știrbei Vodă 32, Craiova, Dolj",
            e.Place);
        Assert.Equal("Craiova", e.City);
    }

    /// <summary>
    /// Eine Postleitzahl im FREITEXT der Spielstaette bleibt im Ortstext erhalten, auch ohne
    /// eigenes strukturiertes Feld — der Geocoder liest sie dort selbst heraus.
    /// </summary>
    [Fact]
    public void ParseEvents_KeepsAPostalCodeThatIsOnlyInsideTheFreeTextName()
    {
        var e = Events().Single(x => x.EventId == "41743");

        Assert.Contains("500484", e.Place);
        Assert.Null(e.PostalCode);
    }

    /// <summary>
    /// Die kompletten nationalen Mannschaftsligen haben KEINEN festen Austragungsort — 5 von 31
    /// gemessenen Turnieren. Kein Spielort heisst kein Ortstext, nicht ein geratener.
    /// </summary>
    [Fact]
    public void ParseEvents_TournamentsWithoutAVenue_GetNoPlace()
    {
        var e = Events().Single(x => x.EventId == "42172");

        Assert.Null(e.Place);
        Assert.Null(e.City);
    }

    [Fact]
    public void ApplyVenue_FallsBackToTheVenueNameWhenThereAreNoAddressFields()
    {
        var e = new ParsedFrsahEvent { EventId = "1", Name = "x" };
        using var doc = JsonDocument.Parse(
            """{"id":1,"venue":"CATTIA – Strada Institutului 35, 500484 Brasov"}""");

        FrsahCalendarService.ApplyVenue(e, doc.RootElement);

        Assert.Equal("CATTIA – Strada Institutului 35, 500484 Brasov", e.Place);
        Assert.Null(e.City);
    }

    [Fact]
    public void ApplyVenue_BuildsAPlaceTextWithStructuredFields()
    {
        var e = new ParsedFrsahEvent { EventId = "1", Name = "x" };
        using var doc = JsonDocument.Parse(
            """{"id":1,"venue":"Grand Hotel","address":"Str. Exemplu 1","city":"Cluj-Napoca","province":"Cluj","country":"Romania"}""");

        FrsahCalendarService.ApplyVenue(e, doc.RootElement);

        Assert.Equal("Grand Hotel, Str. Exemplu 1, Cluj-Napoca, Cluj", e.Place);
        Assert.Equal("Cluj-Napoca", e.City);
        Assert.Equal("Romania", e.Country);
    }

    // ----- Ziel-Pruefung ---------------------------------------------------------

    [Fact]
    public void EnsureAllowedTarget_RefusesAnyOtherHostAndPlainHttp()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FrsahCalendarService.EnsureAllowedTarget(new Uri("https://example.com/wp-json/")));
        Assert.Throws<InvalidOperationException>(() =>
            FrsahCalendarService.EnsureAllowedTarget(new Uri("http://frsah.ro/x")));

        FrsahCalendarService.EnsureAllowedTarget(
            new Uri("https://frsah.ro/wp-json/tribe/events/v1/events"));
    }

    /// <summary>
    /// Die robots.txt der Quelle antwortet mit 403 (nicht lesbar) statt mit einer Regel — dies ist
    /// eine SELBSTBESCHRAENKUNG, keine von der Quelle genannte Wartezeit (siehe Klassenkommentar).
    /// </summary>
    [Fact]
    public void CrawlDelay_IsASelfImposedLimitNotASourceRule() =>
        Assert.Equal(TimeSpan.FromSeconds(5), FrsahCalendarService.CrawlDelay);
}
