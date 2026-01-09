// MsnFloor.cs
using ZeroEditor.Zero1.Editors.MsnMap.Editor;

namespace ZeroEditor.Zero1.Editors.MsnMap
{
    public sealed partial class MsnFloor : IMapOverlaySource
    {
        public int FloorIndex { get; init; }

        public uint FloorBaseOff { get; set; }
        public uint Map0RootOff { get; set; }
        public uint Map5RootOff { get; set; }
        public uint Map10RootOff { get; set; }

        public void WriteBackAll(byte[] raw)
        {
            WriteBackDoors(raw);
            WriteBackFurniture(raw);
            WriteBackDoorLinkage(raw);
            WriteBackRoomPositions(raw);
            WriteBackSpatial(raw);
        }
    }
}
