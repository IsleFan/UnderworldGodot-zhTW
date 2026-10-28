using Godot;
using Underworld.Sfx;

namespace Underworld
{
    /// <summary>
    /// UW2's one digital channel (SOUND.C digi_fx_play, update_digi_playback). The mono sample is mixed into a generator
    /// stream a little at a time, with the AIL pan and volume of the moment, the way AIL plays the sample through two
    /// small buffers. A sound played from a place keeps its source: each frame its pan and volume are worked out again
    /// from where the player now is (sound_move).
    /// </summary>
    public static class DigitalEffectPlayback
    {
        /// <summary>About as much as AIL's two 2 KB buffers hold at the samples' rates (so a pan change is heard as late).</summary>
        const float BufferSeconds = 0.2f;

        static AudioStreamGenerator stream;
        static AudioStreamGeneratorPlayback playback;
        static short[] sample;
        static int position;
        static int capacity;
        static byte vol, pan;
        static int loopsLeft;
        static bool shuttingDown;

        //digi_src: the place a sound comes from, or none (play_effect_here)
        static bool hasSource;
        static int sourceX, sourceY, sourceEffect, sourceVolDelta;

        /// <summary>
        /// Starts the sample on the player. With a source, the pan and volume follow it while it plays.
        /// </summary>
        /// <param name="loops">How many times more it plays once it has played out (digi_fx_play: (length >> 6) - 1 for a
        /// SOUNDS.DAT length of 5000 or more).</param>
        public static void Play(AudioStreamPlayer player, short[] mono, int mixRate, byte volume, byte panning, int loops = 0,
            bool fromPlace = false, int x = 0, int y = 0, int effectNo = 0, int volDelta = 0)
        {
            if (shuttingDown)
            {
                return;
            }
            stream = new AudioStreamGenerator { MixRate = mixRate, BufferLength = BufferSeconds };
            player.Stream = stream;
            player.Play();
            playback = (AudioStreamGeneratorPlayback)player.GetStreamPlayback();
            capacity = playback.GetFramesAvailable();
            sample = mono;
            position = 0;
            loopsLeft = loops;
            vol = volume;
            pan = panning;
            hasSource = fromPlace;
            sourceX = x; sourceY = y; sourceEffect = effectNo; sourceVolDelta = volDelta;
            Fill();
        }

        /// <summary>
        /// update_digi_playback, each frame: re-pans a sound with a source, tops up the buffer and stops the player once
        /// the sample has played out.
        /// </summary>
        public static void Update(AudioStreamPlayer player)
        {
            if ((stream == null) || shuttingDown)
            {//quitting keeps the wrappers for ReleaseBindings
                return;
            }
            if ((player == null) || !player.Playing || (player.Stream != stream))
            {//stopped (kill_effect), or the player was given another stream (a cutscene's speech)
                stream = null; playback = null; sample = null; hasSource = false;
                return;
            }
            if (hasSource)
            {
                var r = UWsoundeffects.CalculateSoundFallOff(sourceX, sourceY, sourceEffect, sourceVolDelta);
                vol = r.Vol;
                pan = r.Pan;
            }
            Fill();
            if ((position >= sample.Length) && (playback.GetFramesAvailable() >= capacity))
            {
                if (loopsLeft > 0)
                {//a looping sound starts again once both buffers have played out
                    loopsLeft--;
                    position = 0;
                    Fill();
                    return;
                }
                player.Stop();
                stream = null; playback = null; sample = null; hasSource = false;
            }
        }

        /// <summary>
        /// Quitting, phase one: stops the player and detaches its stream so the AudioServer can collect the playback.
        /// See UnderworldRoot.RequestQuit and issue #78.
        /// </summary>
        public static void BeginShutdown(AudioStreamPlayer player)
        {
            if ((player != null) && GodotObject.IsInstanceValid(player))
            {
                player.Stop();
                player.Stream = null;
            }
            sample = null; hasSource = false;
            shuttingDown = true;
        }

        /// <summary>Quitting, phase two: disposes the wrappers once the server has collected the playback.</summary>
        public static void ReleaseBindings()
        {
            playback?.Dispose();
            playback = null;
            stream?.Dispose();
            stream = null;
        }

        static void Fill()
        {
            var count = System.Math.Min(playback.GetFramesAvailable(), sample.Length - position);
            if (count <= 0)
            {
                return;
            }
            StereoPanBake.Gains(vol, pan, out int gainL, out int gainR);
            var scaleL = (float)gainL / StereoPanBake.GainDenominator / 32768f;
            var scaleR = (float)gainR / StereoPanBake.GainDenominator / 32768f;
            var frames = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                var s = sample[position + i];
                frames[i] = new Vector2(s * scaleL, s * scaleR);
            }
            position += count;
            playback.PushBuffer(frames);
        }
    }
}
