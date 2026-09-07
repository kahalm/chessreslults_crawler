using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.DTOs;

/// <summary>
/// Die Teilnahme EINES Spielers an EINEM Turnier, wie die chess-results-Spielersuche sie meldet.
/// Ein Abruf liefert die ganze Historie — vergangene UND kuenftige Turniere.
///
/// <para>Die Felder unterhalb von <see cref="EndDate"/> kamen mit der Turnierverlauf-Ansicht
/// dazu; sie sind rein zusaetzlich, der bisherige Aufrufer (Auto-Abo) liest sie nicht.</para>
/// </summary>
public class PlayerTournamentResponse
{
    public string TournamentId { get; set; } = "";
    public string TournamentName { get; set; } = "";
    public string? EndDate { get; set; }

    /// <summary>
    /// Startnummer des Spielers in diesem Turnier — steht NUR im Link auf den Namen und ist der
    /// Schluessel zur Spielerkarte (Punkte, Platz, Performance).
    /// </summary>
    public int? Snr { get; set; }

    public string? PlayerName { get; set; }
    /// <summary>chess-results-Ident-Nummer; bei Auslandsturnieren steht dort „0".</summary>
    public string? IdentNumber { get; set; }
    public string? FideId { get; set; }
    public string? Club { get; set; }
    public string? Federation { get; set; }

    /// <summary>Platz; <c>null</c> heisst „noch nicht gespielt" (Spalte enthaelt „-").</summary>
    public int? Rank { get; set; }
    public int? Rounds { get; set; }
    /// <summary>Teilnehmerzahl des Turniers (Spalte „n").</summary>
    public int? PlayerCount { get; set; }

    public static PlayerTournamentResponse FromParsed(ParsedPlayerTournament p) => new()
    {
        TournamentId = p.TournamentId,
        TournamentName = p.TournamentName,
        EndDate = p.EndDate,
        Snr = p.Snr,
        PlayerName = p.PlayerName,
        IdentNumber = p.IdentNumber,
        FideId = p.FideId,
        Club = p.Club,
        Federation = p.Federation,
        Rank = p.Rank,
        Rounds = p.Rounds,
        PlayerCount = p.PlayerCount,
    };
}

public class PlayerSearchResponse
{
    public string Name { get; set; } = "";
    public string? FideId { get; set; }
    public string? ChessResultsId { get; set; }
    public int? Elo { get; set; }
    public string? Country { get; set; }
    public string? Title { get; set; }

    public static PlayerSearchResponse FromParsed(ParsedPlayerSearchResult p) => new()
    {
        Name = p.Name,
        FideId = p.FideId,
        ChessResultsId = p.ChessResultsId,
        Elo = p.Elo,
        Country = p.Country,
        Title = p.Title
    };
}
