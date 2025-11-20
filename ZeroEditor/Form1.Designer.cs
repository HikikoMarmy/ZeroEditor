namespace ZeroEditor
{
    partial class Form1
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

		#region Windows Form Designer generated code

		private void InitializeComponent()
		{
			menuStrip1 = new MenuStrip();
			toolStripMenuItem1 = new ToolStripMenuItem();
			openToolStripMenuItem = new ToolStripMenuItem();
			exitToolStripMenuItem = new ToolStripMenuItem();
			iSOToolStripMenuItem = new ToolStripMenuItem();
			btnExtractISO = new ToolStripMenuItem();
			btnRebuildISO = new ToolStripMenuItem();
			GitHubToolStripMenuItem = new ToolStripMenuItem();
			splitContainer1 = new SplitContainer();
			fileTreeView = new TreeView();
			splitContainer2 = new SplitContainer();
			panelEditor = new Panel();
			txtLog = new TextBox();
			version = new ToolStripMenuItem();
			menuStrip1.SuspendLayout();
			( (System.ComponentModel.ISupportInitialize)splitContainer1 ).BeginInit();
			splitContainer1.Panel1.SuspendLayout();
			splitContainer1.Panel2.SuspendLayout();
			splitContainer1.SuspendLayout();
			( (System.ComponentModel.ISupportInitialize)splitContainer2 ).BeginInit();
			splitContainer2.Panel1.SuspendLayout();
			splitContainer2.Panel2.SuspendLayout();
			splitContainer2.SuspendLayout();
			SuspendLayout();
			// 
			// menuStrip1
			// 
			menuStrip1.Items.AddRange( new ToolStripItem[] { toolStripMenuItem1, iSOToolStripMenuItem, GitHubToolStripMenuItem, version } );
			menuStrip1.Location = new Point( 0, 0 );
			menuStrip1.Name = "menuStrip1";
			menuStrip1.Size = new Size( 1264, 24 );
			menuStrip1.TabIndex = 0;
			menuStrip1.Text = "menuStrip1";
			// 
			// toolStripMenuItem1
			// 
			toolStripMenuItem1.DropDownItems.AddRange( new ToolStripItem[] { openToolStripMenuItem, exitToolStripMenuItem } );
			toolStripMenuItem1.Name = "toolStripMenuItem1";
			toolStripMenuItem1.Size = new Size( 37, 20 );
			toolStripMenuItem1.Text = "File";
			// 
			// openToolStripMenuItem
			// 
			openToolStripMenuItem.Name = "openToolStripMenuItem";
			openToolStripMenuItem.Size = new Size( 139, 22 );
			openToolStripMenuItem.Text = "Open Folder";
			openToolStripMenuItem.Click +=  openToolStripMenuItem_Click ;
			// 
			// exitToolStripMenuItem
			// 
			exitToolStripMenuItem.Name = "exitToolStripMenuItem";
			exitToolStripMenuItem.Size = new Size( 139, 22 );
			exitToolStripMenuItem.Text = "Exit";
			exitToolStripMenuItem.Click +=  exitToolStripMenuItem_Click ;
			// 
			// iSOToolStripMenuItem
			// 
			iSOToolStripMenuItem.DropDownItems.AddRange( new ToolStripItem[] { btnExtractISO, btnRebuildISO } );
			iSOToolStripMenuItem.Name = "iSOToolStripMenuItem";
			iSOToolStripMenuItem.Size = new Size( 37, 20 );
			iSOToolStripMenuItem.Text = "ISO";
			// 
			// btnExtractISO
			// 
			btnExtractISO.Name = "btnExtractISO";
			btnExtractISO.Size = new Size( 135, 22 );
			btnExtractISO.Text = "Extract ISO";
			btnExtractISO.Click +=  btnExtractISO_Click ;
			// 
			// btnRebuildISO
			// 
			btnRebuildISO.Name = "btnRebuildISO";
			btnRebuildISO.Size = new Size( 135, 22 );
			btnRebuildISO.Text = "Rebuild ISO";
			btnRebuildISO.Click +=  btnRebuildISO_Click ;
			// 
			// GitHubToolStripMenuItem
			// 
			GitHubToolStripMenuItem.Alignment = ToolStripItemAlignment.Right;
			GitHubToolStripMenuItem.Name = "GitHubToolStripMenuItem";
			GitHubToolStripMenuItem.Size = new Size( 55, 20 );
			GitHubToolStripMenuItem.Text = "Github";
			GitHubToolStripMenuItem.Click +=  GitHubMenuItem_Click ;
			// 
			// splitContainer1
			// 
			splitContainer1.Dock = DockStyle.Fill;
			splitContainer1.Location = new Point( 0, 24 );
			splitContainer1.Name = "splitContainer1";
			// 
			// splitContainer1.Panel1
			// 
			splitContainer1.Panel1.Controls.Add( fileTreeView );
			// 
			// splitContainer1.Panel2
			// 
			splitContainer1.Panel2.Controls.Add( splitContainer2 );
			splitContainer1.Size = new Size( 1264, 705 );
			splitContainer1.SplitterDistance = 268;
			splitContainer1.TabIndex = 1;
			// 
			// fileTreeView
			// 
			fileTreeView.Dock = DockStyle.Fill;
			fileTreeView.Location = new Point( 0, 0 );
			fileTreeView.Name = "fileTreeView";
			fileTreeView.Size = new Size( 268, 705 );
			fileTreeView.TabIndex = 0;
			// 
			// splitContainer2
			// 
			splitContainer2.Dock = DockStyle.Fill;
			splitContainer2.Location = new Point( 0, 0 );
			splitContainer2.Name = "splitContainer2";
			splitContainer2.Orientation = Orientation.Horizontal;
			// 
			// splitContainer2.Panel1
			// 
			splitContainer2.Panel1.Controls.Add( panelEditor );
			// 
			// splitContainer2.Panel2
			// 
			splitContainer2.Panel2.Controls.Add( txtLog );
			splitContainer2.Size = new Size( 992, 705 );
			splitContainer2.SplitterDistance = 532;
			splitContainer2.TabIndex = 2;
			// 
			// panelEditor
			// 
			panelEditor.BackColor = SystemColors.ControlDark;
			panelEditor.Dock = DockStyle.Fill;
			panelEditor.Location = new Point( 0, 0 );
			panelEditor.Name = "panelEditor";
			panelEditor.Size = new Size( 992, 532 );
			panelEditor.TabIndex = 1;
			// 
			// txtLog
			// 
			txtLog.Dock = DockStyle.Fill;
			txtLog.Location = new Point( 0, 0 );
			txtLog.Multiline = true;
			txtLog.Name = "txtLog";
			txtLog.ReadOnly = true;
			txtLog.ScrollBars = ScrollBars.Both;
			txtLog.Size = new Size( 992, 169 );
			txtLog.TabIndex = 0;
			txtLog.WordWrap = false;
			// 
			// version
			// 
			version.Alignment = ToolStripItemAlignment.Right;
			version.Enabled = false;
			version.Name = "version";
			version.Size = new Size( 75, 20 );
			version.Text = "Version 0.0";
			// 
			// Form1
			// 
			AutoScaleDimensions = new SizeF( 7F, 15F );
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size( 1264, 729 );
			Controls.Add( splitContainer1 );
			Controls.Add( menuStrip1 );
			MainMenuStrip = menuStrip1;
			Name = "Form1";
			Text = "Zero Editor";
			menuStrip1.ResumeLayout( false );
			menuStrip1.PerformLayout();
			splitContainer1.Panel1.ResumeLayout( false );
			splitContainer1.Panel2.ResumeLayout( false );
			( (System.ComponentModel.ISupportInitialize)splitContainer1 ).EndInit();
			splitContainer1.ResumeLayout( false );
			splitContainer2.Panel1.ResumeLayout( false );
			splitContainer2.Panel2.ResumeLayout( false );
			splitContainer2.Panel2.PerformLayout();
			( (System.ComponentModel.ISupportInitialize)splitContainer2 ).EndInit();
			splitContainer2.ResumeLayout( false );
			ResumeLayout( false );
			PerformLayout();
		}

		#endregion

		private MenuStrip menuStrip1;
		private ToolStripMenuItem toolStripMenuItem1;
		private ToolStripMenuItem openToolStripMenuItem;
		private ToolStripMenuItem exitToolStripMenuItem;
		private ToolStripMenuItem iSOToolStripMenuItem;
		private ToolStripMenuItem btnExtractISO;
		private ToolStripMenuItem btnRebuildISO;
		private SplitContainer splitContainer1;
		private TreeView fileTreeView;
		private TextBox txtLog;
		private Panel panelEditor;
		private ToolStripMenuItem GitHubToolStripMenuItem;
		private SplitContainer splitContainer2;
		private ToolStripMenuItem version;
	}
}
