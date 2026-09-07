using ChessResultsCrawler.Services;
using Xunit;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Der Handler, durch den ALLE Abrufe durch den VPN-Tunnel gehen. Beide Einstellungen sind
/// gemessene Konsequenzen, keine Vorlieben — deshalb je ein Test, der sie festnagelt.
/// </summary>
public class CrawlHttpHandlerTests
{
    /// <summary>
    /// SSRF-Schutz: der CrawlerService folgt Redirects von Hand und prueft jeden Hop VOR dem
    /// Absenden. Stellt jemand das um, laeuft HttpClient eine Kette blind bis zu einem internen
    /// Host.
    /// </summary>
    [Fact]
    public void Create_FolgtRedirectsNichtAutomatisch()
    {
        Assert.False(CrawlHttpHandler.Create().AllowAutoRedirect);
    }

    /// <summary>
    /// DIE Bedingung, die den Fehler behebt: eine Verbindung darf die VPN-Rotation nicht
    /// ueberleben. Ist die Pool-Lebensdauer NICHT kuerzer als der Mindestabstand zweier Abrufe,
    /// greift der naechste Abruf eine Verbindung von vor der Rotation, das Lesen haengt, und erst
    /// der 30-s-Timeout beendet es (gemessen: 27 von 176 Abrufen, je 37 s).
    /// </summary>
    [Fact]
    public void Create_HaeltVerbindungenKuerzerAlsDenAbstandZweierAbrufe()
    {
        var handler = CrawlHttpHandler.Create();
        var minDelay = TimeSpan.FromMilliseconds(CrawlerService.DefaultMinDelayMs);

        Assert.True(handler.PooledConnectionIdleTimeout < minDelay,
            $"IdleTimeout {handler.PooledConnectionIdleTimeout} muss unter {minDelay} liegen");
        Assert.True(handler.PooledConnectionLifetime < minDelay,
            $"Lifetime {handler.PooledConnectionLifetime} muss unter {minDelay} liegen");
        // Und nicht null: innerhalb EINES Abrufs (Redirect-Kette, Hops im Millisekundenabstand)
        // soll die Verbindung durchaus wiederverwendet werden.
        Assert.True(handler.PooledConnectionIdleTimeout > TimeSpan.Zero);
    }
}
