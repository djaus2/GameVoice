namespace GameVoice;

/// <summary>A browser voice the admin has allowed players to use.</summary>
public sealed record VoiceCatalogEntry(
    string BrowserFamily,
    string Token,
    string VoiceName,
    string? VoiceLang,
    string? VoiceUri,
    DateTime UpdatedUtc);

/// <summary>Catalogue of allowed voices per browser family (the app owns the storage, e.g. a DB table).</summary>
public interface IVoiceCatalog
{
    Task LoadAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<VoiceCatalogEntry> GetVoices(string browserFamily);

    IReadOnlyCollection<string> GetTokens(string browserFamily);

    Task<int> ReplaceVoicesAsync(
        string browserFamily,
        IEnumerable<(string Token, string VoiceName, string? VoiceLang, string? VoiceUri)> voices,
        CancellationToken cancellationToken = default);
}
