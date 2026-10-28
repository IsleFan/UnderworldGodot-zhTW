using System.Diagnostics;
using System.IO;
using Godot;
using Underworld.Sfx;

namespace Underworld
{
    /// <summary>
    /// Wrapper for calling appropriate Sound Effects in game. Entry points
    /// mirror the UW1/UW2 asm:
    ///   PlaySoundEffectAtAvatar         — non-positional (UW1 seg014_B1B / UW2 1DB2)
    ///   PlaySoundEffectAtCoordinate     — positional (UW1 seg014_8AE / UW2 1C50)
    ///   PlaySoundEffectAtObject         — object-based wrapper (UW1 seg014_B9C / UW2 1E7E)
    /// </summary>
    public class UWsoundeffects : UWClass
    {
        public const byte SoundEffectHit1 = 0x3;
        public const byte SoundEffectHit2 = 0x4;
        public const byte SoundEffectBowTwang = 0x9;
        public const byte SoundEffectDoor = 0xB;
        public const byte SoundEffectLanding = 0xF;
        public const byte SoundEffectSpellNotReady = 0xB; // spell timers are not yet implemented.
        public const byte SoundEffectSpell = 0x10;
        public const byte SoundEffectKlang = 0x11;
        public const byte SoundEffectRumble = 0x12;
        public const byte SoundEffectLockPick = 0x13;
        public const byte SoundEffectPortcullis = 0x14;
        public const byte SoundEffectSpellFailure = 0x16;
        public const byte SoundEffectLight = 0x20;
        public const byte SoundEffectSpellRing1 = 0x2A;
        public const byte SoundEffectSpellRing2 = 0x2c;
        public const byte SoundEffectFail = 0x2D;

        /// <summary>
        /// The sink every sound eventually flows through. pan is 0..0x7F
        /// (0x40 = centre); velocityOffset is a signed-byte delta added to
        /// the SOUNDS.DAT[+2] base velocity before clamping.
        /// Source: UW1 seg014_B1B (UW1_asm.asm:64927-65021) for UW1 path,
        ///         UW2 PlaySoundEffect_1DB2 (uw2_asm.asm:82585-82751) for UW2 path.
        /// </summary>
        /// <summary>The effect slots and UW2's digital channel (SOUND.C fx_play, digi_fx_play): UW1 has four MIDI slots, UW2 three.</summary>
        static EffectChannels channels;
        static EffectChannels Channels => channels ??= new EffectChannels(_RES == GAME_UW2 ? 3 : 4);

        /// <summary>No effect was played (the original's 0xFF).</summary>
        public const int NoEffect = -1;

        /// <returns>The effect's handle for <see cref="KillEffect"/>: its MIDI slot, or 100 for UW2's digital channel; NoEffect when it was not played.</returns>
        public static int PlaySoundEffectAtAvatar(byte effectno, byte pan, byte velocityOffset)
        {
            if ((effectno == 90) || (effectno == 91))
            {
                // TODO footstep sounds from NPCs, needs special handling
                return NoEffect;
            }
            if (!playerdat.SoundEffectsEnabled) return NoEffect;

            if (_RES == GAME_UW2)
            {
                return PlayUw2(effectno, pan, velocityOffset);
            }
            else
            {
                // UW1 OPL/TVFX path — mono hardware, pan is ignored by
                // the TVFX backend (authentic AdLib behaviour). Volume
                // attenuation flows through the backend's velocityOffset
                // hook. See src/audio/sfx/godot/SoundEffects.cs.
                return PlayMidi(effectno, pan, velocityOffset);
            }
        }

        /// <summary>
        /// fx_play: the effect takes a free MIDI slot for its length, or is dropped with every slot busy (UW2 also
        /// drops patch 0). A volume of 0 plays nothing: UW2 returns before playing, UW1's note on with velocity 0 is
        /// silent.
        /// </summary>
        static int PlayMidi(byte effectno, byte pan, byte velocityOffset)
        {
            var table = Sfx.SoundEffects.SoundDat;
            if (effectno >= table.Length)
            {
                Sfx.SoundEffects.Play(effectno, pan: pan, velocityOffset: velocityOffset);//warns and drops
                return NoEffect;
            }
            var entry = table[effectno];
            if ((_RES == GAME_UW2) && (entry.PatchNum == 0))
            {
                return NoEffect;
            }
            var slot = Channels.AcquireMidi((long)Time.GetTicksMsec(), entry.DurationWord);
            if (slot < 0)
            {
                return NoEffect;
            }
            if (entry.Velocity + (sbyte)velocityOffset > 0)
            {
                Sfx.SoundEffects.Play(effectno, pan: pan, velocityOffset: velocityOffset, slot: slot);
            }
            return slot;
        }

        /// <summary>Stops an effect by its handle (kill_effect): the digital channel, or a MIDI slot's note.</summary>
        public static void KillEffect(int handle)
        {
            if (handle >= EffectChannels.DigitalHandle)
            {
                main.instance?.DigitalAudioPlayer?.Stop();
            }
            else if (handle >= 0)
            {
                Channels.ReleaseMidi(handle);
                Sfx.SoundEffects.NoteOff(handle);
            }
        }

        /// <summary>Stops every effect (kill_all_effects): all MIDI slots and the digital channel.</summary>
        public static void KillAllEffects()
        {
            Channels.ReleaseAllMidi();
            Sfx.SoundEffects.NoteOff(-1);
            if (_RES == GAME_UW2)
            {
                main.instance?.DigitalAudioPlayer?.Stop();
            }
        }

        /// <summary>
        /// Object-based positional emit. Source: UW1 seg014_B9C
        /// (UW1_asm.asm:65027-65091), UW2 PlaySoundEffectAtObject_1E7E
        /// (uw2_asm.asm:82757-82863). Extracts packed (tile&lt;&lt;3 | fine)
        /// coords from the object and forwards.
        /// </summary>
        public static int PlaySoundEffectAtObject(byte effectNo, uwObject obj, int volDelta)
        {
            // Packed coords: (tile << 3) | fine. 8 units per tile.
            // uw2_asm.asm:82814-82848 pattern.
            int packedX = (obj.tileX << 3) | (obj.xpos & 0x7);
            int packedY = (obj.tileY << 3) | (obj.ypos & 0x7);
            return PlaySoundEffectAtCoordinate(effectNo, packedX, packedY, volDelta);
        }

        /// <summary>
        /// Positional emit by packed coord. Source: UW1 seg014_8AE
        /// (UW1_asm.asm:64454-64921), UW2 PlaySoundEffect_1C50
        /// (uw2_asm.asm:82356-82579).
        ///
        /// Special-cases:
        ///   effectNo >= 100  — guardian laughter, played full-volume centre-pan.
        ///   effectNo == 90   — NPC walk footstep, redirects to SOUNDS.DAT entry 1.
        ///   effectNo == 91   — NPC run footstep, redirects to SOUNDS.DAT entry 2.
        /// (uw2_asm.asm:82405-82425 in PlaySoundEffect_1C50.)
        /// </summary>
        public static int PlaySoundEffectAtCoordinate(byte effectNo, int packedX, int packedY, int volDelta)
        {
            if (!playerdat.SoundEffectsEnabled) return NoEffect;

            // Guardian laughter bypasses distance attenuation entirely.
            if (effectNo >= 100)
            {
                return PlaySoundEffectAtAvatar(effectNo, pan: 0x40, velocityOffset: 0);
            }

            // Footstep id redirection — uw2_asm.asm:82405-82425.
            byte lookupId = effectNo switch
            {
                90 => 1,
                91 => 2,
                _ => effectNo,
            };

            var r = CalculateSoundFallOff(packedX, packedY, lookupId, volDelta);
            if (r.Culled) return NoEffect;

            // Convert absolute vol back into a velocityOffset for the sink,
            // which adds it to SOUNDS.DAT[+2] before clamping. Round-trip is
            // exact: baseVel + (r.Vol - baseVel) = r.Vol.
            byte baseVel = LookupBaseVelocity(lookupId);
            sbyte velocityOffset = (sbyte)(r.Vol - baseVel);
            //play_effect: a sound that goes to the digital channel keeps its place (digi_src) to be re-panned from
            digitalSource = (packedX, packedY, lookupId, volDelta);
            try
            {
                return PlaySoundEffectAtAvatar(lookupId, pan: r.Pan, velocityOffset: (byte)velocityOffset);
            }
            finally
            {
                digitalSource = null;
            }
        }

        /// <summary>The place of the sound being played from one, for the digital channel.</summary>
        static (int x, int y, int effect, int volDelta)? digitalSource;

        /// <summary>
        /// Compute (vol, pan, culled) from source position, player state,
        /// and a SOUNDS.DAT entry id. Pure pass-through to
        /// <see cref="PositionalAudio.Sample"/> with the coord + heading
        /// extraction wired to the port's player object.
        /// </summary>
        public static SoundFalloff CalculateSoundFallOff(int packedX, int packedY, int effectNo, int volDelta)
        {
            var player = playerdat.playerObject;

            // Player coords: (npc_xhome << 3) + xpos, matching
            // uw2_asm.asm:79391-79421 which reads obj+0x16 tile-home bits
            // shifted up 3, then adds obj+2 fine-fraction bits.
            int playerX = (player.npc_xhome << 3) + (player.xpos & 0x7);
            int playerY = (player.npc_yhome << 3) + (player.ypos & 0x7);

            // 8-bit heading. Per UW2 uw2_asm.asm:79427-79441:
            //   heading8 = (tileOctant << 5) | subAngle.
            byte heading8 = (byte)(((player.heading & 0x07) << 5) | (player.npc_heading & 0x1F));

            int baseVelocity = LookupBaseVelocity((byte)effectNo);

            return PositionalAudio.Sample(
                srcX: packedX, srcY: packedY,
                playerX: playerX, playerY: playerY,
                heading8: heading8,
                baseVelocity: baseVelocity,
                volDelta: (sbyte)volDelta);
        }

        /// <summary>
        /// SOUNDS.DAT[+2] per-sound base velocity. Returns 0x7F as a
        /// conservative default when the SFX subsystem hasn't loaded
        /// its entry table. Source: UW1_asm.asm:64622, uw2_asm.asm:82447.
        /// </summary>
        private static byte LookupBaseVelocity(byte effectNo)
        {
            var table = Sfx.SoundEffects.SoundDat;
            return effectNo < table.Length ? table[effectNo].Velocity : (byte)0x7F;
        }

        /// <summary>
        /// UW2 VOC digital-sample playback path. Loads the VOC, bakes the
        /// mono sample into stereo-interleaved int16 using Miles AIL2's
        /// pan_graph × V / 16129 curve (external/AIL2/DMASOUND.ASM:287-294
        /// and :993-1006), wraps in an AudioStreamWav, plays through
        /// DigitalAudioPlayer. Falls back to the MIDI sink on missing VOC
        /// (uw2_asm.asm:82573 analogue).
        /// </summary>
        private static int PlayUw2(byte effectno, byte pan, byte velocityOffset)
        {
            if (effectno == 0xFF) return NoEffect;

            // Effective vol = clamp(baseVel + signed velocityOffset, 0..0x7F).
            // Matches UW1_asm.asm:64836-64849 / uw2_asm.asm:82447-82638.
            byte baseVel = LookupBaseVelocity(effectno);
            int effective = baseVel + (sbyte)velocityOffset;
            if (effective < 0) effective = 0;
            if (effective > 0x7F) effective = 0x7F;
            byte vol = (byte)effective;
            if ((effectno < 100) && (vol == 0))
            {//play_effect, play_effect_here: nothing at no volume
                return NoEffect;
            }

            // vocLoader.Load prepends BasePath/SOUND/ internally
            // (src/loaders/vocloader.cs:55).
            string vocName = effectno >= 100
                ? $"UW{effectno - 100:0#}.VOC"   // guardian laughter
                : $"SP{effectno:0#}.VOC";
            string fullPath = Path.Combine(BasePath, "SOUND", vocName);

            //an effect SOUNDS.DAT marks as not digital plays its MIDI patch, though SP40.VOC exists for 0x28
            var table = Sfx.SoundEffects.SoundDat;
            var digi = effectno >= 100 || effectno >= table.Length || table[effectno].Digi;
            //digi_fx_play: the one digital channel goes to a sound of at least the priority of the one playing
            //(SOUNDS.DAT's priority plus the volume, the Guardian's 0x7D00); one that loses goes to MIDI
            var priority = effectno >= 100 ? EffectChannels.GuardianPriority
                : (effectno < table.Length ? table[effectno].Priority : 0) + vol;
            if (digi && File.Exists(fullPath)
                && Channels.TryTakeDigital(priority, main.instance?.DigitalAudioPlayer?.Playing == true))
            {
                if (effectno>2)
                {
                    Debug.Print($"Playing sound {vocName} vol={vol:X2} pan={pan:X2}");
                }                
                var cached = CachedSample(effectno, vocName);
                if (cached.HasValue)
                {
                    var (mono, rate) = cached.Value;
                    var source = effectno <= 0x63 ? digitalSource : null;
                    var length = effectno < table.Length ? table[effectno].DurationWord : 0;
                    var loops = (effectno <= 0x63) && (length >= 5000) ? (length >> 6) - 1 : 0;
                    DigitalEffectPlayback.Play(main.instance.DigitalAudioPlayer, mono, rate, vol, pan, loops,
                        fromPlace: source.HasValue,
                        x: source?.x ?? 0, y: source?.y ?? 0, effectNo: source?.effect ?? 0, volDelta: source?.volDelta ?? 0);
                    return EffectChannels.DigitalHandle;
                }
            }
            if (effectno >= 100)
            {
                return NoEffect;
            }
            // No VOC, or the digital channel kept by a stronger sound: the MIDI effect.
            return PlayMidi(effectno, pan, velocityOffset);
        }

        /// <summary>
        /// digi_fx_play's cache of four sounds (ds_sounds_in_ems): a sound already there is not read again; a new one
        /// replaces the one played longest ago. (The original's EMS pages can run out, which turns the digital sound off
        /// for the session; a memory limit of DOS, not kept.)
        /// </summary>
        static readonly System.Collections.Generic.List<(byte fx, short[] mono, int rate)> sampleCache = new();

        static (short[] mono, int rate)? CachedSample(byte effectno, string vocName)
        {
            for (int i = 0; i < sampleCache.Count; i++)
            {
                if (sampleCache[i].fx == effectno)
                {
                    var hit = sampleCache[i];
                    sampleCache.RemoveAt(i);
                    sampleCache.Add(hit);
                    return (hit.mono, hit.rate);
                }
            }
            AudioSample sound = vocLoader.Load(vocName);
            if (sound == null || sound.AudioBuffer == null)
            {
                return null;
            }
            // AudioSample.AudioBuffer is signed 8-bit PCM already (vocLoader subtracts 128 at src/loaders/vocloader.cs:96,114).
            var entry = (effectno, VocToPcm16Mono(sound), sound.SampleRate);
            if (sampleCache.Count >= 4)
            {
                sampleCache.RemoveAt(0);
            }
            sampleCache.Add(entry);
            return (entry.Item2, entry.SampleRate);
        }

        private static short[] VocToPcm16Mono(AudioSample sound)
        {
            // AudioSample.AudioBuffer stores signed 8-bit PCM (already
            // centred on 0; see src/loaders/vocloader.cs:96,114). Interpret
            // each byte as an sbyte and promote to int16 by shift-left 8.
            byte[] src = sound.AudioBuffer;
            var dst = new short[src.Length];
            for (int i = 0; i < src.Length; i++)
                dst[i] = (short)((sbyte)src[i] << 8);
            return dst;
        }
    }
}
