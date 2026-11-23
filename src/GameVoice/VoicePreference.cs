namespace GameVoice;

/// <summary>A user's preferred text-to-speech voice. Null members mean "browser default".</summary>
public sealed record VoicePreference(string? VoiceKey, string? Language, string? Region)
{
    public static VoicePreference Default { get; } = new(null, null, null);

    public bool IsDefault => VoiceKey is null && Language is null && Region is null;

    /// <summary>Trims values, lower-cases the language, upper-cases the region and maps blanks to null.</summary>
    public static VoicePreference Normalize(string? voiceKey, string? language, string? region)
    {
        static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        return new VoicePreference(
            Clean(voiceKey),
            Clean(language)?.ToLowerInvariant(),
            Clean(region)?.ToUpperInvariant());
    }
}
