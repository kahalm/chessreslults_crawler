using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Der Kalender des englischen Verbands (ECF) — 256 kuenftige Turniere, 86 % davon nicht auf
/// chess-results, und als einzige Quelle des Projekts mit fertigen KOORDINATEN.
/// </summary>
public class EcfCalendarServiceTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static List<(ParsedEcfEvent Event, int? VenueId)> Events() =>
        EcfCalendarService.ParseEvents(Fixture("ecf-events.json")).Events;

    private static Dictionary<int, EcfCalendarService.ParsedEcfVenue> Venues() =>
        EcfCalendarService.ParseVenues(Fixture("ecf-venues.json")).Venues.ToDictionary(v => v.Id);

    /// <summary>Termine und Spielstaetten zusammengefuehrt — so, wie der Abruf es tut.</summary>
    private static List<ParsedEcfEvent> Joined()
    {
        var venues = Venues();
        var result = new List<ParsedEcfEvent>();
        foreach (var (e, venueId) in Events())
        {
            if (venueId is { } id && venues.TryGetValue(id, out var venue))
                EcfCalendarService.Apply(e, venue);
            result.Add(e);
        }
        return result;
    }

    // ----- Die Termine -------------------------------------------------------

    [Fact]
    public void ParseEvents_ReadsEveryRow()
    {
        var events = Events();

        Assert.Equal(15, events.Count);
        Assert.All(events, e => Assert.Matches(@"^\d+$", e.Event.EventId));
        Assert.All(events, e => Assert.NotEqual("", e.Event.Name));
        Assert.Equal(events.Count, events.Select(e => e.Event.EventId).Distinct().Count());
    }

    /// <summary>
    /// Die Titel kommen mit HTML-Entitaeten. Ohne Aufloesung stuende „Women&amp;#038;s" so im
    /// Kalender.
    /// </summary>
    [Fact]
    public void ParseEvents_DecodesTheHtmlEntitiesInTheTitle()
    {
        var names = Events().Select(e => e.Event.Name).ToList();

        Assert.DoesNotContain(names, n => n.Contains("&#") || n.Contains("&amp;"));
        Assert.Contains(names, n => n.Contains("Women’s & Girls’"));
    }

    [Fact]
    public void ParseEvents_KeepsStartAndEndDate()
    {
        var e = Events().Single(x => x.Event.EventId == "88941").Event;

        Assert.Equal(new DateOnly(2026, 9, 8), e.StartDate);
        Assert.Equal(e.StartDate, e.EndDate);
        Assert.Contains(Events(), x => x.Event.EndDate > x.Event.StartDate);
    }

    [Fact]
    public void ParseEvents_ReportsTheTotalPageCount() =>
        Assert.Equal(1, EcfCalendarService.ParseEvents(Fixture("ecf-events.json")).TotalPages);

    /// <summary>
    /// Die Schlagworte der Quelle sind gepflegt und beantworten Fragen, die sonst nur der Name
    /// andeutet: „Juniors Only" traegt 50 der 256 Turniere, „Online" 28, „Meeting" 4.
    /// </summary>
    [Fact]
    public void ParseEvents_KeepsTheCategories()
    {
        var events = Events();

        Assert.Contains(events, e => e.Event.Categories.Contains("Juniors Only"));
        Assert.Contains(events, e => e.Event.Categories.Contains("Online"));
        Assert.Contains(events, e => e.Event.Categories.Contains("Meeting"));
        Assert.All(events, e => Assert.Equal(e.Event.Categories.Distinct().Count(),
            e.Event.Categories.Count));
    }

    /// <summary>Im Termin steht die Spielstaette nur als NUMMER — die Felder gibt es woanders.</summary>
    [Fact]
    public void ParseEvents_ReturnsTheVenueIdNotTheVenue()
    {
        var withVenue = Events().Count(e => e.VenueId is not null);

        Assert.True(withVenue >= 12, $"Nur {withVenue} von 15 mit Spielstaetten-Nummer");
    }

    [Fact]
    public void ParseEvents_SkipsUnusableRowsInsteadOfThrowing()
    {
        var (events, _) = EcfCalendarService.ParseEvents(
            """
            {"events":[{"id":1,"title":"","start_date":"2026-09-08 10:00:00"},
                       {"title":"A","start_date":"2026-09-08 10:00:00"},
                       {"id":3,"title":"C","start_date":"kein Datum"},
                       {"id":4,"title":"D","start_date":"2026-09-08 10:00:00"}],"total_pages":1}
            """);

        Assert.Equal("4", Assert.Single(events).Event.EventId);
    }

    // ----- Die Spielstaetten -------------------------------------------------

    /// <summary>
    /// Der Sonderfall dieser Quelle: sie liefert KOORDINATEN mit. Fuer zwei Drittel der Turniere
    /// entfaellt das Geocoding damit vollstaendig.
    /// </summary>
    [Fact]
    public void ParseVenues_ReadsAddressPostcodeAndCoordinates()
    {
        var venues = Venues().Values.ToList();

        Assert.NotEmpty(venues);
        Assert.Contains(venues, v => v.Lat is not null && v.Lon is not null);
        Assert.Contains(venues, v => v.PostalCode is { Length: > 0 });
        Assert.All(venues.Where(v => v.Lat is not null),
            v => Assert.InRange(v.Lat!.Value, -90, 90));
    }

    [Fact]
    public void Apply_BringsTheCoordinatesToTheEvent()
    {
        var withGeo = Joined().Where(e => e.Lat is not null).ToList();

        Assert.True(withGeo.Count >= 8, $"Nur {withGeo.Count} von 15 mit Koordinaten");
        Assert.All(withGeo, e => Assert.NotNull(e.Lon));
    }

    /// <summary>
    /// Der Ortstext wird so gebaut, dass der Ortsname hinten steht: der Geocoder nimmt bei einem
    /// Text mit Ziffer den LETZTEN Ortstreffer, und das soll die Stadt sein, nicht die Strasse.
    /// </summary>
    [Fact]
    public void Apply_BuildsAPlaceTextWithTheTownAtTheEnd()
    {
        var e = new ParsedEcfEvent { EventId = "1", Name = "x" };
        EcfCalendarService.Apply(e, new EcfCalendarService.ParsedEcfVenue(
            1, "Durham Clayport Library", "8 Millennium Place", "Durham", "DH1 1WA", null,
            54.7779, -1.5752));

        Assert.Equal("Durham Clayport Library, 8 Millennium Place, Durham, DH1 1WA", e.Place);
        Assert.Equal("Durham", e.City);
        Assert.Equal("DH1 1WA", e.PostalCode);
    }

    /// <summary>
    /// Ohne strukturierte Felder traegt der NAME die ganze Anschrift — dann ist er der Ortstext.
    /// Das ist kein Sonderfall, sondern ein Drittel der Spielstaetten.
    /// </summary>
    [Fact]
    public void Apply_FallsBackToTheVenueNameWhenThereAreNoAddressFields()
    {
        var e = new ParsedEcfEvent { EventId = "1", Name = "x" };
        EcfCalendarService.Apply(e, new EcfCalendarService.ParsedEcfVenue(
            1, "The Clissold Arms @ 105 Fortis Green, London, N2 9HR", null, null, null, null,
            null, null));

        Assert.Equal("The Clissold Arms @ 105 Fortis Green, London, N2 9HR", e.Place);
        Assert.Null(e.Lat);
    }

    /// <summary>
    /// 0/0 liegt im Golf von Guinea — dort landet ein leeres Koordinatenfeld, nicht ein Turnier.
    /// </summary>
    [Fact]
    public void ParseVenues_RejectsTheNullIslandAndOutOfRangeValues()
    {
        var (venues, _) = EcfCalendarService.ParseVenues(
            """
            {"venues":[{"id":1,"venue":"A","geo_lat":0,"geo_lng":0},
                       {"id":2,"venue":"B","geo_lat":999,"geo_lng":10},
                       {"id":3,"venue":"C","geo_lat":51.5,"geo_lng":-0.12}],"total_pages":1}
            """);

        Assert.Null(venues.Single(v => v.Id == 1).Lat);
        Assert.Null(venues.Single(v => v.Id == 2).Lat);
        Assert.Equal(51.5, venues.Single(v => v.Id == 3).Lat);
    }

    // ----- Ziel-Pruefung -----------------------------------------------------

    [Fact]
    public void EnsureAllowedTarget_RefusesAnyOtherHostAndPlainHttp()
    {
        Assert.Throws<InvalidOperationException>(() =>
            EcfCalendarService.EnsureAllowedTarget(new Uri("https://example.com/wp-json/")));
        Assert.Throws<InvalidOperationException>(() =>
            EcfCalendarService.EnsureAllowedTarget(new Uri("http://www.englishchess.org.uk/x")));

        EcfCalendarService.EnsureAllowedTarget(
            new Uri("https://www.englishchess.org.uk/wp-json/tribe/events/v1/events"));
    }

    /// <summary>Die Quelle nennt in ihrer robots.txt eine Wartezeit, und die gilt hier.</summary>
    [Fact]
    public void CrawlDelay_IsTheOneTheSourceAsksFor() =>
        Assert.Equal(TimeSpan.FromSeconds(10), EcfCalendarService.CrawlDelay);
}
