namespace ZeroEditor.Zero1.Editors.Sgd
{
	partial class SgdEditor
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
			button1 = new Button();
			SuspendLayout();
			// 
			// button1
			// 
			button1.Location = new Point( 39, 28 );
			button1.Name = "button1";
			button1.Size = new Size( 75, 23 );
			button1.TabIndex = 0;
			button1.Text = "Export";
			button1.UseVisualStyleBackColor = true;
			button1.Click +=  button1_Click ;
			// 
			// SgdEditor
			// 
			AutoScaleDimensions = new SizeF( 7F, 15F );
			AutoScaleMode = AutoScaleMode.Font;
			Controls.Add( button1 );
			Name = "SgdEditor";
			Size = new Size( 568, 258 );
			ResumeLayout( false );
		}

		#endregion

		private Button button1;
	}
}
