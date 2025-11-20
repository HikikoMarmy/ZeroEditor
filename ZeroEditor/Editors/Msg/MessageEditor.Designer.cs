using System.Windows.Forms;
using System.Drawing;

namespace ZeroEditor.Editors
{
	partial class MessageEditor
	{
		private System.ComponentModel.IContainer components = null;
		private SplitContainer splitContainer1;
		private RichTextBox richEdit;
		private TreeView treeView;

		protected override void Dispose( bool disposing )
		{
			if( disposing && ( components != null ) )
				components.Dispose();
			base.Dispose( disposing );
		}

		private void InitializeComponent()
		{
			splitContainer1 = new SplitContainer();
			btnSaveMsg = new Button();
			treeView = new TreeView();
			richEdit = new RichTextBox();
			( (System.ComponentModel.ISupportInitialize)splitContainer1 ).BeginInit();
			splitContainer1.Panel1.SuspendLayout();
			splitContainer1.Panel2.SuspendLayout();
			splitContainer1.SuspendLayout();
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
			splitContainer1.Panel1.Controls.Add( btnSaveMsg );
			splitContainer1.Panel1.Controls.Add( treeView );
			// 
			// splitContainer1.Panel2
			// 
			splitContainer1.Panel2.Controls.Add( richEdit );
			splitContainer1.Size = new Size( 900, 560 );
			splitContainer1.SplitterDistance = 260;
			splitContainer1.TabIndex = 0;
			// 
			// btnSaveMsg
			// 
			btnSaveMsg.Anchor =   AnchorStyles.Bottom  |  AnchorStyles.Left   |  AnchorStyles.Right ;
			btnSaveMsg.Location = new Point( 3, 531 );
			btnSaveMsg.Name = "btnSaveMsg";
			btnSaveMsg.Size = new Size( 254, 26 );
			btnSaveMsg.TabIndex = 1;
			btnSaveMsg.Text = "Save";
			btnSaveMsg.UseVisualStyleBackColor = true;
			btnSaveMsg.Click +=  this.btnSaveMsg_Click;
			// 
			// treeView
			// 
			treeView.Anchor =    AnchorStyles.Top  |  AnchorStyles.Bottom   |  AnchorStyles.Left   |  AnchorStyles.Right ;
			treeView.Location = new Point( 0, 0 );
			treeView.Name = "treeView";
			treeView.Size = new Size( 260, 525 );
			treeView.TabIndex = 0;
			treeView.AfterSelect +=  treeView_AfterSelect ;
			// 
			// richEdit
			// 
			richEdit.AcceptsTab = true;
			richEdit.BackColor = Color.FromArgb( 32, 32, 32 );
			richEdit.Dock = DockStyle.Fill;
			richEdit.Font = new Font( "Consolas", 14.25F, FontStyle.Regular, GraphicsUnit.Point, 0 );
			richEdit.Location = new Point( 0, 0 );
			richEdit.Name = "richEdit";
			richEdit.Size = new Size( 636, 560 );
			richEdit.TabIndex = 1;
			richEdit.Text = "";
			richEdit.WordWrap = false;
			richEdit.TextChanged +=  richEdit_TextChanged ;
			// 
			// MessageEditor
			// 
			AutoScaleDimensions = new SizeF( 7F, 15F );
			AutoScaleMode = AutoScaleMode.Font;
			Controls.Add( splitContainer1 );
			Name = "MessageEditor";
			Size = new Size( 900, 560 );
			splitContainer1.Panel1.ResumeLayout( false );
			splitContainer1.Panel2.ResumeLayout( false );
			( (System.ComponentModel.ISupportInitialize)splitContainer1 ).EndInit();
			splitContainer1.ResumeLayout( false );
			ResumeLayout( false );
		}

		private Button btnSaveMsg;
	}
}
