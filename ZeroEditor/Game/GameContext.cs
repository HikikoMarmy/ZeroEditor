using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ZeroEditor.Game
{
	public enum GameId { FF1, FF2, FF3 }
	public enum GameRegion { PAL, NTSCU, NTSCJ, PROTO, Debug, Unknown }

	public abstract class GameContext
	{
		public GameKey Key { get; }
		public virtual string ElfName => Key.Serial;
		public IReadOnlyList<string> FileTable => _fileTable;
		public virtual PKVersion DefaultPkVersion => PKVersion.Unknown;
		public virtual PKVersion ResolveArchiveVersion( string filePath ) => DefaultPkVersion;
		public virtual bool IsPathEnabledForLocale( string normalizedPath ) => true;
		public IEnumerable<string> EnabledPaths() => FileTable.Where( IsPathEnabledForLocale );

		public virtual IPKArchive CreateArchiveHandler( string filePath, Stream stream )
		{
			var version = ResolveArchiveVersion( filePath );
			return version switch
			{
				PKVersion.PK2 => new PK2Archive( stream ),
				PKVersion.PK4 => new PK4Archive( stream ),
				_ => throw new NotSupportedException( $"No PK version mapping for {filePath} in {Key}" )
			};
		}

		private readonly List<string> _fileTable;

		protected GameContext( GameKey key, IEnumerable<string> fileTable )
		{
			Key = key;
			_fileTable = fileTable?.Distinct().ToList() ?? new List<string>();
		}
	}

	public readonly record struct GameKey( GameId Game, GameRegion Region, string Serial )
	{
		public override string ToString() => $"{Game}-{Region}-{Serial}";
	}

	public enum PKVersion { Unknown = 0, PK2 = 2, PK4 = 4 }

	public interface IPKArchive : IDisposable
	{
		PKVersion Version { get; }
		int Alignment { get; }
		string Endianness { get; }
		IReadOnlyList<PKEntry> Entries { get; }
		void ExtractAll( string outDir, ITim2PngBridge tim2Bridge );
		void RebuildFromFolder( string inDir, ITim2PngBridge tim2Bridge );
	}

	public sealed record PKEntry( string Name, long Offset, int Size, string Type );

	public abstract class PKArchiveBase : IPKArchive
	{
		private readonly Stream _stream;
		public PKVersion Version { get; protected set; }
		public int Alignment { get; protected set; } = 16;
		public string Endianness { get; protected set; } = "little";
		public List<PKEntry> MutableEntries { get; } = new();
		public IReadOnlyList<PKEntry> Entries => MutableEntries;

		protected PKArchiveBase( Stream stream )
		{
			_stream = stream;
		}

		public abstract void Parse();
		public abstract void ExtractAll( string outDir, ITim2PngBridge tim2Bridge );
		public abstract void RebuildFromFolder( string inDir, ITim2PngBridge tim2Bridge );
		public void Dispose() => _stream.Dispose();
	}

	public sealed class PK2Archive : PKArchiveBase
	{
		public PK2Archive( Stream s ) : base( s )
		{
			Version = PKVersion.PK2;
		}

		public override void Parse()
		{
		}

		public override void ExtractAll( string o, ITim2PngBridge t )
		{
		}

		public override void RebuildFromFolder( string i, ITim2PngBridge t )
		{
		}
	}

	public sealed class PK4Archive : PKArchiveBase
	{
		public PK4Archive( Stream s ) : base( s )
		{
			Version = PKVersion.PK4;
		}

		public override void Parse()
		{
		}

		public override void ExtractAll( string o, ITim2PngBridge t )
		{
		}

		public override void RebuildFromFolder( string i, ITim2PngBridge t )
		{
		}
	}

	public interface ITim2PngBridge
	{
		void ExtractTim2( Stream tim2Stream, string outDir );
		void RebuildTim2( string inDir, Stream outTim2Stream );
	}

	public static class GameContextFactory
	{
		private readonly record struct VariantKey( GameId Game, GameRegion Region, string Serial );

		private static readonly IReadOnlyDictionary<string, Func<GameContext>> BySerial =
			new Dictionary<string, Func<GameContext>>( StringComparer.OrdinalIgnoreCase )
			{
				[ "SLES_508.21" ] = () => new Game_Zero_1_EU(),
				[ "SLUS_203.88" ] = () => new Game_Zero_1_US(),
				[ "SLPS_250.74" ] = () => new Game_Zero_1_JP(),
				[ "SLES_523.84" ] = () => new Game_Zero_2_EU(),
				[ "SLUS_207.66" ] = () => new Game_Zero_2_US(),
				[ "SLPS_253.03" ] = () => new Game_Zero_2_JP(),
				[ "SLPS_999.99" ] = () => new Game_Zero_2_PROTO(),
				[ "SLES_538.25" ] = () => new Game_Zero_3_EU(),
				[ "SLPS_255.44" ] = () => new Game_Zero_3_PROTO_AUG(),
				[ "SLUS_212.44" ] = () => new Game_Zero_3_PROTO_SEP()
			};

		private static readonly Dictionary<VariantKey, Func<GameContext>> ByVariant =
			new Dictionary<VariantKey, Func<GameContext>>
			{
				[ new VariantKey( GameId.FF1, GameRegion.PAL,	"SLES_508.21" ) ] = () => new Game_Zero_1_EU(),
				[ new VariantKey( GameId.FF1, GameRegion.NTSCU, "SLUS_203.88" ) ] = () => new Game_Zero_1_US(),
				[ new VariantKey( GameId.FF1, GameRegion.NTSCJ, "SLPS_250.74" ) ] = () => new Game_Zero_1_JP(),
				[ new VariantKey( GameId.FF2, GameRegion.PAL,	"SLES_523.84" ) ] = () => new Game_Zero_2_EU(),
				[ new VariantKey( GameId.FF2, GameRegion.Debug, "SLES_523.84" ) ] = () => new Game_Zero_2_EU_DBG(),
				[ new VariantKey( GameId.FF2, GameRegion.NTSCU, "SLUS_207.66" ) ] = () => new Game_Zero_2_US(),
				[ new VariantKey( GameId.FF2, GameRegion.NTSCJ, "SLPS_253.03" ) ] = () => new Game_Zero_2_JP(),
				[ new VariantKey( GameId.FF2, GameRegion.PROTO, "SLPS_999.99" ) ] = () => new Game_Zero_2_PROTO(),
				[ new VariantKey( GameId.FF3, GameRegion.PAL,	"SLES_538.25" ) ] = () => new Game_Zero_3_EU(),
				[ new VariantKey( GameId.FF3, GameRegion.PROTO, "SLPS_255.44" ) ] = () => new Game_Zero_3_PROTO_AUG(),
				[ new VariantKey( GameId.FF3, GameRegion.PROTO, "SLUS_212.44" ) ] = () => new Game_Zero_3_PROTO_SEP()
			};

		public static GameContext FromSerial( string serial )
		{
			if( serial is null )
				throw new ArgumentNullException( nameof( serial ) );
			if( BySerial.TryGetValue( serial, out var ctor ) )
				return ctor();
			throw new NotSupportedException( $"Unsupported serial: {serial}" );
		}

		public static GameContext FromGameKey( GameId game, GameRegion region, string serial )
		{
			var key = new VariantKey( game, region, serial );
			if( ByVariant.TryGetValue( key, out var ctor ) )
				return ctor();
			return FromSerial( serial );
		}

		public static GameContext FromManifest( string manifestPath )
		{
			var m = ManifestJson.Load( manifestPath );
			if( !string.IsNullOrWhiteSpace( m.GameSerial ) && m.Game.HasValue && m.Region.HasValue )
				return FromGameKey( m.Game.Value, m.Region.Value, m.GameSerial );
			var elf = m.Elf ?? m.GameSerial ?? throw new InvalidDataException( "Manifest missing elf/game_serial." );
			return FromSerial( elf );
		}

		public static GameContext FromSystemCnf( string systemCnfPath )
		{
			if( systemCnfPath is null )
				throw new ArgumentNullException( nameof( systemCnfPath ) );
			var text = File.ReadAllText( systemCnfPath, Encoding.ASCII );
			var serial = ExtractSerialFromSystemCnf( text );
			var ver = ExtractVersionFromSystemCnf( text );

			if( serial.Equals( Game_Zero_2_EU.SerialConst, StringComparison.OrdinalIgnoreCase ) )
			{
				if( string.Equals( ver, "1.00", StringComparison.OrdinalIgnoreCase ) )
					return new Game_Zero_2_EU_DBG();
				if( string.Equals( ver, "1.01", StringComparison.OrdinalIgnoreCase ) )
					return new Game_Zero_2_EU();
			}

			return FromSerial( serial );
		}

		private static string ExtractSerialFromSystemCnf( string text )
		{
			var marker = "cdrom0:\\";
			var idx = text.IndexOf( marker, StringComparison.OrdinalIgnoreCase );
			if( idx < 0 )
				throw new InvalidDataException( "SYSTEM.CNF missing cdrom0 reference." );
			var start = idx + marker.Length;
			var end = text.IndexOf( ';', start );
			if( end < 0 )
				throw new InvalidDataException( "SYSTEM.CNF cdrom0 line missing ';'." );
			var serial = text.Substring( start, end - start ).Trim();
			if( string.IsNullOrEmpty( serial ) )
				throw new InvalidDataException( "SYSTEM.CNF cdrom0 serial is empty." );
			return serial;
		}

		private static string ExtractVersionFromSystemCnf( string text )
		{
			var lines = text.Split( new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries );
			foreach( var raw in lines )
			{
				var line = raw.Trim();
				if( line.Length == 0 )
					continue;
				var idxVer = line.IndexOf( "VER", StringComparison.OrdinalIgnoreCase );
				if( idxVer != 0 )
					continue;
				var idxEq = line.IndexOf( '=' );
				if( idxEq < 0 )
					continue;
				var value = line.Substring( idxEq + 1 ).Trim();
				return value;
			}
			return string.Empty;
		}
	}
}
