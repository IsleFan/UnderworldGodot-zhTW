namespace Underworld.Sfx;

/// <summary>
/// Backend-agnostic SFX trigger interface. TVFX over OPL is the only
/// implementation, used whatever the music synth (see SoundEffects.Initialize).
/// </summary>
public interface ISfxBackend
{
    void Play(SoundEntry entry, byte pan, byte velocityOffset);
    void Dispose();
}
