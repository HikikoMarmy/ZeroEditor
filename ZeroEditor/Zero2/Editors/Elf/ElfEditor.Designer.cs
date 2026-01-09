using System.Drawing;
using System.Windows.Forms;

namespace ZeroEditor.Zero2.Editors
{
	partial class Zero2ElfEditor
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
			splitContainer1 = new SplitContainer();
			listBoxData = new ListBox();
			btnSaveChanges = new Button();
			( (System.ComponentModel.ISupportInitialize)splitContainer1 ).BeginInit();
			splitContainer1.Panel1.SuspendLayout();
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
			splitContainer1.Panel1.Padding = Padding.Empty;
			splitContainer1.Panel1.Controls.Add( listBoxData );
			splitContainer1.Panel1.Controls.Add( btnSaveChanges );
			// 
			// splitContainer1.Panel2
			// 
			splitContainer1.Panel2.Padding = Padding.Empty;
			splitContainer1.Size = new Size( 1177, 709 );
			splitContainer1.SplitterDistance = 162;
			splitContainer1.TabIndex = 0;
			// 
			// listBoxData
			// 
			listBoxData.Dock = DockStyle.Fill;
			listBoxData.FormattingEnabled = true;
			listBoxData.IntegralHeight = false;
			listBoxData.ItemHeight = 15;
			listBoxData.Location = new Point( 0, 0 );
			listBoxData.Name = "listBoxData";
			listBoxData.Size = new Size( 162, 673 );
			listBoxData.TabIndex = 1;
			listBoxData.SelectedIndexChanged += listBoxData_SelectedIndexChanged;
			// 
			// btnSaveChanges
			// 
			btnSaveChanges.Dock = DockStyle.Bottom;
			btnSaveChanges.Location = new Point( 0, 673 );
			btnSaveChanges.Name = "btnSaveChanges";
			btnSaveChanges.Size = new Size( 162, 36 );
			btnSaveChanges.TabIndex = 2;
			btnSaveChanges.Text = "Save Changes";
			btnSaveChanges.UseVisualStyleBackColor = true;
			btnSaveChanges.Click += btnSaveChanges_Click;
			// 
			// Zero1ElfEditor
			// 
			AutoScaleDimensions = new SizeF( 7F, 15F );
			AutoScaleMode = AutoScaleMode.Font;
			Controls.Add( splitContainer1 );
			Name = "Zero1ElfEditor";
			Size = new Size( 1177, 709 );
			splitContainer1.Panel1.ResumeLayout( false );
			( (System.ComponentModel.ISupportInitialize)splitContainer1 ).EndInit();
			splitContainer1.ResumeLayout( false );
			ResumeLayout( false );
		}

		private SplitContainer splitContainer1;
		private ListBox listBoxData;
		private Button btnSaveChanges;

		#endregion
	}
}
