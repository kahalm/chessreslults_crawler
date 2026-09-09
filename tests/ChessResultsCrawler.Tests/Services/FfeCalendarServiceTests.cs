using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Der Kalender des franzoesischen Verbands (FFE).
///
/// <para>Warum die Quelle zaehlt: von 40 gegengeprueften FFE-Turnieren stehen ZWEI auf
/// chess-results (5 %) — die FFE fuehrt die nicht-FIDE-gewerteten Vereins- und Ligue-Turniere,
/// die dort praktisch gar nicht vorkommen. Der groesste Einzel-Zugewinn der Quellenrunde.</para>
///
/// <para>Die Vorlagen sind Ausschnitte der echten Seiten, ausgewaehlt nach VARIANTEN und nicht
/// nach Reihenfolge: die Oktoberliste traegt einen zweistelligen und einen einstelligen Starttag
/// und einen Pager mit zwei Seiten, die Septemberliste die Ligue-Homologationen (EST/NAQ/NOR/BRE)
/// und einen Pager mit drei; von den Turnierseiten eine mit Anschrift und eine OHNE (der Fall,
/// der in neun gemessenen einmal auftrat).</para>
/// </summary>
public class FfeCalendarServiceTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static string October() => Fixture("ffe-liste-octobre-2026.html");
    private static string September() => Fixture("ffe-liste-septembre-2026.html");

    // ----- Die Monatsliste ---------------------------------------------------

    [Fact]
    public void ParseList_ReadsEveryRow()
    {
        var events = FfeCalendarService.ParseList(October(), 2026);

        Assert.Equal(5, events.Count);
        Assert.All(events, e => Assert.NotEqual("", e.Name));
        Assert.All(events, e => Assert.NotEqual("", e.EventId));
        Assert.All(events, e => Assert.NotEqual("", e.City));
    }

    /// <summary>
    /// Die FFE-Nummer aus dem Link ist die Identitaet des Eintrags. Ohne sie liesse sich ein
    /// umbenanntes Turnier nicht von einem neuen unterscheiden, und der Bestand bekaeme jede
    /// Nacht ein Duplikat mehr.
    /// </summary>
    [Fact]
    public void ParseList_EveryRowCarriesAUniqueId()
    {
        var events = FfeCalendarService.ParseList(October(), 2026);

        Assert.All(events, e => Assert.Matches(@"^\d+$", e.EventId));
        Assert.Equal(events.Count, events.Select(e => e.EventId).Distinct().Count());
    }

    [Fact]
    public void ParseList_ReadsTheInlineFields()
    {
        var events = FfeCalendarService.ParseList(October(), 2026);

        var morvan = Assert.Single(events, e => e.EventId == "72680");
        Assert.Equal("1er Open international d'Echecs du Morvan - Trophée du Parc", morvan.Name);
        Assert.Equal(new DateOnly(2026, 10, 31), morvan.StartDate);
        Assert.Equal("SAINT BRISSON", morvan.City);
        Assert.Equal("58", morvan.Department);
        Assert.Equal("FFE", morvan.HomologatedBy);
        Assert.Equal("https://www.echecs.asso.fr/FicheTournoi.aspx?Ref=72680", morvan.Url);
    }

    /// <summary>
    /// Ein einstelliger Starttag („3 oct.") ist derselbe Fall wie ein zweistelliger — er stand
    /// nur nicht in der ersten Zeile der Vorlage.
    /// </summary>
    [Fact]
    public void ParseList_ReadsASingleDigitDay()
    {
        var events = FfeCalendarService.ParseList(October(), 2026);

        var seichamps = Assert.Single(events, e => e.EventId == "73153");
        Assert.Equal(new DateOnly(2026, 10, 3), seichamps.StartDate);
    }

    /// <summary>
    /// Das JAHR steht nicht in der Zeile, sondern in der Zwischenueberschrift des Monatsblocks
    /// („octobre 2026"). Sie schlaegt die Jahreszahl, die der Aufrufer mitgibt — sonst waere ein
    /// Aufruf mit falschem Fallback still ein Jahr daneben.
    /// </summary>
    [Fact]
    public void ParseList_TakesTheYearFromTheMonthHeading_NotFromTheCaller()
    {
        var events = FfeCalendarService.ParseList(October(), 1999);

        Assert.All(events, e => Assert.Equal(2026, e.StartDate.Year));
        Assert.All(events, e => Assert.Equal(10, e.StartDate.Month));
    }

    /// <summary>
    /// Die Liste ist ABSTEIGEND nach Termin sortiert — Seite 1 traegt das Monatsende. Daran
    /// haengt die Abbruchbedingung beim Blaettern: liegt schon die letzte Zeile einer Seite vor
    /// dem Stichtag, steht auf den folgenden nur noch aelteres.
    /// </summary>
    [Fact]
    public void ParseList_IsSortedDescendingByDate()
    {
        var events = FfeCalendarService.ParseList(October(), 2026);

        var dates = events.Select(e => e.StartDate).ToList();
        Assert.Equal(dates.OrderByDescending(d => d).ToList(), dates);
    }

    /// <summary>
    /// „Homologiert von" ist nicht immer die FFE: die Ligues fuehren eigene Wertungen (EST, NAQ,
    /// NOR, BRE, IDF). Es ist deshalb ausdruecklich NICHT die Region des Turniers — das
    /// Departement ist es.
    /// </summary>
    [Fact]
    public void ParseList_ReadsTheLigueHomologation()
    {
        var events = FfeCalendarService.ParseList(September(), 2026);

        Assert.Equal("NAQ", Assert.Single(events, e => e.EventId == "72386").HomologatedBy);
        Assert.Equal("NOR", Assert.Single(events, e => e.EventId == "72953").HomologatedBy);
        Assert.Equal("BRE", Assert.Single(events, e => e.EventId == "72976").HomologatedBy);
        Assert.Equal("EST", Assert.Single(events, e => e.EventId == "72464").HomologatedBy);
    }

    /// <summary>
    /// Eine fuehrende Null im Departement gehoert dazu („09" = Ariege). Als Zahl gelesen und
    /// wieder ausgegeben waere es „9" und passte zu keiner Postleitzahl mehr.
    /// </summary>
    [Fact]
    public void ParseList_KeepsTheLeadingZeroOfADepartment()
    {
        var events = FfeCalendarService.ParseList(September(), 2026);

        Assert.Equal("09", Assert.Single(events, e => e.EventId == "72984").Department);
    }

    /// <summary>
    /// Der Pager sagt, wie viele Seiten der Monat hat — und damit, wie viele Postbacks er kostet.
    /// Ohne diese Zahl bekaeme man von einem Monat mit 43 Turnieren die ersten 40 und wuesste es
    /// nicht; und weil absteigend sortiert wird, fehlten ausgerechnet die naechstliegenden.
    /// </summary>
    [Fact]
    public void ParsePageCount_ReadsThePager()
    {
        Assert.Equal(2, FfeCalendarService.ParsePageCount(October()));
        Assert.Equal(3, FfeCalendarService.ParsePageCount(September()));
    }

    /// <summary>Eine Seite ohne Pager ist eine Seite, nicht null.</summary>
    [Fact]
    public void ParsePageCount_DefaultsToOne()
    {
        Assert.Equal(1, FfeCalendarService.ParsePageCount("<html><body>nichts</body></html>"));
    }

    /// <summary>
    /// Das <c>__VIEWSTATE</c> ist der Ausweis fuer jedes Postback. Ein <c>__EVENTVALIDATION</c>
    /// verlangt diese Seite nicht — anders als chess-results.
    /// </summary>
    [Fact]
    public void ParseViewState_ReadsTheHiddenField()
    {
        var viewState = FfeCalendarService.ParseViewState(October());

        Assert.NotNull(viewState);
        Assert.NotEqual("", viewState);
        Assert.DoesNotContain("__EVENTVALIDATION", October(), StringComparison.Ordinal);
    }

    // ----- Monatsnamen -------------------------------------------------------

    /// <summary>
    /// Alle zwoelf Kurzformen sind an der echten Seite nachgemessen (je ein Monat Januar bis
    /// Dezember abgerufen). Sie stehen als Tabelle im Code und kommen bewusst NICHT aus
    /// <c>CultureInfo("fr-FR")</c>: deren Kurzformen stammen aus ICU, und die Schreibweise hat
    /// sich zwischen ICU-Fassungen schon geaendert — ein Container mit anderer ICU-Version wuerde
    /// dann still keine Termine mehr lesen.
    /// </summary>
    [Theory]
    [InlineData("janv.", 1)]
    [InlineData("févr.", 2)]
    [InlineData("mars", 3)]
    [InlineData("avr.", 4)]
    [InlineData("mai", 5)]
    [InlineData("juin", 6)]
    [InlineData("juil.", 7)]
    [InlineData("août", 8)]
    [InlineData("sept.", 9)]
    [InlineData("oct.", 10)]
    [InlineData("nov.", 11)]
    [InlineData("déc.", 12)]
    public void MonthOf_ReadsEveryAbbreviationOfTheSource(string text, int expected)
    {
        Assert.Equal(expected, FfeCalendarService.MonthOf(text));
    }

    /// <summary>Die Turnierseite schreibt die Monate aus — dieselbe Tabelle bedient beide.</summary>
    [Theory]
    [InlineData("janvier", 1)]
    [InlineData("février", 2)]
    [InlineData("juillet", 7)]
    [InlineData("décembre", 12)]
    public void MonthOf_ReadsTheFullNamesToo(string text, int expected)
    {
        Assert.Equal(expected, FfeCalendarService.MonthOf(text));
    }

    /// <summary>„juin" und „juil." beginnen gleich — sie duerfen nicht verwechselt werden.</summary>
    [Fact]
    public void MonthOf_TellsJuneFromJuly()
    {
        Assert.Equal(6, FfeCalendarService.MonthOf("juin"));
        Assert.Equal(7, FfeCalendarService.MonthOf("juil."));
    }

    [Fact]
    public void MonthOf_RejectsSomethingElse()
    {
        Assert.Equal(0, FfeCalendarService.MonthOf("Januar"));
        Assert.Equal(0, FfeCalendarService.MonthOf(""));
        Assert.Equal(0, FfeCalendarService.MonthOf(null));
    }

    /// <summary>
    /// Steht die Zeile im Januar und die Ueberschrift im Dezember, gehoert sie ins naechste Jahr.
    /// Der Fall ist selten (die Monatsseite fuehrt normalerweise nur ihren Monat), aber die
    /// stillschweigende Alternative waere ein Termin ein ganzes Jahr daneben.
    /// </summary>
    [Fact]
    public void ParseListDate_MovesAcrossTheTurnOfTheYear()
    {
        Assert.Equal(new DateOnly(2027, 1, 3),
            FfeCalendarService.ParseListDate("3 janv.", 12, 2026));
        Assert.Equal(new DateOnly(2026, 12, 28),
            FfeCalendarService.ParseListDate("28 déc.", 1, 2027));
    }

    /// <summary>Der 31. Februar ist kein Datum, sondern eine kaputte Zeile.</summary>
    [Fact]
    public void ParseListDate_RejectsADayThatDoesNotExist()
    {
        Assert.Null(FfeCalendarService.ParseListDate("31 févr.", 2, 2027));
        Assert.Null(FfeCalendarService.ParseListDate("", 10, 2026));
    }

    // ----- Die Turnierseite --------------------------------------------------

    /// <summary>
    /// Die Turnierseite traegt alles, was die Liste nicht hat. Das ENDDATUM ist der eigentliche
    /// Grund, sie zu holen: ohne Ende stuende ein zweitaegiges Open im Kalender nur an seinem
    /// ersten Tag.
    /// </summary>
    [Fact]
    public async Task ParseDetailAsync_ReadsTheFields()
    {
        var detail = await FfeCalendarService.ParseDetailAsync(Fixture("ffe-fiche-72680.html"));

        Assert.NotNull(detail);
        Assert.Equal(new DateOnly(2026, 10, 31), detail.StartDate);
        Assert.Equal(new DateOnly(2026, 11, 1), detail.EndDate);
        Assert.Equal("58", detail.Department);
        Assert.Equal("SAINT BRISSON", detail.City);
        Assert.Equal(5, detail.Rounds);
        Assert.Equal("Suisse", detail.PairingSystem);
        Assert.Contains("30", detail.TimeControl);
        Assert.Contains("58230", detail.Address);
    }

    /// <summary>
    /// Die POSTLEITZAHL in der Anschrift ist der Zugewinn dieser Quelle: sie ist der einzige Weg
    /// der Verortung, der auf wenige Kilometer genau ist, und Frankreich hat reichlich
    /// gleichnamige Orte.
    /// </summary>
    [Fact]
    public async Task ParseDetailAsync_TheAddressCarriesThePostalCode()
    {
        var detail = await FfeCalendarService.ParseDetailAsync(Fixture("ffe-fiche-72680.html"));

        Assert.NotNull(detail?.Address);
        Assert.Matches(@"\b\d{5}\b", detail.Address);
    }

    /// <summary>
    /// Ein Feld kann FEHLEN — von neun gemessenen Turnieren hatte eines keine Anschrift. Fehlend
    /// heisst <c>null</c> und nicht leer: daran haengt auf der RookHub-Seite, ob ein vorhandener
    /// Wert stehen bleibt.
    /// </summary>
    [Fact]
    public async Task ParseDetailAsync_AMissingFieldIsNull()
    {
        var detail = await FfeCalendarService.ParseDetailAsync(Fixture("ffe-fiche-72420.html"));

        Assert.NotNull(detail);
        Assert.Null(detail.Address);
        Assert.Equal(new DateOnly(2026, 10, 29), detail.StartDate);
        Assert.Equal(new DateOnly(2026, 10, 31), detail.EndDate);
        Assert.Equal(6, detail.Rounds);
    }

    /// <summary>Eine Seite ohne Turnier ist kein Turnier — nicht eines mit lauter Nullen.</summary>
    [Fact]
    public async Task ParseDetailAsync_ReturnsNullForSomethingElse()
    {
        Assert.Null(await FfeCalendarService.ParseDetailAsync("<html><body>Fehler</body></html>"));
    }

    /// <summary>Eintaegige Turniere nennen denselben Tag zweimal.</summary>
    [Fact]
    public void ParseDetailDates_ReadsBothEnds()
    {
        var (start, end) = FfeCalendarService.ParseDetailDates(
            "samedi 06 février 2027 - samedi 06 février 2027");

        Assert.Equal(new DateOnly(2027, 2, 6), start);
        Assert.Equal(new DateOnly(2027, 2, 6), end);
    }

    [Fact]
    public void SplitLieu_SeparatesDepartmentAndCity()
    {
        Assert.Equal(("58", "SAINT BRISSON"), FfeCalendarService.SplitLieu("58 - SAINT BRISSON"));
        Assert.Equal(("09", "SAINT GIRONS"), FfeCalendarService.SplitLieu("09 - SAINT GIRONS"));
        Assert.Equal(((string?)null, "PARIS"), FfeCalendarService.SplitLieu("PARIS"));
        Assert.Equal(((string?)null, (string?)null), FfeCalendarService.SplitLieu(null));
    }

    // ----- Der Schutz --------------------------------------------------------

    /// <summary>
    /// Derselbe Schutz wie bei den anderen Hosts: https und ein exakter Hostvergleich. Ein
    /// Tippfehler in einer URL soll keinen fremden Server treffen.
    /// </summary>
    [Fact]
    public void EnsureAllowedTarget_AcceptsOnlyTheFederationHost()
    {
        FfeCalendarService.EnsureAllowedTarget(
            new Uri("https://www.echecs.asso.fr/ListeTournois.aspx?Action=RES&Mois=10&Annee=2026"));

        Assert.Throws<InvalidOperationException>(() =>
            FfeCalendarService.EnsureAllowedTarget(new Uri("https://example.com/ListeTournois.aspx")));
        Assert.Throws<InvalidOperationException>(() =>
            FfeCalendarService.EnsureAllowedTarget(new Uri("http://www.echecs.asso.fr/ListeTournois.aspx")));
    }

    /// <summary>Die Adresse der Turnierseite wird gebaut, nicht aus der Liste uebernommen.</summary>
    [Fact]
    public void DetailUrl_PointsAtTheFederationHost()
    {
        var url = FfeCalendarService.DetailUrl("72680");

        Assert.Equal("https://www.echecs.asso.fr/FicheTournoi.aspx?Ref=72680", url);
        FfeCalendarService.EnsureAllowedTarget(new Uri(url));
    }
}
