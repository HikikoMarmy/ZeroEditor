using ZeroEditor.Game;

namespace ZeroEditor
{
    public static class AppState
    {
        public static GameContext? CurrentContext { get; set; }
        public static GameId? CurrentGameId => CurrentContext?.Key.Game;
        public static GameRegion? CurrentRegion => CurrentContext?.Key.Region;
        public static string? CurrentSerial => CurrentContext?.Key.Serial;

        public static string? CurrentProjectRoot { get; private set; }
        public static string? CurrentFilesRoot { get; private set; }
        public static string? CurrentManifestPath { get; private set; }

        public static bool HasProject =>
            !string.IsNullOrWhiteSpace(CurrentProjectRoot) &&
            !string.IsNullOrWhiteSpace(CurrentFilesRoot) &&
            Directory.Exists(CurrentFilesRoot);

        public static void SetProject(ProjectLayout.Layout layout)
        {
            CurrentProjectRoot = layout.ProjectRoot;
            CurrentFilesRoot = layout.FilesRoot;
            CurrentManifestPath = layout.ManifestPath;
        }

        public static void ClearProject()
        {
            CurrentProjectRoot = null;
            CurrentFilesRoot = null;
            CurrentManifestPath = null;
        }

        public static string? TryResolveFilesRelative(string relPath)
        {
            if (string.IsNullOrWhiteSpace(relPath))
                return null;

            if (string.IsNullOrWhiteSpace(CurrentFilesRoot))
                return null;

            var p = relPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            if (p.StartsWith(ProjectLayout.FilesFolderName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                p = p.Substring(ProjectLayout.FilesFolderName.Length + 1);

            return Path.Combine(CurrentFilesRoot, p);
        }
    }
}
