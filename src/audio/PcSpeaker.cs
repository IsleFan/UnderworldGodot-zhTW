using Godot;

namespace Underworld
{
    /// <summary>
    /// The PC speaker (MISCUTIL.C do_beep: sound, delay, nosound): a square wave of a frequency for some milliseconds.
    /// </summary>
    public static class PcSpeaker
    {
        const int MixRate = 22050;
        static AudioStreamPlayer player;

        public static void Beep(int freq, int ms)
        {
            if (main.instance == null)
            {
                return;
            }
            if (player == null)
            {
                player = new AudioStreamPlayer();
                main.instance.AddChild(player);
            }
            var samples = MixRate * ms / 1000;
            var data = new byte[samples * 2];
            var half = MixRate / (2.0 * freq);
            for (int i = 0; i < samples; i++)
            {
                short v = (short)(((int)(i / half) & 1) == 0 ? 0x2000 : -0x2000);
                data[i * 2] = (byte)(v & 0xFF);
                data[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
            }
            player.Stream = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = MixRate, Data = data };
            player.Play();
        }
    }
}
