// MsnFloor.Doors.cs
using System.Runtime.InteropServices;

namespace ZeroEditor.Zero1.Editors.MsnMap
{
    // MAP_DOOR_DAT (10)
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DoorDataPop
    {
        public float rot;
        public ushort pos_x;
        public short pos_y;
        public ushort pos_z;
        public short pad_s;
        public ushort type;
        public ushort mdl_no;
    }

    public sealed partial class MsnFloor
    {
        public uint DoorRootOff { get; set; }
        public List<short> DoorIds { get; } = new();
        public List<DoorEntry> Doors { get; } = new();
        public Dictionary<short, DoorEntry> DoorById { get; } = new();

        public sealed class DoorEntry
        {
            public short DoorId;
            public DoorDataPop Data;
            public int DoorDataPopOffset;
            public int DoorIndex;
            public int DoorPtrBlockOffset;
        }

        public void WriteBackDoors(byte[] raw)
        {
            foreach (var d in Doors)
            {
                int off = d.DoorDataPopOffset;
                if (off < 0 || off + 0x10 > raw.Length)
                    continue;

                var data = d.Data;
                MemoryMarshal.Write(raw.AsSpan(off, 0x10), ref data);
            }
        }
    }
}
