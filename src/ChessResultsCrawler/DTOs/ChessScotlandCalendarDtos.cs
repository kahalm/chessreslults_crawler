namespace ChessResultsCrawler.DTOs;

using ChessResultsCrawler.Services;

/// <summary>Ein Turnier aus dem Kalender von Chess Scotland.</summary>
public class ChessScotlandEventResponse
{
    /// <summary>Der URL-Bestandteil hinter <c>/calendar/</c> — lesbare, aber teils lange Kennung.</summary>
    public string Slug { get; set; } = "";

    public string Name { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public string Url { get; set; } = "";

    /// <summary>Publikums-Schlagworte: „Adult", „Junior", „International", „Online".</summary>
    public List<string> Categories { get; set; } = [];

    /// <summary>Bedenkzeit-Klasse(n): „Standard", „Allegro", „Blitz", „Fide".</summary>
    public List<string> TimeControls { get; set; } = [];

    public static ChessScotlandEventResponse FromParsed(ParsedChessScotlandEvent e) => new()
    {
        Slug = e.Slug,
        Name = e.Name,
        StartDate = e.StartDate.ToString("yyyy-MM-dd"),
        EndDate = e.EndDate.ToString("yyyy-MM-dd"),
        Url = e.Url,
        Categories = e.Categories,
        TimeControls = e.TimeControls,
    };
}

/// <summary>
/// Die Detailseite EINES Turniers — heute nur der Spielort, wenn die Ausschreibung eine
/// erkennbare Postleitzahl enthaelt (19 % gemessen). Ein Abruf je Turnier, deshalb eine eigene
/// Antwort statt sie an die Liste zu haengen.
/// </summary>
public class ChessScotlandDetailResponse
{
    public string? Venue { get; set; }

    public static ChessScotlandDetailResponse FromParsed(ParsedChessScotlandDetail d) => new()
    {
        Venue = d.Venue,
    };
}
