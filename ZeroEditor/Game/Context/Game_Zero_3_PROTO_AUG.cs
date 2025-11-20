namespace ZeroEditor.Game
{
	public sealed class Game_Zero_3_PROTO_AUG : GameContext
	{
		public const string SerialConst = "SLPS_255.44";

		public Game_Zero_3_PROTO_AUG()
			: base( new GameKey( GameId.FF3, GameRegion.NTSCJ, SerialConst ), Array.Empty<string>() )
		{ }

		public override PKVersion DefaultPkVersion => PKVersion.PK4;
		public override PKVersion ResolveArchiveVersion( string filePath ) => PKVersion.PK4;
	}
}
