# GameVoice

Reusable Blazor Server voice library (speech recognition, TTS, voice floor) for Draughts, Reversi and future games
such as Chess and WalkieTalkie. It is a Razor class library published as the [`GameVoice`](https://www.nuget.org/packages/GameVoice) package on nuget.org.

## What's in the package

| Piece | Purpose |
|---|---|
| `wwwroot/gameVoice.js` (`window.GameVoice`) | Browser speech recognition (push-to-talk), TTS unlock, voice enumeration/diagnostics, voice matching and `speak*` helpers. Calls back into .NET via `OnVoiceTranscript`, `OnVoiceError` and `OnVoiceEnded`. |
| `VoiceRoom` | Game-independent voice state for one room: talk floor (take/renew/release with expiry), room mute (only the muter can unmute) and each user's preferred TTS voice/language/region. Thread-safe. |
| `VoiceRoomService` | Room-id based operations on `VoiceRoom`s; notifies the app's `IVoiceRoomHost` after each change. |
| `IVoiceRoomHost` | Implemented by the app (e.g. the game service): finds a room by id and reacts to changes (touch timeouts, raise UI updates, announce voices). |
| `VoiceControls.razor` | Talk (hold to talk) and Mute/Unmute buttons. Owns the push-to-talk lifecycle: floor acquire/renew every 2 s/release, `GameVoice.start/stop`, transcript and error callbacks. |
| `VoicePreference` | A user's TTS voice key/language/region, with normalisation. |
| `IVoiceCatalog`, `VoiceCatalogEntry`, `VoiceCatalog` | Interface for the admin-allowed voice catalogue (storage stays in the app, e.g. a `UsableMsVoice` table) and token/key helpers. |
| `IVoicePreferenceStore` | Optional interface for persisting per-user voice preferences. |

Text chat, game rules and persistence stay in each app.

## Using it in an app

1. Reference the package (from nuget.org, so no extra package source or token is needed):

   ```xml
   <PackageReference Include="GameVoice" Version="1.0.0" />
   ```

2. Load the script before the app's own scripts in `App.razor`:

   ```html
   <script src="_content/GameVoice/gameVoice.js"></script>
   ```

3. Give each game (room) a `VoiceRoom` and implement `IVoiceRoomHost` on the service that owns the games:

   ```csharp
   public class ChessGame
   {
       public VoiceRoom Voice { get; } = new();
   }

   public class ChessService : IVoiceRoomHost
   {
       public VoiceRoom? FindVoiceRoom(string roomId) => GetGame(roomId)?.Voice;

       public void OnVoiceRoomChanged(string roomId, VoiceRoomChange change)
       {
           // touch the game's idle timer and raise your GameUpdated event
       }
   }
   ```

4. Register services in `Program.cs`:

   ```csharp
   builder.Services.AddSingleton<ChessService>();
   builder.Services.AddSingleton<IVoiceRoomHost>(sp => sp.GetRequiredService<ChessService>());
   builder.Services.AddGameVoice();
   ```

5. Detect support once after first render and drop the controls into the page (`@using GameVoice` in `_Imports.razor`):

   ```razor
   <VoiceControls @ref="_voiceControls"
                  RoomId="@_gameId" UserId="@_userId" UserName="@_userName"
                  Supported="@_voiceSupported" SupportChecked="@_voiceSupportChecked" TalkEnabled="@_talkEnabled"
                  TranscriptReceived="OnVoiceTranscript"
                  MessageRaised="OnVoiceMessage" />
   ```

   ```csharp
   await JS.InvokeAsync<bool>("GameVoice.installTtsUnlocker");
   _voiceSupported = await JS.InvokeAsync<bool>("GameVoice.isSupported");
   ```

   `TranscriptReceived` gets the trimmed final text; post it to your chat (e.g. with a `[VOICE] ` prefix).
   Use `_voiceControls.Status` for a status line ("Hold to talk", "Sam talking…", "Mic error: …").
   To read messages aloud call `GameVoice.speakAndDescribe(text, voiceKey, language, region)` or
   `GameVoice.speakDefaultAndDescribe(text)` with the sender's `room.GetPreference(senderId)`.

Rooms are not tied to two players: a WalkieTalkie group or a lobby can be a `VoiceRoom` too.

## Building and releasing

```bash
dotnet test GameVoice.slnx
dotnet pack src/GameVoice -c Release -o artifacts
```

To publish, bump nothing and push a tag: `git tag v1.0.1 && git push origin v1.0.1`. The
[publish workflow](.github/workflows/publish.yml) packs that version and pushes it to nuget.org using nuget.org
[Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) (policy: owner `djaus2`, repo `GameVoice`, workflow `publish.yml`; no API key or secret). New versions take a few minutes to appear after nuget.org validates them.
Then bump `<PackageReference Include="GameVoice" Version="..." />` in each app.

To try library changes in an app before publishing, pack to a local folder and restore from it:

```bash
dotnet pack src/GameVoice -c Release -o ~/localfeed -p:Version=1.0.1-dev
dotnet add <app>.csproj package GameVoice --version 1.0.1-dev --source ~/localfeed
```
