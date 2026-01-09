using System.ComponentModel;
using System.Text.RegularExpressions;
using ZeroEditor.Zero1.Editors.MsnMap.Editor;

namespace ZeroEditor.Zero1.Editors.MsnMap
{
    public partial class MsnMapEditor : UserControl
    {
        private string? _path;
        private int _missionNo;
        private byte[]? _raw;

        private readonly List<MsnFloor> _floors = new();

        private readonly BindingList<DoorRow> _doorRows = new();
        private readonly BindingList<RoomLinkRow> _roomLinkRows = new();
        private readonly BindingList<RoomPosRow> _roomPosRows = new();
        private readonly BindingList<FurnRow> _furnRows = new();

        private MsnFloor? _currentFloor;
        private short? _selectedDoorId;
        private byte? _selectedRoomId;
        private int? _selectedFurnIndex;
        private bool _atlasLoadAttempted;

        public MsnMapEditor()
        {
            InitializeComponent();

            ThemeManager.ApplyDark(this);

            btn_save.Click += Btn_Save_Click;
            treeView.AfterSelect += TreeView_AfterSelect;

            chkAtlas.CheckedChanged += (_, __) => { mapPreview.ShowAtlas = chkAtlas.Checked; };
            chkGrid.CheckedChanged += (_, __) => { mapPreview.ShowGrid = chkGrid.Checked; mapPreview.Invalidate(); };
            chkRooms.CheckedChanged += (_, __) => { mapPreview.ShowRooms = chkRooms.Checked; mapPreview.Invalidate(); };
            chkDoors.CheckedChanged += (_, __) => { mapPreview.ShowDoors = chkDoors.Checked; mapPreview.Invalidate(); };
            chkFurniture.CheckedChanged += (_, __) => { mapPreview.ShowFurniture = chkFurniture.Checked; mapPreview.Invalidate(); };
            chkAreas.CheckedChanged += (_, __) => { mapPreview.ShowAreas = chkAreas.Checked; mapPreview.Invalidate(); };
            chkCollisions.CheckedChanged += (_, __) => { mapPreview.ShowCollisions = chkCollisions.Checked; mapPreview.Invalidate(); };
            chkHighlightSelectedRoom.CheckedChanged += (_, __) => { mapPreview.HighlightSelectedRoomTile = chkHighlightSelectedRoom.Checked; mapPreview.Invalidate(); };
            chkHeight.CheckedChanged += (_, __) => { mapPreview.ShowHeightMap = chkHeight.Checked; mapPreview.Invalidate(); };


            tabControl.SelectedIndexChanged += (_, __) => UpdateMapPreview();

            ConfigureGridEmpty(gridDoors);
            ConfigureGridEmpty(gridRoomLinks);
            ConfigureGridEmpty(gridRoomPos);
            ConfigureGridEmpty(gridFurniture);

            mapPreview.RoomNudgeRequested += MapPreview_RoomNudgeRequested;

            gridDoors.SelectionChanged += (_, __) =>
            {
                var row = gridDoors.CurrentRow?.DataBoundItem as DoorRow;
                _selectedDoorId = row?.DoorId;
                _selectedRoomId = null;
                _selectedFurnIndex = null;
                UpdateMapPreview();
            };

            gridRoomLinks.SelectionChanged += (_, __) =>
            {
                var row = gridRoomLinks.CurrentRow?.DataBoundItem as RoomLinkRow;
                _selectedRoomId = row?.RoomId;
                _selectedDoorId = row?.DoorId;
                _selectedFurnIndex = null;
                UpdateMapPreview();
            };

            gridRoomPos.SelectionChanged += (_, __) =>
            {
                var row = gridRoomPos.CurrentRow?.DataBoundItem as RoomPosRow;
                _selectedRoomId = row?.RoomId;
                _selectedDoorId = null;
                _selectedFurnIndex = null;
                UpdateMapPreview();
            };

            gridFurniture.SelectionChanged += (_, __) =>
            {
                var row = gridFurniture.CurrentRow?.DataBoundItem as FurnRow;
                _selectedRoomId = row?.RoomId;
                _selectedDoorId = null;
                _selectedFurnIndex = row?.Backing != null ? row.Backing.FurnDataPopOffset : null;
                UpdateMapPreview();
            };
        }

        private void MapPreview_RoomNudgeRequested(int dx, int dz)
        {
            if (_currentFloor == null || !_selectedRoomId.HasValue)
                return;

            var rid = _selectedRoomId.Value;

            if (!_currentFloor.RoomPosByRoomId.TryGetValue(rid, out var roomEntry))
                return;

            var roomRow = _roomPosRows.FirstOrDefault(r => r.RoomId == rid);
            if (roomRow == null || roomRow.Backing == null)
                return;

            int newX = roomEntry.X + dx;
            int newZ = roomEntry.Z + dz;

            if (newX < 0) newX = 0;
            if (newX > ushort.MaxValue) newX = ushort.MaxValue;
            if (newZ < 0) newZ = 0;
            if (newZ > ushort.MaxValue) newZ = ushort.MaxValue;

            int appliedDx = newX - roomEntry.X;
            int appliedDz = newZ - roomEntry.Z;

            if (appliedDx == 0 && appliedDz == 0)
                return;

            _currentFloor.TranslateSpatialForRoom(rid, appliedDx, appliedDz);

            roomEntry.X = (ushort)newX;
            roomEntry.Z = (ushort)newZ;

            roomRow.X = roomEntry.X;
            roomRow.Z = roomEntry.Z;
            roomRow.Backing.X = roomEntry.X;
            roomRow.Backing.Z = roomEntry.Z;

            if (_currentFloor.RoomLinks.TryGetValue(rid, out var roomLinks))
            {
                foreach (var l in roomLinks)
                {
                    if (_currentFloor.DoorById.TryGetValue(l.DoorId, out var dEntry))
                    {
                        int dx2 = dEntry.Data.pos_x + appliedDx;
                        int dz2 = dEntry.Data.pos_z + appliedDz;

                        if (dx2 < 0) dx2 = 0;
                        if (dx2 > ushort.MaxValue) dx2 = ushort.MaxValue;
                        if (dz2 < 0) dz2 = 0;
                        if (dz2 > ushort.MaxValue) dz2 = ushort.MaxValue;

                        var pop = dEntry.Data;
                        pop.pos_x = (ushort)dx2;
                        pop.pos_z = (ushort)dz2;
                        dEntry.Data = pop;

                        var dRow = _doorRows.FirstOrDefault(x => x.DoorId == dEntry.DoorId);
                        if (dRow != null && dRow.Backing != null)
                        {
                            dRow.PosX = pop.pos_x;
                            dRow.PosZ = pop.pos_z;
                            dRow.Backing.Data = pop;
                        }
                    }
                }
            }

            if (_currentFloor.FurnitureByRoom.TryGetValue(rid, out var furnList))
            {
                foreach (var fEntry in furnList)
                {
                    int fx = fEntry.Data.pos_x + appliedDx;
                    int fz = fEntry.Data.pos_z + appliedDz;

                    if (fx < 0) fx = 0;
                    if (fx > ushort.MaxValue) fx = ushort.MaxValue;
                    if (fz < 0) fz = 0;
                    if (fz > ushort.MaxValue) fz = ushort.MaxValue;

                    var fd = fEntry.Data;
                    fd.pos_x = (ushort)fx;
                    fd.pos_z = (ushort)fz;
                    fEntry.Data = fd;

                    var fRow = _furnRows.FirstOrDefault(x => x.Backing != null && x.Backing.FurnDataPopOffset == fEntry.FurnDataPopOffset);
                    if (fRow != null && fRow.Backing != null)
                    {
                        fRow.PosX = fd.pos_x;
                        fRow.PosZ = fd.pos_z;
                        fRow.Backing.Data = fd;
                    }
                }
            }

            gridRoomPos.Refresh();
            gridDoors.Refresh();
            gridFurniture.Refresh();
            UpdateMapPreview();
        }

        public void LoadFile(string path)
        {
            _path = path;
            _missionNo = ParseMissionNoFromFileName(path);
            _raw = File.ReadAllBytes(path);

            _floors.Clear();
            _floors.AddRange(MsnMapParser.ParseDoors(_raw, _missionNo));

            EnsureAtlasLoaded();

            BuildTree();

            var root = treeView.Nodes.Count > 0 ? treeView.Nodes[0] : null;
            var firstFloor = root?.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Tag is MsnFloor);
            if (firstFloor != null)
                treeView.SelectedNode = firstFloor;
            else
                SetCurrentFloor(null);
        }

        private void EnsureAtlasLoaded()
        {
            if (_atlasLoadAttempted)
                return;

            _atlasLoadAttempted = true;

            mapPreview.LoadAtlasFromProjectTm2(
                filesRelativeTm2Path: MapPreviewControl.DefaultAtlasTm2FilesRelPath,
                clutSet: 0,
                halfAlpha: true,
                showAtlas: true,
                alpha: 0.90f);
        }

        private static int ParseMissionNoFromFileName(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var m = Regex.Match(name, @"msn(\d\d)", RegexOptions.IgnoreCase);
            if (!m.Success) return 0;
            return int.Parse(m.Groups[1].Value);
        }

        private void BuildTree()
        {
            treeView.BeginUpdate();
            try
            {
                treeView.Nodes.Clear();

                var root = new TreeNode(Path.GetFileName(_path ?? "map.obj"));
                treeView.Nodes.Add(root);

                string[] floorNames = ["Basement", "Floor 1", "Floor 2", "Attic"];

                for (int fi = 0; fi < 4; fi++)
                {
                    if (!FloorExist.IsFloorAvailable(_missionNo, fi))
                        continue;

                    var f = _floors.FirstOrDefault(x => x.FloorIndex == fi);
                    if (f == null)
                    {
                        root.Nodes.Add(new TreeNode($"{floorNames[fi]} (no data)"));
                        continue;
                    }

                    var floorNode = new TreeNode($"{floorNames[fi]}  (rooms {f.RoomPosByRoomId.Count}, doors {f.Doors.Count}, furn {f.Furniture.Count})")
                    {
                        Tag = f
                    };

                    root.Nodes.Add(floorNode);
                }

                root.Expand();
            }
            finally
            {
                treeView.EndUpdate();
            }
        }

        private void TreeView_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is MsnFloor floor)
            {
                SetCurrentFloor(floor);
                return;
            }

            SetCurrentFloor(null);
        }

        private void SetCurrentFloor(MsnFloor? floor)
        {
            _currentFloor = floor;
            _selectedDoorId = null;
            _selectedRoomId = null;
            _selectedFurnIndex = null;

            if (floor == null)
            {
                ConfigureGridEmpty(gridDoors);
                ConfigureGridEmpty(gridRoomLinks);
                ConfigureGridEmpty(gridRoomPos);
                ConfigureGridEmpty(gridFurniture);

                gridDoors.DataSource = null;
                gridRoomLinks.DataSource = null;
                gridRoomPos.DataSource = null;
                gridFurniture.DataSource = null;

                mapPreview.SetData(null);
                return;
            }

            BuildDoorRows(floor);
            BuildRoomLinkRows(floor);
            BuildRoomPosRows(floor);
            BuildFurnRows(floor);

            ConfigureDoorsGrid();
            gridDoors.DataSource = _doorRows;

            ConfigureRoomLinkGrid();
            gridRoomLinks.DataSource = _roomLinkRows;

            ConfigureRoomPosGrid();
            gridRoomPos.DataSource = _roomPosRows;

            ConfigureFurnitureGrid();
            gridFurniture.DataSource = _furnRows;

            UpdateMapPreview();
        }

        private void UpdateMapPreview()
        {
            EnsureAtlasLoaded();
            mapPreview.SetData(_currentFloor, _selectedRoomId, _selectedDoorId, _selectedFurnIndex);
        }

        private void BuildDoorRows(MsnFloor floor)
        {
            _doorRows.Clear();
            foreach (var d in floor.Doors)
            {
                _doorRows.Add(new DoorRow
                {
                    DoorId = d.DoorId,
                    Rot = d.Data.rot,
                    PosX = d.Data.pos_x,
                    PosY = d.Data.pos_y,
                    PosZ = d.Data.pos_z,
                    Type = d.Data.type,
                    Model = d.Data.mdl_no,
                    Backing = d
                });
            }
        }

        private void BuildRoomLinkRows(MsnFloor floor)
        {
            _roomLinkRows.Clear();
            foreach (var kv in floor.RoomLinks.OrderBy(k => k.Key))
            {
                byte roomId = kv.Key;
                foreach (var c in kv.Value)
                {
                    _roomLinkRows.Add(new RoomLinkRow
                    {
                        RoomId = roomId,
                        RoomName = RoomNames.Get(roomId),
                        DoorId = c.DoorId,
                        BeyondRoomId = c.BeyondRoomId,
                        BeyondRoomName = RoomNames.Get(c.BeyondRoomId),
                        Unk = c.Unk,
                        Backing = c
                    });
                }
            }
        }

        private void BuildRoomPosRows(MsnFloor floor)
        {
            _roomPosRows.Clear();

            IEnumerable<byte> order = floor.RoomDispOrder.Count > 0
                ? floor.RoomDispOrder
                : floor.RoomPosByRoomId.Keys.OrderBy(x => x);

            foreach (var roomId in order)
            {
                if (!floor.RoomPosByRoomId.TryGetValue(roomId, out var e))
                    continue;

                _roomPosRows.Add(new RoomPosRow
                {
                    RoomId = e.RoomId,
                    RoomName = RoomNames.Get(e.RoomId),
                    X = e.X,
                    Y = e.Y,
                    Z = e.Z,
                    W = e.Height,
                    Backing = e
                });
            }

            foreach (var kv in floor.RoomPosByRoomId.OrderBy(k => k.Key))
            {
                if (_roomPosRows.Any(r => r.RoomId == kv.Key))
                    continue;

                var e = kv.Value;
                _roomPosRows.Add(new RoomPosRow
                {
                    RoomId = e.RoomId,
                    RoomName = RoomNames.Get(e.RoomId),
                    X = e.X,
                    Y = e.Y,
                    Z = e.Z,
                    W = e.Height,
                    Backing = e
                });
            }
        }

        private void BuildFurnRows(MsnFloor floor)
        {
            _furnRows.Clear();

            foreach (var f in floor.Furniture.OrderBy(x => x.RoomId).ThenBy(x => x.FurnDataPopOffset))
            {
                _furnRows.Add(new FurnRow
                {
                    RoomId = f.RoomId,
                    RoomName = RoomNames.Get(f.RoomId),
                    PosX = f.Data.pos_x,
                    PosY = f.Data.pos_y,
                    PosZ = f.Data.pos_z,
                    RotX = f.Data.rot_x,
                    RotY = f.Data.rot_y,
                    AttrId = f.Data.attr_id,
                    ModelNo = f.Data.model_no,
                    Id = f.Data.id,
                    Top = f.Data.top,
                    Btm = f.Data.btm,
                    Snum = f.Data.snum,
                    Unk19 = f.Data.unk19,
                    Unk1A = f.Data.unk1A,
                    Unk1B = f.Data.unk1B,
                    Backing = f
                });
            }
        }

        private void Btn_Save_Click(object? sender, EventArgs e)
        {
            if (_path == null || _raw == null)
                return;

            try
            {
                foreach (var row in _doorRows)
                {
                    if (row.Backing == null) continue;

                    var pop = row.Backing.Data;
                    pop.rot = row.Rot;
                    pop.pos_x = row.PosX;
                    pop.pos_y = row.PosY;
                    pop.pos_z = row.PosZ;
                    pop.type = row.Type;
                    pop.mdl_no = row.Model;
                    row.Backing.Data = pop;
                }

                foreach (var row in _roomLinkRows)
                {
                    if (row.Backing == null) continue;
                    row.Backing.BeyondRoomId = row.BeyondRoomId;
                    row.Backing.Unk = row.Unk;
                }

                foreach (var row in _roomPosRows)
                {
                    if (row.Backing == null) continue;
                    row.Backing.X = row.X;
                    row.Backing.Y = row.Y;
                    row.Backing.Z = row.Z;
                    row.Backing.Height = row.W;
                }

                foreach (var row in _furnRows)
                {
                    if (row.Backing == null) continue;

                    var d = row.Backing.Data;
                    d.pos_x = row.PosX;
                    d.pos_y = row.PosY;
                    d.pos_z = row.PosZ;
                    d.rot_x = row.RotX;
                    d.rot_y = row.RotY;
                    d.attr_id = row.AttrId;
                    d.model_no = row.ModelNo;
                    d.id = row.Id;
                    d.top = row.Top;
                    d.btm = row.Btm;
                    d.snum = row.Snum;
                    d.unk19 = row.Unk19;
                    d.unk1A = row.Unk1A;
                    d.unk1B = row.Unk1B;
                    row.Backing.Data = d;
                }

                foreach (var f in _floors)
                {
                    f.WriteBackDoors(_raw);
                    f.WriteBackDoorLinkage(_raw);
                    f.WriteBackRoomPositions(_raw);
                    f.WriteBackFurniture(_raw);
                    f.WriteBackSpatial(_raw);
                }

                File.WriteAllBytes(_path, _raw);

                MessageBox.Show(this, "Saved map changes.", "Save",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Save failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ConfigureDoorsGrid()
        {
            ConfigureCommonGrid(gridDoors);

            gridDoors.AutoGenerateColumns = false;
            gridDoors.Columns.Clear();

            gridDoors.Columns.Add(MakeCol(nameof(DoorRow.DoorId), "DoorId", readOnly: true, width: 70));
            gridDoors.Columns.Add(MakeCol(nameof(DoorRow.PosX), "PosX", width: 70));
            gridDoors.Columns.Add(MakeCol(nameof(DoorRow.PosY), "PosY", width: 70));
            gridDoors.Columns.Add(MakeCol(nameof(DoorRow.PosZ), "PosZ", width: 70));
            gridDoors.Columns.Add(MakeCol(nameof(DoorRow.Rot), "Rot", width: 90));
            gridDoors.Columns.Add(MakeCol(nameof(DoorRow.Type), "Type", width: 70));
            gridDoors.Columns.Add(MakeCol(nameof(DoorRow.Model), "Model", width: 70));
        }

        private void ConfigureRoomLinkGrid()
        {
            ConfigureCommonGrid(gridRoomLinks);

            gridRoomLinks.AutoGenerateColumns = false;
            gridRoomLinks.Columns.Clear();

            gridRoomLinks.Columns.Add(MakeCol(nameof(RoomLinkRow.RoomId), "RoomId", readOnly: true, width: 70));
            gridRoomLinks.Columns.Add(MakeCol(nameof(RoomLinkRow.RoomName), "RoomName", readOnly: true, width: 220));
            gridRoomLinks.Columns.Add(MakeCol(nameof(RoomLinkRow.DoorId), "DoorId", readOnly: true, width: 70));
            gridRoomLinks.Columns.Add(MakeCol(nameof(RoomLinkRow.BeyondRoomId), "BeyondId", width: 90));
            gridRoomLinks.Columns.Add(MakeCol(nameof(RoomLinkRow.BeyondRoomName), "BeyondName", readOnly: true, width: 220));
            gridRoomLinks.Columns.Add(MakeCol(nameof(RoomLinkRow.Unk), "Unk", width: 70));
        }

        private void ConfigureRoomPosGrid()
        {
            ConfigureCommonGrid(gridRoomPos);

            gridRoomPos.AutoGenerateColumns = false;
            gridRoomPos.Columns.Clear();

            gridRoomPos.Columns.Add(MakeCol(nameof(RoomPosRow.RoomId), "RoomId", readOnly: true, width: 70));
            gridRoomPos.Columns.Add(MakeCol(nameof(RoomPosRow.RoomName), "RoomName", readOnly: true, width: 240));
            gridRoomPos.Columns.Add(MakeCol(nameof(RoomPosRow.X), "X", width: 70));
            gridRoomPos.Columns.Add(MakeCol(nameof(RoomPosRow.Y), "Y", width: 70));
            gridRoomPos.Columns.Add(MakeCol(nameof(RoomPosRow.Z), "Z", width: 70));
            gridRoomPos.Columns.Add(MakeCol(nameof(RoomPosRow.W), "W", width: 80));
        }

        private void ConfigureFurnitureGrid()
        {
            ConfigureCommonGrid(gridFurniture);

            gridFurniture.AutoGenerateColumns = false;
            gridFurniture.Columns.Clear();

            gridFurniture.Columns.Add(MakeCol(nameof(FurnRow.RoomId), "RoomId", readOnly: true, width: 70));
            gridFurniture.Columns.Add(MakeCol(nameof(FurnRow.RoomName), "RoomName", readOnly: true, width: 240));
            gridFurniture.Columns.Add(MakeCol(nameof(FurnRow.PosX), "PosX", width: 70));
            gridFurniture.Columns.Add(MakeCol(nameof(FurnRow.PosY), "PosY", width: 70));
            gridFurniture.Columns.Add(MakeCol(nameof(FurnRow.PosZ), "PosZ", width: 70));
            gridFurniture.Columns.Add(MakeCol(nameof(FurnRow.RotX), "RotX", width: 90));
            gridFurniture.Columns.Add(MakeCol(nameof(FurnRow.RotY), "RotY", width: 90));
            gridFurniture.Columns.Add(MakeCol(nameof(FurnRow.ModelNo), "Model", width: 80));
            gridFurniture.Columns.Add(MakeCol(nameof(FurnRow.Id), "Id", width: 80));
            gridFurniture.Columns.Add(MakeCol(nameof(FurnRow.AttrId), "Attr", width: 80));
            gridFurniture.Columns.Add(MakeCol(nameof(FurnRow.Top), "Top", width: 70));
            gridFurniture.Columns.Add(MakeCol(nameof(FurnRow.Btm), "Btm", width: 70));
            gridFurniture.Columns.Add(MakeCol(nameof(FurnRow.Snum), "Snum", width: 70));
            gridFurniture.Columns.Add(MakeCol(nameof(FurnRow.Unk19), "U19", width: 70));
            gridFurniture.Columns.Add(MakeCol(nameof(FurnRow.Unk1A), "U1A", width: 70));
            gridFurniture.Columns.Add(MakeCol(nameof(FurnRow.Unk1B), "U1B", width: 70));
        }

        private static void ConfigureCommonGrid(DataGridView gv)
        {
            gv.AllowUserToAddRows = false;
            gv.AllowUserToDeleteRows = false;
            gv.RowHeadersVisible = false;
            gv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gv.MultiSelect = false;
        }

        private static void ConfigureGridEmpty(DataGridView gv)
        {
            ConfigureCommonGrid(gv);
            gv.AutoGenerateColumns = false;
            gv.Columns.Clear();
            gv.DataSource = null;
        }

        private static DataGridViewTextBoxColumn MakeCol(string prop, string header, bool readOnly = false, int width = 0)
        {
            return new DataGridViewTextBoxColumn
            {
                DataPropertyName = prop,
                HeaderText = header,
                ReadOnly = readOnly,
                AutoSizeMode = width > 0 ? DataGridViewAutoSizeColumnMode.None : DataGridViewAutoSizeColumnMode.Fill,
                Width = width > 0 ? width : 100
            };
        }

        private sealed class DoorRow
        {
            public short DoorId { get; set; }
            public ushort PosX { get; set; }
            public short PosY { get; set; }
            public ushort PosZ { get; set; }
            public float Rot { get; set; }
            public ushort Type { get; set; }
            public ushort Model { get; set; }
            public MsnFloor.DoorEntry? Backing { get; set; }
        }

        private sealed class RoomLinkRow
        {
            public byte RoomId { get; set; }
            public string RoomName { get; set; } = "";
            public short DoorId { get; set; }
            public byte BeyondRoomId { get; set; }
            public string BeyondRoomName { get; set; } = "";
            public byte Unk { get; set; }
            public MsnFloor.LinkEntry? Backing { get; set; }
        }

        private sealed class RoomPosRow
        {
            public byte RoomId { get; set; }
            public string RoomName { get; set; } = "";
            public ushort X { get; set; }
            public short Y { get; set; }
            public ushort Z { get; set; }
            public short W { get; set; }
            public MsnFloor.RoomPosEntry? Backing { get; set; }
        }

        private sealed class FurnRow
        {
            public byte RoomId { get; set; }
            public string RoomName { get; set; } = "";
            public ushort PosX { get; set; }
            public short PosY { get; set; }
            public ushort PosZ { get; set; }
            public float RotX { get; set; }
            public float RotY { get; set; }
            public short AttrId { get; set; }
            public short ModelNo { get; set; }
            public short Id { get; set; }
            public short Top { get; set; }
            public short Btm { get; set; }
            public byte Snum { get; set; }
            public byte Unk19 { get; set; }
            public byte Unk1A { get; set; }
            public byte Unk1B { get; set; }
            public MsnFloor.FurnEntry? Backing { get; set; }
        }
    }
}
