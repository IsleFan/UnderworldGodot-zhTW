using System;
using System.IO;

namespace Underworld
{
    // UW2 LEV.ARK container header layout (verified from DataLoader.LoadUWBlock):
    //
    //   Bytes 0-3:              NoOfBlocks (Int32 LE)
    //   Bytes 4-5:              2 padding/unknown bytes (0x0000 in all observed files)
    //   Bytes 6 .. 6+(N*4)-1:  offsets[N]          (N × Int32 LE; 0 = block absent)
    //   Bytes 6+(N*4) ..
    //          6+(N*8)-1:       compressionFlags[N] (N × Int32 LE; 0=none, 2=compressed)
    //   Bytes 6+(N*8) ..
    //          6+(N*12)-1:      dataLengths[N]      (N × Int32 LE; uncompressed length)
    //   Bytes 6+(N*12) ..
    //          6+(N*16)-1:      reservedSpace[N]    (N × Int32 LE)
    //   Then block data at each recorded offset.
    //
    // UW1 LEV.ARK container header layout (default case in DataLoader.LoadUWBlock):
    //   Bytes 0-1:              NoOfBlocks (Int16 LE)
    //   Bytes 2 .. 2+(N*4)-1:  offsets[N]          (N × Int32 LE; 0 = block absent)
    //   Block data follows immediately after the offset table.
    //   No per-block metadata; targetDataLen is passed in by the caller.
    //
    // For UW2 we always write uncompressed blocks (compressionFlag = 0) to avoid the
    // incomplete RepackUW2 compressor.  The loader handles uncompressed blocks at
    // DataLoader.cs:418-422.

    /// <summary>
    /// Rebuilds a LEV.ARK container from in-memory game state.
    /// Visited levels use UWTileMap.dungeons[i].lev_ark_block.Data directly.
    /// Unvisited levels pass through from LevArkLoader.lev_ark_file_data.
    /// </summary>
    public static class LevArkWriter
    {
        // UW2 per-level block size (tilemap + animation overlay): the length every level block in
        // the shipped LEV.ARK and in DOS saves decompresses to (the loader's 0x8000 is only the
        // room it reads into).
        private const int UW2BlockSize = 0x7E08;
        // UW1 per-level block size
        private const int UW1BlockSize = UWTileMap.TileMapDataSize; // 0x7C08
        // UW1 per-level animation-overlay block size. 64 slots × 6 bytes, matching
        // the targetDataLen LevArkLoader.LoadOverlayBlock asks for.
        private const int UW1OverlayBlockSize = 64 * 6;

        /// <summary>
        /// Serialize one level block (UWBlock) to the raw bytes that should be
        /// stored in the ARK container.  For UW2 the result is exactly 0x7E08 bytes;
        /// for UW1 it is TileMapDataSize bytes.
        /// </summary>
        public static byte[] SerializeLevelBlock(UWBlock block)
        {
            int targetSize = (UWClass._RES == UWClass.GAME_UW2) ? UW2BlockSize : UW1BlockSize;
            byte[] result = new byte[targetSize];
            if (block?.Data != null)
            {
                int copyLen = Math.Min(block.Data.Length, targetSize);
                Buffer.BlockCopy(block.Data, 0, result, 0, copyLen);
            }
            if (UWClass._RES == UWClass.GAME_UW2)
            {
                CompactOverlays(result, UW2OverlayOffset);
                ClearUW2TimersPastCount(result);
                CrunchObjects(result);
            }
            return result;
        }

        private const int ObjectsOffset = 0x4000;
        private const int MobileSize = 0x1B;
        private const int MobileFreeListOffset = 0x7300;
        private const int StaticFreeListOffset = 0x74FC;

        static int Word(byte[] block, int offset) => block[offset] | (block[offset + 1] << 8);

        static int ObjectOffset(int index) => index < 256
            ? ObjectsOffset + index * MobileSize
            : ObjectsOffset + 256 * MobileSize + (index - 256) * 8;

        /// <summary>
        /// Obj_ListOkay for every level loaded this session (UW2): whether ObjCrunch would find their lists consistent.
        /// </summary>
        public static bool UW2ObjectListsOkay()
        {
            if ((UWClass._RES != UWClass.GAME_UW2) || (UWTileMap.dungeons == null))
            {
                return true;
            }
            foreach (var map in UWTileMap.dungeons)
            {
                var data = map?.lev_ark_block?.Data;
                if (data == null)
                {
                    continue;
                }
                var copy = new byte[UW2BlockSize];
                Buffer.BlockCopy(data, 0, copy, 0, Math.Min(data.Length, copy.Length));
                if (!CrunchObjects(copy))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// ObjCrunch (UW2 MAP.C Map_Save): when the object lists are consistent (Obj_ListOkay), every free record is
        /// zeroed, so the saved block is clean: the whole of a mobile one, the first 8 bytes of a static one. When they
        /// are not, the block is saved as it is, since a free record that is also in use must not be wiped; the player
        /// has been asked first (string 0x96, see UW2ObjectListsOkay). (A save is refused while an object is held, so the
        /// cursor's object, which the original also counts, is never there.)
        /// </summary>
        public static bool CrunchObjects(byte[] block)
        {
            var mobileFree = (short)Word(block, 0x7C02);
            var staticFree = (short)Word(block, 0x7C04);
            var counts = new int[1024];
            bool bad = false;
            int nmobile = 0, nstatic = 0;
            for (int i = 0; i <= mobileFree; i++)
            {
                var index = Word(block, MobileFreeListOffset + i * 2) & 0x3FF;
                bad |= counts[index]++ > 0;
                nmobile++;
            }
            for (int i = 0; i <= staticFree; i++)
            {
                var index = Word(block, StaticFreeListOffset + i * 2) & 0x3FF;
                bad |= counts[index]++ > 0;
                nstatic++;
            }
            int mobcount = 0, objcount = 0;
            //flog_list: each tile's list, and what each object holds; a list met more than 10 times is a loop
            bool CountList(int index)
            {
                while (index != 0)
                {
                    if (++counts[index] > 1)
                    {
                        bad = true;
                        if (counts[index] > 10)
                        {
                            return false;
                        }
                    }
                    var o = ObjectOffset(index);
                    if (((block[o + 1] & 0x80) == 0) && ((Word(block, o + 6) >> 6) != 0) && !CountList(Word(block, o + 6) >> 6))
                    {
                        return false;
                    }
                    if (index < 256)
                    {
                        mobcount++;
                    }
                    else
                    {
                        objcount++;
                    }
                    index = Word(block, o + 4) >> 6;
                }
                return true;
            }
            for (int tile = 0; tile < 64 * 64; tile++)
            {
                if (!CountList(Word(block, tile * 4 + 2) >> 6))
                {
                    bad = true;
                    break;
                }
            }
            if (counts[1] == 0)
            {//the player off the map (GrSq < 0): count_list counts it by itself, as on another level's block
                counts[1]++;
                mobcount++;
            }
            if (bad || (nmobile + mobcount != 0xFF) || (nstatic + objcount != 0x300))
            {
                return false;
            }
            for (int i = 0; i <= mobileFree; i++)
            {
                Array.Clear(block, ObjectOffset(Word(block, MobileFreeListOffset + i * 2) & 0x3FF), MobileSize);
            }
            for (int i = 0; i <= staticFree; i++)
            {
                Array.Clear(block, ObjectOffset(Word(block, StaticFreeListOffset + i * 2) & 0x3FF), 8);
            }
            return true;
        }

        // UW2 animation list in the level block: 64 entries of 6 bytes (link word, frames
        // left, tile x, tile y) at 0x7C08; UW1 keeps the same list as a block of its own.
        private const int UW2OverlayOffset = 0x7C08;
        // UW2 timer list: 64 words at 0x7D88, counted up to the first zero.
        private const int UW2TimerOffset = 0x7D88;

        /// <summary>
        /// Anim_Save (UW2 MAP.C) zeroes the timer list past timercount, the entries it has: everything after the
        /// first zero word, so a stale value cannot be counted by DOS.
        /// </summary>
        private static void ClearUW2TimersPastCount(byte[] block)
        {
            var end = UW2TimerOffset + 64 * 2;
            var i = UW2TimerOffset;
            while ((i < end) && ((block[i] | block[i + 1]) != 0))
            {
                i += 2;
            }
            Array.Clear(block, i, end - i);
        }
        private const int UW2OverlayCount = 64;

        /// <summary>
        /// Writes the animation list the way DOS's Anim_Save does: the running entries first,
        /// zeroes after them. DOS counts entries up to the first zero object index (MAP.C
        /// Anim_Load), while the port frees an entry by running its frames down to 0 and leaves
        /// the link in place. Such an entry is live to DOS, which ends it on the first frame and
        /// removes the object from the map (animation class flag 0x20): a door that had finished
        /// opening vanished, leaving the doorway as a passage.
        /// </summary>
        private static void CompactOverlays(byte[] block, int listOffset)
        {
            var live = new byte[UW2OverlayCount * 6];
            int count = 0;
            for (int i = 0; i < UW2OverlayCount; i++)
            {
                int ptr = listOffset + i * 6;
                int link = (block[ptr] | (block[ptr + 1] << 8)) >> 6;
                int frames = block[ptr + 2] | (block[ptr + 3] << 8);
                if (link != 0 && frames != 0)
                {
                    Buffer.BlockCopy(block, ptr, live, count++ * 6, 6);
                }
            }
            Buffer.BlockCopy(live, 0, block, listOffset, live.Length);
        }

        /// <summary>
        /// Serialize one UW1 animation-overlay block to the fixed 384 bytes the
        /// container stores. UW1 only: UW2 keeps overlays inside the level block
        /// itself, and the demo keeps them in LEVEL13.ANX rather than in the ARK.
        /// </summary>
        public static byte[] SerializeOverlayBlock(UWBlock block)
        {
            byte[] result = new byte[UW1OverlayBlockSize];
            if (block?.Data != null)
            {
                // A short buffer just means the remaining overlay slots are absent, so
                // zero-padding is correct. An over-long one cannot be represented in 64
                // six-byte slots and means something upstream grew the buffer, so say so
                // rather than truncating in silence. Writing a well-formed block still
                // beats throwing: LEV.ARK is written after DESC, PLAYER.DAT and
                // BGLOBALS.DAT, so aborting here would leave a half-updated save slot.
                // Console.Error, not GD.PushWarning: this runs in the headless save
                // tests, and touching Godot with no engine segfaults rather than
                // throwing. Not Debug.Print or Trace.WriteLine either, since those
                // compile out without DEBUG/TRACE and the warning would vanish from
                // release builds, which is where a silent truncation matters most.
                if (block.Data.Length > UW1OverlayBlockSize)
                {
                    Console.Error.WriteLine(
                        $"LevArkWriter: UW1 overlay block is {block.Data.Length} bytes, expected {UW1OverlayBlockSize}; truncating.");
                }
                int copyLen = Math.Min(block.Data.Length, UW1OverlayBlockSize);
                Buffer.BlockCopy(block.Data, 0, result, 0, copyLen);
                //Anim_Save: DOS counts the entries up to the first zero object index, so the running ones go first
                CompactOverlays(result, 0);
            }
            return result;
        }

        /// <summary>
        /// Rebuild the full LEV.ARK container from current game state.
        /// Returns the raw bytes ready to write to disk as SAVE{n}/LEV.ARK.
        /// </summary>
        public static byte[] Serialize()
        {
            return UWClass._RES == UWClass.GAME_UW2
                ? AssembleUW2Ark()
                : AssembleUW1Ark();
        }

        // -----------------------------------------------------------------------
        // UW2 writer
        // -----------------------------------------------------------------------

        private static byte[] AssembleUW2Ark()
        {
            // UW2 block layout: 80 levels × 4 slot types = 320 blocks.
            //   blocks   0..79  = tilemap+overlay for level 0..79
            //   blocks  80..159 = texmap for level 0..79
            //   blocks 160..239 = automap for level 0..79
            //   blocks 240..319 = notes for level 0..79

            int noOfBlocks = 320;

            // ---- Step 1: gather each block's raw bytes -------------------------
            byte[][] blockData = new byte[noOfBlocks][];

            for (int i = 0; i < noOfBlocks; i++)
            {
                UWBlock src = ExtractSourceBlock(i, targetLen: -1);
                blockData[i] = src?.Data; // null means absent (address == 0)
            }

            // For visited levels, replace the tilemap block (index 0..79) with live data.
            if (UWTileMap.dungeons != null)
            {
                for (int lvl = 0; lvl < UWTileMap.NO_OF_LEVELS; lvl++)
                {
                    if (UWTileMap.dungeons[lvl] != null)
                    {
                        UWBlock live = UWTileMap.dungeons[lvl].lev_ark_block;
                        if (live?.Data != null)
                        {
                            blockData[lvl] = SerializeLevelBlock(live);
                        }
                    }
                }
            }

            // Replace automap blocks (160..239) with the in-memory automap of every level loaded this
            // session, as SaveAutoMapLevel writes PlayersMap to block 0x9F + level (1-based) on each
            // save and level change. Without this the UW2 exploration since the game was loaded is lost.
            if (automap.automaps != null)
            {
                for (int lvl = 0; lvl < Math.Min(80, automap.automaps.Length); lvl++)
                {
                    if (automap.automaps[lvl]?.buffer != null &&
                        automap.automaps[lvl].buffer.Length == 64 * 64)
                    {
                        blockData[160 + lvl] = automap.automaps[lvl].buffer;
                    }
                }
            }

            // Replace automap-note blocks (240..319) with the in-memory notes.
            // A level whose notes were all deleted serialises to an empty array, which the
            // layout step below turns into an absent block. Skipping the replacement instead
            // would write the source ARK's notes back out and they would reappear on reload.
            // automapsnotes[lvl] is only non-null for a level that has been loaded, and the
            // constructor reads the source block, so an empty list means no notes.
            if (automapnote.automapsnotes != null)
            {
                for (int lvl = 0; lvl < 80; lvl++)
                {
                    if (automapnote.automapsnotes[lvl] != null)
                    {
                        blockData[240 + lvl] = automapnote.automapsnotes[lvl].Serialize();
                    }
                }
            }

            // ---- Step 2: compute layout ----------------------------------------
            // Header size: 4 (count) + 2 (padding) + N * (4+4+4+4) = 6 + N*16
            int headerSize = 6 + noOfBlocks * 16;
            int[] offsets = new int[noOfBlocks];
            int[] flags = new int[noOfBlocks];
            int[] lengths = new int[noOfBlocks];
            int[] reserved = new int[noOfBlocks];

            byte[] source = LevArkLoader.lev_ark_file_data;
            //the source's block flags (put_arc keeps bits 0 and 2): DATA has 7 or 5 for the maps, automaps and notes, 3 or 1 for the textures
            int SourceFlags(int i)
            {
                if (source != null && source.Length >= 6 + noOfBlocks * 16 && (int)Loader.getAt(source, 0, 32) == noOfBlocks)
                {
                    return (int)Loader.getAt(source, 6 + (i * 4) + (noOfBlocks * 4), 32);
                }
                return (i >= 80 && i < 160) ? 1 : 5;
            }

            // Assign offsets: layout all present blocks sequentially after header.
            int cursor = headerSize;
            for (int i = 0; i < noOfBlocks; i++)
            {
                if (blockData[i] != null && blockData[i].Length > 0)
                {
                    offsets[i] = cursor;
                    flags[i] = SourceFlags(i) & 5; // written uncompressed (bit 1 clear), bits 0 and 2 kept as put_arc keeps them
                    lengths[i] = blockData[i].Length;
                    reserved[i] = blockData[i].Length;//packed one after another: its allocation is its length, so DOS rewrites it in place
                    cursor += blockData[i].Length;
                }
                else
                {
                    // An absent block carries no allocation. Every absent block in the
                    // shipped UW2 archive has offset, length and reserved all zero.
                    offsets[i] = 0;
                    flags[i] = SourceFlags(i) & 5;
                    lengths[i] = 0;
                    reserved[i] = 0;
                }
            }

            // ---- Step 3: write output ------------------------------------------
            using var ms = new MemoryStream(cursor);
            using var bw = new BinaryWriter(ms);

            // Header: count (Int32) + padding (Int16)
            bw.Write((int)noOfBlocks);
            bw.Write((short)0); // 2 padding bytes

            // Offsets table
            for (int i = 0; i < noOfBlocks; i++) bw.Write(offsets[i]);
            // Compression flags table
            for (int i = 0; i < noOfBlocks; i++) bw.Write(flags[i]);
            // Data lengths table
            for (int i = 0; i < noOfBlocks; i++) bw.Write(lengths[i]);
            // Reserved space table
            for (int i = 0; i < noOfBlocks; i++) bw.Write(reserved[i]);

            // Block data
            for (int i = 0; i < noOfBlocks; i++)
            {
                if (blockData[i] != null && blockData[i].Length > 0)
                {
                    bw.Write(blockData[i]);
                }
            }

            return ms.ToArray();
        }

        // -----------------------------------------------------------------------
        // UW1 writer
        // -----------------------------------------------------------------------

        private static byte[] AssembleUW1Ark()
        {
            // UW1 block layout: 9 levels × 15 slot types = 135 blocks.
            //   blocks  0..8   = level tilemap
            //   blocks  9..17  = per-level overlay
            //   blocks 18..26  = texmap
            //   blocks 27..35  = automap
            //   blocks 36..44  = notes
            //   blocks 45..134 = unused

            int noOfBlocks = 135;
            byte[][] blockData = new byte[noOfBlocks][];

            // For UW1, LoadUWBlock (default case) requires the caller to supply targetDataLen
            // because the format has no per-block length metadata, only an offset table.
            // UW1BlockLength measures each block from the offsets, which handles all block
            // types (tilemap, overlay, texmap, automap, notes) without hard-coding per-type
            // sizes.
            byte[] uw1Src = LevArkLoader.lev_ark_file_data;
            int uw1HeaderBlocks = (uw1Src != null) ? (int)Loader.getAt(uw1Src, 0, 16) : noOfBlocks;

            for (int i = 0; i < noOfBlocks; i++)
            {
                int tLen;
                if (i < 9)
                {
                    tLen = UW1BlockSize;
                }
                else if (uw1Src == null || i >= uw1HeaderBlocks)
                {
                    tLen = 0;
                }
                else
                {
                    tLen = LevArkLoader.UW1BlockLength(uw1Src, i, uw1HeaderBlocks);
                }
                UWBlock src = ExtractSourceBlock(i, targetLen: tLen);
                blockData[i] = src?.Data;
            }

            // Replace visited tilemap blocks with live dungeon data.
            if (UWTileMap.dungeons != null)
            {
                // Same bound as the overlay loop below: dungeons is a public static
                // array, so do not assume its length agrees with NO_OF_LEVELS.
                int tileLevels = Math.Min(UWTileMap.NO_OF_LEVELS, UWTileMap.dungeons.Length);
                for (int lvl = 0; lvl < tileLevels; lvl++)
                {
                    if (UWTileMap.dungeons[lvl] != null)
                    {
                        UWBlock live = UWTileMap.dungeons[lvl].lev_ark_block;
                        if (live?.Data != null)
                        {
                            blockData[lvl] = SerializeLevelBlock(live);
                        }
                    }
                }
            }

            // Replace the texture map blocks (18..26) of the levels loaded: Tybal's maze swaps a floor texture in the
            // level's own texture map (swap_tmap), which SaveLevel writes back.
            if (UWClass._RES == UWClass.GAME_UW1 && UWTileMap.dungeons != null)
            {
                int texLevels = Math.Min(9, UWTileMap.dungeons.Length);
                for (int lvl = 0; lvl < texLevels; lvl++)
                {
                    var liveTex = UWTileMap.dungeons[lvl]?.tex_ark_block?.Data;
                    if (liveTex != null && blockData[18 + lvl] != null && liveTex.Length >= blockData[18 + lvl].Length)
                    {
                        var copy = new byte[blockData[18 + lvl].Length];
                        Buffer.BlockCopy(liveTex, 0, copy, 0, copy.Length);
                        blockData[18 + lvl] = copy;
                    }
                }
            }

            // Replace overlay blocks (9..17) with the live animation-overlay data
            // for any visited level.
            //
            // Without this the two halves of an in-flight animation disagree. A
            // moving door writes its object record into the tilemap block (door.cs
            // sets item 0x1CF) and its overlay record into ovl_ark_block (animo.cs
            // stores the object link, tile and duration). Serialising only the
            // tilemap leaves a moving-door object with no matching overlay, which
            // is an internally inconsistent LEV.ARK and is what a save taken
            // mid-animation produced. See hankmorgan/UnderworldGodot#43.
            //
            // UW1 proper only. UW2 has no separate overlay block, and the demo
            // reads overlays from LEVEL13.ANX rather than from the ARK, so in
            // neither case does block 9+lvl hold overlay data to replace.
            if (UWClass._RES == UWClass.GAME_UW1 && UWTileMap.dungeons != null)
            {
                // dungeons is a public static array, so bound by its actual length as
                // well as by NO_OF_LEVELS rather than trusting the two to agree.
                int overlayLevels = Math.Min(UWTileMap.NO_OF_LEVELS, UWTileMap.dungeons.Length);
                for (int lvl = 0; lvl < overlayLevels; lvl++)
                {
                    UWBlock liveOverlay = UWTileMap.dungeons[lvl]?.ovl_ark_block;
                    if (liveOverlay?.Data != null)
                    {
                        blockData[9 + lvl] = SerializeOverlayBlock(liveOverlay);
                    }
                }
            }

            // Replace automap blocks (27..35) with the in-memory automap buffer
            // for any visited level. DOS UW.EXE writes at least block 27 (the
            // current level's automap) on every save; without it, UW.EXE treats
            // the level as un-automapped and may mis-render the automap view.
            //
            // Each per-level automap buffer is a fixed 4096 bytes (64×64 tiles
            // × 1 byte/tile of visited+display flags).
            if (automap.automaps != null)
            {
                for (int lvl = 0; lvl < 9; lvl++)
                {
                    if (automap.automaps[lvl]?.buffer != null &&
                        automap.automaps[lvl].buffer.Length == 64 * 64)
                    {
                        blockData[27 + lvl] = automap.automaps[lvl].buffer;
                    }
                }
            }

            // Replace automap-note blocks (36..44) with the in-memory notes.
            // Without this, newly-created notes are silently lost on save because
            // ExtractSourceBlock returns the pre-play bytes from the source ARK. The same
            // applies in reverse to deletion: a level whose notes were all deleted
            // serialises to an empty array, which the layout step turns into an absent
            // block, matching how the shipped archive represents a level with no notes.
            if (automapnote.automapsnotes != null)
            {
                for (int lvl = 0; lvl < 9; lvl++)
                {
                    if (automapnote.automapsnotes[lvl] != null)
                    {
                        blockData[36 + lvl] = automapnote.automapsnotes[lvl].Serialize();
                    }
                }
            }

            // Header: 2-byte count + N × 4-byte offsets
            int headerSize = 2 + noOfBlocks * 4;
            int[] offsets = new int[noOfBlocks];
            int cursor = headerSize;
            for (int i = 0; i < noOfBlocks; i++)
            {
                if (blockData[i] != null && blockData[i].Length > 0)
                {
                    offsets[i] = cursor;
                    cursor += blockData[i].Length;
                }
                else
                {
                    offsets[i] = 0;
                }
            }

            using var ms = new MemoryStream(cursor);
            using var bw = new BinaryWriter(ms);

            bw.Write((short)noOfBlocks);
            for (int i = 0; i < noOfBlocks; i++) bw.Write(offsets[i]);
            for (int i = 0; i < noOfBlocks; i++)
            {
                if (blockData[i] != null && blockData[i].Length > 0)
                    bw.Write(blockData[i]);
            }

            return ms.ToArray();
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Extract block <paramref name="blockNo"/> from the source ARK file data.
        /// Returns null if the block is absent (address == 0).
        /// </summary>
        private static UWBlock ExtractSourceBlock(int blockNo, int targetLen)
        {
            byte[] src = LevArkLoader.lev_ark_file_data;
            if (src == null) return null;
            DataLoader.LoadUWBlock(src, blockNo, targetLen, out UWBlock uwb);
            return (uwb.DataLen > 0) ? uwb : null;
        }
    }
}
