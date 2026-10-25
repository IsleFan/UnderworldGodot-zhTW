using System;
using System.IO;
using Godot;

namespace Underworld.Sfx;

/// <summary>
/// Game-facing SFX entry point. Game code calls
/// <see cref="Play(int, byte, byte)"/>; the façade resolves the sound id,
/// dispatches to the active backend, and silently no-ops if SFX failed to
/// initialise (missing data files, unsupported synth choice).
///
/// Every synth plays the effects on the TVFX/OPL backend, the Sound Blaster's:
/// with <c>synth=cm32l|mt32|soundfont</c> the music is MT-32 or SoundFont and the
/// effects OPL. The original played them on the MT-32 too (UW.MT's bank 1), which
/// is not implemented; a SoundFont cannot hold those timbres at all.
/// </summary>
public static class SoundEffects
{
    public static SoundEntry[] SoundDat = Array.Empty<SoundEntry>();
    private static ISfxBackend _backend;

    /// <summary>
    /// Boot-time setup. Call once after <see cref="SfxStreamPlayer.Instance"/>
    /// is in the scene tree and after <c>uwsettings.instance</c> is loaded.
    /// Caller is responsible for synth selection match: pass the same string
    /// that selected the music synth.
    /// </summary>
    /// <param name="synth">music-synth string from uwsettings — every known one routes to TVFX, others log + no-op.</param>
    /// <param name="uw1SoundDir">absolute path to UW1's SOUND/ directory.</param>
    public static void Initialize(string synth, string uw1SoundDir)
    {
        SoundDat = Array.Empty<SoundEntry>();
        _backend = null;

        // Always load SOUNDS.DAT — the file is small and the trigger lookups
        // need it regardless of which backend is active. If it's missing,
        // disable SFX entirely.
        var soundsPath = Path.Combine(uw1SoundDir, "SOUNDS.DAT");
        if (!File.Exists(soundsPath))
        {
            GD.PushWarning($"SoundEffects: SOUNDS.DAT not found at {soundsPath}; SFX disabled.");
            return;
        }
        try { SoundDat = SoundsDatLoader.Load(soundsPath); }
        catch (Exception e)
        {
            GD.PushWarning($"SoundEffects: SOUNDS.DAT parse failed: {e.Message}; SFX disabled.");
            return;
        }

        // Backend selection. Lower-cased synth string per uwsettings convention.
        string s = synth?.ToLowerInvariant() ?? "soundfont";
        switch (s)
        {
            case "opl":
            case "cm32l":
            case "mt32":
            case "soundfont":
                _backend = TryCreateOplBackend(uw1SoundDir);
                break;
            default:
                GD.PushWarning($"SoundEffects: unknown synth='{s}'; SFX disabled.");
                break;
        }
    }

    private static ISfxBackend TryCreateOplBackend(string uw1SoundDir)
    {
        var bankPath = Path.Combine(uw1SoundDir, "UW.AD");
        if (!File.Exists(bankPath))
        {
            GD.PushWarning($"SoundEffects: UW.AD not found at {bankPath}; OPL SFX disabled.");
            return null;
        }
        var player = SfxStreamPlayer.Instance;
        if (player == null)
        {
            GD.PushError("SoundEffects: SfxStreamPlayer.Instance is null. " +
                         "Add SfxStreamPlayer to the scene tree before calling SoundEffects.Initialize.");
            return null;
        }
        try
        {
            var bank = TvfxPatchBank.Load(bankPath);
            return new TvfxSfxBackend(bank, player);
        }
        catch (Exception e)
        {
            GD.PushWarning($"SoundEffects: UW.AD load failed: {e.Message}; OPL SFX disabled.");
            return null;
        }
    }

    /// <summary>
    /// Trigger a sound effect. <paramref name="soundId"/> is the SOUNDS.DAT
    /// entry index (UW1 has 24 entries, 0..23). Out-of-range ids are dropped
    /// with a one-line warning. Pan is a standard MIDI 0..0x7F (OPL backend
    /// ignores it; future MT-32 backend honours it).
    /// </summary>
    public static void Play(int soundId, byte pan = 0x40, byte velocityOffset = 0)
    {
        if (_backend == null) return;
        if ((uint)soundId >= (uint)SoundDat.Length)
        {
            GD.PushWarning($"SoundEffects.Play: id {soundId} out of range (have {SoundDat.Length} entries)");
            return;
        }
        _backend.Play(SoundDat[soundId], pan, velocityOffset);
    }

    /// <summary>Tear-down for orderly shutdown. Does not destroy the
    /// <see cref="SfxStreamPlayer"/> node — that is managed by the scene tree.</summary>
    public static void Shutdown()
    {
        _backend?.Dispose();
        _backend = null;
        SoundDat = Array.Empty<SoundEntry>();
    }
}
