using System.Drawing;
using System.Windows.Forms;

namespace ZeroEditor.Common
{
	partial class Tim2Editor
	{
		private System.ComponentModel.IContainer components = null;
		protected override void Dispose( bool disposing )
		{
			if( disposing && ( components != null ) )
				components.Dispose();
			base.Dispose( disposing );
		}

		private void InitializeComponent()
		{
			splitContainer1 = new SplitContainer();
			pictureBox = new PictureBox();
			rightPanel = new Panel();
			lblInfo = new Label();
			chkHalfAlpha = new CheckBox();
			btnExport = new Button();
			btnImport = new Button();
			( (System.ComponentModel.ISupportInitialize)splitContainer1 ).BeginInit();
			splitContainer1.Panel1.SuspendLayout();
			splitContainer1.Panel2.SuspendLayout();
			splitContainer1.SuspendLayout();
			( (System.ComponentModel.ISupportInitialize)pictureBox ).BeginInit();
			rightPanel.SuspendLayout();
			SuspendLayout();
			// 
			// splitContainer1
			// 
			splitContainer1.Dock = DockStyle.Fill;
			splitContainer1.Location = new Point( 0, 0 );
			splitContainer1.Name = "splitContainer1";
			// 
			// splitContainer1.Panel1
			// 
			splitContainer1.Panel1.Controls.Add( pictureBox );
			// 
			// splitContainer1.Panel2
			// 
			splitContainer1.Panel2.Controls.Add( rightPanel );
			splitContainer1.Size = new Size( 820, 540 );
			splitContainer1.SplitterDistance = 620;
			splitContainer1.TabIndex = 0;
			// 
			// pictureBox
			// 
			pictureBox.Dock = DockStyle.Fill;
			pictureBox.Location = new Point( 0, 0 );
			pictureBox.Name = "pictureBox";
			pictureBox.Size = new Size( 620, 540 );
			pictureBox.TabIndex = 0;
			pictureBox.TabStop = false;
			// 
			// rightPanel
			// 
			rightPanel.Controls.Add( lblInfo );
			rightPanel.Controls.Add( chkHalfAlpha );
			rightPanel.Controls.Add( btnExport );
			rightPanel.Controls.Add( btnImport );
			rightPanel.Dock = DockStyle.Fill;
			rightPanel.Location = new Point( 0, 0 );
			rightPanel.Name = "rightPanel";
			rightPanel.Padding = new Padding( 10 );
			rightPanel.Size = new Size( 196, 540 );
			rightPanel.TabIndex = 0;
			// 
			// lblInfo
			// 
			lblInfo.Anchor =   AnchorStyles.Top  |  AnchorStyles.Left   |  AnchorStyles.Right ;
			lblInfo.Location = new Point( 10, 128 );
			lblInfo.Name = "lblInfo";
			lblInfo.Size = new Size( 176, 390 );
			lblInfo.TabIndex = 3;
			// 
			// chkHalfAlpha
			// 
			chkHalfAlpha.Anchor =   AnchorStyles.Top  |  AnchorStyles.Left   |  AnchorStyles.Right ;
			chkHalfAlpha.Checked = true;
			chkHalfAlpha.CheckState = CheckState.Checked;
			chkHalfAlpha.Location = new Point( 10, 10 );
			chkHalfAlpha.Name = "chkHalfAlpha";
			chkHalfAlpha.Size = new Size( 176, 24 );
			chkHalfAlpha.TabIndex = 0;
			chkHalfAlpha.Text = "Half alpha";
			chkHalfAlpha.UseVisualStyleBackColor = true;
			// 
			// btnExport
			// 
			btnExport.Anchor =   AnchorStyles.Top  |  AnchorStyles.Left   |  AnchorStyles.Right ;
			btnExport.Location = new Point( 10, 86 );
			btnExport.Name = "btnExport";
			btnExport.Size = new Size( 176, 28 );
			btnExport.TabIndex = 2;
			btnExport.Text = "Export PNG";
			btnExport.UseVisualStyleBackColor = true;
			// 
			// btnImport
			// 
			btnImport.Anchor =   AnchorStyles.Top  |  AnchorStyles.Left   |  AnchorStyles.Right ;
			btnImport.Location = new Point( 10, 50 );
			btnImport.Name = "btnImport";
			btnImport.Size = new Size( 176, 28 );
			btnImport.TabIndex = 1;
			btnImport.Text = "Import PNG";
			btnImport.UseVisualStyleBackColor = true;
			// 
			// Tim2Editor
			// 
			AutoScaleDimensions = new SizeF( 7F, 15F );
			AutoScaleMode = AutoScaleMode.Font;
			Controls.Add( splitContainer1 );
			Name = "Tim2Editor";
			Size = new Size( 820, 540 );
			splitContainer1.Panel1.ResumeLayout( false );
			splitContainer1.Panel2.ResumeLayout( false );
			( (System.ComponentModel.ISupportInitialize)splitContainer1 ).EndInit();
			splitContainer1.ResumeLayout( false );
			( (System.ComponentModel.ISupportInitialize)pictureBox ).EndInit();
			rightPanel.ResumeLayout( false );
			ResumeLayout( false );
		}

		private SplitContainer splitContainer1;
		private PictureBox pictureBox;
		private Panel rightPanel;
		private CheckBox chkHalfAlpha;
		private Button btnImport;
		private Button btnExport;
		private Label lblInfo;
	}
}
