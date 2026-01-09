// MsnFloor.RoomLinks.cs
namespace ZeroEditor.Zero1.Editors.MsnMap
{
    // Room <-> door linkage table
    public sealed partial class MsnFloor
    {
        public Dictionary<byte, List<LinkEntry>> RoomLinks { get; } = new();

        public sealed class LinkEntry
        {
            public short DoorId;
            public byte BeyondRoomId;
            public byte Unk;
            public int FileOffset;
        }

        public void WriteBackDoorLinkage(byte[] raw)
        {
            foreach (var kv in RoomLinks)
            {
                foreach (var e in kv.Value)
                {
                    int off = e.FileOffset;
                    if (off < 0 || off + 4 > raw.Length)
                        continue;

                    raw[off + 0] = (byte)(e.DoorId & 0xFF);
                    raw[off + 1] = (byte)((e.DoorId >> 8) & 0xFF);
                    raw[off + 2] = e.BeyondRoomId;
                    raw[off + 3] = e.Unk;
                }
            }
        }
    }
}
