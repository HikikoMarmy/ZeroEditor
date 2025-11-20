namespace ZeroEditor.Editors
{
	partial class HexEditor
	{
		private System.ComponentModel.IContainer components = null;

		protected override void Dispose( bool disposing )
		{
			if( disposing && ( components != null ) )
			{
				components.Dispose();
			}
			base.Dispose( disposing );
		}

		#region Component Designer generated code

		private void InitializeComponent()
		{
			hexBox = new Be.Windows.Forms.HexBox();
			dataInspector = new DataGridView();
			splitContainer1 = new SplitContainer();
			( (System.ComponentModel.ISupportInitialize)dataInspector ).BeginInit();
			( (System.ComponentModel.ISupportInitialize)splitContainer1 ).BeginInit();
			splitContainer1.Panel1.SuspendLayout();
			splitContainer1.Panel2.SuspendLayout();
			splitContainer1.SuspendLayout();
			SuspendLayout();
			// 
			// hexBox
			// 
			hexBox.BackColor = Color.FromArgb( 64, 64, 64 );
			hexBox.Dock = DockStyle.Fill;
			hexBox.Font = new Font( "Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point, 0 );
			hexBox.ForeColor = SystemColors.ButtonHighlight;
			hexBox.Location = new Point( 0, 0 );
			hexBox.Name = "hexBox";
			hexBox.ShadowSelectionColor = Color.FromArgb( 100, 60, 188, 255 );
			hexBox.Size = new Size( 716, 587 );
			hexBox.TabIndex = 0;
			// 
			// dataInspector
			// 
			dataInspector.BackgroundColor = Color.FromArgb( 64, 64, 64 );
			dataInspector.BorderStyle = BorderStyle.None;
			dataInspector.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			dataInspector.Dock = DockStyle.Fill;
			dataInspector.Location = new Point( 0, 0 );
			dataInspector.Name = "dataInspector";
			dataInspector.Size = new Size( 285, 587 );
			dataInspector.TabIndex = 1;
			// 
			// splitContainer1
			// 
			splitContainer1.Dock = DockStyle.Fill;
			splitContainer1.Location = new Point( 0, 0 );
			splitContainer1.Name = "splitContainer1";
			// 
			// splitContainer1.Panel1
			// 
			splitContainer1.Panel1.Controls.Add( hexBox );
			splitContainer1.Panel1MinSize = 80;
			// 
			// splitContainer1.Panel2
			// 
			splitContainer1.Panel2.Controls.Add( dataInspector );
			splitContainer1.Size = new Size( 1005, 587 );
			splitContainer1.SplitterDistance = 716;
			splitContainer1.TabIndex = 2;
			// 
			// HexEditor
			// 
			AutoScaleDimensions = new SizeF( 7F, 15F );
			AutoScaleMode = AutoScaleMode.Font;
			Controls.Add( splitContainer1 );
			Name = "HexEditor";
			Size = new Size( 1005, 587 );
			( (System.ComponentModel.ISupportInitialize)dataInspector ).EndInit();
			splitContainer1.Panel1.ResumeLayout( false );
			splitContainer1.Panel2.ResumeLayout( false );
			( (System.ComponentModel.ISupportInitialize)splitContainer1 ).EndInit();
			splitContainer1.ResumeLayout( false );
			ResumeLayout( false );
		}

		#endregion

		private Be.Windows.Forms.HexBox hexBox;
		private DataGridView dataInspector;
		private SplitContainer splitContainer1;
	}
}
