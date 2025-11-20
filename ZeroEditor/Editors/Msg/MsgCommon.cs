using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Drawing;

namespace ZeroEditor.Editors
{
	public sealed class MsgPage
	{
		public int GroupIndex;
		public int MessageIndex;
		public int PageIndex;
		public byte[] Raw;
		public byte[] PreludeRaw;
		public string Text;
	}

	public sealed class MsgMessage
	{
		public int GroupIndex;
		public int MessageIndex;
		public List<MsgPage> Pages = new();
	}

	public sealed class MsgGroup
	{
		public int GroupIndex;
		public List<MsgMessage> Messages = new();
	}

	public interface IMsgDocument
	{
		IReadOnlyList<MsgGroup> Groups { get; }
	}

	public sealed class MsgDocument : IMsgDocument
	{
		public List<MsgGroup> MutableGroups { get; } = new();
		public IReadOnlyList<MsgGroup> Groups => MutableGroups;

		internal byte[] FF3_HeaderPrefix;
		internal uint[] FF3_HeaderU32;
		internal int[] FF3_OrigS;
		internal int FF3_OrigLen;
	}

	public sealed class HighlightRule
	{
		public Regex Pattern { get; init; }
		public Color? ForeColor { get; init; }
		public Color? BackColor { get; init; }
		public bool Bold { get; init; }
		public bool Italic { get; init; }

		public HighlightRule( Regex pattern, Color? fg = null, Color? bg = null, bool bold = false, bool italic = false )
		{
			Pattern = pattern ?? throw new ArgumentNullException( nameof( pattern ) );
			ForeColor = fg;
			BackColor = bg;
			Bold = bold;
			Italic = italic;
		}
	}

	public sealed class PaintSpan
	{
		public int Start { get; init; }
		public int Length { get; init; }
		public Color ForeColor { get; init; }
		public Color? BackColor { get; init; }
	}

	public interface IMsgHighlighter
	{
		IReadOnlyList<HighlightRule> GetRules() => Array.Empty<HighlightRule>();
		IReadOnlyList<PaintSpan> GetSpans( string text ) => Array.Empty<PaintSpan>();
	}

	public interface IMsgProfile
	{
		string Name { get; }
		IMsgCodec CreateCodec();
		IMsgDocument Parse( byte[] blob );
		byte[] Save( IMsgDocument doc );
		IMsgHighlighter? CreateHighlighter() => null;
	}

	public interface IMsgCodec
	{
		string DecodePage( byte[] pageBytes );
		byte[] EncodePage( string text );
	}
}
