// MsnFloor.Rooms.cs
namespace ZeroEditor.Zero1.Editors.MsnMap
{
    // MAP_ROOM_DAT (0)
    public sealed partial class MsnFloor
    {
        public List<byte> RoomIds { get; } = new();

        // Room order list from MAP_ROOM_DAT’s room-id list
        public List<byte> RoomDispOrder { get; } = new();

        public Dictionary<byte, RoomPosEntry> RoomPosByRoomId { get; } = new();

        public sealed class RoomPosEntry
        {
            public byte RoomId;
            public ushort X;
            public short Y;
            public ushort Z;
            public short Height;
            public int FileOffset;
            public int RoomIndex;
            public uint PtrWordAddrOff;
        }

        public void WriteBackRoomPositions(byte[] raw)
        {
            foreach (var kv in RoomPosByRoomId)
            {
                var e = kv.Value;
                int off = e.FileOffset;
                if (off < 0 || off + 8 > raw.Length)
                    continue;

                raw[off + 0] = (byte)(e.X & 0xFF);
                raw[off + 1] = (byte)((e.X >> 8) & 0xFF);

                raw[off + 2] = (byte)(e.Y & 0xFF);
                raw[off + 3] = (byte)((e.Y >> 8) & 0xFF);

                raw[off + 4] = (byte)(e.Z & 0xFF);
                raw[off + 5] = (byte)((e.Z >> 8) & 0xFF);

                raw[off + 6] = (byte)(e.Height & 0xFF);
                raw[off + 7] = (byte)((e.Height >> 8) & 0xFF);
            }
        }
    }
}
