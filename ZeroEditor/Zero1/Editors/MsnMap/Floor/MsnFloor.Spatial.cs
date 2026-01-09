// MsnFloor.Spatial.cs
using ZeroEditor.Zero1.Editors.MsnMap.Editor;
using ZeroEditor.Zero1.Editors.MsnMap.Floor;

namespace ZeroEditor.Zero1.Editors.MsnMap
{
    public sealed partial class MsnFloor
    {
        // PosInAreaJudge0
        public Dictionary<byte, MsnRoomCollision> RoomCollisionByRoomId { get; } = new();

        // PosInAreaJudge1 for MAP_HEIGHT
        public Dictionary<byte, MsnHeightCollision> HeightCollisionByRoomId { get; } = new();

        public List<MapAreaPoly> RoomAreas { get; } = new();
        public List<MapAreaPoly> HeightAreas { get; } = new();

        private bool _spatialBuilt;
        private int _spatialCell = 1024;
        private readonly Dictionary<int, List<byte>> _cellToRooms = new();
        private readonly Dictionary<byte, RectangleF> _roomAabb = new();

        public void InvalidateSpatialIndex()
        {
            _spatialBuilt = false;
            _cellToRooms.Clear();
            _roomAabb.Clear();
        }

        public void EnsureSpatialIndex(int cellWorld = 1024)
        {
            cellWorld = Math.Max(64, cellWorld);
            if (_spatialBuilt && _spatialCell == cellWorld)
                return;

            _spatialBuilt = true;
            _spatialCell = cellWorld;
            _cellToRooms.Clear();
            _roomAabb.Clear();

            for (int i = 0; i < RoomAreas.Count; i++)
            {
                var a = RoomAreas[i];

                if (!_roomAabb.TryGetValue(a.RoomId, out var cur))
                    cur = RectangleF.FromLTRB(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);

                var pts = a.PointsWorld;
                if (pts == null || pts.Length == 0)
                    continue;

                float l = cur.Left, t = cur.Top, r = cur.Right, b = cur.Bottom;

                for (int p = 0; p < pts.Length; p++)
                {
                    var pt = pts[p];
                    if (pt.X < l) l = pt.X;
                    if (pt.Y < t) t = pt.Y;
                    if (pt.X > r) r = pt.X;
                    if (pt.Y > b) b = pt.Y;
                }

                _roomAabb[a.RoomId] = RectangleF.FromLTRB(l, t, r, b);
            }

            foreach (var kv in _roomAabb)
            {
                byte roomId = kv.Key;
                var aabb = kv.Value;

                int x0 = (int)MathF.Floor(aabb.Left / _spatialCell);
                int x1 = (int)MathF.Floor(aabb.Right / _spatialCell);
                int z0 = (int)MathF.Floor(aabb.Top / _spatialCell);
                int z1 = (int)MathF.Floor(aabb.Bottom / _spatialCell);

                for (int cz = z0; cz <= z1; cz++)
                    for (int cx = x0; cx <= x1; cx++)
                    {
                        int key = (cz << 16) ^ (cx & 0xFFFF);

                        if (!_cellToRooms.TryGetValue(key, out var list))
                            _cellToRooms[key] = list = new List<byte>(8);

                        if (!list.Contains(roomId))
                            list.Add(roomId);
                    }
            }
        }

        public IEnumerable<MapOverlayShape> GetOverlayShapes()
        {
            foreach (var a in RoomAreas)
            {
                if (a.PointsWorld == null || a.PointsWorld.Length < 3)
                    continue;

                yield return new MapOverlayPolygon
                {
                    Kind = "area",
                    RoomId = a.RoomId,
                    PointsWorld = a.PointsWorld,
                    Filled = false,
                    StrokeColorArgb = unchecked((int)0xAA00FF00),
                    StrokeWidth = 1.5f
                };
            }

            foreach (var h in HeightAreas)
            {
                if (h.PointsWorld == null || h.PointsWorld.Length < 3)
                    continue;

                yield return new MapOverlayPolygon
                {
                    Kind = "collision",
                    RoomId = h.RoomId,
                    PointsWorld = h.PointsWorld,
                    Filled = true,
                    FillColorArgb = unchecked((int)0x2200A0FF),
                    StrokeColorArgb = unchecked((int)0xFF00A0FF),
                    StrokeWidth = 1.0f
                };
            }
        }

        public void TranslateSpatialForRoom(byte roomId, int dx, int dz)
        {
            if (RoomCollisionByRoomId.TryGetValue(roomId, out var rc))
                rc.Translate(dx, dz);

            if (HeightCollisionByRoomId.TryGetValue(roomId, out var hc))
                hc.Translate(dx, dz);

            RebuildOverlayPolysForRoom(roomId);
        }

        public void RebuildOverlayPolys()
        {
            RoomAreas.Clear();
            HeightAreas.Clear();

            foreach (var kv in RoomCollisionByRoomId)
                AppendRoomBoundsPolys(kv.Key, kv.Value);

            foreach (var kv in HeightCollisionByRoomId)
                AppendHeightAreaPolys(kv.Key, kv.Value);

            InvalidateSpatialIndex();
            EnsureSpatialIndex(_spatialCell);
        }

        public void RebuildOverlayPolysForRoom(byte roomId)
        {
            RoomAreas.RemoveAll(p => p.RoomId == roomId);
            HeightAreas.RemoveAll(p => p.RoomId == roomId);

            if (RoomCollisionByRoomId.TryGetValue(roomId, out var rc))
                AppendRoomBoundsPolys(roomId, rc);

            if (HeightCollisionByRoomId.TryGetValue(roomId, out var hc))
                AppendHeightAreaPolys(roomId, hc);

            InvalidateSpatialIndex();
            EnsureSpatialIndex(_spatialCell);
        }

        private void AppendRoomBoundsPolys(byte roomId, MsnRoomCollision rc)
        {
            foreach (var r in rc.Regions)
            {
                var pts = BuildRoomBoundsPolygon(r);
                if (pts == null || pts.Length < 3)
                    continue;

                RoomAreas.Add(new MapAreaPoly
                {
                    RoomId = roomId,
                    Type = r.Type,
                    HeightIndex = null,
                    HeightValue = null,
                    PointsWorld = pts
                });
            }
        }

        private void AppendHeightAreaPolys(byte roomId, MsnHeightCollision hc)
        {
            if (!RoomCollisionByRoomId.ContainsKey(roomId))
                return;

            foreach (var layer in hc.Layers)
                foreach (var r in layer.Regions)
                {
                    var pts = BuildHeightPolygon(roomId, r);
                    if (pts == null || pts.Length < 3)
                        continue;

                    HeightAreas.Add(new MapAreaPoly
                    {
                        RoomId = roomId,
                        Type = r.Type,
                        HeightIndex = layer.LayerIndex,
                        HeightValue = layer.HeightValue,
                        PointsWorld = pts
                    });
                }
        }

        private static PointF[]? BuildRoomBoundsPolygon(MsnConvexRegion r)
        {
            if (r.Type is not (1 or 2 or 3))
                return null;

            if (r.Edges.Count != 4)
                return null;

            var seed = new RectangleF(0, 0, 65535, 65535);
            return BuildPolygonCore(r, seed, tol: 0.5f);
        }

        private PointF[]? BuildHeightPolygon(byte roomId, MsnConvexRegion r)
        {
            if (r.Type is not (1 or 2 or 3))
                return null;

            if (r.Edges.Count != 4)
                return null;

            var seed = GetRoomSeedBounds(roomId);
            if (seed.Width <= 0 || seed.Height <= 0)
                return null;

            return BuildPolygonCore(r, seed, tol: 0.5f);
        }

        private static PointF[]? BuildPolygonCore(MsnConvexRegion r, RectangleF seed, float tol)
        {
            float left = seed.Left;
            float top = seed.Top;
            float right = seed.Right;
            float bottom = seed.Bottom;

            if (right <= left || bottom <= top)
                return null;

            var poly = new List<PointF>(4)
            {
                new(left,  top),
                new(right, top),
                new(right, bottom),
                new(left,  bottom),
            };

            var edges = r.EdgesBySlot;
            for (int slot = 0; slot < 4; slot++)
            {
                var e = edges[slot];
                if (e == null)
                    return null;

                var rule = GetRule(r.Type, slot);
                poly = ClipAgainstPlane(poly, e, rule, tol);
                if (poly.Count == 0)
                    return null;
            }

            return poly.Count >= 3 ? poly.ToArray() : null;
        }

        private enum PlaneRule { LE, GE }

        private static PlaneRule GetRule(byte type, int slot)
        {
            return type switch
            {
                1 => (slot == 0) ? PlaneRule.LE : PlaneRule.GE,
                2 => (slot <= 1) ? PlaneRule.LE : PlaneRule.GE,
                3 => (slot <= 2) ? PlaneRule.LE : PlaneRule.GE,
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
        }

        private static List<PointF> ClipAgainstPlane(List<PointF> poly, MsnHalfPlaneEdge e, PlaneRule rule, float tol)
        {
            float F(PointF p) => e.MulX * p.Y + e.MulZ * p.X + e.C;

            bool Inside(float f) => rule == PlaneRule.LE ? (f <= tol) : (f >= -tol);

            var output = new List<PointF>(poly.Count + 2);
            if (poly.Count == 0) return output;

            PointF prev = poly[^1];
            float fPrev = F(prev);
            bool prevIn = Inside(fPrev);

            foreach (var cur in poly)
            {
                float fCur = F(cur);
                bool curIn = Inside(fCur);

                if (curIn)
                {
                    if (!prevIn)
                        output.Add(IntersectSegmentWithPlane(prev, cur, fPrev, fCur));
                    output.Add(cur);
                }
                else if (prevIn)
                {
                    output.Add(IntersectSegmentWithPlane(prev, cur, fPrev, fCur));
                }

                prev = cur;
                fPrev = fCur;
                prevIn = curIn;
            }

            return output;
        }

        private static PointF IntersectSegmentWithPlane(PointF a, PointF b, float fa, float fb)
        {
            float denom = (fa - fb);
            if (MathF.Abs(denom) < 1e-8f)
                return a;

            float t = fa / denom;
            float x = a.X + (b.X - a.X) * t;
            float z = a.Y + (b.Y - a.Y) * t;
            return new PointF(x, z);
        }

        private RectangleF GetRoomSeedBounds(byte roomId)
        {
            bool any = false;
            float minX = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxZ = float.MinValue;

            for (int i = 0; i < RoomAreas.Count; i++)
            {
                var p = RoomAreas[i];
                if (p.RoomId != roomId)
                    continue;

                var pts = p.PointsWorld;
                if (pts == null || pts.Length == 0)
                    continue;

                any = true;

                for (int j = 0; j < pts.Length; j++)
                {
                    var pt = pts[j];
                    if (pt.X < minX) minX = pt.X;
                    if (pt.Y < minZ) minZ = pt.Y;
                    if (pt.X > maxX) maxX = pt.X;
                    if (pt.Y > maxZ) maxZ = pt.Y;
                }
            }

            if (any && minX != float.MaxValue)
            {
                const float margin = 512f;

                minX = MathF.Max(0, minX - margin);
                minZ = MathF.Max(0, minZ - margin);
                maxX = MathF.Min(65535, maxX + margin);
                maxZ = MathF.Min(65535, maxZ + margin);

                return new RectangleF(minX, minZ, maxX - minX, maxZ - minZ);
            }

            if (RoomPosByRoomId.TryGetValue(roomId, out var rp))
            {
                float cx = rp.X;
                float cz = rp.Z;

                const float half = 12000f;

                float minX2 = MathF.Max(0, cx - half);
                float minZ2 = MathF.Max(0, cz - half);
                float maxX2 = MathF.Min(65535, cx + half);
                float maxZ2 = MathF.Min(65535, cz + half);

                return new RectangleF(minX2, minZ2, maxX2 - minX2, maxZ2 - minZ2);
            }

            return new RectangleF(0, 0, 0, 0);
        }

        public bool TryGetRoomAt(ushort x, ushort z, out byte roomId)
        {
            roomId = 0xFF;

            foreach (var kv in RoomCollisionByRoomId)
            {
                var rc = kv.Value;
                if (rc == null || rc.Regions.Count == 0)
                    continue;

                if (IsPointInsideAnyRegion(rc.Regions, x, z))
                {
                    roomId = kv.Key;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetRoomAtFast(ushort x, ushort z, out byte roomId)
        {
            roomId = 0xFF;

            if (!_spatialBuilt)
                EnsureSpatialIndex(_spatialCell);

            int cx = x / _spatialCell;
            int cz = z / _spatialCell;
            int key = (cz << 16) ^ (cx & 0xFFFF);

            if (!_cellToRooms.TryGetValue(key, out var candidates) || candidates.Count == 0)
                return false;

            foreach (var rid in candidates)
            {
                if (_roomAabb.TryGetValue(rid, out var aabb))
                {
                    if (x < aabb.Left || x > aabb.Right || z < aabb.Top || z > aabb.Bottom)
                        continue;
                }

                if (RoomCollisionByRoomId.TryGetValue(rid, out var rc) &&
                    rc.Regions.Count > 0 &&
                    IsPointInsideAnyRegion(rc.Regions, x, z))
                {
                    roomId = rid;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetHeightAt(
            ushort x,
            ushort z,
            out short height,
            out byte roomId,
            out int layerIndex,
            bool includeRoomBaseHeight = true)
        {
            height = 0;
            roomId = 0xFF;
            layerIndex = -1;

            if (!TryGetRoomAt(x, z, out roomId))
                return false;

            if (!HeightCollisionByRoomId.TryGetValue(roomId, out var hc))
                return false;

            for (int i = 0; i < hc.Layers.Count; i++)
            {
                var layer = hc.Layers[i];
                if (layer.Regions.Count == 0)
                    continue;

                if (IsPointInsideAnyRegion(layer.Regions, x, z))
                {
                    layerIndex = layer.LayerIndex;

                    short h = layer.HeightValue;
                    if (includeRoomBaseHeight && RoomPosByRoomId.TryGetValue(roomId, out var rp))
                        h = unchecked((short)(h + rp.Height));

                    height = h;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetHeightAtFast(
            ushort x, ushort z,
            out short height,
            out byte roomId,
            out int layerIndex,
            bool includeRoomBaseHeight = true)
        {
            height = 0;
            roomId = 0xFF;
            layerIndex = -1;

            if (!TryGetRoomAtFast(x, z, out roomId))
                return false;

            if (!HeightCollisionByRoomId.TryGetValue(roomId, out var hc))
                return false;

            for (int i = 0; i < hc.Layers.Count; i++)
            {
                var layer = hc.Layers[i];
                if (layer.Regions.Count == 0)
                    continue;

                if (IsPointInsideAnyRegion(layer.Regions, x, z))
                {
                    layerIndex = layer.LayerIndex;

                    short h = layer.HeightValue;
                    if (includeRoomBaseHeight && RoomPosByRoomId.TryGetValue(roomId, out var rp))
                        h = unchecked((short)(h + rp.Height));

                    height = h;
                    return true;
                }
            }

            return false;
        }

        private static bool IsPointInsideAnyRegion(List<MsnConvexRegion> regions, ushort x, ushort z)
        {
            for (int i = 0; i < regions.Count; i++)
            {
                if (IsPointInsideRegion(regions[i], x, z))
                    return true;
            }
            return false;
        }

        private static bool IsPointInsideRegion(MsnConvexRegion r, ushort x, ushort z)
        {
            if (r.Type is not (1 or 2 or 3))
                return false;

            // Game uses integer compares when doing checks
            const long tolLE = 0;   // <= 0
            const long tolGE = -1;  // >= -1

            var edges = r.EdgesBySlot;
            for (int slot = 0; slot < 4; slot++)
            {
                var e = edges[slot];
                if (e == null)
                    return false;

                long f = (long)e.MulX * z + (long)e.MulZ * x + e.C;
                var rule = GetRule(r.Type, slot);

                if (rule == PlaneRule.LE)
                {
                    if (f > tolLE) return false;
                }
                else
                {
                    if (f < tolGE) return false;
                }
            }

            return true;
        }

        public void WriteBackSpatial(byte[] raw)
        {
            foreach (var kv in RoomCollisionByRoomId)
                kv.Value.WriteBack(raw);

            foreach (var kv in HeightCollisionByRoomId)
                kv.Value.WriteBack(raw);
        }
    }
}
