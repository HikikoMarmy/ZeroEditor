using System.Drawing;
using System.Windows.Forms;

namespace ZeroEditor.Editors.Elf.Zero1
{
	partial class FogEditor
	{
		private System.ComponentModel.IContainer components = null;
		private TableLayoutPanel layoutMain;
		private TableLayoutPanel layoutLeft;
		private ComboBox cmbSet;
		private ListBox lst;
		private TableLayoutPanel layoutRight;
		private Label lblFar;
		private NumericUpDown numFar;
		private Label lblNear;
		private NumericUpDown numNear;
		private Label lblMax;
		private NumericUpDown numMax;
		private Label lblMin;
		private NumericUpDown numMin;
		private Label lblR;
		private NumericUpDown numR;
		private Label lblG;
		private NumericUpDown numG;
		private Label lblB;
		private NumericUpDown numB;
		private Label lblA;
		private NumericUpDown numA;
		private Label lblColor;
		private Panel pnlColor;
		private Label lblRange;
		private ZeroEditor.Editors.Elf.Zero1.FogRangePanel pnlRange;

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
			cmbSet = new ComboBox();
			lst = new ListBox();
			layoutRight = new TableLayoutPanel();
			lblFar = new Label();
			numFar = new NumericUpDown();
			lblNear = new Label();
			numNear = new NumericUpDown();
			lblMax = new Label();
			numMax = new NumericUpDown();
			lblMin = new Label();
			numMin = new NumericUpDown();
			lblR = new Label();
			numR = new NumericUpDown();
			lblG = new Label();
			numG = new NumericUpDown();
			lblB = new Label();
			numB = new NumericUpDown();
			lblA = new Label();
			numA = new NumericUpDown();
			lblColor = new Label();
			pnlColor = new Panel();
			lblRange = new Label();
			pnlRange = new FogRangePanel();
			layoutMain.SuspendLayout();
			layoutLeft.SuspendLayout();
			layoutRight.SuspendLayout();
			( (System.ComponentModel.ISupportInitialize)numFar ).BeginInit();
			( (System.ComponentModel.ISupportInitialize)numNear ).BeginInit();
			( (System.ComponentModel.ISupportInitialize)numMax ).BeginInit();
			( (System.ComponentModel.ISupportInitialize)numMin ).BeginInit();
			( (System.ComponentModel.ISupportInitialize)numR ).BeginInit();
			( (System.ComponentModel.ISupportInitialize)numG ).BeginInit();
			( (System.ComponentModel.ISupportInitialize)numB ).BeginInit();
			( (System.ComponentModel.ISupportInitialize)numA ).BeginInit();
			SuspendLayout();
			// 
			// layoutMain
			// 
			layoutMain.ColumnCount = 2;
			layoutMain.ColumnStyles.Add( new ColumnStyle( SizeType.Absolute, 220F ) );
			layoutMain.ColumnStyles.Add( new ColumnStyle( SizeType.Percent, 100F ) );
			layoutMain.Controls.Add( layoutLeft, 0, 0 );
			layoutMain.Controls.Add( layoutRight, 1, 0 );
			layoutMain.Dock = DockStyle.Fill;
			layoutMain.Location = new Point( 0, 0 );
			layoutMain.Name = "layoutMain";
			layoutMain.RowCount = 1;
			layoutMain.RowStyles.Add( new RowStyle( SizeType.Percent, 100F ) );
			layoutMain.Size = new Size( 876, 548 );
			layoutMain.TabIndex = 0;
			// 
			// layoutLeft
			// 
			layoutLeft.ColumnCount = 1;
			layoutLeft.ColumnStyles.Add( new ColumnStyle( SizeType.Percent, 100F ) );
			layoutLeft.Controls.Add( cmbSet, 0, 0 );
			layoutLeft.Controls.Add( lst, 0, 1 );
			layoutLeft.Dock = DockStyle.Fill;
			layoutLeft.Location = new Point( 3, 3 );
			layoutLeft.Name = "layoutLeft";
			layoutLeft.RowCount = 2;
			layoutLeft.RowStyles.Add( new RowStyle() );
			layoutLeft.RowStyles.Add( new RowStyle( SizeType.Percent, 100F ) );
			layoutLeft.Size = new Size( 214, 542 );
			layoutLeft.TabIndex = 0;
			// 
			// cmbSet
			// 
			cmbSet.Dock = DockStyle.Top;
			cmbSet.DropDownStyle = ComboBoxStyle.DropDownList;
			cmbSet.FlatStyle = FlatStyle.Flat;
			cmbSet.FormattingEnabled = true;
			cmbSet.Items.AddRange( new object[] { "World Fog", "Camera Fog" } );
			cmbSet.Location = new Point( 3, 3 );
			cmbSet.Name = "cmbSet";
			cmbSet.Size = new Size( 208, 23 );
			cmbSet.TabIndex = 0;
			// 
			// lst
			// 
			lst.BorderStyle = BorderStyle.None;
			lst.Dock = DockStyle.Fill;
			lst.FormattingEnabled = true;
			lst.IntegralHeight = false;
			lst.ItemHeight = 15;
			lst.Location = new Point( 3, 32 );
			lst.Name = "lst";
			lst.Size = new Size( 208, 507 );
			lst.TabIndex = 1;
			// 
			// layoutRight
			// 
			layoutRight.ColumnCount = 2;
			layoutRight.ColumnStyles.Add( new ColumnStyle( SizeType.Absolute, 140F ) );
			layoutRight.ColumnStyles.Add( new ColumnStyle( SizeType.Percent, 100F ) );
			layoutRight.Controls.Add( lblFar, 0, 0 );
			layoutRight.Controls.Add( numFar, 1, 0 );
			layoutRight.Controls.Add( lblNear, 0, 1 );
			layoutRight.Controls.Add( numNear, 1, 1 );
			layoutRight.Controls.Add( lblMax, 0, 2 );
			layoutRight.Controls.Add( numMax, 1, 2 );
			layoutRight.Controls.Add( lblMin, 0, 3 );
			layoutRight.Controls.Add( numMin, 1, 3 );
			layoutRight.Controls.Add( lblR, 0, 4 );
			layoutRight.Controls.Add( numR, 1, 4 );
			layoutRight.Controls.Add( lblG, 0, 5 );
			layoutRight.Controls.Add( numG, 1, 5 );
			layoutRight.Controls.Add( lblB, 0, 6 );
			layoutRight.Controls.Add( numB, 1, 6 );
			layoutRight.Controls.Add( lblA, 0, 7 );
			layoutRight.Controls.Add( numA, 1, 7 );
			layoutRight.Controls.Add( lblColor, 0, 8 );
			layoutRight.Controls.Add( pnlColor, 1, 8 );
			layoutRight.Controls.Add( lblRange, 0, 9 );
			layoutRight.Controls.Add( pnlRange, 1, 9 );
			layoutRight.Dock = DockStyle.Fill;
			layoutRight.Location = new Point( 223, 3 );
			layoutRight.Name = "layoutRight";
			layoutRight.RowCount = 10;
			layoutRight.RowStyles.Add( new RowStyle() );
			layoutRight.RowStyles.Add( new RowStyle() );
			layoutRight.RowStyles.Add( new RowStyle() );
			layoutRight.RowStyles.Add( new RowStyle() );
			layoutRight.RowStyles.Add( new RowStyle() );
			layoutRight.RowStyles.Add( new RowStyle() );
			layoutRight.RowStyles.Add( new RowStyle() );
			layoutRight.RowStyles.Add( new RowStyle() );
			layoutRight.RowStyles.Add( new RowStyle() );
			layoutRight.RowStyles.Add( new RowStyle( SizeType.Percent, 100F ) );
			layoutRight.Size = new Size( 650, 542 );
			layoutRight.TabIndex = 1;
			// 
			// lblFar
			// 
			lblFar.Anchor = AnchorStyles.Left;
			lblFar.AutoSize = true;
			lblFar.Location = new Point( 3, 7 );
			lblFar.Name = "lblFar";
			lblFar.Size = new Size( 49, 15 );
			lblFar.TabIndex = 0;
			lblFar.Text = "Fog Far:";
			// 
			// numFar
			// 
			numFar.Anchor =  AnchorStyles.Left  |  AnchorStyles.Right ;
			numFar.DecimalPlaces = 3;
			numFar.Increment = new decimal( new int[] { 1, 0, 0, 131072 } );
			numFar.Location = new Point( 143, 3 );
			numFar.Maximum = new decimal( new int[] { 1000000, 0, 0, 0 } );
			numFar.Minimum = new decimal( new int[] { 1000000, 0, 0, int.MinValue } );
			numFar.Name = "numFar";
			numFar.Size = new Size( 504, 23 );
			numFar.TabIndex = 1;
			// 
			// lblNear
			// 
			lblNear.Anchor = AnchorStyles.Left;
			lblNear.AutoSize = true;
			lblNear.Location = new Point( 3, 36 );
			lblNear.Name = "lblNear";
			lblNear.Size = new Size( 58, 15 );
			lblNear.TabIndex = 2;
			lblNear.Text = "Fog Near:";
			// 
			// numNear
			// 
			numNear.Anchor =  AnchorStyles.Left  |  AnchorStyles.Right ;
			numNear.DecimalPlaces = 3;
			numNear.Increment = new decimal( new int[] { 1, 0, 0, 131072 } );
			numNear.Location = new Point( 143, 32 );
			numNear.Maximum = new decimal( new int[] { 1000000, 0, 0, 0 } );
			numNear.Minimum = new decimal( new int[] { 1000000, 0, 0, int.MinValue } );
			numNear.Name = "numNear";
			numNear.Size = new Size( 504, 23 );
			numNear.TabIndex = 3;
			// 
			// lblMax
			// 
			lblMax.Anchor = AnchorStyles.Left;
			lblMax.AutoSize = true;
			lblMax.Location = new Point( 3, 65 );
			lblMax.Name = "lblMax";
			lblMax.Size = new Size( 56, 15 );
			lblMax.TabIndex = 4;
			lblMax.Text = "Fog Max:";
			// 
			// numMax
			// 
			numMax.Anchor =  AnchorStyles.Left  |  AnchorStyles.Right ;
			numMax.DecimalPlaces = 3;
			numMax.Increment = new decimal( new int[] { 1, 0, 0, 131072 } );
			numMax.Location = new Point( 143, 61 );
			numMax.Maximum = new decimal( new int[] { 1000000, 0, 0, 0 } );
			numMax.Minimum = new decimal( new int[] { 1000000, 0, 0, int.MinValue } );
			numMax.Name = "numMax";
			numMax.Size = new Size( 504, 23 );
			numMax.TabIndex = 5;
			// 
			// lblMin
			// 
			lblMin.Anchor = AnchorStyles.Left;
			lblMin.AutoSize = true;
			lblMin.Location = new Point( 3, 94 );
			lblMin.Name = "lblMin";
			lblMin.Size = new Size( 54, 15 );
			lblMin.TabIndex = 6;
			lblMin.Text = "Fog Min:";
			// 
			// numMin
			// 
			numMin.Anchor =  AnchorStyles.Left  |  AnchorStyles.Right ;
			numMin.DecimalPlaces = 3;
			numMin.Increment = new decimal( new int[] { 1, 0, 0, 131072 } );
			numMin.Location = new Point( 143, 90 );
			numMin.Maximum = new decimal( new int[] { 1000000, 0, 0, 0 } );
			numMin.Minimum = new decimal( new int[] { 1000000, 0, 0, int.MinValue } );
			numMin.Name = "numMin";
			numMin.Size = new Size( 504, 23 );
			numMin.TabIndex = 7;
			// 
			// lblR
			// 
			lblR.Anchor = AnchorStyles.Left;
			lblR.AutoSize = true;
			lblR.Location = new Point( 3, 123 );
			lblR.Name = "lblR";
			lblR.Size = new Size( 17, 15 );
			lblR.TabIndex = 8;
			lblR.Text = "R:";
			// 
			// numR
			// 
			numR.Anchor =  AnchorStyles.Left  |  AnchorStyles.Right ;
			numR.Location = new Point( 143, 119 );
			numR.Maximum = new decimal( new int[] { 255, 0, 0, 0 } );
			numR.Name = "numR";
			numR.Size = new Size( 504, 23 );
			numR.TabIndex = 9;
			// 
			// lblG
			// 
			lblG.Anchor = AnchorStyles.Left;
			lblG.AutoSize = true;
			lblG.Location = new Point( 3, 152 );
			lblG.Name = "lblG";
			lblG.Size = new Size( 18, 15 );
			lblG.TabIndex = 10;
			lblG.Text = "G:";
			// 
			// numG
			// 
			numG.Anchor =  AnchorStyles.Left  |  AnchorStyles.Right ;
			numG.Location = new Point( 143, 148 );
			numG.Maximum = new decimal( new int[] { 255, 0, 0, 0 } );
			numG.Name = "numG";
			numG.Size = new Size( 504, 23 );
			numG.TabIndex = 11;
			// 
			// lblB
			// 
			lblB.Anchor = AnchorStyles.Left;
			lblB.AutoSize = true;
			lblB.Location = new Point( 3, 181 );
			lblB.Name = "lblB";
			lblB.Size = new Size( 17, 15 );
			lblB.TabIndex = 12;
			lblB.Text = "B:";
			// 
			// numB
			// 
			numB.Anchor =  AnchorStyles.Left  |  AnchorStyles.Right ;
			numB.Location = new Point( 143, 177 );
			numB.Maximum = new decimal( new int[] { 255, 0, 0, 0 } );
			numB.Name = "numB";
			numB.Size = new Size( 504, 23 );
			numB.TabIndex = 13;
			// 
			// lblA
			// 
			lblA.Anchor = AnchorStyles.Left;
			lblA.AutoSize = true;
			lblA.Location = new Point( 3, 210 );
			lblA.Name = "lblA";
			lblA.Size = new Size( 18, 15 );
			lblA.TabIndex = 14;
			lblA.Text = "A:";
			// 
			// numA
			// 
			numA.Anchor =  AnchorStyles.Left  |  AnchorStyles.Right ;
			numA.Location = new Point( 143, 206 );
			numA.Maximum = new decimal( new int[] { 255, 0, 0, 0 } );
			numA.Name = "numA";
			numA.Size = new Size( 504, 23 );
			numA.TabIndex = 15;
			// 
			// lblColor
			// 
			lblColor.Anchor = AnchorStyles.Left;
			lblColor.AutoSize = true;
			lblColor.Location = new Point( 3, 239 );
			lblColor.Name = "lblColor";
			lblColor.Size = new Size( 39, 15 );
			lblColor.TabIndex = 16;
			lblColor.Text = "Color:";
			// 
			// pnlColor
			// 
			pnlColor.Anchor =  AnchorStyles.Left  |  AnchorStyles.Right ;
			pnlColor.BorderStyle = BorderStyle.FixedSingle;
			pnlColor.Location = new Point( 143, 235 );
			pnlColor.Margin = new Padding( 3, 3, 160, 3 );
			pnlColor.Name = "pnlColor";
			pnlColor.Size = new Size( 347, 24 );
			pnlColor.TabIndex = 17;
			// 
			// lblRange
			// 
			lblRange.Anchor = AnchorStyles.Left;
			lblRange.AutoSize = true;
			lblRange.Location = new Point( 3, 394 );
			lblRange.Name = "lblRange";
			lblRange.Size = new Size( 87, 15 );
			lblRange.TabIndex = 18;
			lblRange.Text = "Range Preview:";
			// 
			// pnlRange
			// 
			pnlRange.BorderStyle = BorderStyle.FixedSingle;
			pnlRange.Dock = DockStyle.Fill;
			pnlRange.GetActive = null;
			pnlRange.Location = new Point( 143, 268 );
			pnlRange.Margin = new Padding( 3, 6, 3, 3 );
			pnlRange.Name = "pnlRange";
			pnlRange.Size = new Size( 504, 271 );
			pnlRange.TabIndex = 19;
			// 
			// FogEditor
			// 
			AutoScaleDimensions = new SizeF( 7F, 15F );
			AutoScaleMode = AutoScaleMode.Font;
			Controls.Add( layoutMain );
			Name = "FogEditor";
			Size = new Size( 876, 548 );
			layoutMain.ResumeLayout( false );
			layoutLeft.ResumeLayout( false );
			layoutRight.ResumeLayout( false );
			layoutRight.PerformLayout();
			( (System.ComponentModel.ISupportInitialize)numFar ).EndInit();
			( (System.ComponentModel.ISupportInitialize)numNear ).EndInit();
			( (System.ComponentModel.ISupportInitialize)numMax ).EndInit();
			( (System.ComponentModel.ISupportInitialize)numMin ).EndInit();
			( (System.ComponentModel.ISupportInitialize)numR ).EndInit();
			( (System.ComponentModel.ISupportInitialize)numG ).EndInit();
			( (System.ComponentModel.ISupportInitialize)numB ).EndInit();
			( (System.ComponentModel.ISupportInitialize)numA ).EndInit();
			ResumeLayout( false );
		}
	}
}
