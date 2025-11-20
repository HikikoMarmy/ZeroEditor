using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using ZeroEditor.Tim2;

namespace ZeroEditor.Zero3.Editors.B2D
{
	public interface ITextureResolver
	{
		Bitmap? Resolve( string textureName );
	}

	public sealed class FolderTextureResolver : ITextureResolver
	{
		readonly string _root;
		readonly System.Collections.Generic.Dictionary<string, Bitmap?> _cache = new( System.StringComparer.OrdinalIgnoreCase );
		public FolderTextureResolver( string root ) { _root = root; }
		public Bitmap? Resolve( string textureName )
		{
			if( _cache.TryGetValue( textureName, out var bmp ) )
				return bmp;
			string[] tryNames = new[] { textureName, textureName + ".tm2", textureName + ".cl2", textureName + ".TM2", textureName + ".CL2" };
			foreach( var n in tryNames )
			{
				var path = System.IO.Path.Combine( _root, n );
				if( !System.IO.File.Exists( path ) )
					continue;
				try
				{
					using var fs = System.IO.File.OpenRead( path );
					var img = Tim2Image.Load( fs );
					var bm = Tim2Decode.DecodeToBitmap( img, 0, true );
					_cache[ textureName ] = bm;
					return bm;
				}
				catch { }
			}
			_cache[ textureName ] = null;
			return null;
		}
	}

	public sealed partial class B2DEditor : UserControl
	{
		B2dFile? _model;
		string? _path;
		ITextureResolver? _resolver;
		readonly TreeView tv = new() { Dock = DockStyle.Left, Width = 260, HideSelection = false };
		readonly PropertyGrid pg = new() { Dock = DockStyle.Right, Width = 300 };
		readonly ToolStrip ts = new() { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
		readonly SpriteCanvas canvas = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
		readonly StatusStrip status = new();
		readonly ToolStripStatusLabel lbl = new() { Text = "" };

		public B2DEditor()
		{
			BuildUi();
		}

		void BuildUi()
		{
			var btnOpen = new ToolStripButton( "Open" ) { DisplayStyle = ToolStripItemDisplayStyle.Text };
			var btnSave = new ToolStripButton( "Save" ) { DisplayStyle = ToolStripItemDisplayStyle.Text };
			var btnReset = new ToolStripButton( "Reset View" ) { DisplayStyle = ToolStripItemDisplayStyle.Text };
			var btnZoomIn = new ToolStripButton( "+" ) { DisplayStyle = ToolStripItemDisplayStyle.Text };
			var btnZoomOut = new ToolStripButton( "-" ) { DisplayStyle = ToolStripItemDisplayStyle.Text };
			btnOpen.Click += ( s, e ) => { using var ofd = new OpenFileDialog { Filter = "B2D|*.b2d|All|*.*" }; if( ofd.ShowDialog( this ) == DialogResult.OK ) LoadFile( ofd.FileName ); };
			btnSave.Click += ( s, e ) => Save();
			btnReset.Click += ( s, e ) => canvas.Fit();
			btnZoomIn.Click += ( s, e ) => { canvas.Zoom *= 1.25f; canvas.Invalidate(); };
			btnZoomOut.Click += ( s, e ) => { canvas.Zoom /= 1.25f; canvas.Invalidate(); };
			ts.Items.AddRange( new ToolStripItem[] { btnOpen, btnSave, new ToolStripSeparator(), btnReset, btnZoomIn, btnZoomOut } );
			tv.AfterSelect += ( s, e ) => OnTreeSelect( e.Node?.Tag );
			canvas.SelectionChanged += ( s, e ) => SelectSpriteOnTree( canvas.SelectedSprite );
			canvas.MouseMove += ( s, e ) => { lbl.Text = $"Mouse: {e.Location}  Zoom: {canvas.Zoom:F2}"; };
			status.Items.Add( lbl );
			Controls.Add( canvas );
			Controls.Add( pg );
			Controls.Add( tv );
			Controls.Add( ts );
			Controls.Add( status );
		}

		public void LoadFile( string path )
		{
			_path = path;
			_model = B2dIO.Read( path );
			_resolver = new FolderTextureResolver( Path.GetDirectoryName( path )! );
			canvas.SetModel( _model, _resolver );
			RebuildTree();
		}

		public void Save()
		{
			if( _model == null || string.IsNullOrEmpty( _path ) )
				return;
			if( pg.SelectedObject is SpriteProxy sp )
				sp.Push();
			B2dIO.Write( _model, _path );
		}

		void RebuildTree()
		{
			tv.BeginUpdate();
			tv.Nodes.Clear();
			if( _model == null )
			{ tv.EndUpdate(); return; }
			var root = new TreeNode( Path.GetFileName( _path ) ) { Tag = null };
			var byPage = _model.SpritesInOrder.GroupBy( s => s.TextureId );
			foreach( var g in byPage.OrderBy( g => g.Key ) )
			{
				int raw = ( g.Key >= 0 && g.Key < _model.DenseToRaw.Count ) ? _model.DenseToRaw[ g.Key ] : -1;
				var texName = ( raw >= 0 && _model.ResourceById.TryGetValue( raw, out var nm ) ) ? nm : "(unbound)";
				var pNode = new TreeNode( $"[{g.Key}] {texName}" ) { Tag = g.Key };
				foreach( var sp in g )
				{
					var n = new TreeNode( sp.ToString() ) { Tag = sp };
					pNode.Nodes.Add( n );
				}
				root.Nodes.Add( pNode );
			}
			tv.Nodes.Add( root );
			root.Expand();
			tv.EndUpdate();
		}

		void OnTreeSelect( object? tag )
		{
			if( _model == null )
				return;
			if( tag is B2dSprite sp )
			{
				canvas.SelectedSprite = sp;
				pg.SelectedObject = new SpriteProxy( sp, _model, canvas );
			}
			else if( tag is int texId )
			{
				canvas.FilterTextureId = texId;
				pg.SelectedObject = null;
			}
			else
			{
				canvas.FilterTextureId = null;
				pg.SelectedObject = null;
			}
		}

		void SelectSpriteOnTree( B2dSprite? sp )
		{
			if( sp == null || tv.Nodes.Count == 0 )
				return;
			foreach( TreeNode pn in tv.Nodes[ 0 ].Nodes )
				foreach( TreeNode n in pn.Nodes )
					if( ReferenceEquals( n.Tag, sp ) )
					{ tv.SelectedNode = n; return; }
		}
	}

	public sealed class SpriteProxy
	{
		readonly B2dSprite _sp;
		readonly B2dFile _model;
		readonly SpriteCanvas _canvas;
		public SpriteProxy( B2dSprite sp, B2dFile model, SpriteCanvas canvas ) { _sp = sp; _model = model; _canvas = canvas; SyncFrom(); }
		void SyncFrom()
		{
			Name = _sp.Name;
			Material = _sp.Material;
			TextureId = _sp.TextureId;
			X = _sp.SrcRect.X;
			Y = _sp.SrcRect.Y;
			W = _sp.SrcRect.Width;
			H = _sp.SrcRect.Height;
		}
		public void Push()
		{
			_sp.Name = Name ?? string.Empty;
			_sp.Material = Material;
			_sp.TextureId = TextureId;
			if( TextureId >= 0 && TextureId < _model.DenseToRaw.Count )
				_sp.OriginalTextureId = _model.DenseToRaw[ TextureId ];
			_sp.SrcRect = new Rectangle( X, Y, W, H );
			_canvas.Invalidate();
		}
		public string? Name { get; set; }
		public int Material { get; set; }
		public int TextureId { get; set; }
		public int X { get; set; }
		public int Y { get; set; }
		public int W { get; set; }
		public int H { get; set; }
	}
}
