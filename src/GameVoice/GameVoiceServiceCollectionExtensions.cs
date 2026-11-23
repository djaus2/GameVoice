using Microsoft.Extensions.DependencyInjection;

namespace GameVoice;

public static class GameVoiceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="VoiceRoomService"/>. The app must also register <see cref="IVoiceRoomHost"/>
    /// and <see cref="IVoicePreferenceStore"/> (and <see cref="IVoiceCatalog"/> if it uses one).
    /// </summary>
    public static IServiceCollection AddGameVoice(this IServiceCollection services)
    {
        services.AddSingleton<VoiceRoomService>();
        return services;
    }
}
