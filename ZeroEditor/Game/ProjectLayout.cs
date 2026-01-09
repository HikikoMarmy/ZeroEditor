namespace ZeroEditor.Game
{
    public class ProjectLayout
    {
        public const string ManifestFileName = "manifest.json";
        public const string FilesFolderName = "files";
        public const string BaseIsoFileName = "_base.iso";
        public const string RebuildFolderName = "_rebuild";

        public sealed record Layout(
            string ProjectRoot,
            string FilesRoot,
            string ManifestPath,
            string BaseIsoPath,
            string RebuildDir
        );

        public static Layout FromManifestPath(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
                throw new ArgumentException("manifestPath is empty.", nameof(manifestPath));
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("Manifest not found.", manifestPath);

            var projectRoot = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
            return FromProjectRoot(projectRoot);
        }

        public static Layout FromProjectRoot(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new ArgumentException("projectRoot is empty.", nameof(projectRoot));

            projectRoot = Path.GetFullPath(projectRoot);

            var manifestPath = Path.Combine(projectRoot, ManifestFileName);
            var filesRoot = Path.Combine(projectRoot, FilesFolderName);
            if (!Directory.Exists(filesRoot))
                Directory.CreateDirectory(filesRoot);

            var baseIsoPath = Path.Combine(projectRoot, BaseIsoFileName);
            var rebuildDir = Path.Combine(projectRoot, RebuildFolderName);

            return new Layout(
                ProjectRoot: projectRoot,
                FilesRoot: filesRoot,
                ManifestPath: manifestPath,
                BaseIsoPath: baseIsoPath,
                RebuildDir: rebuildDir
            );
        }

        public static string ResolveFilesRoot(string projectRoot)
        {
            var files = Path.Combine(projectRoot, FilesFolderName);
            return Directory.Exists(files) ? files : projectRoot;
        }

        public static string ResolveManifestPath(string projectRoot)
        {
            var p = Path.Combine(projectRoot, ManifestFileName);
            if (File.Exists(p))
                return p;

            var legacy = Path.Combine(projectRoot, "_manifest.json");
            if (File.Exists(legacy))
                return legacy;

            return p;
        }
    }
}
