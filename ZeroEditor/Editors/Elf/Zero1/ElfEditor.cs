using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ZeroEditor.Editors.Elf.Zero1;
using ZeroEditor.Game;
using ZeroEditor.Zero1.Common;
using ZeroEditor.Zero1.DataType;
using ZeroEditor.Zero1.Editors.Elf;
using static BinUtil;

namespace ZeroEditor.Zero1.Editors
{
	public partial class Zero1ElfEditor : UserControl
	{
		private FileStream? _elfStream;
		private BinaryReader? _br;
		private BinaryWriter? _bw;

		private Segment[] _JibakuSegments = Array.Empty<Segment>();
		private readonly List<List<ENE_DAT>> _JibakuData = new();

		private List<FogParams> _fogParams = new();
		private List<FogRgb> _fogRgbs = new();
		private List<FogParams> _fogParamFinder = new();
		private List<FogRgb> _fogRgbFinder = new();
		private List<MapItemDat> _mapItems = new();

		private Segment _fogParamSeg;
		private Segment _fogRgbSeg;
		private Segment _fogParamFinderSeg;
		private Segment _fogRgbFinderSeg;
		private Segment _mapItemSeg;

		public Zero1ElfEditor()
		{
			InitializeComponent();
			ThemeManager.ApplyDark( this );
			this.Disposed += ( _, __ ) => DisposeElfIo();
		}

		public void LoadElf( string path, GameContext ctx )
		{
			DisposeElfIo();

			_elfStream = new FileStream( path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite );
			_br = new BinaryReader( _elfStream, Encoding.Default, leaveOpen: true );
			_bw = new BinaryWriter( _elfStream, Encoding.Default, leaveOpen: true );

			if( ctx is not IZero1ElfLayout elf )
				throw new NotSupportedException( $"Context {ctx.Key} lacks IZero1ElfLayout." );

			_JibakuSegments = elf.JibakuByNight ?? Array.Empty<Segment>();
			if( _JibakuSegments.Length == 0 )
				throw new NotSupportedException( $"No Jibaku segments for {ctx.Key.Region}." );

			_JibakuData.Clear();
			foreach( var seg in _JibakuSegments )
				_JibakuData.Add( BinUtil.ReadArrayAt<ENE_DAT>( _br!, seg ) );

			_fogParamSeg = elf.FogParam;
			_fogRgbSeg = elf.FogRgb;
			_fogParamFinderSeg = elf.FogParamFinder;
			_fogRgbFinderSeg = elf.FogRgbFinder;
			_mapItemSeg = elf.MapItemData;

			_fogParams = _fogParamSeg.Count > 0 ? BinUtil.ReadArrayAt<FogParams>( _br!, _fogParamSeg ) : new List<FogParams>();
			_fogRgbs = _fogRgbSeg.Count > 0 ? BinUtil.ReadArrayAt<FogRgb>( _br!, _fogRgbSeg ) : new List<FogRgb>();
			_fogParamFinder = _fogParamFinderSeg.Count > 0 ? BinUtil.ReadArrayAt<FogParams>( _br!, _fogParamFinderSeg ) : new List<FogParams>();
			_fogRgbFinder = _fogRgbFinderSeg.Count > 0 ? BinUtil.ReadArrayAt<FogRgb>( _br!, _fogRgbFinderSeg ) : new List<FogRgb>();
			_mapItems = _mapItemSeg.Count > 0 ? BinUtil.ReadArrayAt<MapItemDat>( _br!, _mapItemSeg ) : new List<MapItemDat>();

			if( _fogParams.Count == 0 || _fogRgbs.Count == 0 )
				Debug.WriteLine( "Warning: Fog tables were not found or empty." );
			if( _fogParams.Count != 0 && _fogParams.Count != _fogParamSeg.Count )
				Debug.WriteLine( $"Warning: FogParams count = {_fogParams.Count}, expected {_fogParamSeg.Count}." );
			if( _fogRgbs.Count != 0 && _fogRgbs.Count != _fogRgbSeg.Count )
				Debug.WriteLine( $"Warning: FogRgb count = {_fogRgbs.Count}, expected {_fogRgbSeg.Count}." );
			if( _fogParamFinder.Count != 0 && _fogParamFinder.Count != _fogParamFinderSeg.Count )
				Debug.WriteLine( $"Warning: FogParamFinder count = {_fogParamFinder.Count}, expected {_fogParamFinderSeg.Count}." );
			if( _fogRgbFinder.Count != 0 && _fogRgbFinder.Count != _fogRgbFinderSeg.Count )
				Debug.WriteLine( $"Warning: FogRgbFinder count = {_fogRgbFinder.Count}, expected {_fogRgbFinderSeg.Count}." );
			if( _mapItems.Count != 0 && _mapItems.Count != _mapItemSeg.Count )
				Debug.WriteLine( $"Warning: MapItemDat count = {_mapItems.Count}, expected {_mapItemSeg.Count}." );

			listBoxData.Items.Clear();
			listBoxData.Items.Add( "Jibaku Enemy Data" );
			listBoxData.Items.Add( "Fuyu Enemy Data" );
			listBoxData.Items.Add( "Auto Enemy Data" );
			listBoxData.Items.Add( "Fog Data" );
			listBoxData.Items.Add( "Map Item Data" );
			listBoxData.SelectedIndex = 0;
		}

		private void DisposeElfIo()
		{
			try
			{ _bw?.Flush(); }
			catch { }
			_bw?.Dispose();
			_bw = null;
			_br?.Dispose();
			_br = null;
			_elfStream?.Dispose();
			_elfStream = null;
		}

		private void listBoxData_SelectedIndexChanged( object sender, EventArgs e )
		{
			if( listBoxData.SelectedIndex == -1 )
				return;

			switch( listBoxData.SelectedItem?.ToString() )
			{
				case "Jibaku Enemy Data":
				{
					var jEditor = new JibakuEditor( _JibakuData ) { Dock = DockStyle.Fill };
					splitContainer1.Panel2.Controls.Clear();
					splitContainer1.Panel2.Controls.Add( jEditor );
				}
				break;

				case "Fog Data":
				{
					var fogEditor = new FogEditor( _fogParams, _fogRgbs, _fogParamFinder, _fogRgbFinder ) { Dock = DockStyle.Fill };
					splitContainer1.Panel2.Controls.Clear();
					splitContainer1.Panel2.Controls.Add( fogEditor );
				}
				break;

				case "Map Item Data":
				{
					var mapItemEditor = new MapItemEditor( _mapItems ) { Dock = DockStyle.Fill };
					splitContainer1.Panel2.Controls.Clear();
					splitContainer1.Panel2.Controls.Add( mapItemEditor );
				}
				break;
			}
		}

		private void btnSaveChanges_Click( object sender, EventArgs e )
		{
			try
			{
				SaveElf();
				MessageBox.Show( this, "Saved changes to ELF.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information );
			}
			catch( Exception ex )
			{
				MessageBox.Show( this, ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error );
			}
		}

		private void SaveElf()
		{
			if( _bw == null || _elfStream == null )
				throw new InvalidOperationException( "ELF not loaded." );

			if( _JibakuSegments.Length == _JibakuData.Count )
			{
				for( int i = 0; i < _JibakuSegments.Length; i++ )
					BinUtil.WriteArrayAt( _bw, _JibakuSegments[ i ], _JibakuData[ i ] );
			}

			if( _fogParamSeg.Count > 0 )
				AssertCountAndWrite( _bw, _fogParamSeg, _fogParams );
			if( _fogRgbSeg.Count > 0 )
				AssertCountAndWrite( _bw, _fogRgbSeg, _fogRgbs );
			if( _fogParamFinderSeg.Count > 0 )
				AssertCountAndWrite( _bw, _fogParamFinderSeg, _fogParamFinder );
			if( _fogRgbFinderSeg.Count > 0 )
				AssertCountAndWrite( _bw, _fogRgbFinderSeg, _fogRgbFinder );

			if( _mapItemSeg.Count > 0 )
				AssertCountAndWrite( _bw, _mapItemSeg, _mapItems );

			_bw.Flush();
			_elfStream.Flush( true );
		}

		private static void AssertCountAndWrite<T>( BinaryWriter bw, Segment seg, List<T> data ) where T : struct
		{
			if( data.Count != seg.Count )
				throw new InvalidOperationException( $"Count mismatch writing {typeof( T ).Name}: data.Count={data.Count}, segment.Count={seg.Count}" );
			BinUtil.WriteArrayAt( bw, seg, data );
		}
	}
}
