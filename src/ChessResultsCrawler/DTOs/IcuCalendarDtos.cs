namespace ChessResultsCrawler.DTOs;

using ChessResultsCrawler.Services;

/// <summary>Ein Turnier aus dem Kalender der Irish Chess Union.</summary>
public class IcuEventResponse
{
    /// <summary>Die Nummer hinter <c>/events/</c> — eine echte, stabile Kennung.</summary>
    public string EventId { get; set; } = "";

    public string Name { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public string Url { get; set; } = "";

    /// <summary>Der Spielort, oft die volle Anschrift samt Eircode. <c>null</c> bei „TBA".</summary>
    public string? Place { get; set; }

    /// <summary>Koordinaten aus dem Kartenblock der Trefferseite — ohne eigenen Abruf.</summary>
    public double? Lat { get; set; }
    public double? Lon { get; set; }

    /// <summary>
    /// Schlagworte der Zeile: „Classical"/„Rapid"/„Blitz", „FIDE-rated", „Women only",
    /// „Foreign", „Junior International".
    /// </summary>
    public List<string> Categories { get; set; } = [];

    /// <summary>Nach dem Namen kein Turnier, sondern Unterricht, Lehrgang oder Sitzung.</summary>
    public bool NonTournament { get; set; }

    public static IcuEventResponse FromParsed(ParsedIcuEvent e) => new()
    {
        EventId = e.EventId,
        Name = e.Name,
        StartDate = e.StartDate.ToString("yyyy-MM-dd"),
        EndDate = e.EndDate.ToString("yyyy-MM-dd"),
        Url = e.Url,
        Place = e.Place,
        Lat = e.Lat,
        Lon = e.Lon,
        Categories = e.Categories,
        NonTournament = e.NonTournament,
    };
}

/// <summary>
/// Was die Detailseite eines ICU-Turniers ueber die Trefferliste hinaus hergibt — bewusst wenig
/// (siehe <see cref="IcuCalendarService"/>).
/// </summary>
public class IcuDetailResponse
{
    /// <summary>Die chess-results-Nummer, wenn die Seite dorthin verweist (2 von 28 Seiten).</summary>
    public string? ChessResultsId { get; set; }

    /// <summary>Bereits gemeldete Spieler („28 entries") — 6 von 28 Seiten nennen sie.</summary>
    public int? PlayerCount { get; set; }

    public string? Website { get; set; }

    public static IcuDetailResponse FromParsed(ParsedIcuDetail d) => new()
    {
        ChessResultsId = d.ChessResultsId,
        PlayerCount = d.PlayerCount,
        Website = d.Website,
    };
}
