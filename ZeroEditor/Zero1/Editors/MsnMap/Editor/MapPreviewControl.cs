using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ZeroEditor.Tim2;
using ZeroEditor.Zero1.Common.Map;

namespace ZeroEditor.Zero1.Editors.MsnMap.Editor
{
    public interface IMapOverlaySource
    {
        IEnumerable<MapOverlayShape> GetOverlayShapes();
    }

    public abstract class MapOverlayShape
    {
        public string Kind { get; init; } = "";
        public byte? RoomId { get; init; } = null;
        public bool Filled { get; init; } = false;
        public float StrokeWidth { get; init; } = 2f;
        public int StrokeColorArgb { get; init; } = unchecked((int)0xCC00FFFF);
        public int FillColorArgb { get; init; } = unchecked((int)0x3300FFFF);
    }

    public sealed class MapOverlayPolyline : MapOverlayShape
    {
        public PointF[] PointsWorld { get; init; } = Array.Empty<PointF>();
        public bool Closed { get; init; } = true;
    }

    public sealed class MapOverlayPolygon : MapOverlayShape
    {
        public PointF[] PointsWorld { get; init; } = Array.Empty<PointF>();
    }

    public sealed class MapPreviewControl : Control
    {
        public const string DefaultAtlasTm2FilesRelPath = @"bin\tim\pl_smap_e_pk2_index\0005.tm2";

        private MsnFloor? _floor;
        private byte? _selectedRoomId;
        private short? _selectedDoorId;
        private int? _selectedFurnKey;

        private Image? _atlasImage;
        private bool _ownsAtlasImage;

        private readonly Dictionary<byte, Bitmap> _floorCompositeCache = new();
        private readonly Dictionary<int, Bitmap> _roomUvCropCache = new();

        private bool _showAtlas = true;
        private float _atlasAlpha = 0.90f;

        public byte? AtlasFloorIdOverride { get; set; }

        private float _zoom = 1f;
        private PointF _pan = new(0, 0);
        private bool _panning;
        private Point _panStartMouse;
        private PointF _panStartPan;

        private RectangleF _lastBounds;
        private float _lastFitScale;
        private float _lastOx;
        private float _lastOy;
        private bool _hasLastTransform;

        private float _lastSx, _lastSy, _lastTx, _lastTy;
        private byte _lastAtlasFloorId;
        private bool _hasLastAtlasFit;

        public float AtlasScaleBias { get; set; } = 1.0f;
        public float AtlasNudgeWorldX { get; set; } = 650.0f;
        public float AtlasNudgeWorldZ { get; set; } = 400.0f;

        public float DoorLeafLengthWorld { get; set; } = 520f;
        public float DoorDoubleSeparationWorld { get; set; } = 40f;
        public float DoorGroupQuantizeWorld { get; set; } = 24f;

        public bool ShowAtlas
        {
            get => _showAtlas;
            set { _showAtlas = value; Invalidate(); }
        }

        public bool ShowGrid { get; set; } = true;
        public bool ShowRooms { get; set; } = true;
        public bool ShowDoors { get; set; } = true;
        public bool ShowFurniture { get; set; } = true;


        public bool ShowAreas { get; set; } = true;
        public bool ShowCollisions { get; set; } = true;

        public bool HighlightSelectedRoomTile { get; set; } = true;

        public bool ShowHeightMap { get; set; } = true;

        public int HeightMapStepWorld { get; set; } = 128;

        public float HeightMapAlpha { get; set; } = 0.35f;

        public bool HeightMapUseWorldHeight { get; set; } = true;

        private Bitmap? _heightMapCache;
        private bool _heightMapDirty = true;
        public int HeightMapCellWorld { get; set; } = 160;

        private float _hmWorldX0, _hmWorldZ0;
        private int _hmStepWorld;
        private int _hmCols, _hmRows;

        private float _hmWorldLeft, _hmWorldRight;
        private float _hmWorldTopZ, _hmWorldBottomZ;


        private RectangleF _hmLastBounds;
        private float _hmLastFitScale;
        private float _hmLastZoom;
        private PointF _hmLastPan;
        private Size _hmLastClient;
        private bool _hmHasKey;


        private bool _hmRangeDirty = true;
        private bool _hmHasRange;
        private short _hmMin, _hmMax;
        private bool _hmLastUseWorldHeight;

        public event Action<int, int>? RoomNudgeRequested;

        public MapPreviewControl()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.FromArgb(24, 24, 24);
            ForeColor = Color.Gainsboro;
            TabStop = true;
        }

        public void SetAtlas(Image? atlasImage, bool showAtlas = true, float alpha = 0.90f)
        {
            DisposeOwnedAtlas();

            _atlasImage = atlasImage;
            _showAtlas = showAtlas;
            _atlasAlpha = Math.Clamp(alpha, 0f, 1f);

            ClearCaches();
            Invalidate();
        }

        public bool LoadAtlasFromProjectTm2(
            string? filesRelativeTm2Path = null,
            int clutSet = 0,
            bool halfAlpha = true,
            bool showAtlas = true,
            float alpha = 0.90f)
        {
            filesRelativeTm2Path ??= DefaultAtlasTm2FilesRelPath;

            var fullPath = AppState.TryResolveFilesRelative(filesRelativeTm2Path);
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
                return false;

            try
            {
                using var fs = File.OpenRead(fullPath);
                var tim2 = Tim2Image.Load(fs);
                var bmp = Tim2Decode.DecodeToBitmap(tim2, clutSet, halfAlpha);

                DisposeOwnedAtlas();

                _atlasImage = bmp;
                _ownsAtlasImage = true;

                _showAtlas = showAtlas;
                _atlasAlpha = Math.Clamp(alpha, 0f, 1f);

                ClearCaches();
                Invalidate();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void SetData(
            MsnFloor? floor,
            byte? selectedRoomId = null,
            short? selectedDoorId = null,
            int? selectedFurnKey = null)
        {
            MarkHeightDirty();

            _floor = floor;
            _selectedRoomId = selectedRoomId;
            _selectedDoorId = selectedDoorId;
            _selectedFurnKey = selectedFurnKey;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearCaches();
                DisposeOwnedAtlas();
            }
            base.Dispose(disposing);
        }

        private void DisposeOwnedAtlas()
        {
            if (_ownsAtlasImage)
            {
                _atlasImage?.Dispose();
                _atlasImage = null;
                _ownsAtlasImage = false;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            InvalidateHeightMap();
        }


        private void ClearCaches()
        {
            foreach (var kv in _floorCompositeCache)
                kv.Value.Dispose();
            _floorCompositeCache.Clear();

            foreach (var kv in _roomUvCropCache)
                kv.Value.Dispose();
            _roomUvCropCache.Clear();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            var k = keyData & Keys.KeyCode;
            if (k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down)
                return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (_floor == null || !_selectedRoomId.HasValue)
                return;

            int step = 8;
            if (e.Shift) step = 32;
            if (e.Control) step = 1;

            int dx = 0;
            int dz = 0;

            if (e.KeyCode == Keys.Left) dx = -step;
            else if (e.KeyCode == Keys.Right) dx = step;
            else if (e.KeyCode == Keys.Up) dz = step;
            else if (e.KeyCode == Keys.Down) dz = -step;

            if (dx != 0 || dz != 0)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                RoomNudgeRequested?.Invoke(dx, dz);
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            _zoom = 1f;
            _pan = new PointF(0, 0);
            InvalidateHeightMap();
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            Focus();

            if (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Right)
            {
                _panning = true;
                _panStartMouse = e.Location;
                _panStartPan = _pan;
                Capture = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!_panning)
                return;

            var dx = e.X - _panStartMouse.X;
            var dy = e.Y - _panStartMouse.Y;
            _pan = new PointF(_panStartPan.X + dx, _panStartPan.Y + dy);
            InvalidateHeightMap();
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Right)
            {
                _panning = false;
                Capture = false;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if (_floor == null || !_hasLastTransform)
                return;

            var before = ScreenToWorld(e.Location);

            float factor = e.Delta > 0 ? 1.15f : (1f / 1.15f);
            _zoom = Math.Clamp(_zoom * factor, 0.08f, 30f);

            var afterScreen = WorldToScreen(before.X, before.Y);
            _pan = new PointF(_pan.X + (e.X - afterScreen.X), _pan.Y + (e.Y - afterScreen.Y));

            InvalidateHeightMap();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            if (_floor == null)
            {
                using var b = new SolidBrush(ForeColor);
                const string s0 = "No floor selected";
                var sz0 = g.MeasureString(s0, Font);
                g.DrawString(s0, Font, b,
                    (ClientSize.Width - sz0.Width) * 0.5f,
                    (ClientSize.Height - sz0.Height) * 0.5f);
                _hasLastTransform = false;
                _hasLastAtlasFit = false;
                return;
            }

            var rooms = _floor.RoomPosByRoomId.Values.ToList();
            var doors = _floor.Doors;
            var furn = _floor.Furniture;

            var bounds = ComputeWorldBounds(rooms, doors, furn);
            if (bounds.Width <= 1 || bounds.Height <= 1)
                bounds = new RectangleF(0, 0, 1, 1);

            var view = GetViewRect();
            float fit = MathF.Min(view.Width / bounds.Width, view.Height / bounds.Height);
            float ox = view.Left + (view.Width - bounds.Width * fit) * 0.5f;
            float oy = view.Top + (view.Height - bounds.Height * fit) * 0.5f;

            _lastBounds = bounds;
            _lastFitScale = fit;
            _lastOx = ox;
            _lastOy = oy;
            _hasLastTransform = true;

            if (ShowGrid)
            {
                using var gridPen = new Pen(Color.FromArgb(40, 255, 255, 255), 1f);
                DrawGrid(g, view, gridPen);
            }

            if (ShowAtlas)
                DrawAtlasIfAvailable(g);

            using var doorPen = new Pen(Color.FromArgb(220, 255, 60, 60), 6.0f);
            using var doorSelPen = new Pen(Color.FromArgb(255, 255, 210, 80), 10.0f);
            using var furnBrush = new SolidBrush(Color.FromArgb(210, 210, 210, 210));
            using var furnSelBrush = new SolidBrush(Color.FromArgb(255, 255, 210, 80));
            using var labelFill = new SolidBrush(Color.FromArgb(170, 0, 0, 0));
            using var labelText = new SolidBrush(Color.FromArgb(230, 255, 255, 255));

            // Rooms
            if (ShowRooms)
                DrawRooms(g, rooms);

            // Doors
            if (ShowDoors)
                DrawDoorsHingedAndSeparated(g, doors, doorPen, doorSelPen);

            // Furniture
            if (ShowFurniture)
            {
                const int furnDot = 6;
                foreach (var f in furn)
                {
                    var p = WorldToScreen(f.Data.pos_x, f.Data.pos_z);
                    var rr = new RectangleF(p.X - furnDot * 0.5f, p.Y - furnDot * 0.5f, furnDot, furnDot);
                    bool selected = _selectedFurnKey.HasValue && _selectedFurnKey.Value == f.FurnDataPopOffset;
                    g.FillEllipse(selected ? furnSelBrush : furnBrush, rr);
                }
            }

            if (ShowHeightMap)
            {
                EnsureHeightMapCache();
                if (_heightMapCache != null)
                    DrawHeightMapCached(g, _heightMapCache);
            }

            DrawOverlaysIfAny(g);

            var title = $"Floor {_floor.FloorIndex}   Rooms {rooms.Count}   Doors {doors.Count}   Furn {furn.Count}";
            DrawLabel(g, title, new PointF(8, 6), Font, labelFill, labelText);

            if (_selectedRoomId.HasValue)
            {
                var rid = _selectedRoomId.Value;
                var rn = RoomNames.Get(rid);
                DrawLabel(g, $"{rid}  {rn}", new PointF(8, 28f), Font, labelFill, labelText);
            }
        }

        private void DrawRooms(Graphics g, List<MsnFloor.RoomPosEntry> rooms)
        {
            using var dotBrush = new SolidBrush(Color.FromArgb(200, 150, 200, 255));
            using var txtBrush = new SolidBrush(Color.FromArgb(220, 230, 230, 230));
            using var haloBrush = new SolidBrush(Color.FromArgb(160, 0, 0, 0));

            foreach (var r in rooms)
            {
                var p = WorldToScreen(r.X, r.Z);

                float radius = (_selectedRoomId.HasValue && _selectedRoomId.Value == r.RoomId) ? 6f : 4f;
                g.FillEllipse(dotBrush, p.X - radius, p.Y - radius, radius * 2f, radius * 2f);

                // small label
                string s = r.RoomId.ToString();
                var sz = g.MeasureString(s, Font);
                var rr = new RectangleF(p.X + 6, p.Y - sz.Height * 0.5f, sz.Width + 6, sz.Height + 2);
                g.FillRectangle(haloBrush, rr);
                g.DrawString(s, Font, txtBrush, p.X + 9, p.Y - sz.Height * 0.5f + 1);
            }
        }

        private void DrawOverlaysIfAny(Graphics g)
        {
            if (_floor is not IMapOverlaySource src)
                return;

            var shapes = src.GetOverlayShapes();
            foreach (var sh in shapes)
            {
                bool isArea = string.Equals(sh.Kind, "area", StringComparison.OrdinalIgnoreCase);
                bool isCol = string.Equals(sh.Kind, "collision", StringComparison.OrdinalIgnoreCase);

                if (isArea && !ShowAreas) continue;
                if (isCol && !ShowCollisions) continue;

                bool emphasize = sh.RoomId.HasValue && _selectedRoomId.HasValue && sh.RoomId.Value == _selectedRoomId.Value;
                float width = emphasize ? (sh.StrokeWidth + 1.5f) : sh.StrokeWidth;

                var stroke = Color.FromArgb(sh.StrokeColorArgb);
                var fill = Color.FromArgb(sh.FillColorArgb);

                if (sh is MapOverlayPolygon poly && poly.PointsWorld.Length >= 3)
                {
                    var pts = poly.PointsWorld.Select(p => WorldToScreen(p.X, p.Y)).ToArray();

                    if (sh.Filled)
                    {
                        using var b = new SolidBrush(fill);
                        g.FillPolygon(b, pts);
                    }

                    using var p = new Pen(stroke, width);
                    g.DrawPolygon(p, pts);
                }
                else if (sh is MapOverlayPolyline line && line.PointsWorld.Length >= 2)
                {
                    var pts = line.PointsWorld.Select(p => WorldToScreen(p.X, p.Y)).ToArray();
                    using var p = new Pen(stroke, width);
                    if (line.Closed)
                        g.DrawPolygon(p, pts);
                    else
                        g.DrawLines(p, pts);
                }
            }
        }

        private void DrawDoorsHingedAndSeparated(
            Graphics g,
            List<MsnFloor.DoorEntry> doors,
            Pen doorPen,
            Pen doorSelPen)
        {
            float len = DoorLeafLengthWorld;
            float leafSep = DoorDoubleSeparationWorld;
            float groupQuant = DoorGroupQuantizeWorld;

            var groups = doors
                .GroupBy(d =>
                {
                    int gx = (int)MathF.Round(d.Data.pos_x / groupQuant);
                    int gz = (int)MathF.Round(d.Data.pos_z / groupQuant);
                    return (gx, gz);
                })
                .ToList();

            foreach (var grp in groups)
            {
                var list = grp.ToList();
                list.Sort((a, b) =>
                {
                    int cmp = a.Data.rot.CompareTo(b.Data.rot);
                    return cmp != 0 ? cmp : a.DoorId.CompareTo(b.DoorId);
                });

                int n = list.Count;

                for (int i = 0; i < n; i++)
                {
                    var d = list[i];

                    float x = d.Data.pos_x;
                    float z = d.Data.pos_z;
                    float ang = -d.Data.rot;

                    float fx = MathF.Cos(ang);
                    float fz = MathF.Sin(ang);
                    float px = -fz;
                    float pz = fx;

                    float t = (n == 1) ? 0f : (i - (n - 1) * 0.5f);
                    float off = t * leafSep;

                    float hx = x + px * off;
                    float hz = z + pz * off;

                    float ex = hx + fx * len;
                    float ez = hz + fz * len;

                    var a = WorldToScreen(hx, hz);
                    var b = WorldToScreen(ex, ez);

                    var pen = (_selectedDoorId.HasValue && _selectedDoorId.Value == d.DoorId) ? doorSelPen : doorPen;
                    g.DrawLine(pen, a, b);
                }
            }
        }

        private void DrawAtlasIfAvailable(Graphics g)
        {
            _hasLastAtlasFit = false;

            if (!_showAtlas || _atlasImage == null || _floor == null)
                return;

            byte atlasFloorId = ResolveAtlasFloorId(_floor);

            if (!_floorCompositeCache.TryGetValue(atlasFloorId, out var composite))
            {
                try
                {
                    composite = MapAtlasComposer.BuildFloorComposite(_atlasImage, atlasFloorId);
                    _floorCompositeCache[atlasFloorId] = composite;
                }
                catch
                {
                    return;
                }
            }

            if (composite.Width <= 1 || composite.Height <= 1)
                return;

            if (!MapAtlasTransform.TryFit(_floor, atlasFloorId, out float sx, out float sy, out float tx, out float ty))
                return;

            float k = AtlasScaleBias;
            if (MathF.Abs(k) < 1e-6f) k = 1f;

            float pivotX = 0f;
            float pivotY = composite.Height;

            sx *= k;
            sy *= k;

            tx = tx + (1f - k) * pivotX;
            ty = ty + (1f - k) * pivotY;

            float kX = MapAtlasTransform.GetWorldToMapX();
            float kZ = MapAtlasTransform.GetWorldToMapZ();
            float mapH = MapAtlasTransform.GetMapHeight();

            tx += sx * (AtlasNudgeWorldX * kX);
            ty += sy * (-(AtlasNudgeWorldZ * kZ));

            _hasLastAtlasFit = true;
            _lastSx = sx; _lastSy = sy; _lastTx = tx; _lastTy = ty;
            _lastAtlasFloorId = atlasFloorId;

            var w00 = PixelToWorld(0f, 0f, sx, sy, tx, ty, kX, kZ, mapH);
            var w11 = PixelToWorld(composite.Width, composite.Height, sx, sy, tx, ty, kX, kZ, mapH);

            var p00 = WorldToScreen(w00.X, w00.Y);
            var p11 = WorldToScreen(w11.X, w11.Y);

            var destI = Rectangle.Round(RectFromPoints(p00, p11));
            if (destI.Width < 1 || destI.Height < 1)
                return;

            using var baseIA = new ImageAttributes();
            baseIA.SetColorMatrix(new ColorMatrix
            {
                Matrix00 = 1f,
                Matrix11 = 1f,
                Matrix22 = 1f,
                Matrix33 = _atlasAlpha,
                Matrix44 = 1f
            });

            var prevSm = g.SmoothingMode;
            var prevInterp = g.InterpolationMode;
            var prevPix = g.PixelOffsetMode;
            var prevClip = g.Clip;

            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            g.DrawImage(composite, destI, 0, 0, composite.Width, composite.Height, GraphicsUnit.Pixel, baseIA);

            if (HighlightSelectedRoomTile)
                DrawSelectedRoomUvTint(g, destI, atlasFloorId, composite.Width, composite.Height);

            g.Clip = prevClip;
            g.PixelOffsetMode = prevPix;
            g.InterpolationMode = prevInterp;
            g.SmoothingMode = prevSm;
        }

        private void DrawSelectedRoomUvTint(Graphics g, Rectangle dest, byte atlasFloorId, int compositeW, int compositeH)
        {
            if (_selectedRoomId == null || _atlasImage == null)
                return;

            int rid = _selectedRoomId.Value;
            if ((uint)rid >= MapRoomAtlas.RoomCount)
                return;

            var room = MapRoomAtlas.Get(atlasFloorId, rid);
            if (room.IsBlank || room.FloorId != atlasFloorId)
                return;

            var uv = room.UV;
            if (uv.Width <= 0 || uv.Height <= 0)
                return;

            int key = (atlasFloorId << 8) | (rid & 0xFF);
            if (!_roomUvCropCache.TryGetValue(key, out var crop))
            {
                crop = CreateUvCropBitmap(_atlasImage, uv);
                _roomUvCropCache[key] = crop;
            }

            float dx = (float)dest.Width / compositeW;
            float dy = (float)dest.Height / compositeH;

            var dst = Rectangle.Round(new RectangleF(
                dest.Left + room.BasePos.X * dx,
                dest.Top + room.BasePos.Y * dy,
                uv.Width * dx,
                uv.Height * dy));

            if (dst.Width < 1 || dst.Height < 1)
                return;

            using var ia = new ImageAttributes();
            ia.SetColorMatrix(new ColorMatrix
            {
                Matrix00 = 0.4f,
                Matrix11 = 0.8f,
                Matrix22 = 0.8f,
                Matrix33 = 1f,

                Matrix40 = 0.0f,
                Matrix41 = 0.2f,
                Matrix42 = 0.2f,
                Matrix44 = 1f
            });

            g.SetClip(dest);
            g.DrawImage(crop, dst, 0, 0, crop.Width, crop.Height, GraphicsUnit.Pixel, ia);
        }

        private static Bitmap CreateUvCropBitmap(Image atlas, Rectangle uv)
        {
            var srcBmp = atlas as Bitmap;
            if (srcBmp == null)
            {
                var tmp = new Bitmap(atlas.Width, atlas.Height, PixelFormat.Format32bppArgb);
                using (var gg = Graphics.FromImage(tmp))
                    gg.DrawImage(atlas, 0, 0, atlas.Width, atlas.Height);
                srcBmp = tmp;
            }

            var crop = new Bitmap(uv.Width, uv.Height, PixelFormat.Format32bppArgb);
            using (var gg = Graphics.FromImage(crop))
            {
                gg.CompositingMode = CompositingMode.SourceCopy;
                gg.InterpolationMode = InterpolationMode.NearestNeighbor;
                gg.PixelOffsetMode = PixelOffsetMode.Half;
                gg.DrawImage(srcBmp,
                    new Rectangle(0, 0, uv.Width, uv.Height),
                    uv,
                    GraphicsUnit.Pixel);
            }

            return crop;
        }

        private static PointF PixelToWorld(
            float px, float py,
            float sx, float sy,
            float tx, float ty,
            float kX, float kZ,
            float mapH)
        {
            if (MathF.Abs(sx) < 1e-6f) sx = 1e-6f;
            if (MathF.Abs(sy) < 1e-6f) sy = 1e-6f;

            float mx = (px - tx) / sx;
            float my = (py - ty) / sy;

            float wx = mx / kX;
            float wz = (mapH - my) / kZ;
            return new PointF(wx, wz);
        }

        private byte ResolveAtlasFloorId(MsnFloor floor)
        {
            if (AtlasFloorIdOverride.HasValue)
                return AtlasFloorIdOverride.Value;

            byte byIndex = (byte)floor.FloorIndex;

            if (MapRoomAtlas.EnumerateFloor(byIndex).Any())
                return byIndex;

            if (MapRoomAtlas.EnumerateFloor(1).Any())
                return 1;

            return byIndex;
        }

        private PointF WorldToScreen(float wx, float wz)
        {
            if (!_hasLastTransform)
                return new PointF(0, 0);

            float scale = _lastFitScale * _zoom;
            float x = _lastOx + (wx - _lastBounds.Left) * scale + _pan.X;
            float y = _lastOy + (_lastBounds.Bottom - wz) * scale + _pan.Y;
            return new PointF(x, y);
        }

        private PointF ScreenToWorld(Point p)
        {
            float scale = _lastFitScale * _zoom;
            if (scale <= 1e-6f) scale = 1e-6f;

            float wx = _lastBounds.Left + ((p.X - _lastOx - _pan.X) / scale);
            float wz = _lastBounds.Bottom - ((p.Y - _lastOy - _pan.Y) / scale);
            return new PointF(wx, wz);
        }

        private static void DrawLabel(Graphics g, string text, PointF pos, Font font, Brush fill, Brush fg)
        {
            var sz = g.MeasureString(text, font);
            var r = new RectangleF(pos.X, pos.Y, sz.Width + 10f, sz.Height + 6f);
            g.FillRectangle(fill, r);
            g.DrawString(text, font, fg, new PointF(pos.X + 5f, pos.Y + 3f));
        }

        private RectangleF GetViewRect()
        {
            const float pad = 12f;
            return new RectangleF(
                pad,
                pad + 18f,
                ClientSize.Width - pad * 2f,
                ClientSize.Height - pad * 2f - 18f);
        }

        private static RectangleF ComputeWorldBounds(
            List<MsnFloor.RoomPosEntry> rooms,
            List<MsnFloor.DoorEntry> doors,
            List<MsnFloor.FurnEntry> furn)
        {
            bool any = false;
            float minX = 0, minZ = 0, maxX = 0, maxZ = 0;

            void Add(float x, float z)
            {
                if (!any)
                {
                    any = true;
                    minX = maxX = x;
                    minZ = maxZ = z;
                    return;
                }

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (z < minZ) minZ = z;
                if (z > maxZ) maxZ = z;
            }

            foreach (var r in rooms) Add(r.X, r.Z);
            foreach (var d in doors) Add(d.Data.pos_x, d.Data.pos_z);
            foreach (var f in furn) Add(f.Data.pos_x, f.Data.pos_z);

            const float pad = 220f;
            return new RectangleF(minX - pad, minZ - pad, (maxX - minX) + pad * 2f, (maxZ - minZ) + pad * 2f);
        }

        private static RectangleF RectFromPoints(PointF a, PointF b)
        {
            float x0 = MathF.Min(a.X, b.X);
            float y0 = MathF.Min(a.Y, b.Y);
            float x1 = MathF.Max(a.X, b.X);
            float y1 = MathF.Max(a.Y, b.Y);
            return new RectangleF(x0, y0, x1 - x0, y1 - y0);
        }

        private static void DrawGrid(Graphics g, RectangleF view, Pen p)
        {
            const int step = 40;

            for (int x = (int)view.Left; x <= (int)view.Right; x += step)
                g.DrawLine(p, x, view.Top, x, view.Bottom);

            for (int y = (int)view.Top; y <= (int)view.Bottom; y += step)
                g.DrawLine(p, view.Left, y, view.Right, y);
        }

        private void DrawHeightMap(Graphics g)
        {
            if (_floor == null || !_hasLastTransform)
                return;

            var bounds = _lastBounds;

            int step = Math.Max(8, HeightMapStepWorld);

            bool any = false;
            short hMin = 0, hMax = 0;

            for (float wz = bounds.Top; wz <= bounds.Bottom; wz += step)
            {
                for (float wx = bounds.Left; wx <= bounds.Right; wx += step)
                {
                    ushort x = (ushort)Math.Clamp((int)wx, 0, ushort.MaxValue);
                    ushort z = (ushort)Math.Clamp((int)wz, 0, ushort.MaxValue);

                    if (_floor.TryGetHeightAt(x, z, out short h, out _, out _, includeRoomBaseHeight: HeightMapUseWorldHeight))
                    {
                        if (!any)
                        {
                            any = true;
                            hMin = hMax = h;
                        }
                        else
                        {
                            if (h < hMin) hMin = h;
                            if (h > hMax) hMax = h;
                        }
                    }
                }
            }

            if (!any)
                return;

            int range = hMax - hMin;
            if (range == 0) range = 1;

            float scale = _lastFitScale * _zoom;
            float cellPx = Math.Max(1f, step * scale);

            for (float wz = bounds.Top; wz <= bounds.Bottom; wz += step)
            {
                for (float wx = bounds.Left; wx <= bounds.Right; wx += step)
                {
                    ushort x = (ushort)Math.Clamp((int)wx, 0, ushort.MaxValue);
                    ushort z = (ushort)Math.Clamp((int)wz, 0, ushort.MaxValue);

                    if (!_floor.TryGetHeightAt(x, z, out short h, out _, out _, includeRoomBaseHeight: HeightMapUseWorldHeight))
                        continue;

                    float t = (h - hMin) / (float)range;

                    Color c = HeatColor(t, HeightMapAlpha);

                    var p = WorldToScreen(wx, wz);

                    float half = cellPx * 0.5f;

                    using var b = new SolidBrush(c);
                    g.FillRectangle(b, p.X - half, p.Y - half, cellPx, cellPx);
                }
            }
        }

        private static Color HeatColor(float t, float alpha)
        {
            t = Math.Clamp(t, 0f, 1f);
            alpha = Math.Clamp(alpha, 0f, 1f);

            // 0.0: dark blue
            // 0.25: cyan
            // 0.50: green
            // 0.75: yellow
            // 1.0: red
            float r = 0, g = 0, b = 0;

            if (t < 0.25f)
            {
                float u = t / 0.25f;
                r = 0f;
                g = u;
                b = 1f;
            }
            else if (t < 0.50f)
            {
                float u = (t - 0.25f) / 0.25f;
                r = 0f;
                g = 1f;
                b = 1f - u;
            }
            else if (t < 0.75f)
            {
                float u = (t - 0.50f) / 0.25f;
                r = u;
                g = 1f;
                b = 0f;
            }
            else
            {
                float u = (t - 0.75f) / 0.25f;
                r = 1f;
                g = 1f - u;
                b = 0f;
            }

            int A = (int)(alpha * 255f);
            int R = (int)(r * 255f);
            int G = (int)(g * 255f);
            int B = (int)(b * 255f);

            return Color.FromArgb(A, R, G, B);
        }

        private void EnsureHeightMapCache()
        {
            if (_floor == null || !_hasLastTransform)
                return;

            if (_hmHasKey)
            {
                bool changed =
                    _hmLastClient != ClientSize ||
                    _hmLastZoom != _zoom ||
                    _hmLastPan != _pan ||
                    _hmLastFitScale != _lastFitScale ||
                    _hmLastBounds != _lastBounds;

                if (changed)
                    _heightMapDirty = true;
            }

            if (_hmLastUseWorldHeight != HeightMapUseWorldHeight)
            {
                _hmLastUseWorldHeight = HeightMapUseWorldHeight;
                _hmRangeDirty = true;
                _heightMapDirty = true;
            }

            // Nothing to do
            if (!_heightMapDirty && _heightMapCache != null)
                return;

            EnsureHeightRange();
            if (!_hmHasRange)
                return;

            _heightMapDirty = false;

            _heightMapCache?.Dispose();
            _heightMapCache = null;

            int step = Math.Max(8, HeightMapStepWorld);
            var bounds = _lastBounds;

            float startX = MathF.Floor(bounds.Left / step) * step;
            float startZ = MathF.Floor(bounds.Top / step) * step;
            float endX = MathF.Ceiling(bounds.Right / step) * step;
            float endZ = MathF.Ceiling(bounds.Bottom / step) * step;

            float zTopWorld = MathF.Ceiling(bounds.Bottom / step) * step;
            float zBotWorld = MathF.Floor(bounds.Top / step) * step;


            int cols = (int)MathF.Ceiling((endX - startX) / step) + 1;
            int rows = (int)MathF.Ceiling((zTopWorld - zBotWorld) / step) + 1;

            float half = step * 0.5f;

            cols = Math.Clamp(cols, 1, 2048);
            rows = Math.Clamp(rows, 1, 2048);

            // X edges
            _hmWorldLeft = startX - half;
            _hmWorldRight = startX + (cols - 1) * step + half;

            // Z edges
            _hmWorldTopZ = zTopWorld + half;
            _hmWorldBottomZ = zTopWorld - (rows - 1) * step - half;

            int range = _hmMax - _hmMin;
            if (range == 0) range = 1;

            _floor.EnsureSpatialIndex(1024);

            var bmp = new Bitmap(cols, rows, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, cols, rows);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                unsafe
                {
                    byte* basePtr = (byte*)data.Scan0;

                    for (int r = 0; r < rows; r++)
                    {
                        uint* row = (uint*)(basePtr + r * data.Stride);
                        float wz = zTopWorld - r * step;

                        for (int c = 0; c < cols; c++)
                        {
                            float wx = startX + c * step;

                            ushort x = (ushort)Math.Clamp((int)wx, 0, ushort.MaxValue);
                            ushort z = (ushort)Math.Clamp((int)wz, 0, ushort.MaxValue);

                            if (!_floor.TryGetHeightAtFast(x, z, out short h, out _, out _, includeRoomBaseHeight: HeightMapUseWorldHeight))
                            {
                                row[c] = 0x00000000;
                                continue;
                            }

                            float t = (h - _hmMin) / (float)range;
                            row[c] = HeatColorArgb(t, HeightMapAlpha);
                        }
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            // Store cache + build params
            _heightMapCache = bmp;
            _hmWorldX0 = startX;
            _hmWorldZ0 = startZ;
            _hmStepWorld = step;
            _hmCols = cols;
            _hmRows = rows;

            // Update transform key
            _hmLastBounds = _lastBounds;
            _hmLastFitScale = _lastFitScale;
            _hmLastZoom = _zoom;
            _hmLastPan = _pan;
            _hmLastClient = ClientSize;
            _hmHasKey = true;
        }

        private void DrawHeightMapCached(Graphics g, Bitmap bmp)
        {
            if (_floor == null || !_hasLastTransform)
                return;

            var p00 = WorldToScreen(_hmWorldLeft, _hmWorldTopZ);
            var p11 = WorldToScreen(_hmWorldRight, _hmWorldBottomZ);

            var dest = Rectangle.Round(RectFromPoints(p00, p11));
            if (dest.Width < 1 || dest.Height < 1)
                return;

            var prevSm = g.SmoothingMode;
            var prevInterp = g.InterpolationMode;
            var prevPix = g.PixelOffsetMode;

            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            g.DrawImage(bmp, dest);

            g.PixelOffsetMode = prevPix;
            g.InterpolationMode = prevInterp;
            g.SmoothingMode = prevSm;
        }

        private static uint HeatColorArgb(float t, float alpha)
        {
            t = Math.Clamp(t, 0f, 1f);
            alpha = Math.Clamp(alpha, 0f, 1f);

            float r, g, b;

            if (t < 0.25f)
            {
                float u = t / 0.25f;
                r = 0f; g = u; b = 1f;
            }
            else if (t < 0.50f)
            {
                float u = (t - 0.25f) / 0.25f;
                r = 0f; g = 1f; b = 1f - u;
            }
            else if (t < 0.75f)
            {
                float u = (t - 0.50f) / 0.25f;
                r = u; g = 1f; b = 0f;
            }
            else
            {
                float u = (t - 0.75f) / 0.25f;
                r = 1f; g = 1f - u; b = 0f;
            }

            byte A = (byte)(alpha * 255f);
            byte R = (byte)(r * 255f);
            byte G = (byte)(g * 255f);
            byte B = (byte)(b * 255f);

            return (uint)(A << 24 | R << 16 | G << 8 | B);
        }

        public void MarkHeightDirty()
        {
            _heightMapDirty = true;
            _hmRangeDirty = true;
            _hmHasKey = false;
            Invalidate();
        }


        private void InvalidateHeightMap()
        {
            _heightMapDirty = true;
        }

        private void EnsureHeightRange()
        {
            if (_floor == null) return;
            if (!_hmRangeDirty && _hmHasRange) return;

            _hmRangeDirty = false;
            _hmHasRange = false;

            int step = Math.Max(64, HeightMapStepWorld);
            var bounds = _lastBounds;

            float startX = MathF.Floor(bounds.Left / step) * step;
            float startZ = MathF.Floor(bounds.Top / step) * step;
            float endX = MathF.Ceiling(bounds.Right / step) * step;
            float endZ = MathF.Ceiling(bounds.Bottom / step) * step;

            _floor.EnsureSpatialIndex(1024);

            bool any = false;
            short hMin = 0, hMax = 0;

            for (float wz = startZ; wz <= endZ; wz += step)
                for (float wx = startX; wx <= endX; wx += step)
                {
                    ushort x = (ushort)Math.Clamp((int)wx, 0, ushort.MaxValue);
                    ushort z = (ushort)Math.Clamp((int)wz, 0, ushort.MaxValue);

                    if (_floor.TryGetHeightAtFast(x, z, out short h, out _, out _, includeRoomBaseHeight: HeightMapUseWorldHeight))
                    {
                        if (!any) { any = true; hMin = hMax = h; }
                        else { if (h < hMin) hMin = h; if (h > hMax) hMax = h; }
                    }
                }

            if (!any) return;

            _hmMin = hMin;
            _hmMax = hMax;
            _hmHasRange = true;
        }


    }
}
