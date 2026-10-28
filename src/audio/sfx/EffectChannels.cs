namespace Underworld.Sfx;

/// <summary>
/// The game side of the original's effect channels (SOUND.C), kept apart from the synthesis so it can be tested:
///   MIDI effects take one of a few slots (fx_play's fx_mask: UW1 four, UW2 three); a new effect with every slot
///   busy is dropped. A slot is held for the effect's SOUNDS.DAT length (1/256 s, counted down at 16 Hz after a
///   divide by 16; fx_timer) or until it is killed (kill_effect, kill_all_effects); 0xFFFF holds it.
///   UW2's one digital channel (digi_fx_play) is taken by a new sound whose priority (SOUNDS.DAT priority plus its
///   volume; the Guardian's 0x7D00) is at least that of the sound playing, else the sound goes to MIDI.
/// Handles are what play_effect returns: a MIDI slot, or 100 for the digital channel.
/// </summary>
public sealed class EffectChannels
{
    public const int DigitalHandle = 100;
    public const int GuardianPriority = 0x7D00;

    private readonly long[] _slotEnds;
    private int _digitalPriority;

    public EffectChannels(int midiSlots)
    {
        _slotEnds = new long[midiSlots];
    }

    public int MidiSlots => _slotEnds.Length;

    /// <summary>The first free MIDI slot, held from now for the effect's length; -1 when all are busy.</summary>
    public int AcquireMidi(long nowMs, ushort lengthWord)
    {
        for (int i = 0; i < _slotEnds.Length; i++)
        {
            if (_slotEnds[i] <= nowMs)
            {
                _slotEnds[i] = lengthWord == 0xFFFF ? long.MaxValue : nowMs + System.Math.Max(1, lengthWord * 1000L / 256);
                return i;
            }
        }
        return -1;
    }

    public bool MidiBusy(int slot, long nowMs) => slot >= 0 && slot < _slotEnds.Length && _slotEnds[slot] > nowMs;

    public void ReleaseMidi(int slot)
    {
        if (slot >= 0 && slot < _slotEnds.Length)
        {
            _slotEnds[slot] = 0;
        }
    }

    public void ReleaseAllMidi()
    {
        for (int i = 0; i < _slotEnds.Length; i++)
        {
            _slotEnds[i] = 0;
        }
    }

    /// <summary>
    /// Whether a sound of priority <paramref name="priority"/> may have the digital channel, which is busy when a sound
    /// is still playing on it. Taking it records the priority the next sound must match.
    /// </summary>
    public bool TryTakeDigital(int priority, bool busy)
    {
        if (busy && priority < _digitalPriority)
        {
            return false;
        }
        _digitalPriority = priority;
        return true;
    }
}
