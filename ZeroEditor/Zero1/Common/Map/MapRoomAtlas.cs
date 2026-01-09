namespace ZeroEditor.Zero1.Common.Map
{
    public sealed class MapRoom
    {
        public byte RoomId { get; }
        public byte FloorId { get; }

        public Rectangle UV { get; }
        public Point BasePos { get; }

        public bool IsBlank => UV.Width == 0 || UV.Height == 0;

        public MapRoom(byte roomId, byte floorId, int u, int v, int w, int h, int x, int y)
        {
            RoomId = roomId;
            FloorId = floorId;
            UV = new Rectangle(u, v, w, h);
            BasePos = new Point(x, y);
        }
    }

    public static class MapRoomAtlas
    {
        public const int RoomCount = 42;

        private static readonly MapRoom[] _defaults =
        {
            new( 0, 1,   2,436, 48,61, 224,405),
            new( 1, 1,   1,347, 79,47, 142,365),
            new( 2, 1,  82,324, 35,75, 204,359),
            new( 3, 1,   1,394, 63,42, 223,370),
            new( 4, 1, 240,  2, 76,64, 263,370),
            new( 5, 1,  87,453, 32,44, 305,369),
            new( 6, 1, 156,380, 76,60, 297,318),
            new( 7, 1,   1,249, 55,37, 155,282),
            new( 8, 1,   1,286, 78,61, 135,311),
            new( 9, 1,  75,231, 34,46, 144,318),
            new(10, 1,  83,280, 34,41, 203,324),
            new(11, 1, 117,280, 36,51, 230,325),
            new(12, 1,  80,398, 34,49, 258,317),
            new(13, 1, 178,  5, 57,61, 258,316),
            new(14, 1, 159,324, 60,55, 299,268),
            new(15, 1,   1,168, 46,38, 142,254),
            new(16, 1,  76,140, 97,90, 186,241),
            new(17, 1, 118,396, 34,79, 275,246),
            new(18, 2, 322,  3, 42,27, 309,249),
            new(19, 1,   1,206, 43,43, 196,225),
            new(20, 3, 160,443,131,46, 193,314),
            new(21, 1, 167,241, 75,83, 289,159),
            new(22, 1,  76,  3, 98,137,168,117),
            new(23, 1, 118,335, 39,61, 257,190),
            new(24, 2, 210,206, 33,16, 233,310),
            new(25, 1, 176, 68, 79,135,289, 32),
            new(26, 1, 174,205, 35,36, 328, 37),
            new(27, 1,   1, 75, 75,93,  90,175),
            new(28, 0, 318, 76,118,101,115,134),
            new(29, 1,   1,  3, 66,72,  64,112),
            new(30, 0, 312,177,127,100,175,231),
            new(31, 0, 365,  4, 36,28, 200,232),
            new(32, 0, 401,  4, 69,72, 285,290),
            new(33, 0, 307,279, 81,91, 316,333),
            new(34, 0, 306,381,122,82, 369,357),
            new(35, 2, 243,206, 38,33, 201,346),
            new(36, 1,  53,450, 32,45, 282,366),
            new(37, 2,   0,  0,  0, 0,   0,308),
            new(38, 2, 256,108, 56,50, 218,308),
            new(39, 2,   0,  0,  0, 0,   0,308),
            new(40, 1,  83,280, 34,41, 203,324),
            new(41, 2, 283,207, 19,34, 262,321),
        };

        public static readonly MapRoom[] Overrides =
        {
            new( 3, 2, 242,242, 63,63, 223,350),
            new( 8, 2, 255,158, 60,44, 177,334),
            new( 9, 2, 257, 68, 56,40, 143,322),
            new(14, 2, 233,385, 56,57, 302,271),
            new(17, 2, 242,306, 58,66, 260,261),
            new(20, 2, 235,375, 50,11, 275,321),
            new(29, 0, 319, 30, 47,43,  75,131),
        };

        private static readonly Dictionary<int, MapRoom> _byFloorAndRoom = BuildOverrideIndex();

        private static int Key(byte floorId, byte roomId) => (floorId << 8) | roomId;

        private static Dictionary<int, MapRoom> BuildOverrideIndex()
        {
            var d = new Dictionary<int, MapRoom>(Overrides.Length);
            foreach (var r in Overrides)
                d.Add(Key(r.FloorId, r.RoomId), r);
            return d;
        }

        public static MapRoom GetDefault(int roomId)
        {
            if ((uint)roomId >= RoomCount)
                throw new ArgumentOutOfRangeException(nameof(roomId));
            return _defaults[roomId];
        }

        public static MapRoom Get(byte floorId, int roomId)
        {
            if ((uint)roomId >= RoomCount)
                throw new ArgumentOutOfRangeException(nameof(roomId));

            byte rid = (byte)roomId;

            if (_byFloorAndRoom.TryGetValue(Key(floorId, rid), out var ov))
                return ov;

            return _defaults[rid];
        }

        public static bool TryGet(byte floorId, int roomId, out MapRoom room)
        {
            room = default!;
            if ((uint)roomId >= RoomCount) return false;

            byte rid = (byte)roomId;

            if (_byFloorAndRoom.TryGetValue(Key(floorId, rid), out room))
                return true;

            room = _defaults[rid];
            return true;
        }

        public static IEnumerable<MapRoom> EnumerateFloor(byte floorId)
        {
            for (int i = 0; i < RoomCount; i++)
            {
                var r = Get(floorId, i);

                if (!r.IsBlank && r.FloorId == floorId)
                    yield return r;
            }
        }

    }
}
