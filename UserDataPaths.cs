namespace TypingPet;

internal static class UserDataPaths
{
    public static string Root { get; } = ResolveRoot();
    private static string LegacyRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TypingPetPrototype");

    public static string Assets => Path.Combine(Root, "assets");
    public static string Settings => Path.Combine(Root, "settings.json");
    public static string Counter => Path.Combine(Root, "counter.txt");
    public static string DailyStatistics => Path.Combine(Root, "daily-statistics.json");

    private static string ResolveRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "TypingPet.csproj")))
                return Path.Combine(directory.FullName, "data");

        return Path.Combine(AppContext.BaseDirectory, "data");
    }

    public static void Initialize()
    {
        Directory.CreateDirectory(Assets);
        if (File.Exists(Settings) || !File.Exists(Path.Combine(LegacyRoot, "settings.json"))) return;

        // Copy the old user data once. Never move or delete it; it remains a rollback copy.
        var legacyAssets = Path.Combine(LegacyRoot, "assets");
        if (Directory.Exists(legacyAssets))
            foreach (var source in Directory.EnumerateFiles(legacyAssets))
            {
                var destination = Path.Combine(Assets, Path.GetFileName(source));
                if (!File.Exists(destination)) File.Copy(source, destination);
            }
        var oldCounter = Path.Combine(LegacyRoot, "counter.txt");
        if (File.Exists(oldCounter) && !File.Exists(Counter)) File.Copy(oldCounter, Counter);
        var pending = Settings + ".migrating";
        File.Copy(Path.Combine(LegacyRoot, "settings.json"), pending, overwrite: true);
        File.Move(pending, Settings, overwrite: true);
    }
}
