// MapDoorParser.cs
using System.Diagnostics;
using System.Runtime.InteropServices;
using ZeroEditor.Zero1.Editors.MsnMap.Floor;

namespace ZeroEditor.Zero1.Editors.MsnMap
{
    public static class MsnMapParser
    {
        private const bool LOG = false;

        private static string HexList(IEnumerable<byte> xs) =>
            string.Join(",", xs.Select(x => x.ToString("X2")));

        public static List<MsnFloor> ParseDoors(byte[] raw, int missionNo)
        {
            var b = new BlobView(raw);

            if (!b.InBounds(0, 0x10))
                throw new InvalidOperationException("Map obj too small for floor header.");

            uint[] floorRootOffs = new uint[4]
            {
                b.U32(0x00),
                b.U32(0x04),
                b.U32(0x08),
                b.U32(0x0C),
            };

            var floors = new List<MsnFloor>(4);

            for (int fi = 0; fi < 4; fi++)
            {
                if (!FloorExist.IsFloorAvailable(missionNo, fi))
                    continue;

                uint floorRootOff = floorRootOffs[fi];
                if (floorRootOff == 0 || !b.InBounds((int)floorRootOff, 4 * 13))
                    continue;

                // Floor root points to a table of 13 u32
                var floor = new MsnFloor
                {
                    FloorIndex = fi,
                    FloorBaseOff = floorRootOff,

                    Map0RootOff = b.U32((int)floorRootOff + 0x00), // MAP_ROOM_DAT
                    Map5RootOff = b.U32((int)floorRootOff + 0x14), // MAP_HEIGHT
                    Map10RootOff = b.U32((int)floorRootOff + 0x28), // MAP_DOOR_DAT

                    DoorRootOff = 0,
                    FurnRootOff = 0,
                };

                // MAP_ROOM_DAT: parse room info + room bounds polygons (PosInAreaJudge0).
                TryParseRoomPositionsAndRoomBounds(b, floor, floor.Map0RootOff);

                // MAP_HEIGHT: parse height areas (PosInAreaJudge1).
                TryParseHeightAreas(b, floor, floor.Map5RootOff);

                floor.RebuildOverlayPolys();

                // Furniture root is MAP_FURNITUR (11) at floorRoot + 11*4 = 0x2C.
                uint furnRootOff = b.U32((int)floorRootOff + 0x2C);
                floor.FurnRootOff = furnRootOff;
                TryParseFurniture(b, floor, furnRootOff);

                // Doors (MAP_DOOR_DAT=10) is special-cased in the game code?
                uint doorRootOff = floor.Map10RootOff;
                if (doorRootOff == 0 || !b.InBounds((int)doorRootOff, 0x0C))
                {
                    floors.Add(floor);
                    continue;
                }

                floor.DoorRootOff = doorRootOff;

                uint pairOff = b.U32((int)doorRootOff + 0x00);
                uint roomLinksTblOff = b.U32((int)doorRootOff + 0x04);
                uint doorPtrTblOff = b.U32((int)doorRootOff + 0x08);

                if (pairOff == 0 || !b.InBounds((int)pairOff, 0x08))
                {
                    floors.Add(floor);
                    continue;
                }

                uint doorRoomNumOff = b.U32((int)pairOff + 0x00);
                uint doorKeyListOff = b.U32((int)pairOff + 0x04);

                // Door ID list [u16 count][s16 ids[count]]
                if (doorKeyListOff == 0 || !b.InBounds((int)doorKeyListOff, 2))
                {
                    floors.Add(floor);
                    continue;
                }

                ushort doorCount = b.U16((int)doorKeyListOff);
                int doorIdListBytes = 2 + doorCount * 2;
                if (!b.InBounds((int)doorKeyListOff, doorIdListBytes))
                {
                    floors.Add(floor);
                    continue;
                }

                floor.DoorIds.Clear();
                for (int i = 0; i < doorCount; i++)
                    floor.DoorIds.Add(b.S16((int)doorKeyListOff + 2 + i * 2));

                if (doorCount == 0)
                {
                    floors.Add(floor);
                    continue;
                }

                // Room list [u8 roomCount][u8 ids[roomCount]]
                if (doorRoomNumOff == 0 || !b.InBounds((int)doorRoomNumOff, 1))
                {
                    floors.Add(floor);
                    continue;
                }

                byte roomCount = b.U8((int)doorRoomNumOff);
                if (!b.InBounds((int)doorRoomNumOff + 1, roomCount))
                {
                    floors.Add(floor);
                    continue;
                }

                floor.RoomIds.Clear();
                for (int i = 0; i < roomCount; i++)
                    floor.RoomIds.Add(b.U8((int)doorRoomNumOff + 1 + i));

                if (!b.InBounds((int)roomLinksTblOff, roomCount * 4) ||
                    !b.InBounds((int)doorPtrTblOff, doorCount * 4))
                {
                    floors.Add(floor);
                    continue;
                }

                // Room Links table, PER ROOM [u8 cnt][pad?][records...]
                floor.RoomLinks.Clear();
                for (int i = 0; i < roomCount; i++)
                {
                    uint roomLinkOff = b.U32((int)roomLinksTblOff + i * 4);
                    if (roomLinkOff == 0 || !b.InBounds((int)roomLinkOff, 2))
                        continue;

                    byte cnt = b.U8((int)roomLinkOff);
                    int needBytes = 2 + cnt * 4;
                    if (!b.InBounds((int)roomLinkOff, needBytes))
                        continue;

                    byte roomId = floor.RoomIds[i];
                    var list = new List<MsnFloor.LinkEntry>(cnt);

                    for (int j = 0; j < cnt; j++)
                    {
                        int rec = (int)roomLinkOff + 2 + j * 4;

                        short did = b.S16(rec + 0);
                        byte beyond = b.U8(rec + 2);
                        byte unk = b.U8(rec + 3);

                        list.Add(new MsnFloor.LinkEntry
                        {
                            DoorId = did,
                            BeyondRoomId = beyond,
                            Unk = unk,
                            FileOffset = rec
                        });
                    }

                    floor.RoomLinks[roomId] = list;
                }

                // Door pointers...
                floor.Doors.Clear();
                floor.DoorById.Clear();

                for (int i = 0; i < doorCount; i++)
                {
                    uint offBlock = b.U32((int)doorPtrTblOff + i * 4);
                    if (offBlock == 0 || !b.InBounds((int)offBlock, 4))
                        continue;

                    uint doorDataOff = b.U32((int)offBlock);
                    if (doorDataOff == 0 || !b.InBounds((int)doorDataOff, 0x10))
                        continue;

                    var pop = b.ReadStruct<DoorDataPop>((int)doorDataOff);

                    var entry = new MsnFloor.DoorEntry
                    {
                        DoorId = i < floor.DoorIds.Count ? floor.DoorIds[i] : (short)0,
                        Data = pop,
                        DoorDataPopOffset = (int)doorDataOff,
                        DoorIndex = i,
                        DoorPtrBlockOffset = (int)offBlock
                    };

                    floor.Doors.Add(entry);
                    floor.DoorById[entry.DoorId] = entry;
                }

                floors.Add(floor);
            }

            return floors;
        }

        private static void TryParseFurniture(BlobView b, MsnFloor floor, uint furnRootOff)
        {
            floor.FurnRoomIds.Clear();
            floor.FurnitureByRoom.Clear();
            floor.Furniture.Clear();

            if (furnRootOff == 0 || !b.InBounds((int)furnRootOff, 8))
                return;

            // map-root first u32 is RoomIdList offset
            uint roomListOff = b.U32((int)furnRootOff + 0x00);
            if (roomListOff == 0 || !b.InBounds((int)roomListOff, 1))
                return;

            byte roomCount = b.U8((int)roomListOff + 0x00);
            if (roomCount == 0 || roomCount > 0x3C)
                return;

            if (!b.InBounds((int)roomListOff, 1 + roomCount))
                return;

            if (!b.InBounds((int)furnRootOff + 0x04, roomCount * 4))
                return;

            for (int i = 0; i < roomCount; i++)
                floor.FurnRoomIds.Add(b.U8((int)roomListOff + 0x01 + i));

            int furnStructSize = Marshal.SizeOf<FurnDataPop>();

            for (int i = 0; i < roomCount; i++)
            {
                byte roomId = floor.FurnRoomIds[i];

                uint perRoomTblOff = b.U32((int)furnRootOff + 0x04 + i * 4);
                if (perRoomTblOff == 0 || !b.InBounds((int)perRoomTblOff, 8))
                    continue;

                uint countBlockOff = b.U32((int)perRoomTblOff + 0x00);
                if (countBlockOff == 0 || !b.InBounds((int)countBlockOff, 1))
                    continue;

                byte count = b.U8((int)countBlockOff + 0x00);
                if (count == 0)
                    continue;

                if (!b.InBounds((int)perRoomTblOff + 0x04, count * 4))
                    continue;

                var list = new List<MsnFloor.FurnEntry>(count);

                for (int j = 0; j < count; j++)
                {
                    int ptrInTable = (int)perRoomTblOff + 0x04 + j * 4;
                    uint wordAddrOff = b.U32(ptrInTable);
                    if (wordAddrOff == 0 || !b.InBounds((int)wordAddrOff, 4))
                        continue;

                    uint furnDataOff = b.U32((int)wordAddrOff);
                    if (furnDataOff == 0 || !b.InBounds((int)furnDataOff, furnStructSize))
                        continue;

                    var data = b.ReadStruct<FurnDataPop>((int)furnDataOff);

                    var entry = new MsnFloor.FurnEntry
                    {
                        RoomId = roomId,
                        Data = data,
                        FurnDataPopOffset = (int)furnDataOff,
                        WordAddrOff = wordAddrOff,
                        PtrInTableOffset = ptrInTable
                    };

                    list.Add(entry);
                    floor.Furniture.Add(entry);
                }

                if (list.Count > 0)
                    floor.FurnitureByRoom[roomId] = list;
            }
        }

        private static void TryParseRoomPositionsAndRoomBounds(BlobView b, MsnFloor floor, uint map0RootOff)
        {
            floor.RoomCollisionByRoomId.Clear();
            floor.RoomPosByRoomId.Clear();
            floor.RoomDispOrder.Clear();

            if (map0RootOff == 0 || !b.InBounds((int)map0RootOff, 4))
                return;

            uint roomListOff = b.U32((int)map0RootOff + 0x00);
            if (roomListOff == 0 || !b.InBounds((int)roomListOff, 1))
                return;

            byte roomCount = b.U8((int)roomListOff + 0x00);
            if (roomCount == 0 || roomCount > 0x3C)
                return;

            if (!b.InBounds((int)roomListOff, 1 + roomCount))
                return;

            // Per-room pointer table starts at map0RootOff + 4 (room 0) and continues roomCount entries
            if (!b.InBounds((int)map0RootOff + 0x04, roomCount * 4))
                return;

            for (int i = 0; i < roomCount; i++)
                floor.RoomDispOrder.Add(b.U8((int)roomListOff + 0x01 + i));

            // Parse each room entry
            for (int i = 0; i < roomCount; i++)
            {
                byte roomId = floor.RoomDispOrder[i];

                uint areaSetPtrTableOff = b.U32((int)map0RootOff + 0x04 + i * 4);
                if (areaSetPtrTableOff == 0 || !b.InBounds((int)areaSetPtrTableOff, 4))
                    continue;

                uint metaOff = b.U32((int)areaSetPtrTableOff + 0x00);
                if (metaOff == 0 || !b.InBounds((int)metaOff, 9))
                    continue;

                // RoomInfo lives at metaOff + 0..7
                ushort x = b.U16((int)metaOff + 0);
                short y = b.S16((int)metaOff + 2);
                ushort z = b.U16((int)metaOff + 4);
                short roomBaseHeight = b.S16((int)metaOff + 6);

                floor.RoomPosByRoomId[roomId] = new MsnFloor.RoomPosEntry
                {
                    RoomId = roomId,
                    X = x,
                    Y = y,
                    Z = z,
                    Height = roomBaseHeight,
                    FileOffset = (int)metaOff,
                    RoomIndex = i,
                    PtrWordAddrOff = areaSetPtrTableOff
                };

                // Room bounds polygon data for PosInAreaJudge0!!!
                // count at metaOff + 8, types at metaOff + 9
                if (!b.InBounds((int)metaOff + 8, 1))
                    continue;

                byte polyCount = b.U8((int)metaOff + 8);
                if (polyCount == 0)
                    continue;

                if (!b.InBounds((int)metaOff + 9, polyCount))
                    continue;

                if (!b.InBounds((int)areaSetPtrTableOff, 4 * (1 + polyCount)))
                    continue;

                var rc = new MsnRoomCollision
                {
                    RoomId = roomId,
                    RoomIndex = i,
                    HeaderOff = (int)metaOff,
                    PtrWordAddrOff = (int)areaSetPtrTableOff,
                    polyCount = polyCount
                };

                for (int si = 0; si < polyCount; si++)
                {
                    byte type = b.U8((int)metaOff + 9 + si);

                    uint shapeOff = b.U32((int)areaSetPtrTableOff + 4 * (si + 1));
                    if (shapeOff == 0)
                        continue;

                    var region = ParseConvexRegion(b, (int)shapeOff, type);
                    if (region != null)
                        rc.Regions.Add(region);
                }

                if (rc.Regions.Count > 0)
                    floor.RoomCollisionByRoomId[roomId] = rc;
            }
        }

        private static void TryParseHeightAreas(BlobView b, MsnFloor floor, uint map5RootOff)
        {
            floor.HeightCollisionByRoomId.Clear();

            if (map5RootOff == 0 || !b.InBounds((int)map5RootOff, 4))
                return;

            uint roomListOff = b.U32((int)map5RootOff + 0x00);
            if (roomListOff == 0 || !b.InBounds((int)roomListOff, 1))
                return;

            byte roomCount = b.U8((int)roomListOff + 0x00);
            if (roomCount == 0 || roomCount > 0x3C)
                return;

            if (!b.InBounds((int)roomListOff, 1 + roomCount))
                return;

            if (!b.InBounds((int)map5RootOff + 0x04, roomCount * 4))
                return;

            if (LOG)
            {
                var ids = new byte[roomCount];
                for (int i = 0; i < roomCount; i++)
                    ids[i] = b.U8((int)roomListOff + 1 + i);
                Debug.WriteLine($"[MAP_HEIGHT] floor={floor.FloorIndex} root=0x{map5RootOff:X} roomCount={roomCount} ids=[{HexList(ids)}]");
            }

            for (int ri = 0; ri < roomCount; ri++)
            {
                byte roomId = b.U8((int)roomListOff + 1 + ri);

                uint perRoomTableOff = b.U32((int)map5RootOff + 0x04 + ri * 4);
                if (perRoomTableOff == 0 || !b.InBounds((int)perRoomTableOff, 4))
                    continue;

                uint headerOff = b.U32((int)perRoomTableOff + 0x00);
                if (headerOff == 0 || !b.InBounds((int)headerOff, 1))
                    continue;

                byte areaCount = b.U8((int)headerOff + 0x00);
                if (areaCount == 0)
                    continue;

                if (!b.InBounds((int)perRoomTableOff, 4 * (1 + areaCount)))
                    continue;

                var hc = new MsnHeightCollision
                {
                    RoomId = roomId,
                    DataRoomIndex = ri
                };

                for (int ai = 0; ai < areaCount; ai++)
                {
                    uint areaSetPtrTableOff = b.U32((int)perRoomTableOff + 0x04 + ai * 4);
                    if (areaSetPtrTableOff == 0 || !b.InBounds((int)areaSetPtrTableOff, 4))
                        continue;

                    uint metaOff = b.U32((int)areaSetPtrTableOff + 0x00);
                    if (metaOff == 0 || !b.InBounds((int)metaOff + 5, 1))
                        continue;

                    short heightValue = b.S16((int)metaOff + 0);

                    byte polyCount = b.U8((int)metaOff + 4);
                    if (polyCount == 0)
                        continue;

                    if (!b.InBounds((int)metaOff + 5, polyCount))
                        continue;

                    if (!b.InBounds((int)areaSetPtrTableOff, 4 * (1 + polyCount)))
                        continue;

                    var area = new MsnHeightLayer
                    {
                        LayerIndex = ai,
                        PtrTableOff = (int)areaSetPtrTableOff,
                        HeaderOff = (int)metaOff,
                        polyCount = polyCount,
                        HeightValue = heightValue
                    };

                    for (int pi = 0; pi < polyCount; pi++)
                    {
                        byte type = b.U8((int)metaOff + 5 + pi);

                        uint shapeOff = b.U32((int)areaSetPtrTableOff + 4 * (pi + 1));
                        if (shapeOff == 0)
                            continue;

                        var region = ParseConvexRegion(b, (int)shapeOff, type);
                        if (region != null)
                            area.Regions.Add(region);
                    }

                    if (area.Regions.Count > 0)
                        hc.Layers.Add(area);
                }

                if (hc.Layers.Count > 0)
                    floor.HeightCollisionByRoomId[roomId] = hc;

                if (LOG)
                    Debug.WriteLine($"[MAP_HEIGHT] floor={floor.FloorIndex} roomId={roomId:X2} areas={hc.Layers.Count} regions={hc.Layers.Sum(x => x.Regions.Count)}");
            }
        }

        private static MsnConvexRegion? ParseConvexRegion(BlobView b, int shapeOff, byte type)
        {
            const int planeBytes = 8;
            const int planeCount = 4;

            if (type is not (1 or 2 or 3))
                return null;

            if (!b.InBounds(shapeOff, planeBytes * planeCount))
                return null;

            var r = new MsnConvexRegion
            {
                Type = type,
                ShapeOff = shapeOff
            };

            bool anyNonZero = false;

            for (int slot = 0; slot < planeCount; slot++)
            {
                int off = shapeOff + slot * planeBytes;

                short mulX = b.S16(off + 0);
                short mulZ = b.S16(off + 2);
                int c = b.S32(off + 4);

                if (mulX != 0 || mulZ != 0 || c != 0)
                    anyNonZero = true;

                r.Edges.Add(new MsnHalfPlaneEdge
                {
                    Slot = slot,
                    MulX = mulX,
                    MulZ = mulZ,
                    C = c,
                    OffMulX = off + 0,
                    OffMulZ = off + 2,
                    OffC = off + 4
                });
            }

            if (!anyNonZero)
                return null;

            return r;
        }

        private sealed class BlobView
        {
            private readonly byte[] _d;
            public BlobView(byte[] data) => _d = data;

            public int Length => _d.Length;

            public bool InBounds(int off, int size = 1) => off >= 0 && off + size <= _d.Length;
            public int Remaining(int off) => off < 0 ? 0 : Math.Max(0, _d.Length - off);

            public byte U8(int off) => _d[off];
            public ushort U16(int off) => BitConverter.ToUInt16(_d, off);
            public uint U32(int off) => BitConverter.ToUInt32(_d, off);
            public short S16(int off) => BitConverter.ToInt16(_d, off);
            public int S32(int off) => BitConverter.ToInt32(_d, off);

            public T ReadStruct<T>(int off) where T : struct
            {
                int size = Marshal.SizeOf<T>();
                if (!InBounds(off, size))
                    throw new InvalidOperationException($"ReadStruct<{typeof(T).Name}> OOB at 0x{off:X}");

                return MemoryMarshal.Read<T>(_d.AsSpan(off, size));
            }

            public byte[] PeekBytes(int off, int len)
            {
                if (!InBounds(off, len)) len = Math.Max(0, Remaining(off));
                len = Math.Max(0, len);
                var buf = new byte[len];
                _d.AsSpan(off, len).CopyTo(buf);
                return buf;
            }
        }
    }
}
