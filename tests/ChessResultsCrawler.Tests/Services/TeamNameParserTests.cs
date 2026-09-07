using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Parser der Team-Startrangliste. Das Fixture ist ein gekuerzter Ausschnitt der ECHTEN
/// Turnierseite tnr1405166 („1. Frauenbundesliga AUT 2026/2027").
///
/// <para>Der Zweck ist nicht die Turnierauswertung: chess-results nennt den Spielort dort als
/// „Mayrhofen, St.Veit", und „St. Veit" gibt es in Tirol UND in Kaernten. Die Vereinsnamen tragen
/// die Unterscheidung mit — „SV - Das Wien - St.Veit/Glan" — und sind damit ein Hinweis, den es
/// sonst nirgends gibt.</para>
/// </summary>
public class TeamNameParserTests
{
    private readonly HtmlParserService _parser = new();

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public async Task ParseTeamNamesAsync_RealTournamentPage_ReadsEveryClub()
    {
        var names = await _parser.ParseTeamNamesAsync(Fixture("tournament-teams.html"));

        Assert.Equal(10, names.Count);
        Assert.Contains("ASVÖ Pamhagen", names);
        Assert.Contains("Grazer Schachgesellschaft", names);
        // Der Name, auf den es ankommt: er nennt den Spielort genauer als der Ortstext.
        Assert.Contains(names, n => n.Contains("St.Veit/Glan", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseTeamNamesAsync_PageWithoutTeamTable_ReturnsEmpty()
    {
        // Die meisten Turniere sind Einzelturniere — leer ist der Normalfall, kein Fehler.
        var names = await _parser.ParseTeamNamesAsync(
            "<html><body><table><tr><th>Nr.</th><th>Name</th></tr>" +
            "<tr><td>1</td><td>Spieler, Anna</td></tr></table></body></html>");

        Assert.Empty(names);
    }

    [Fact]
    public async Task ParseTeamNamesAsync_Garbage_ReturnsEmptyInsteadOfThrowing()
    {
        Assert.Empty(await _parser.ParseTeamNamesAsync("nicht einmal HTML"));
        Assert.Empty(await _parser.ParseTeamNamesAsync(""));
    }

    [Fact]
    public async Task ParseTeamNamesAsync_GermanHeader_IsUnderstoodToo()
    {
        var names = await _parser.ParseTeamNamesAsync(
            "<html><body><table><tr><th>Nr.</th><th>Mannschaft</th></tr>" +
            "<tr><td>1</td><td>SK Dornbirn</td></tr>" +
            "<tr><td>2</td><td>SV ASKÖ St. Veit/Glan</td></tr></table></body></html>");

        Assert.Equal(["SK Dornbirn", "SV ASKÖ St. Veit/Glan"], names);
    }
}
