using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ZeroEditor.Zero2.Editors;

namespace ZeroEditor.Zero2.Editors
{
	public sealed class Zero2EventEditor : UserControl
	{
		private readonly SplitContainer split;
		private readonly ListBox lstEvents;
		private readonly DataGridView grid;
		private readonly ToolStrip tool;
		private readonly ToolStripLabel lblInfo;
		private readonly ToolStripButton btnSaveEvent;
		private readonly ToolStripButton btnReloadEvent;
		private readonly ToolStripButton btnExportCsv;

		private byte[]? _fileBytes;
		private string? _filePath;
		private Dictionary<byte, OpcodeDef> _opMap = EVTable_Zero2.Build();

		private const int SUB_EVENT_OPEN_TBL = 0;
		private const int EV_DAT_INIT_TBL = 1;
		private const int EV_DAT_MAIN_TBL = 2;        
		private const int EV_DAT_IS_DESTRCT_TBL = 3;
		private const int EV_DAT_DESTRCT_TBL = 4;
		private const int EV_DAT_PARENT_TBL = 5;
		private const int EV_DAT_SUBEVENT_TBL_TBL = 6;

		private const byte OPCODE_END = 0xFF;

		private int[] _tblStart = new int[ 7 ];
		private int[] _tblSize = new int[ 7 ];
		private int[] _tblCount = new int[ 7 ];

		private sealed class Insn
		{
			public int Offset; 
			public byte Opcode;
			public string Label = "";
			public int Size;            
			public byte[] Args = Array.Empty<byte>(); 
			public string ArgsHex => BitConverter.ToString( Args ).Replace( "-", " " ).ToUpperInvariant();
		}

		private sealed class EventEntry
		{
			public int Index;  
			public int Offset; 
			public List<Insn> Insns = new();
			public override string ToString() => $"{Index:D5}  @0x{Offset:X}";
		}

		private readonly BindingList<EventEntry> _events = new();

		public Zero2EventEditor()
		{
			ThemeManager.ApplyDark( this );

			split = new SplitContainer
			{
				Dock = DockStyle.Fill,
				Orientation = Orientation.Vertical,
				FixedPanel = FixedPanel.Panel1,
				SplitterDistance = 220,
			};

			lstEvents = new ListBox
			{
				Dock = DockStyle.Fill,
				DrawMode = DrawMode.OwnerDrawFixed,
				ItemHeight = 18
			};
			lstEvents.DrawItem += LstEvents_DrawItem;
			lstEvents.SelectedIndexChanged += LstEvents_SelectedIndexChanged;

			grid = new DataGridView
			{
				Dock = DockStyle.Fill,
				AllowUserToAddRows = false,
				AllowUserToDeleteRows = false,
				AllowUserToResizeRows = false,
				RowHeadersVisible = false,
				AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
				SelectionMode = DataGridViewSelectionMode.FullRowSelect,
				MultiSelect = false,
				ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText,
				BorderStyle = BorderStyle.None,
				BackgroundColor = Color.DarkGray,
				ForeColor = Color.White
			};

			grid.Columns.Add( new DataGridViewTextBoxColumn { Name = "colIdx", HeaderText = "#", Width = 48, ReadOnly = true, FillWeight = 30 } );
			grid.Columns.Add( new DataGridViewTextBoxColumn { Name = "colOff", HeaderText = "Offset", Width = 90, ReadOnly = true, FillWeight = 50 } );
			grid.Columns.Add( new DataGridViewTextBoxColumn { Name = "colOp", HeaderText = "Op", Width = 60, ReadOnly = true, FillWeight = 45 } );
			grid.Columns.Add( new DataGridViewTextBoxColumn { Name = "colLabel", HeaderText = "Label", ReadOnly = true, FillWeight = 160 } );
			grid.Columns.Add( new DataGridViewTextBoxColumn { Name = "colSize", HeaderText = "Size", Width = 50, ReadOnly = true, FillWeight = 40 } );
			grid.Columns.Add( new DataGridViewTextBoxColumn { Name = "colArgs", HeaderText = "Args (hex)", ReadOnly = false, FillWeight = 180 } );

			grid.CellValidating += Grid_CellValidating;
			grid.CellEndEdit += Grid_CellEndEdit;

			tool = new ToolStrip
			{
				GripStyle = ToolStripGripStyle.Hidden,
				Dock = DockStyle.Top
			};
			btnSaveEvent = new ToolStripButton( "Save event" ) { DisplayStyle = ToolStripItemDisplayStyle.Text };
			btnReloadEvent = new ToolStripButton( "Reload event" ) { DisplayStyle = ToolStripItemDisplayStyle.Text };
			btnExportCsv = new ToolStripButton( "Export CSV" ) { DisplayStyle = ToolStripItemDisplayStyle.Text };
			lblInfo = new ToolStripLabel( "—" );

			btnSaveEvent.Click += BtnSaveEvent_Click;
			btnReloadEvent.Click += BtnReloadEvent_Click;
			btnExportCsv.Click += BtnExportCsv_Click;

			tool.Items.Add( btnSaveEvent );
			tool.Items.Add( btnReloadEvent );
			tool.Items.Add( new ToolStripSeparator() );
			tool.Items.Add( btnExportCsv );
			tool.Items.Add( new ToolStripSeparator() );
			tool.Items.Add( lblInfo );

			split.Panel1.Controls.Add( lstEvents );
			split.Panel2.Controls.Add( grid );
			split.Panel2.Controls.Add( tool );

			Controls.Add( split );
		}

		public void LoadEventFile( string path )
		{
			_filePath = path ?? throw new ArgumentNullException( nameof( path ) );
			_fileBytes = File.ReadAllBytes( _filePath );
			ParseTopTables();
			LoadEventList();
			lblInfo.Text = $"Loaded: {Path.GetFileName( _filePath )} | Events: {_events.Count}";
		}

		private void LstEvents_DrawItem( object? sender, DrawItemEventArgs e )
		{
			e.DrawBackground();
			if( e.Index >= 0 && e.Index < _events.Count )
			{
				var it = _events[ e.Index ];
				var text = it.ToString();
				using var br = new SolidBrush( ( e.State & DrawItemState.Selected ) != 0 ? SystemColors.HighlightText : e.ForeColor );
				e.Graphics.DrawString( text, e.Font!, br, e.Bounds );
			}
			e.DrawFocusRectangle();
		}

		private void LstEvents_SelectedIndexChanged( object? sender, EventArgs e )
		{
			try
			{
				if( lstEvents.SelectedItem is EventEntry ev )
				{
					if( ev.Insns.Count == 0 )
						DecodeEvent( ev );

					PopulateGrid( ev );
				}
			}
			catch( Exception ex )
			{
				MessageBox.Show( this, ex.Message, "Decode error" );
			}
		}

		private void BtnReloadEvent_Click( object? sender, EventArgs e )
		{
			if( _filePath == null )
				return;
			if( lstEvents.SelectedItem is not EventEntry ev )
				return;

			_fileBytes = File.ReadAllBytes( _filePath );
			ParseTopTables();
			ev.Insns.Clear();
			DecodeEvent( ev );
			PopulateGrid( ev );
		}

		private void BtnSaveEvent_Click( object? sender, EventArgs e )
		{
			if( _filePath == null || _fileBytes == null )
				return;
			if( lstEvents.SelectedItem is not EventEntry ev )
				return;

			try
			{
				var rebuilt = RebuildEventBytesFromGrid( ev );
				if( rebuilt == null )
					return;

				if( rebuilt.Length != ( ev.Insns.Sum( i => i.Size ) ) )
					throw new InvalidOperationException( "Internal size mismatch; aborting write." );

				int pos = ev.Offset;
				foreach( var ins in ev.Insns )
				{
					if( pos + ins.Size > _fileBytes.Length )
						throw new InvalidOperationException( "Write would exceed file size." );

					_fileBytes[ pos ] = ins.Opcode;
					Buffer.BlockCopy( ins.Args, 0, _fileBytes, pos + 1, ins.Size - 1 );
					pos += ins.Size;
				}

				File.WriteAllBytes( _filePath, _fileBytes );
				lblInfo.Text = $"Saved event {ev.Index} ({ev.Insns.Count} insns, {ev.Insns.Sum( i => i.Size )} bytes)";
			}
			catch( Exception ex )
			{
				MessageBox.Show( this, ex.Message, "Save error" );
			}
		}

		private void BtnExportCsv_Click( object? sender, EventArgs e )
		{
			if( lstEvents.SelectedItem is not EventEntry ev )
				return;

			var sb = new StringBuilder();
			sb.AppendLine( "Idx,Offset,Op,Label,Size,ArgsHex" );
			for( int i = 0; i < ev.Insns.Count; i++ )
			{
				var ins = ev.Insns[ i ];
				sb.AppendLine( $"{i},{ins.Offset:X},{ins.Opcode:X2},{ins.Label},{ins.Size},{ins.ArgsHex}" );
			}

			using var sfd = new SaveFileDialog
			{
				Title = "Export CSV",
				Filter = "CSV|*.csv|All files|*.*",
				FileName = $"event_{ev.Index:D5}.csv"
			};
			if( sfd.ShowDialog( this ) == DialogResult.OK )
			{
				File.WriteAllText( sfd.FileName, sb.ToString() );
				MessageBox.Show( this, "Exported.", "CSV" );
			}
		}

		private void Grid_CellValidating( object? sender, DataGridViewCellValidatingEventArgs e )
		{
			if( grid.Columns[ e.ColumnIndex ].Name != "colArgs" )
				return;

			if( lstEvents.SelectedItem is not EventEntry ev )
				return;
			var ins = ev.Insns[ grid.Rows[ e.RowIndex ].Index ];

			var text = ( e.FormattedValue ?? "" ).ToString() ?? "";
			if( !TryParseArgsHex( text, ins.Size - 1, out var parsed, out string? err ) )
			{
				grid.Rows[ e.RowIndex ].ErrorText = err ?? "Invalid hex";
				e.Cancel = true;
				return;
			}
			grid.Rows[ e.RowIndex ].ErrorText = string.Empty;
		}

		private void Grid_CellEndEdit( object? sender, DataGridViewCellEventArgs e )
		{
			if( e.RowIndex < 0 )
				return;
			if( grid.Columns[ e.ColumnIndex ].Name != "colArgs" )
				return;

			if( lstEvents.SelectedItem is not EventEntry ev )
				return;
			var ins = ev.Insns[ e.RowIndex ];

			var cell = grid.Rows[ e.RowIndex ].Cells[ e.ColumnIndex ];
			var text = ( cell.Value ?? "" ).ToString() ?? "";
			if( TryParseArgsHex( text, ins.Size - 1, out var parsed, out _ ) )
			{
				ins.Args = parsed;
			}
		}

		private void ParseTopTables()
		{
			if( _fileBytes == null )
				throw new InvalidOperationException( "No file loaded." );
			if( _fileBytes.Length < 7 * 4 )
				throw new InvalidDataException( "File too small to contain 7 table pointers." );

			var starts = new List<int>();
			for( int i = 0; i < 7; i++ )
			{
				int off = ReadU32( _fileBytes, i * 4 );
				_tblStart[ i ] = off;
				starts.Add( off );
			}

			var sorted = starts
				.Select( ( off, idx ) => (off, idx) )
				.Where( t => t.off >= 0 && t.off < _fileBytes.Length )
				.OrderBy( t => t.off )
				.ToList();

			var nextMap = new Dictionary<int, int>();
			for( int i = 0; i < sorted.Count; i++ )
			{
				int curOff = sorted[ i ].off;
				int nextOff = ( i + 1 < sorted.Count ) ? sorted[ i + 1 ].off : _fileBytes.Length;
				nextMap[ curOff ] = Math.Max( 0, Math.Min( _fileBytes.Length, nextOff ) - curOff );
			}

			for( int i = 0; i < 7; i++ )
			{
				int s = _tblStart[ i ];
				_tblSize[ i ] = nextMap.TryGetValue( s, out int sz ) ? sz : Math.Max( 0, _fileBytes.Length - s );
				_tblCount[ i ] = ( _tblSize[ i ] / 4 );
			}
		}

		private void LoadEventList()
		{
			_events.Clear();
			if( _fileBytes == null )
				return;

			int baseOff = _tblStart[ EV_DAT_MAIN_TBL ];
			int count = _tblCount[ EV_DAT_MAIN_TBL ];

			for( int i = 0; i < count; i++ )
			{
				int entryOff = baseOff + i * 4;
				if( entryOff + 4 > _fileBytes.Length )
					break;

				int streamOff = ReadU32( _fileBytes, entryOff );
				if( streamOff <= 0 || streamOff >= _fileBytes.Length )
					continue;

				_events.Add( new EventEntry
				{
					Index = i,
					Offset = streamOff
				} );
			}

			lstEvents.BeginUpdate();
			lstEvents.Items.Clear();
			foreach( var ev in _events )
				lstEvents.Items.Add( ev );
			lstEvents.EndUpdate();

			if( lstEvents.Items.Count > 0 )
				lstEvents.SelectedIndex = 0;
		}

		private void DecodeEvent( EventEntry ev )
		{
			if( _fileBytes == null )
				return;

			ev.Insns.Clear();

			int pos = ev.Offset;
			int safety = 0;

			while( pos < _fileBytes.Length && safety++ < 100000 )
			{
				byte op = _fileBytes[ pos ];
				if( op == OPCODE_END )
				{
					ev.Insns.Add( new Insn
					{
						Offset = pos,
						Opcode = op,
						Label = "END",
						Size = 1,
						Args = Array.Empty<byte>()
					} );
					break;
				}

				if( !_opMap.TryGetValue( op, out var def ) )
				{

					ev.Insns.Add( new Insn
					{
						Offset = pos,
						Opcode = op,
						Label = $"UNKNOWN_{op:X2}",
						Size = 1,
						Args = Array.Empty<byte>()
					} );
					pos += 1;
					continue;
				}

				int size = Math.Max( 1, def.Size );
				if( pos + size > _fileBytes.Length )
				{
					size = _fileBytes.Length - pos;
				}

				var ins = new Insn
				{
					Offset = pos,
					Opcode = op,
					Label = def.Label ?? $"OP_{op:X2}",
					Size = size,
					Args = ( size > 1 ) ? _fileBytes.Skip( pos + 1 ).Take( size - 1 ).ToArray() : Array.Empty<byte>()
				};
				ev.Insns.Add( ins );
				pos += size;
			}
		}

		private void PopulateGrid( EventEntry ev )
		{
			grid.SuspendLayout();
			grid.Rows.Clear();

			for( int i = 0; i < ev.Insns.Count; i++ )
			{
				var ins = ev.Insns[ i ];
				grid.Rows.Add(
					i.ToString( CultureInfo.InvariantCulture ),
					$"0x{ins.Offset:X}",
					$"{ins.Opcode:X2}",
					ins.Label,
					ins.Size.ToString( CultureInfo.InvariantCulture ),
					ins.ArgsHex
				);
			}

			grid.ResumeLayout();
			lblInfo.Text = $"Event {ev.Index} | {ev.Insns.Count} insns, {ev.Insns.Sum( x => x.Size )} bytes";
		}

		private byte[]? RebuildEventBytesFromGrid( EventEntry ev )
		{
			for( int r = 0; r < grid.Rows.Count; r++ )
			{
				var row = grid.Rows[ r ];
				string szStr = ( row.Cells[ "colSize" ].Value ?? "0" ).ToString() ?? "0";
				int size = int.TryParse( szStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s ) ? s : 0;

				string argsHex = ( row.Cells[ "colArgs" ].Value ?? "" ).ToString() ?? "";
				if( !TryParseArgsHex( argsHex, Math.Max( 0, size - 1 ), out var parsed, out string? err ) )
				{
					MessageBox.Show( this, $"Row {r}: {err}", "Rebuild error" );
					return null;
				}

				if( r < ev.Insns.Count )
					ev.Insns[ r ].Args = parsed;
			}

			var buf = new List<byte>( ev.Insns.Sum( i => i.Size ) );
			for( int i = 0; i < ev.Insns.Count; i++ )
			{
				var ins = ev.Insns[ i ];
				buf.Add( ins.Opcode );
				if( ins.Size > 1 )
					buf.AddRange( ins.Args );
			}

			return buf.ToArray();
		}

		private static int ReadU32( byte[] src, int offset )
		{
			if( offset < 0 || offset + 4 > src.Length )
				return 0;
			return src[ offset ]
				 | ( src[ offset + 1 ] << 8 )
				 | ( src[ offset + 2 ] << 16 )
				 | ( src[ offset + 3 ] << 24 );
		}

		private static bool TryParseArgsHex( string text, int expectedLen, out byte[] parsed, out string? error )
		{
			parsed = Array.Empty<byte>();
			error = null;

			var cleaned = text.Replace( ",", " " ).Replace( "-", " " ).Replace( "\t", " " )
							  .Trim();
			if( cleaned.Length == 0 )
			{
				if( expectedLen == 0 )
				{ parsed = Array.Empty<byte>(); return true; }
				error = $"Expecting {expectedLen} bytes, got 0.";
				return false;
			}

			var tokens = cleaned.Split( new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries );
			var bytes = new List<byte>( tokens.Length );

			foreach( var t in tokens )
			{
				if( t.Length > 2 )
				{ error = $"Bad token '{t}'"; return false; }
				if( !byte.TryParse( t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b ) )
				{ error = $"Bad hex '{t}'"; return false; }
				bytes.Add( b );
			}

			if( bytes.Count != expectedLen )
			{
				error = $"Args must be exactly {expectedLen} bytes (got {bytes.Count}).";
				return false;
			}

			parsed = bytes.ToArray();
			return true;
		}
	}
}
