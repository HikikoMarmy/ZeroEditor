namespace ZeroEditor.Game
{
	public sealed class Game_Zero_3_PROTO_SEP : GameContext
	{
		public const string SerialConst = "SLUS_212.44";

		public Game_Zero_3_PROTO_SEP()
			: base( new GameKey( GameId.FF3, GameRegion.NTSCU, SerialConst ), Array.Empty<string>() )
		{ }

		public override PKVersion DefaultPkVersion => PKVersion.PK4;
		public override PKVersion ResolveArchiveVersion( string filePath ) => PKVersion.PK4;
	}
}
