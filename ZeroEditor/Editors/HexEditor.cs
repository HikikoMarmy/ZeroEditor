using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Be.Windows.Forms;

namespace ZeroEditor.Editors
{
	public partial class HexEditor : UserControl
	{
		private IByteProvider _provider;

		private volatile bool _shuttingDown = false;

		public HexEditor()
		{
			InitializeComponent();
			ThemeManager.ApplyDark( this );

			hexBox.StringViewVisible = true;
			hexBox.VScrollBarVisible = true;
			hexBox.GroupSeparatorVisible = true;
			hexBox.UseFixedBytesPerLine = true;
			hexBox.BytesPerLine = 16;

			if( dataInspector.Columns.Count == 0 )
			{
				dataInspector.AutoGenerateColumns = false;
				dataInspector.RowHeadersVisible = false;
				dataInspector.AllowUserToAddRows = false;
				dataInspector.AllowUserToDeleteRows = false;
				dataInspector.AllowUserToResizeRows = false;
				dataInspector.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
				dataInspector.MultiSelect = false;
				dataInspector.Columns.Add( new DataGridViewTextBoxColumn
				{
					Name = "colName",
					HeaderText = "Field",
					ReadOnly = true,
					AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
				} );
				dataInspector.Columns.Add( new DataGridViewTextBoxColumn
				{
					Name = "colValue",
					HeaderText = "Value",
					ReadOnly = true,
					AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
				} );
			}

			hexBox.SelectionStartChanged += OnSelChanged;
			hexBox.SelectionLengthChanged += OnSelChanged;
			hexBox.KeyUp += OnKeyMouse;
			hexBox.MouseUp += OnKeyMouse;

			Disposed += ( _, __ ) => SafeCloseFile();
		}

		public void PrepareForClose()
		{
			_shuttingDown = true;

			hexBox.SelectionStartChanged -= OnSelChanged;
			hexBox.SelectionLengthChanged -= OnSelChanged;
			hexBox.KeyUp -= OnKeyMouse;
			hexBox.MouseUp -= OnKeyMouse;

			try
			{ hexBox.ByteProvider = null; }
			catch { }

			if( _provider is IDisposable d )
			{ try { d.Dispose(); } catch { } }
			_provider = null;
		}

		private void OnSelChanged( object? s, EventArgs e )
		{
			if( _shuttingDown )
				return;
			UpdateInspector();
		}
		private void OnKeyMouse( object? s, EventArgs e )
		{
			if( _shuttingDown )
				return;
			UpdateInspector();
		}

		public void LoadStr( string path )
		{
			if( _shuttingDown )
				return;

			if( !IsHandleCreated )
				CreateControl();
			if( !hexBox.IsHandleCreated )
				hexBox.CreateControl();

			ApplyProvider( path );
		}

		private void ApplyProvider( string path )
		{
			if( hexBox.ByteProvider != null )
				hexBox.ByteProvider = null;

			if( _provider is IDisposable dPrev )
				dPrev.Dispose();

			_provider = new FileByteProvider( path );
			hexBox.ByteProvider = _provider;

			hexBox.SelectionStart = 0;
			hexBox.SelectionLength = 0;
			UpdateInspector();
		}

		private void SafeCloseFile()
		{
			if( hexBox != null && !hexBox.IsDisposed )
			{
				try
				{ hexBox.ByteProvider = null; }
				catch { /* ignore */ }
			}
			if( _provider is IDisposable d )
			{
				try
				{ d.Dispose(); }
				catch { }
				_provider = null;
			}
		}

		private void UpdateInspector()
		{
			if( _shuttingDown || IsDisposed || hexBox.IsDisposed )
				return;
			if( _provider == null )
			{ dataInspector.Rows.Clear(); return; }
			if( InvokeRequired )
				return;
			UpdateInspectorCore();
		}


		private void UpdateInspectorCore()
		{
			long start = Math.Max( 0, hexBox.SelectionStart );
			const int previewBytes = 16;
			var preview = ReadBytes( _provider, start, previewBytes );
			var head = ReadBytes( _provider, start, 4 );

			dataInspector.SuspendLayout();
			dataInspector.Rows.Clear();

			AddRow( "Offset", $"0x{start:X} ({start})" );
			AddRow( "Len (shown)", preview.Length.ToString() );
			AddRow( "Bytes", BitDump( preview, previewBytes ) );
			AddRow( "ASCII", AsciiPreview( preview ) );

			if( head.Length >= 1 )
			{
				byte b = head[ 0 ];
				AddRow( "Byte (u8)", b.ToString() );
				AddRow( "Byte (hex)", "0x" + b.ToString( "X2" ) );
			}
			else
			{
				AddRow( "Byte", "—" );
			}

			if( head.Length >= 2 )
			{
				short i16le = BitConverter.ToInt16( head, 0 );
				short i16be = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness( i16le );
				ushort u16le = BitConverter.ToUInt16( head, 0 );
				ushort u16be = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness( u16le );
				AddRow( "Int16 (LE)", i16le.ToString() );
				AddRow( "Int16 (BE)", i16be.ToString() );
				AddRow( "UInt16 (LE)", u16le.ToString() );
				AddRow( "UInt16 (BE)", u16be.ToString() );
			}
			else
			{
				AddRow( "Int16/UInt16", "—" );
			}

			if( head.Length >= 4 )
			{
				int i32le = BitConverter.ToInt32( head, 0 );
				int i32be = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness( i32le );
				uint u32le = BitConverter.ToUInt32( head, 0 );
				uint u32be = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness( u32le );

				float f32le = BitConverter.ToSingle( head, 0 );
				float f32be = BitConverter.Int32BitsToSingle(
					System.Buffers.Binary.BinaryPrimitives.ReverseEndianness( BitConverter.ToInt32( head, 0 ) ) );

				AddRow( "Int32 (LE)", i32le.ToString() );
				AddRow( "Int32 (BE)", i32be.ToString() );
				AddRow( "UInt32 (LE)", u32le.ToString() );
				AddRow( "UInt32 (BE)", u32be.ToString() );
				AddRow( "Float32 (LE)", f32le.ToString( "G9" ) );
				AddRow( "Float32 (BE)", f32be.ToString( "G9" ) );
			}
			else
			{
				AddRow( "Int32/UInt32/Float32", "—" );
			}

			dataInspector.ResumeLayout();
		}

		private static byte[] ReadBytes( IByteProvider provider, long start, int count )
		{
			var buf = new byte[ count ];
			int i = 0;
			for( ; i < count; i++ )
			{
				long idx = start + i;
				try
				{ buf[ i ] = provider.ReadByte( idx ); }
				catch { break; }
			}
			if( i == count )
				return buf;
			Array.Resize( ref buf, i );
			return buf;
		}

		private static string BitDump( byte[] data, int maxBytes )
		{
			int n = Math.Min( data.Length, maxBytes );
			var sb = new StringBuilder( n * 3 );
			for( int i = 0; i < n; i++ )
			{
				if( i > 0 )
					sb.Append( ' ' );
				sb.Append( data[ i ].ToString( "X2" ) );
			}
			if( data.Length > maxBytes )
				sb.Append( " …" );
			return sb.ToString();
		}

		private static string AsciiPreview( byte[] data )
		{
			var sb = new StringBuilder( data.Length );
			foreach( var b in data )
			{
				char c = (char)b;
				sb.Append( c >= 0x20 && c <= 0x7E ? c : '.' );
			}
			return sb.ToString();
		}

		private void AddRow( string name, string value ) => dataInspector.Rows.Add( name, value );
	}
}
