using System.Runtime.InteropServices;

namespace ZeroEditor.Zero1.Editors.MsnMap
{
    // MAP_FURNITUR (11)
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FurnDataPop
    {
        public float rot_y;
        public float rot_x;
        public ushort pos_x;
        public short pos_y;
        public ushort pos_z;
        public short attr_id;
        public short model_no;
        public short id;
        public short top;
        public short btm;
        public byte snum;
        public byte unk19;
        public byte unk1A;
        public byte unk1B;
    }
    public sealed partial class MsnFloor
    {
        public uint FurnRootOff { get; set; }
        public List<byte> FurnRoomIds { get; } = new();
        public Dictionary<byte, List<FurnEntry>> FurnitureByRoom { get; } = new();
        public List<FurnEntry> Furniture { get; } = new();

        public sealed class FurnEntry
        {
            public byte RoomId;
            public FurnDataPop Data;
            public int FurnDataPopOffset;
            public uint WordAddrOff;
            public int PtrInTableOffset;
        }

        public void WriteBackFurniture(byte[] raw)
        {
            int size = Marshal.SizeOf<FurnDataPop>();
            foreach (var f in Furniture)
            {
                int off = f.FurnDataPopOffset;
                if (off < 0 || off + size > raw.Length) continue;

                var data = f.Data;
                MemoryMarshal.Write(raw.AsSpan(off, size), ref data);
            }
        }
    }
}
