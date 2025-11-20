using System.Drawing;
using System.Windows.Forms;

namespace ZeroEditor.Editors
{
	partial class AudioEditor
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
			btnPlayStop = new Button();
			btnExport = new Button();
			btnImport = new Button();
			volumeBar = new TrackBar();
			trackAudio = new TrackBar();
			checkBoxRepeat = new CheckBox();
			lblTime = new Label();
			groupBox1 = new GroupBox();
			groupBox3 = new GroupBox();
			comboClips = new ComboBox();
			label1 = new Label();
			groupBox2 = new GroupBox();
			( (System.ComponentModel.ISupportInitialize)volumeBar ).BeginInit();
			( (System.ComponentModel.ISupportInitialize)trackAudio ).BeginInit();
			groupBox1.SuspendLayout();
			groupBox3.SuspendLayout();
			groupBox2.SuspendLayout();
			SuspendLayout();
			btnPlayStop.Location = new Point( 6, 22 );
			btnPlayStop.Name = "btnPlayStop";
			btnPlayStop.Size = new Size( 84, 51 );
			btnPlayStop.TabIndex = 0;
			btnPlayStop.Text = "PLAY";
			btnPlayStop.UseVisualStyleBackColor = true;
			btnPlayStop.Click += buttonPlay_Click;
			btnExport.Location = new Point( 6, 79 );
			btnExport.Name = "btnExport";
			btnExport.Size = new Size( 84, 28 );
			btnExport.TabIndex = 5;
			btnExport.Text = "Export";
			btnExport.UseVisualStyleBackColor = true;
			btnExport.Click += btnExport_Click;
			btnImport.Location = new Point( 6, 113 );
			btnImport.Name = "btnImport";
			btnImport.Size = new Size( 84, 28 );
			btnImport.TabIndex = 6;
			btnImport.Text = "Import";
			btnImport.UseVisualStyleBackColor = true;
			btnImport.Click += btnImport_Click;
			volumeBar.Dock = DockStyle.Fill;
			volumeBar.Location = new Point( 3, 19 );
			volumeBar.Name = "volumeBar";
			volumeBar.Orientation = Orientation.Vertical;
			volumeBar.Size = new Size( 40, 220 );
			volumeBar.TabIndex = 3;
			volumeBar.TickStyle = TickStyle.Both;
			volumeBar.Value = 6;
			trackAudio.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			trackAudio.Location = new Point( 96, 26 );
			trackAudio.Name = "trackAudio";
			trackAudio.Size = new Size( 517, 45 );
			trackAudio.TabIndex = 1;
			checkBoxRepeat.AutoSize = true;
			checkBoxRepeat.Font = new Font( "Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0 );
			checkBoxRepeat.Location = new Point( 6, 147 );
			checkBoxRepeat.Name = "checkBoxRepeat";
			checkBoxRepeat.Size = new Size( 57, 21 );
			checkBoxRepeat.TabIndex = 4;
			checkBoxRepeat.Text = "Loop";
			checkBoxRepeat.UseVisualStyleBackColor = true;
			checkBoxRepeat.CheckedChanged += checkBoxRepeat_CheckedChanged;
			lblTime.Font = new Font( "Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 0 );
			lblTime.Location = new Point( 6, 19 );
			lblTime.Name = "lblTime";
			lblTime.Size = new Size( 163, 20 );
			lblTime.TabIndex = 8;
			lblTime.Text = "00:00 / 00:00";
			lblTime.TextAlign = ContentAlignment.MiddleLeft;
			groupBox1.Controls.Add( groupBox3 );
			groupBox1.Controls.Add( groupBox2 );
			groupBox1.Controls.Add( btnPlayStop );
			groupBox1.Controls.Add( trackAudio );
			groupBox1.Controls.Add( checkBoxRepeat );
			groupBox1.Controls.Add( btnImport );
			groupBox1.Controls.Add( btnExport );
			groupBox1.Location = new Point( 6, 3 );
			groupBox1.Name = "groupBox1";
			groupBox1.Size = new Size( 671, 270 );
			groupBox1.TabIndex = 9;
			groupBox1.TabStop = false;
			groupBox3.Controls.Add( comboClips );
			groupBox3.Controls.Add( lblTime );
			groupBox3.Controls.Add( label1 );
			groupBox3.Location = new Point( 102, 77 );
			groupBox3.Name = "groupBox3";
			groupBox3.Size = new Size( 511, 184 );
			groupBox3.TabIndex = 11;
			groupBox3.TabStop = false;
			groupBox3.Text = "Information";
			comboClips.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			comboClips.DropDownStyle = ComboBoxStyle.DropDownList;
			comboClips.FormattingEnabled = true;
			comboClips.Location = new Point( 6, 42 );
			comboClips.Name = "comboClips";
			comboClips.Size = new Size( 499, 23 );
			comboClips.TabIndex = 9;
			comboClips.SelectedIndexChanged += ClipCombo_SelectedIndexChanged;
			label1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			label1.Font = new Font( "Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 0 );
			label1.Location = new Point( 6, 75 );
			label1.Name = "label1";
			label1.Size = new Size( 499, 106 );
			label1.TabIndex = 7;
			groupBox2.Controls.Add( volumeBar );
			groupBox2.Location = new Point( 619, 22 );
			groupBox2.Name = "groupBox2";
			groupBox2.Size = new Size( 46, 242 );
			groupBox2.TabIndex = 10;
			groupBox2.TabStop = false;
			groupBox2.Text = "Vol";
			AutoScaleDimensions = new SizeF( 7F, 15F );
			AutoScaleMode = AutoScaleMode.Font;
			BackColor = SystemColors.Control;
			Controls.Add( groupBox1 );
			Name = "AudioEditor";
			Size = new Size( 680, 276 );
			( (System.ComponentModel.ISupportInitialize)volumeBar ).EndInit();
			( (System.ComponentModel.ISupportInitialize)trackAudio ).EndInit();
			groupBox1.ResumeLayout( false );
			groupBox1.PerformLayout();
			groupBox3.ResumeLayout( false );
			groupBox2.ResumeLayout( false );
			groupBox2.PerformLayout();
			ResumeLayout( false );
		}

		private Button btnPlayStop;
		private Button btnExport;
		private Button btnImport;
		private TrackBar volumeBar;
		private TrackBar trackAudio;
		private CheckBox checkBoxRepeat;
		private Label lblTime;
		private GroupBox groupBox1;
		private Label label1;
		private GroupBox groupBox3;
		private GroupBox groupBox2;
		private ComboBox comboClips;
	}
}
