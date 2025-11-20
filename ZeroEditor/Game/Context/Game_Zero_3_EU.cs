namespace ZeroEditor.Game
{
	public sealed class Game_Zero_3_EU : GameContext
	{
		public const string SerialConst = "SLES_538.25";

		public Game_Zero_3_EU()
			: base( new GameKey( GameId.FF3, GameRegion.PAL, SerialConst ), Array.Empty<string>() )
		{ }

		public override PKVersion DefaultPkVersion => PKVersion.PK4;
		public override PKVersion ResolveArchiveVersion( string filePath ) => PKVersion.PK4;
	}
}
