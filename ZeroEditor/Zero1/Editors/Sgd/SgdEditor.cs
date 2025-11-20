using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ZeroEditor.Zero1.Editors.Sgd
{
	public partial class SgdEditor : UserControl
	{
		private string _path = "";

		public SgdEditor()
		{
			InitializeComponent();
		}

		public void LoadFile( string path )
		{
			_path = path;
		}

		private void button1_Click( object sender, EventArgs e )
		{
			if( string.IsNullOrEmpty( _path ) )
				return;
			using var sfd = new SaveFileDialog { Filter = "glTF Binary|*.glb", FileName = Path.GetFileNameWithoutExtension( _path ) + ".glb" };
			if( sfd.ShowDialog( this ) != DialogResult.OK )
				return;

			var model = SgdReader.Load( _path );
			SgdExporter.SaveGlb( model, sfd.FileName );
		}
	}
}
