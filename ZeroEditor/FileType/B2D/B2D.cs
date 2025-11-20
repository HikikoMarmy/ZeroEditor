using System;
using System.Collections.Generic;

namespace ZeroEditor.Zero3.Editors.B2D
{
	public sealed class B2dFile
	{
		public IReadOnlyList<string> StringTable => _strings;
		public Dictionary<int, string> ResourceById { get; } = new();
		public Dictionary<int, int> RawToDense { get; } = new();
		public List<int> DenseToRaw { get; } = new();
		public List<B2dSprite> SpritesInOrder { get; } = new();
		public uint StringTableSize;
		public uint StringTableOffset;
		public uint Misc;
		internal List<string> _strings = new();
		internal Dictionary<string, int> _stringToOffset = new( StringComparer.Ordinal );
		internal List<uint> _u32 = new();
		internal Dictionary<int, int> _op2Starts = new();
		internal Dictionary<int, int> _op11Starts = new();
		internal Dictionary<int, bool> _op2Swap = new();
		internal Dictionary<int, uint> _op2Flag = new();
		internal Dictionary<int, bool> _op11NameAt8 = new();
		public int GetOrAddString( string s )
		{
			if( _stringToOffset.TryGetValue( s, out var off ) )
				return off;
			_strings.Add( s );
			_stringToOffset[ s ] = -1;
			return -1;
		}
	}
}
