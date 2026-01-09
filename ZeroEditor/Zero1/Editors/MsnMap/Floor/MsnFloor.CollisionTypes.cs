namespace ZeroEditor.Zero1.Editors.MsnMap.Floor
{
    public sealed class MsnConvexRegion
    {
        public byte Type;
        public int ShapeOff;

        // Must contain exactly 4 planes (slots 0..3)
        public List<MsnHalfPlaneEdge> Edges { get; } = new(4);

        private MsnHalfPlaneEdge[]? _edgesBySlot;
        public MsnHalfPlaneEdge[] EdgesBySlot => _edgesBySlot ??= BuildEdgesBySlot();

        private MsnHalfPlaneEdge[] BuildEdgesBySlot()
        {
            var arr = new MsnHalfPlaneEdge[4];
            for (int i = 0; i < Edges.Count; i++)
            {
                var e = Edges[i];
                if ((uint)e.Slot < 4)
                    arr[e.Slot] = e;
            }
            return arr;
        }

        private void InvalidateCaches() => _edgesBySlot = null;

        public void Translate(int dx, int dz)
        {
            for (int i = 0; i < Edges.Count; i++)
                Edges[i].Translate(dx, dz);

            InvalidateCaches();
        }

        public void WriteBack(byte[] raw)
        {
            for (int i = 0; i < Edges.Count; i++)
                Edges[i].WriteBack(raw);
        }
    }

    // PosInAreaJudgeSub
    public sealed class MsnHalfPlaneEdge
    {
        public int Slot;  // 0..3

        public short MulX;
        public short MulZ;
        public int C;

        public int OffMulX;
        public int OffMulZ;
        public int OffC;

        public void Translate(int dx, int dz)
        {
            long delta = (long)MulX * dz + (long)MulZ * dx;
            C = unchecked((int)(C - delta));
        }

        public void WriteBack(byte[] raw)
        {
            if (OffMulX >= 0 && OffMulX + 2 <= raw.Length)
            {
                raw[OffMulX + 0] = (byte)(MulX & 0xFF);
                raw[OffMulX + 1] = (byte)(MulX >> 8);
            }

            if (OffMulZ >= 0 && OffMulZ + 2 <= raw.Length)
            {
                raw[OffMulZ + 0] = (byte)(MulZ & 0xFF);
                raw[OffMulZ + 1] = (byte)(MulZ >> 8);
            }

            if (OffC >= 0 && OffC + 4 <= raw.Length)
            {
                raw[OffC + 0] = (byte)(C & 0xFF);
                raw[OffC + 1] = (byte)(C >> 8);
                raw[OffC + 2] = (byte)(C >> 16);
                raw[OffC + 3] = (byte)(C >> 24);
            }
        }
    }

    public sealed class MsnRoomCollision
    {
        public byte RoomId;
        public int RoomIndex;
        public int HeaderOff;
        public int PtrWordAddrOff;
        public byte polyCount;

        public List<MsnConvexRegion> Regions { get; } = new();

        public void Translate(int dx, int dz)
        {
            foreach (var r in Regions)
                r.Translate(dx, dz);
        }

        public void WriteBack(byte[] raw)
        {
            foreach (var r in Regions)
                r.WriteBack(raw);
        }
    }

    public sealed class MsnHeightCollision
    {
        public byte RoomId;
        public int DataRoomIndex;

        public List<MsnHeightLayer> Layers { get; } = new();

        public void Translate(int dx, int dz)
        {
            foreach (var l in Layers)
                l.Translate(dx, dz);
        }

        public void WriteBack(byte[] raw)
        {
            foreach (var l in Layers)
                l.WriteBack(raw);
        }
    }

    public sealed class MsnHeightLayer
    {
        public int LayerIndex;

        // AreaSetPtrTable offset
        public int PtrTableOff;

        // metaOff for MAP_HEIGHT
        public int HeaderOff;

        public byte polyCount;

        public short HeightValue;

        public List<MsnConvexRegion> Regions { get; } = new();

        public void Translate(int dx, int dz)
        {
            foreach (var r in Regions)
                r.Translate(dx, dz);
        }

        public void WriteBack(byte[] raw)
        {
            foreach (var r in Regions)
                r.WriteBack(raw);
        }
    }

    public sealed class MapAreaPoly
    {
        public byte RoomId;
        public byte Type;
        public int? HeightIndex;
        public short? HeightValue;
        public PointF[] PointsWorld = Array.Empty<PointF>();
    }
}
