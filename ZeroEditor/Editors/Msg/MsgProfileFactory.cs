namespace ZeroEditor.Editors
{
	public static class MsgProfiles
	{
		public static IMsgProfile ForGame( Game.GameId? g ) => g switch
		{
			Game.GameId.FF1 => new FF1MsgProfile(),
			Game.GameId.FF2 => new FF2MsgProfile(),
			Game.GameId.FF3 => new FF3MsgProfile(),
			_ => new FF1MsgProfile()
		};
	}
}
