using ZeroEditor.Zero1.Editors.MsnMap.Editor;

namespace ZeroEditor.Zero1.Editors.MsnMap
{
    partial class MsnMapEditor
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            splitContainer1 = new SplitContainer();
            btn_save = new Button();
            treeView = new TreeView();
            splitRight = new SplitContainer();
            previewHost = new Panel();
            mapPreview = new MapPreviewControl();
            previewTopBar = new Panel();
            flowPreviewToggles = new FlowLayoutPanel();
            chkAtlas = new CheckBox();
            chkGrid = new CheckBox();
            chkRooms = new CheckBox();
            chkDoors = new CheckBox();
            chkFurniture = new CheckBox();
            chkAreas = new CheckBox();
            chkCollisions = new CheckBox();
            chkHighlightSelectedRoom = new CheckBox();
            tabControl = new DarkTabControl();
            tabDoors = new TabPage();
            gridDoors = new DataGridView();
            tabRoomsDoors = new TabPage();
            splitLinks = new SplitContainer();
            gridRoomLinks = new DataGridView();
            gridRoomPos = new DataGridView();
            tabFurniture = new TabPage();
            gridFurniture = new DataGridView();
            chkHeight = new CheckBox();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitRight).BeginInit();
            splitRight.Panel1.SuspendLayout();
            splitRight.Panel2.SuspendLayout();
            splitRight.SuspendLayout();
            previewHost.SuspendLayout();
            previewTopBar.SuspendLayout();
            flowPreviewToggles.SuspendLayout();
            tabControl.SuspendLayout();
            tabDoors.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridDoors).BeginInit();
            tabRoomsDoors.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitLinks).BeginInit();
            splitLinks.Panel1.SuspendLayout();
            splitLinks.Panel2.SuspendLayout();
            splitLinks.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridRoomLinks).BeginInit();
            ((System.ComponentModel.ISupportInitialize)gridRoomPos).BeginInit();
            tabFurniture.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridFurniture).BeginInit();
            SuspendLayout();
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(0, 0);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(btn_save);
            splitContainer1.Panel1.Controls.Add(treeView);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(splitRight);
            splitContainer1.Size = new Size(1539, 656);
            splitContainer1.SplitterDistance = 284;
            splitContainer1.TabIndex = 0;
            // 
            // btn_save
            // 
            btn_save.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            btn_save.Location = new Point(0, 619);
            btn_save.Name = "btn_save";
            btn_save.Size = new Size(284, 34);
            btn_save.TabIndex = 1;
            btn_save.Text = "Save";
            btn_save.UseVisualStyleBackColor = true;
            // 
            // treeView
            // 
            treeView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            treeView.Location = new Point(0, 0);
            treeView.Name = "treeView";
            treeView.Size = new Size(284, 613);
            treeView.TabIndex = 0;
            // 
            // splitRight
            // 
            splitRight.Dock = DockStyle.Fill;
            splitRight.Location = new Point(0, 0);
            splitRight.Name = "splitRight";
            // 
            // splitRight.Panel1
            // 
            splitRight.Panel1.Controls.Add(previewHost);
            // 
            // splitRight.Panel2
            // 
            splitRight.Panel2.Controls.Add(tabControl);
            splitRight.Size = new Size(1251, 656);
            splitRight.SplitterDistance = 775;
            splitRight.TabIndex = 0;
            // 
            // previewHost
            // 
            previewHost.Controls.Add(mapPreview);
            previewHost.Controls.Add(previewTopBar);
            previewHost.Dock = DockStyle.Fill;
            previewHost.Location = new Point(0, 0);
            previewHost.Name = "previewHost";
            previewHost.Size = new Size(775, 656);
            previewHost.TabIndex = 0;
            // 
            // mapPreview
            // 
            mapPreview.AtlasFloorIdOverride = null;
            mapPreview.AtlasNudgeWorldX = 650F;
            mapPreview.AtlasNudgeWorldZ = 400F;
            mapPreview.AtlasScaleBias = 1F;
            mapPreview.BackColor = Color.FromArgb(24, 24, 24);
            mapPreview.Dock = DockStyle.Fill;
            mapPreview.DoorDoubleSeparationWorld = 40F;
            mapPreview.DoorGroupQuantizeWorld = 24F;
            mapPreview.DoorLeafLengthWorld = 520F;
            mapPreview.ForeColor = Color.Gainsboro;
            mapPreview.HeightMapAlpha = 0.35F;
            mapPreview.HeightMapCellWorld = 160;
            mapPreview.HeightMapStepWorld = 128;
            mapPreview.HeightMapUseWorldHeight = true;
            mapPreview.HighlightSelectedRoomTile = true;
            mapPreview.Location = new Point(0, 36);
            mapPreview.Name = "mapPreview";
            mapPreview.ShowAreas = true;
            mapPreview.ShowAtlas = true;
            mapPreview.ShowCollisions = true;
            mapPreview.ShowDoors = true;
            mapPreview.ShowFurniture = true;
            mapPreview.ShowGrid = true;
            mapPreview.ShowHeightMap = true;
            mapPreview.ShowRooms = true;
            mapPreview.Size = new Size(775, 620);
            mapPreview.TabIndex = 0;
            // 
            // previewTopBar
            // 
            previewTopBar.Controls.Add(flowPreviewToggles);
            previewTopBar.Dock = DockStyle.Top;
            previewTopBar.Location = new Point(0, 0);
            previewTopBar.Name = "previewTopBar";
            previewTopBar.Padding = new Padding(6);
            previewTopBar.Size = new Size(775, 36);
            previewTopBar.TabIndex = 1;
            // 
            // flowPreviewToggles
            // 
            flowPreviewToggles.Controls.Add(chkAtlas);
            flowPreviewToggles.Controls.Add(chkGrid);
            flowPreviewToggles.Controls.Add(chkRooms);
            flowPreviewToggles.Controls.Add(chkDoors);
            flowPreviewToggles.Controls.Add(chkFurniture);
            flowPreviewToggles.Controls.Add(chkAreas);
            flowPreviewToggles.Controls.Add(chkCollisions);
            flowPreviewToggles.Controls.Add(chkHeight);
            flowPreviewToggles.Controls.Add(chkHighlightSelectedRoom);
            flowPreviewToggles.Dock = DockStyle.Fill;
            flowPreviewToggles.Location = new Point(6, 6);
            flowPreviewToggles.Name = "flowPreviewToggles";
            flowPreviewToggles.Size = new Size(763, 24);
            flowPreviewToggles.TabIndex = 0;
            // 
            // chkAtlas
            // 
            chkAtlas.AutoSize = true;
            chkAtlas.Checked = true;
            chkAtlas.CheckState = CheckState.Checked;
            chkAtlas.Location = new Point(3, 3);
            chkAtlas.Name = "chkAtlas";
            chkAtlas.Size = new Size(52, 19);
            chkAtlas.TabIndex = 0;
            chkAtlas.Text = "Atlas";
            // 
            // chkGrid
            // 
            chkGrid.AutoSize = true;
            chkGrid.Checked = true;
            chkGrid.CheckState = CheckState.Checked;
            chkGrid.Location = new Point(61, 3);
            chkGrid.Name = "chkGrid";
            chkGrid.Size = new Size(48, 19);
            chkGrid.TabIndex = 1;
            chkGrid.Text = "Grid";
            // 
            // chkRooms
            // 
            chkRooms.AutoSize = true;
            chkRooms.Checked = true;
            chkRooms.CheckState = CheckState.Checked;
            chkRooms.Location = new Point(115, 3);
            chkRooms.Name = "chkRooms";
            chkRooms.Size = new Size(63, 19);
            chkRooms.TabIndex = 2;
            chkRooms.Text = "Rooms";
            // 
            // chkDoors
            // 
            chkDoors.AutoSize = true;
            chkDoors.Checked = true;
            chkDoors.CheckState = CheckState.Checked;
            chkDoors.Location = new Point(184, 3);
            chkDoors.Name = "chkDoors";
            chkDoors.Size = new Size(57, 19);
            chkDoors.TabIndex = 3;
            chkDoors.Text = "Doors";
            // 
            // chkFurniture
            // 
            chkFurniture.AutoSize = true;
            chkFurniture.Checked = true;
            chkFurniture.CheckState = CheckState.Checked;
            chkFurniture.Location = new Point(247, 3);
            chkFurniture.Name = "chkFurniture";
            chkFurniture.Size = new Size(74, 19);
            chkFurniture.TabIndex = 4;
            chkFurniture.Text = "Furniture";
            // 
            // chkAreas
            // 
            chkAreas.AutoSize = true;
            chkAreas.Checked = true;
            chkAreas.CheckState = CheckState.Checked;
            chkAreas.Location = new Point(327, 3);
            chkAreas.Name = "chkAreas";
            chkAreas.Size = new Size(55, 19);
            chkAreas.TabIndex = 5;
            chkAreas.Text = "Areas";
            // 
            // chkCollisions
            // 
            chkCollisions.AutoSize = true;
            chkCollisions.Checked = true;
            chkCollisions.CheckState = CheckState.Checked;
            chkCollisions.Location = new Point(388, 3);
            chkCollisions.Name = "chkCollisions";
            chkCollisions.Size = new Size(77, 19);
            chkCollisions.TabIndex = 6;
            chkCollisions.Text = "Collisions";
            // 
            // chkHighlightSelectedRoom
            // 
            chkHighlightSelectedRoom.AutoSize = true;
            chkHighlightSelectedRoom.Checked = true;
            chkHighlightSelectedRoom.CheckState = CheckState.Checked;
            chkHighlightSelectedRoom.Location = new Point(566, 3);
            chkHighlightSelectedRoom.Name = "chkHighlightSelectedRoom";
            chkHighlightSelectedRoom.Size = new Size(76, 19);
            chkHighlightSelectedRoom.TabIndex = 7;
            chkHighlightSelectedRoom.Text = "Highlight";
            // 
            // tabControl
            // 
            tabControl.Controls.Add(tabDoors);
            tabControl.Controls.Add(tabRoomsDoors);
            tabControl.Controls.Add(tabFurniture);
            tabControl.Dock = DockStyle.Fill;
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl.ForeColor = Color.FromArgb(225, 225, 225);
            tabControl.ItemSize = new Size(140, 28);
            tabControl.Location = new Point(0, 0);
            tabControl.Name = "tabControl";
            tabControl.Padding = new Point(12, 6);
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(472, 656);
            tabControl.SizeMode = TabSizeMode.Fixed;
            tabControl.TabIndex = 0;
            // 
            // tabDoors
            // 
            tabDoors.BackColor = Color.FromArgb(42, 42, 42);
            tabDoors.Controls.Add(gridDoors);
            tabDoors.ForeColor = Color.FromArgb(225, 225, 225);
            tabDoors.Location = new Point(4, 32);
            tabDoors.Name = "tabDoors";
            tabDoors.Padding = new Padding(3);
            tabDoors.Size = new Size(597, 620);
            tabDoors.TabIndex = 0;
            tabDoors.Text = "Doors";
            // 
            // gridDoors
            // 
            gridDoors.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridDoors.Dock = DockStyle.Fill;
            gridDoors.Location = new Point(3, 3);
            gridDoors.Name = "gridDoors";
            gridDoors.Size = new Size(591, 614);
            gridDoors.TabIndex = 0;
            // 
            // tabRoomsDoors
            // 
            tabRoomsDoors.BackColor = Color.FromArgb(42, 42, 42);
            tabRoomsDoors.Controls.Add(splitLinks);
            tabRoomsDoors.ForeColor = Color.FromArgb(225, 225, 225);
            tabRoomsDoors.Location = new Point(4, 32);
            tabRoomsDoors.Name = "tabRoomsDoors";
            tabRoomsDoors.Padding = new Padding(3);
            tabRoomsDoors.Size = new Size(597, 620);
            tabRoomsDoors.TabIndex = 1;
            tabRoomsDoors.Text = "Rooms/Doors";
            // 
            // splitLinks
            // 
            splitLinks.Dock = DockStyle.Fill;
            splitLinks.Location = new Point(3, 3);
            splitLinks.Name = "splitLinks";
            splitLinks.Orientation = Orientation.Horizontal;
            // 
            // splitLinks.Panel1
            // 
            splitLinks.Panel1.Controls.Add(gridRoomLinks);
            // 
            // splitLinks.Panel2
            // 
            splitLinks.Panel2.Controls.Add(gridRoomPos);
            splitLinks.Size = new Size(591, 614);
            splitLinks.SplitterDistance = 306;
            splitLinks.TabIndex = 0;
            // 
            // gridRoomLinks
            // 
            gridRoomLinks.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridRoomLinks.Dock = DockStyle.Fill;
            gridRoomLinks.Location = new Point(0, 0);
            gridRoomLinks.Name = "gridRoomLinks";
            gridRoomLinks.Size = new Size(591, 306);
            gridRoomLinks.TabIndex = 0;
            // 
            // gridRoomPos
            // 
            gridRoomPos.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridRoomPos.Dock = DockStyle.Fill;
            gridRoomPos.Location = new Point(0, 0);
            gridRoomPos.Name = "gridRoomPos";
            gridRoomPos.Size = new Size(591, 304);
            gridRoomPos.TabIndex = 0;
            // 
            // tabFurniture
            // 
            tabFurniture.BackColor = Color.FromArgb(42, 42, 42);
            tabFurniture.Controls.Add(gridFurniture);
            tabFurniture.ForeColor = Color.FromArgb(225, 225, 225);
            tabFurniture.Location = new Point(4, 32);
            tabFurniture.Name = "tabFurniture";
            tabFurniture.Padding = new Padding(3);
            tabFurniture.Size = new Size(464, 620);
            tabFurniture.TabIndex = 2;
            tabFurniture.Text = "Furniture";
            // 
            // gridFurniture
            // 
            gridFurniture.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridFurniture.Dock = DockStyle.Fill;
            gridFurniture.Location = new Point(3, 3);
            gridFurniture.Name = "gridFurniture";
            gridFurniture.Size = new Size(458, 614);
            gridFurniture.TabIndex = 0;
            // 
            // chkHeight
            // 
            chkHeight.AutoSize = true;
            chkHeight.Checked = true;
            chkHeight.CheckState = CheckState.Checked;
            chkHeight.Location = new Point(471, 3);
            chkHeight.Name = "chkHeight";
            chkHeight.Size = new Size(89, 19);
            chkHeight.TabIndex = 8;
            chkHeight.Text = "Height Map";
            // 
            // MsnMapEditor
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(splitContainer1);
            Name = "MsnMapEditor";
            Size = new Size(1539, 656);
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            splitRight.Panel1.ResumeLayout(false);
            splitRight.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitRight).EndInit();
            splitRight.ResumeLayout(false);
            previewHost.ResumeLayout(false);
            previewTopBar.ResumeLayout(false);
            flowPreviewToggles.ResumeLayout(false);
            flowPreviewToggles.PerformLayout();
            tabControl.ResumeLayout(false);
            tabDoors.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridDoors).EndInit();
            tabRoomsDoors.ResumeLayout(false);
            splitLinks.Panel1.ResumeLayout(false);
            splitLinks.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitLinks).EndInit();
            splitLinks.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridRoomLinks).EndInit();
            ((System.ComponentModel.ISupportInitialize)gridRoomPos).EndInit();
            tabFurniture.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridFurniture).EndInit();
            ResumeLayout(false);
        }

        private SplitContainer splitContainer1;
        private TreeView treeView;
        private Button btn_save;
        private SplitContainer splitRight;

        private Panel previewHost;
        private Panel previewTopBar;
        private FlowLayoutPanel flowPreviewToggles;
        private CheckBox chkAtlas;
        private CheckBox chkGrid;
        private CheckBox chkRooms;
        private CheckBox chkDoors;
        private CheckBox chkFurniture;
        private CheckBox chkAreas;
        private CheckBox chkCollisions;
        private CheckBox chkHighlightSelectedRoom;

        private MapPreviewControl mapPreview;

        private DarkTabControl tabControl;
        private TabPage tabDoors;
        private TabPage tabRoomsDoors;
        private TabPage tabFurniture;
        private DataGridView gridDoors;
        private SplitContainer splitLinks;
        private DataGridView gridRoomLinks;
        private DataGridView gridRoomPos;
        private DataGridView gridFurniture;
        private CheckBox chkHeight;
    }
}
