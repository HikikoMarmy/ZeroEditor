using System.Drawing;
using System.Windows.Forms;

namespace ZeroEditor.Editors.Elf.Zero1
{
	partial class MapItemEditor
	{
		private System.ComponentModel.IContainer components = null;
		private TableLayoutPanel layoutMain;
		private TableLayoutPanel layoutLeft;
		private ListBox lst;
		private TableLayoutPanel layoutRight;
		private Label lblItemNo;
		private NumericUpDown numItemNo;
		private Label lblStts;
		private NumericUpDown numStts;
		private Label lblRoom;
		private NumericUpDown numRoom;
		private Label lblMission;
		private NumericUpDown numMission;
		private GroupBox grpPos;
		private TableLayoutPanel layoutPos;
		private Label lblX;
		private NumericUpDown numX;
		private Label lblY;
		private NumericUpDown numY;
		private Label lblZ;
		private NumericUpDown numZ;
		private GroupBox grpMsgs;
		private TableLayoutPanel layoutMsgs;
		private Label lblMsg0;
		private NumericUpDown numMsg0;
		private Label lblMsg1;
		private NumericUpDown numMsg1;

		protected override void Dispose( bool disposing )
		{
			if( disposing && ( components != null ) )
			{
				components.Dispose();
			}
			base.Dispose( disposing );
		}

		private void InitializeComponent()
		{
			layoutMain = new TableLayoutPanel();
			layoutLeft = new TableLayoutPanel();
			lst = new ListBox();
			layoutRight = new TableLayoutPanel();
			lblItemNo = new Label();
			numItemNo = new NumericUpDown();
			lblStts = new Label();
			numStts = new NumericUpDown();
			lblRoom = new Label();
			numRoom = new NumericUpDown();
			lblMission = new Label();
			numMission = new NumericUpDown();
			grpPos = new GroupBox();
			layoutPos = new TableLayoutPanel();
			lblX = new Label();
			numX = new NumericUpDown();
			lblY = new Label();
			numY = new NumericUpDown();
			lblZ = new Label();
			numZ = new NumericUpDown();
			grpMsgs = new GroupBox();
			layoutMsgs = new TableLayoutPanel();
			lblMsg0 = new Label();
			numMsg0 = new NumericUpDown();
			lblMsg1 = new Label();
			numMsg1 = new NumericUpDown();
			layoutMain.SuspendLayout();
			layoutLeft.SuspendLayout();
			layoutRight.SuspendLayout();
			( (System.ComponentModel.ISupportInitialize)numItemNo ).BeginInit();
			( (System.ComponentModel.ISupportInitialize)numStts ).BeginInit();
			( (System.ComponentModel.ISupportInitialize)numRoom ).BeginInit();
			( (System.ComponentModel.ISupportInitialize)numMission ).BeginInit();
			grpPos.SuspendLayout();
			layoutPos.SuspendLayout();
			( (System.ComponentModel.ISupportInitialize)numX ).BeginInit();
			( (System.ComponentModel.ISupportInitialize)numY ).BeginInit();
			( (System.ComponentModel.ISupportInitialize)numZ ).BeginInit();
			grpMsgs.SuspendLayout();
			layoutMsgs.SuspendLayout();
			( (System.ComponentModel.ISupportInitialize)numMsg0 ).BeginInit();
			( (System.ComponentModel.ISupportInitialize)numMsg1 ).BeginInit();
			SuspendLayout();
			// 
			// layoutMain
			// 
			layoutMain.ColumnCount = 2;
			layoutMain.ColumnStyles.Add( new ColumnStyle( SizeType.Absolute, 260F ) );
			layoutMain.ColumnStyles.Add( new ColumnStyle( SizeType.Percent, 100F ) );
			layoutMain.Controls.Add( layoutLeft, 0, 0 );
			layoutMain.Controls.Add( layoutRight, 1, 0 );
			layoutMain.Dock = DockStyle.Fill;
			layoutMain.Location = new Point( 0, 0 );
			layoutMain.Name = "layoutMain";
			layoutMain.RowCount = 1;
			layoutMain.RowStyles.Add( new RowStyle( SizeType.Percent, 100F ) );
			layoutMain.Size = new Size( 900, 560 );
			layoutMain.TabIndex = 0;
			// 
			// layoutLeft
			// 
			layoutLeft.ColumnCount = 1;
			layoutLeft.ColumnStyles.Add( new ColumnStyle( SizeType.Percent, 100F ) );
			layoutLeft.Controls.Add( lst, 0, 0 );
			layoutLeft.Dock = DockStyle.Fill;
			layoutLeft.Location = new Point( 3, 3 );
			layoutLeft.Name = "layoutLeft";
			layoutLeft.RowCount = 1;
			layoutLeft.RowStyles.Add( new RowStyle( SizeType.Percent, 100F ) );
			layoutLeft.Size = new Size( 254, 554 );
			layoutLeft.TabIndex = 0;
			// 
			// lst
			// 
			lst.Dock = DockStyle.Fill;
			lst.FormattingEnabled = true;
			lst.IntegralHeight = false;
			lst.ItemHeight = 15;
			lst.Location = new Point( 0, 0 );
			lst.Margin = new Padding( 0 );
			lst.Name = "lst";
			lst.Size = new Size( 254, 554 );
			lst.TabIndex = 0;
			// 
			// layoutRight
			// 
			layoutRight.ColumnCount = 2;
			layoutRight.ColumnStyles.Add( new ColumnStyle( SizeType.Absolute, 160F ) );
			layoutRight.ColumnStyles.Add( new ColumnStyle( SizeType.Percent, 100F ) );
			layoutRight.Controls.Add( lblItemNo, 0, 0 );
			layoutRight.Controls.Add( numItemNo, 1, 0 );
			layoutRight.Controls.Add( lblStts, 0, 1 );
			layoutRight.Controls.Add( numStts, 1, 1 );
			layoutRight.Controls.Add( lblRoom, 0, 2 );
			layoutRight.Controls.Add( numRoom, 1, 2 );
			layoutRight.Controls.Add( lblMission, 0, 3 );
			layoutRight.Controls.Add( numMission, 1, 3 );
			layoutRight.Controls.Add( grpPos, 0, 4 );
			layoutRight.Controls.Add( grpMsgs, 0, 5 );
			layoutRight.Dock = DockStyle.Fill;
			layoutRight.Location = new Point( 263, 3 );
			layoutRight.Name = "layoutRight";
			layoutRight.RowCount = 6;
			layoutRight.RowStyles.Add( new RowStyle() );
			layoutRight.RowStyles.Add( new RowStyle() );
			layoutRight.RowStyles.Add( new RowStyle() );
			layoutRight.RowStyles.Add( new RowStyle() );
			layoutRight.RowStyles.Add( new RowStyle() );
			layoutRight.RowStyles.Add( new RowStyle( SizeType.Percent, 100F ) );
			layoutRight.Size = new Size( 634, 554 );
			layoutRight.TabIndex = 1;
			// 
			// lblItemNo
			// 
			lblItemNo.Anchor = AnchorStyles.Left;
			lblItemNo.AutoSize = true;
			lblItemNo.Location = new Point( 3, 7 );
			lblItemNo.Name = "lblItemNo";
			lblItemNo.Size = new Size( 102, 15 );
			lblItemNo.TabIndex = 0;
			lblItemNo.Text = "Item Number (id):";
			// 
			// numItemNo
			// 
			numItemNo.Anchor =  AnchorStyles.Left  |  AnchorStyles.Right ;
			numItemNo.Location = new Point( 163, 3 );
			numItemNo.Maximum = new decimal( new int[] { 255, 0, 0, 0 } );
			numItemNo.Name = "numItemNo";
			numItemNo.Size = new Size( 468, 23 );
			numItemNo.TabIndex = 1;
			// 
			// lblStts
			// 
			lblStts.Anchor = AnchorStyles.Left;
			lblStts.AutoSize = true;
			lblStts.Location = new Point( 3, 36 );
			lblStts.Name = "lblStts";
			lblStts.Size = new Size( 29, 15 );
			lblStts.TabIndex = 2;
			lblStts.Text = "Stts:";
			// 
			// numStts
			// 
			numStts.Anchor =  AnchorStyles.Left  |  AnchorStyles.Right ;
			numStts.Location = new Point( 163, 32 );
			numStts.Maximum = new decimal( new int[] { 255, 0, 0, 0 } );
			numStts.Name = "numStts";
			numStts.Size = new Size( 468, 23 );
			numStts.TabIndex = 3;
			// 
			// lblRoom
			// 
			lblRoom.Anchor = AnchorStyles.Left;
			lblRoom.AutoSize = true;
			lblRoom.Location = new Point( 3, 65 );
			lblRoom.Name = "lblRoom";
			lblRoom.Size = new Size( 42, 15 );
			lblRoom.TabIndex = 4;
			lblRoom.Text = "Room:";
			// 
			// numRoom
			// 
			numRoom.Anchor =  AnchorStyles.Left  |  AnchorStyles.Right ;
			numRoom.Location = new Point( 163, 61 );
			numRoom.Maximum = new decimal( new int[] { 255, 0, 0, 0 } );
			numRoom.Name = "numRoom";
			numRoom.Size = new Size( 468, 23 );
			numRoom.TabIndex = 5;
			// 
			// lblMission
			// 
			lblMission.Anchor = AnchorStyles.Left;
			lblMission.AutoSize = true;
			lblMission.Location = new Point( 3, 94 );
			lblMission.Name = "lblMission";
			lblMission.Size = new Size( 70, 15 );
			lblMission.TabIndex = 6;
			lblMission.Text = "Mission No:";
			// 
			// numMission
			// 
			numMission.Anchor =  AnchorStyles.Left  |  AnchorStyles.Right ;
			numMission.Location = new Point( 163, 90 );
			numMission.Maximum = new decimal( new int[] { 255, 0, 0, 0 } );
			numMission.Name = "numMission";
			numMission.Size = new Size( 468, 23 );
			numMission.TabIndex = 7;
			// 
			// grpPos
			// 
			grpPos.Anchor =   AnchorStyles.Top  |  AnchorStyles.Left   |  AnchorStyles.Right ;
			layoutRight.SetColumnSpan( grpPos, 2 );
			grpPos.Controls.Add( layoutPos );
			grpPos.Location = new Point( 3, 119 );
			grpPos.Name = "grpPos";
			grpPos.Padding = new Padding( 8 );
			grpPos.Size = new Size( 628, 110 );
			grpPos.TabIndex = 8;
			grpPos.TabStop = false;
			grpPos.Text = "Position";
			// 
			// layoutPos
			// 
			layoutPos.ColumnCount = 4;
			layoutPos.ColumnStyles.Add( new ColumnStyle( SizeType.Absolute, 80F ) );
			layoutPos.ColumnStyles.Add( new ColumnStyle( SizeType.Percent, 50F ) );
			layoutPos.ColumnStyles.Add( new ColumnStyle( SizeType.Absolute, 80F ) );
			layoutPos.ColumnStyles.Add( new ColumnStyle( SizeType.Percent, 50F ) );
			layoutPos.Controls.Add( lblX, 0, 0 );
			layoutPos.Controls.Add( numX, 1, 0 );
			layoutPos.Controls.Add( lblY, 0, 1 );
			layoutPos.Controls.Add( numY, 1, 1 );
			layoutPos.Controls.Add( lblZ, 2, 0 );
			layoutPos.Controls.Add( numZ, 3, 0 );
			layoutPos.Dock = DockStyle.Fill;
			layoutPos.Location = new Point( 8, 24 );
			layoutPos.Name = "layoutPos";
			layoutPos.RowCount = 2;
			layoutPos.RowStyles.Add( new RowStyle() );
			layoutPos.RowStyles.Add( new RowStyle() );
			layoutPos.Size = new Size( 612, 78 );
			layoutPos.TabIndex = 0;
			// 
			// lblX
			// 
			lblX.Anchor = AnchorStyles.Left;
			lblX.AutoSize = true;
			lblX.Location = new Point( 3, 7 );
			lblX.Name = "lblX";
			lblX.Size = new Size( 17, 15 );
			lblX.TabIndex = 0;
			lblX.Text = "X:";
			// 
			// numX
			// 
			numX.Anchor =  AnchorStyles.Left  |  AnchorStyles.Right ;
			numX.Location = new Point( 83, 3 );
			numX.Maximum = new decimal( new int[] { 32767, 0, 0, 0 } );
			numX.Minimum = new decimal( new int[] { 32768, 0, 0, int.MinValue } );
			numX.Name = "numX";
			numX.Size = new Size( 220, 23 );
			numX.TabIndex = 1;
			// 
			// lblY
			// 
			lblY.Anchor = AnchorStyles.Left;
			lblY.AutoSize = true;
			lblY.Location = new Point( 3, 46 );
			lblY.Name = "lblY";
			lblY.Size = new Size( 17, 15 );
			lblY.TabIndex = 2;
			lblY.Text = "Y:";
			// 
			// numY
			// 
			numY.Anchor =  AnchorStyles.Left  |  AnchorStyles.Right ;
			numY.Location = new Point( 83, 42 );
			numY.Maximum = new decimal( new int[] { 32767, 0, 0, 0 } );
			numY.Minimum = new decimal( new int[] { 32768, 0, 0, int.MinValue } );
			numY.Name = "numY";
			numY.Size = new Size( 220, 23 );
			numY.TabIndex = 3;
			// 
			// lblZ
			// 
			lblZ.Anchor = AnchorStyles.Left;
			lblZ.AutoSize = true;
			lblZ.Location = new Point( 309, 7 );
			lblZ.Name = "lblZ";
			lblZ.Size = new Size( 17, 15 );
			lblZ.TabIndex = 4;
			lblZ.Text = "Z:";
			// 
			// numZ
			// 
			numZ.Anchor =  AnchorStyles.Left  |  AnchorStyles.Right ;
			numZ.Location = new Point( 389, 3 );
			numZ.Maximum = new decimal( new int[] { 32767, 0, 0, 0 } );
			numZ.Minimum = new decimal( new int[] { 32768, 0, 0, int.MinValue } );
			numZ.Name = "numZ";
			numZ.Size = new Size( 220, 23 );
			numZ.TabIndex = 5;
			// 
			// grpMsgs
			// 
			grpMsgs.Anchor =   AnchorStyles.Top  |  AnchorStyles.Left   |  AnchorStyles.Right ;
			layoutRight.SetColumnSpan( grpMsgs, 2 );
			grpMsgs.Controls.Add( layoutMsgs );
			grpMsgs.Location = new Point( 3, 235 );
			grpMsgs.Name = "grpMsgs";
			grpMsgs.Padding = new Padding( 8 );
			grpMsgs.Size = new Size( 628, 100 );
			grpMsgs.TabIndex = 9;
			grpMsgs.TabStop = false;
			grpMsgs.Text = "Get Message IDs";
			// 
			// layoutMsgs
			// 
			layoutMsgs.ColumnCount = 2;
			layoutMsgs.ColumnStyles.Add( new ColumnStyle( SizeType.Absolute, 120F ) );
			layoutMsgs.ColumnStyles.Add( new ColumnStyle( SizeType.Percent, 100F ) );
			layoutMsgs.Controls.Add( lblMsg0, 0, 0 );
			layoutMsgs.Controls.Add( numMsg0, 1, 0 );
			layoutMsgs.Controls.Add( lblMsg1, 0, 1 );
			layoutMsgs.Controls.Add( numMsg1, 1, 1 );
			layoutMsgs.Dock = DockStyle.Fill;
			layoutMsgs.Location = new Point( 8, 24 );
			layoutMsgs.Name = "layoutMsgs";
			layoutMsgs.RowCount = 2;
			layoutMsgs.RowStyles.Add( new RowStyle() );
			layoutMsgs.RowStyles.Add( new RowStyle() );
			layoutMsgs.Size = new Size( 612, 68 );
			layoutMsgs.TabIndex = 0;
			// 
			// lblMsg0
			// 
			lblMsg0.Anchor = AnchorStyles.Left;
			lblMsg0.AutoSize = true;
			lblMsg0.Location = new Point( 3, 7 );
			lblMsg0.Name = "lblMsg0";
			lblMsg0.Size = new Size( 82, 15 );
			lblMsg0.TabIndex = 0;
			lblMsg0.Text = "get_msg0 (id):";
			// 
			// numMsg0
			// 
			numMsg0.Anchor =  AnchorStyles.Left  |  AnchorStyles.Right ;
			numMsg0.Location = new Point( 123, 3 );
			numMsg0.Maximum = new decimal( new int[] { 32767, 0, 0, 0 } );
			numMsg0.Minimum = new decimal( new int[] { 32768, 0, 0, int.MinValue } );
			numMsg0.Name = "numMsg0";
			numMsg0.Size = new Size( 486, 23 );
			numMsg0.TabIndex = 1;
			// 
			// lblMsg1
			// 
			lblMsg1.Anchor = AnchorStyles.Left;
			lblMsg1.AutoSize = true;
			lblMsg1.Location = new Point( 3, 41 );
			lblMsg1.Name = "lblMsg1";
			lblMsg1.Size = new Size( 82, 15 );
			lblMsg1.TabIndex = 2;
			lblMsg1.Text = "get_msg1 (id):";
			// 
			// numMsg1
			// 
			numMsg1.Anchor =  AnchorStyles.Left  |  AnchorStyles.Right ;
			numMsg1.Location = new Point( 123, 37 );
			numMsg1.Maximum = new decimal( new int[] { 32767, 0, 0, 0 } );
			numMsg1.Minimum = new decimal( new int[] { 32768, 0, 0, int.MinValue } );
			numMsg1.Name = "numMsg1";
			numMsg1.Size = new Size( 486, 23 );
			numMsg1.TabIndex = 3;
			// 
			// MapItemEditor
			// 
			AutoScaleDimensions = new SizeF( 7F, 15F );
			AutoScaleMode = AutoScaleMode.Font;
			Controls.Add( layoutMain );
			Name = "MapItemEditor";
			Size = new Size( 900, 560 );
			layoutMain.ResumeLayout( false );
			layoutLeft.ResumeLayout( false );
			layoutRight.ResumeLayout( false );
			layoutRight.PerformLayout();
			( (System.ComponentModel.ISupportInitialize)numItemNo ).EndInit();
			( (System.ComponentModel.ISupportInitialize)numStts ).EndInit();
			( (System.ComponentModel.ISupportInitialize)numRoom ).EndInit();
			( (System.ComponentModel.ISupportInitialize)numMission ).EndInit();
			grpPos.ResumeLayout( false );
			layoutPos.ResumeLayout( false );
			layoutPos.PerformLayout();
			( (System.ComponentModel.ISupportInitialize)numX ).EndInit();
			( (System.ComponentModel.ISupportInitialize)numY ).EndInit();
			( (System.ComponentModel.ISupportInitialize)numZ ).EndInit();
			grpMsgs.ResumeLayout( false );
			layoutMsgs.ResumeLayout( false );
			layoutMsgs.PerformLayout();
			( (System.ComponentModel.ISupportInitialize)numMsg0 ).EndInit();
			( (System.ComponentModel.ISupportInitialize)numMsg1 ).EndInit();
			ResumeLayout( false );
		}
	}
}
