using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Der Ankuendigungskalender der Chess Federation of Canada — 171 kuenftige Eintraege gegen 68 auf
/// chess-results. Die Fixture ist ein Auszug des ECHTEN Datensatzes vom 2026-09-10 und enthaelt
/// genau die Faelle, an denen diese Quelle scheitern kann: zwei Turniere am selben Tag in derselben
/// Stadt, eine Dublette der Quelle selbst, ein Auslandsturnier, das nur an der PROVINZ als solches
/// erkennbar ist, Online-Termine (darunter ein Meldeschluss, der gar kein Turnier ist) und ein
/// Enddatum VOR dem Start.
/// </summary>
public class CfcCalendarServiceTests
{
    private static List<ParsedCfcEvent> Events() =>
        CfcCalendarService.ParsePayload(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "cfc-data.js")));

    [Fact]
    public void ParsePayload_KeepsOnlyCanadianOnSiteTournaments()
    {
        var events = Events();

        // Aus 11 Fixture-Zeilen bleiben 7: eine Dublette wird zu EINEM Eintrag zusammengefuehrt
        // (11 - 1), ein Auslandsturnier und zwei Online-Termine fallen heraus (- 3).
        Assert.Equal(7, events.Count);
        Assert.All(events, e => Assert.NotEqual("", e.Name));
        Assert.All(events, e => Assert.NotNull(e.Province));
        Assert.DoesNotContain(events, e => e.Province == "FO");
        Assert.DoesNotContain(events, e => e.Name.StartsWith("Application for CFC Subsidy"));
    }

    [Fact]
    public void ParsePayload_DropsForeignEventsByProvinceAndNotByType()
    {
        // „FIDE: World CC for People with Disabilities" in Usbekistan traegt type=OTB und nur
        // prov=FO. Wer nach dem Typ filtert, legt ein usbekisches Turnier unter Kanada ab.
        var events = Events();

        Assert.DoesNotContain(events, e => e.Name.Contains("World CC for People with Disabilities"));
    }

    [Fact]
    public void ParsePayload_TellsApartTwoTournamentsOnTheSameDayInTheSameCity()
    {
        // Am 20.09. stehen zwei Turniere in Markham. Termin und Ort allein wuerden sie
        // verschmelzen — deshalb steckt der Name im Schluessel.
        var markham = Events()
            .Where(e => e.StartDate == new DateOnly(2026, 9, 20) && e.Place == "Markham")
            .ToList();

        Assert.Equal(2, markham.Count);
        Assert.Equal(2, markham.Select(e => e.EventId).Distinct().Count());
    }

    [Fact]
    public void ParsePayload_CollapsesTheSourcesOwnDuplicate()
    {
        // „Vancouver Chess Festival #16" steht zweimal im Datensatz, identisch bis auf die
        // Listenposition. Zwei Eintraege daraus zu machen waere ein Fehler DES SCHLUESSELS.
        var vancouver = Events().Where(e => e.Name.Contains("Vancouver Chess Festival")).ToList();

        Assert.Single(vancouver);
    }

    [Fact]
    public void ParsePayload_RepairsAnEndDateBeforeTheStart()
    {
        var reversed = Assert.Single(Events(), e => e.Name == "Fixture Reversed Dates");

        // Ein Enddatum vor dem Start ist ein Tippfehler der Quelle. Ungeprueft stuende das
        // Turnier im Kalender an keinem einzigen Tag.
        Assert.Equal(reversed.StartDate, reversed.EndDate);
    }

    [Fact]
    public void ParsePayload_ReadsPlaceAndProvinceSeparately()
    {
        var e = Assert.Single(Events(), x => x.Name.StartsWith("BCC 2026 Late Summer"));

        Assert.Equal("Burlington", e.Place);
        Assert.Equal("ON", e.Province);
        Assert.Equal(new DateOnly(2026, 8, 25), e.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 22), e.EndDate);
    }

    [Fact]
    public void ParsePayload_WithoutThePayloadMarker_Throws()
    {
        // Die Datei ist ein Site-Build-Artefakt. Aendert sich ihr Format, muss es KNALLEN und
        // nicht still einen leeren Kalender liefern — ein leerer Lauf zieht Turniere zurueck.
        var ex = Assert.Throws<InvalidOperationException>(
            () => CfcCalendarService.ParsePayload("var somethingElse = {};"));

        Assert.Contains("ws_cfc_data", ex.Message);
    }

    [Fact]
    public void EventKeyOf_IsStableAgainstCaseAndPadding()
    {
        var a = CfcCalendarService.EventKeyOf(
            new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 20), " Markham ", " Foo Open ");
        var b = CfcCalendarService.EventKeyOf(
            new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 20), "markham", "foo open");

        Assert.Equal(a, b);
    }

    [Theory]
    [InlineData("http://www.chess.ca/en/events/")]
    [InlineData("https://chess.ca/en/events/")]
    [InlineData("https://forums.chess.ca/")]
    public void EnsureAllowedTarget_RefusesAnythingButTheCalendarHostOverHttps(string url)
        => Assert.Throws<InvalidOperationException>(
            () => CfcCalendarService.EnsureAllowedTarget(new Uri(url)));

    [Fact]
    public void EnsureAllowedTarget_AcceptsTheCalendarHost()
        => CfcCalendarService.EnsureAllowedTarget(new Uri("https://www.chess.ca/ext/cfc-data.abc123def456.js"));
}
