using ZeroEditor.Game;

namespace ZeroEditor
{
	public static class AppState
	{
		public static GameContext? CurrentContext { get; set; }
		public static GameId? CurrentGameId => CurrentContext?.Key.Game;
		public static GameRegion? CurrentRegion => CurrentContext?.Key.Region;
		public static string? CurrentSerial => CurrentContext?.Key.Serial;
	}
}
