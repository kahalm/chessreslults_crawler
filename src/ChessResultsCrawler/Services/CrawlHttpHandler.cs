namespace ChessResultsCrawler.Services;

/// <summary>
/// Der primaere HTTP-Handler fuer ALLE Abrufe, die durch den VPN-Tunnel gehen (chess-results und
/// FIDE-Kalender). Zwei Einstellungen, beide nicht optional:
///
/// <para><b>Keine automatischen Redirects</b> (SSRF-Schutz). <see cref="CrawlerService"/> folgt
/// ihnen von Hand und prueft JEDEN Hop (chess-results.com + https) VOR dem Absenden — sonst
/// folgte <c>HttpClient</c> einer Redirect-Kette (bis 50 Hops) blind bis zu einem internen Host
/// und pruefte erst danach.</para>
///
/// <para><b>Verbindungen ueberleben eine VPN-Rotation nicht.</b> Der Container laeuft mit
/// <c>network_mode: service:gluetun</c>, und <see cref="CrawlerService"/> baut den Tunnel alle
/// <c>RotateAfterRequests</c> (20) Abrufe ab und wieder auf (stop→pause→start). Damit ist JEDE
/// offene TCP/TLS-Verbindung tot — aber der Verbindungspool von <c>SocketsHttpHandler</c> weiss
/// das nicht: der naechste Abruf greift eine gepoolte Verbindung, das Lesen haengt, und erst der
/// 30-s-Timeout des <c>HttpClient</c> beendet es. Nach aussen ist das ein 504 nach 37 Sekunden.
/// Eine Rotation kostet dabei MEHRERE Abrufe, nicht einen: im Pool liegen mehrere Verbindungen,
/// und jede wird einmal totgelaufen, bevor eine frische entsteht.</para>
///
/// <para>Am Dev-Stand gemessen (2026-09-07, Rundenplan-Durchgang): 18 Rotationen, und
/// <b>27 von 176 Abrufen</b> liefen in genau diesen Timeout — rund 1,5 Fehlschlaege je Rotation,
/// je 37 s. Die erfolgreichen Abrufe brauchten im Mittel 3,0 s; die Timeouts machten damit den
/// GROESSTEN Teil der Laufzeit aus. Im Log steht die Ursache unmittelbar davor: „VPN IP rotated
/// → …", dann <c>SocketException (125): Operation canceled</c> aus
/// <c>SslStream.EnsureFullTlsFrameAsync</c>.</para>
///
/// <para>Die Lebensdauer ist deshalb <b>kuerzer als der Abstand zweier Abrufe</b> (Rate-Limiter:
/// mindestens 1,5 s). Innerhalb EINES Abrufs — eine Redirect-Kette hat mehrere Hops im Abstand
/// von Millisekunden — wird die Verbindung weiter wiederverwendet; ueber die Rotationspause
/// hinweg nie. `IdleTimeout` ist dabei der passendere Hebel (die Verbindung ist waehrend der
/// Rotation genau: unbenutzt), `Lifetime` fangt den Fall „lange offen, dann rotiert" mit ab.</para>
/// </summary>
public static class CrawlHttpHandler
{
    /// <summary>
    /// Kuerzer als der kleinste Abstand zweier Crawl-Abrufe (Rate-Limiter, 1,5 s) und kuerzer als
    /// die kuerzeste Rotationspause — aber lang genug fuer die Hops einer Redirect-Kette.
    /// </summary>
    internal static readonly TimeSpan PoolTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Wie lange auf das ZUSTANDEKOMMEN der Verbindung gewartet wird — getrennt vom Zeitlimit der
    /// ganzen Anfrage.
    ///
    /// <para>Ohne das frisst ein Host, den der VPN-Ausgang gar nicht erreicht, das volle Zeitlimit
    /// seiner Quelle. Am 2026-09-09 auf Dev gemessen: <c>federscacchi.com</c> (Italien) und
    /// <c>frsah.ro</c> (Rumaenien) kamen ueber den AirVPN-Ausgang nicht einmal zu einer
    /// TCP-Verbindung (<c>time_connect=0</c> nach ueber zwei Minuten), waehrend beide vom Host
    /// direkt erreichbar sind. Der naechtliche Durchgang wartete dafuer 90 bzw. 60 Sekunden — jede
    /// Nacht, mit derselben Antwort. Ein Verbindungsaufbau, der laenger als das hier braucht, ist
    /// ueber einen VPN kein langsamer Host mehr, sondern ein nicht erreichbarer: chessarbiter
    /// verbindet sich in 28 ms.</para>
    ///
    /// <para>Das Zeitlimit der Quellen bleibt unangetastet — es deckt das LESEN einer grossen Seite
    /// ab (Italien 1,25 MB), und das darf lange dauern.</para>
    /// </summary>
    internal static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(20);

    public static SocketsHttpHandler Create() => new()
    {
        AllowAutoRedirect = false,
        PooledConnectionIdleTimeout = PoolTimeout,
        PooledConnectionLifetime = PoolTimeout,
        ConnectTimeout = ConnectTimeout,
    };
}
