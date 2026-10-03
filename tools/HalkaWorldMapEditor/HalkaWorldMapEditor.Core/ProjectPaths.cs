namespace HalkaWorldMapEditor.Core;

public static class ProjectPaths
{
    public const string AuthoringRelative = "Assets/Content/Maps/Authoring";
    public static bool IsUnityProject(string path) =>
        Directory.Exists(Path.Combine(path, "Assets", "Content", "Maps", "Authoring"));

    public static string? FindNear(string start)
    {
        var current = new DirectoryInfo(start);
        for (var i = 0; current != null && i < 12; i++, current = current.Parent)
        {
            if (IsUnityProject(current.FullName)) return current.FullName;
            var child = Path.Combine(current.FullName, "unity-project");
            if (IsUnityProject(child)) return child;
        }
        return null;
    }

    public static string AuthoringFolder(string projectRoot) =>
        Path.Combine(projectRoot, "Assets", "Content", "Maps", "Authoring");
    public static string CatalogPath(string projectRoot) =>
        Path.Combine(AuthoringFolder(projectRoot), "object_catalog.hwcatalog.json");
    public static string[] MapPaths(string projectRoot) =>
        Directory.GetFiles(AuthoringFolder(projectRoot), "*.hwmap.json")
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
    public static string SpritePath(string projectRoot, string projectRelativePath)
    {
        var fullRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(projectRoot,
            projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Sprite path leaves the Unity project: " + projectRelativePath);
        return fullPath;
    }
}
