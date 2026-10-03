using System.Text.Json;
using System.Drawing;
using System.Globalization;

namespace TypingPet;

internal sealed record DataFolderIntegrityReport(
    string DataDirectory,
    int AssetImageCount,
    int ReferencedImageCount,
    int UnusedImageCount,
    int RemovedImageCount,
    int InvalidImageCount,
    IReadOnlyList<string> Issues)
{
    public bool IsClean => Issues.Count == 0 && UnusedImageCount == 0;
}

internal static class DataFolderIntegrityService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".ico"
    };

    public static DataFolderIntegrityReport CheckAndClean(string dataDirectory, bool inspectImageContent = true)
    {
        var root = Path.GetFullPath(dataDirectory);
        var assetDirectory = Path.GetFullPath(Path.Combine(root, "assets"));
        var issues = new List<string>();
        var referencedAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mayClean = true;

        if (!Directory.Exists(root))
        {
            issues.Add("data 文件夹不存在。");
            return new(root, 0, 0, 0, 0, 0, issues);
        }

        var settingsPath = Path.Combine(root, "settings.json");
        // Only the live settings file determines which assets this running
        // installation can use. The .bak file is a recovery snapshot, not a
        // runtime image source, so backup-only assets are eligible for cleanup.
        var settingsFiles = new List<string> { settingsPath };

        foreach (var settingsFile in settingsFiles)
        {
            if (!File.Exists(settingsFile))
            {
                issues.Add("找不到 settings.json；为保护图片，已跳过自动清理。");
                mayClean = false;
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(settingsFile));
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new JsonException("设置文件根节点不是对象。");
                if (!HasKnownImageConfiguration(document.RootElement))
                    throw new JsonException("缺少可识别的设置组或图片组结构。");

                CollectReferences(document.RootElement, root, assetDirectory, referencedAssets, issues);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
            {
                issues.Add($"{Path.GetFileName(settingsFile)} 无法校验：{ex.Message}；为保护图片，已跳过自动清理。");
                mayClean = false;
            }
        }

        ValidateCounter(Path.Combine(root, "counter.txt"), issues);
        ValidateDailyStatistics(Path.Combine(root, "daily-statistics.json"), issues);

        if (issues.Any(issue => issue.Contains("找不到") || issue.Contains("无法校验") || issue.Contains("引用图片不存在") || issue.Contains("图片列表结构无效")))
            mayClean = false;

        var assets = Directory.Exists(assetDirectory)
            ? Directory.EnumerateFiles(assetDirectory, "*", SearchOption.TopDirectoryOnly)
                .Where(path => ImageExtensions.Contains(Path.GetExtension(path)))
                .ToList()
            : [];

        var invalidImages = 0;
        if (inspectImageContent)
        {
            foreach (var path in assets)
            {
                try
                {
                    using var image = Image.FromFile(path);
                    if (image.Width <= 0 || image.Height <= 0) throw new ArgumentException("图片尺寸无效。");
                }
                catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException)
                {
                    invalidImages++;
                    issues.Add($"图片无法读取：{Path.GetFileName(path)}");
                    if (referencedAssets.Contains(Path.GetFullPath(path))) mayClean = false;
                }
            }
        }

        var unused = assets.Where(path => !referencedAssets.Contains(Path.GetFullPath(path))).ToList();
        var removed = 0;
        if (mayClean)
        {
            foreach (var path in unused)
            {
                try
                {
                    File.Delete(path);
                    removed++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    issues.Add($"未能删除未引用图片 {Path.GetFileName(path)}：{ex.Message}");
                }
            }
        }

        var remainingUnused = mayClean ? unused.Count - removed : unused.Count;
        return new(root, assets.Count, referencedAssets.Count, remainingUnused, removed, invalidImages, issues);
    }

    private static bool HasKnownImageConfiguration(JsonElement root)
    {
        if (root.TryGetProperty("Profiles", out var profiles) && profiles.ValueKind is not (JsonValueKind.Array or JsonValueKind.Null)) return false;
        if (root.TryGetProperty("ImageGroups", out var groups) && groups.ValueKind is not (JsonValueKind.Array or JsonValueKind.Null)) return false;
        var hasProfiles = profiles.ValueKind == JsonValueKind.Array;
        var hasImageGroups = groups.ValueKind == JsonValueKind.Array;
        return hasProfiles || hasImageGroups;
    }

    private static void ValidateCounter(string path, List<string> issues)
    {
        if (!File.Exists(path)) return;
        try
        {
            var text = File.ReadAllText(path).Trim();
            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0)
                issues.Add("counter.txt 不是有效的非负整数；文件已保留。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issues.Add($"counter.txt 无法校验：{ex.Message}；文件已保留。");
        }
    }

    private static void ValidateDailyStatistics(string path, List<string> issues)
    {
        if (!File.Exists(path)) return;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("Date", out var date) || date.ValueKind != JsonValueKind.String
                || !DateOnly.TryParseExact(date.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                || !root.TryGetProperty("InputCount", out var inputCount) || !inputCount.TryGetInt64(out var input) || input < 0
                || !root.TryGetProperty("WordCount", out var wordCount) || !wordCount.TryGetInt64(out var words) || words < 0
                || !root.TryGetProperty("WordHasInput", out var wordHasInput) || wordHasInput.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                issues.Add("daily-statistics.json 字段缺失或值无效；文件已保留。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            issues.Add($"daily-statistics.json 无法校验：{ex.Message}；文件已保留。");
        }
    }

    private static void CollectReferences(
        JsonElement element,
        string dataDirectory,
        string assetDirectory,
        HashSet<string> references,
        List<string> issues,
        bool inImageList = false)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var hasImageGroups = element.TryGetProperty("ImageGroups", out var imageGroups)
                    && imageGroups.ValueKind == JsonValueKind.Array
                    && imageGroups.GetArrayLength() > 0;
                foreach (var property in element.EnumerateObject())
                {
                    // Modern settings load images from ImageGroups. Legacy
                    // Images/Profiles fields remain in settings.json for
                    // migration compatibility, but are ignored once a modern
                    // image-group collection exists, so they must not keep
                    // otherwise-unused asset files alive.
                    if (hasImageGroups && (property.Name.Equals("Profiles", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Equals("Images", StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var isImageList = property.Name.Equals("Images", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Equals("Backgrounds", StringComparison.OrdinalIgnoreCase);
                    if (isImageList && property.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Null))
                    {
                        issues.Add($"图片列表结构无效（{property.Name}）；为保护图片，已跳过自动清理。");
                        continue;
                    }
                    CollectReferences(property.Value, dataDirectory, assetDirectory, references, issues, inImageList || isImageList);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    CollectReferences(item, dataDirectory, assetDirectory, references, issues, inImageList);
                break;
            case JsonValueKind.String when inImageList:
                var original = element.GetString();
                if (string.IsNullOrWhiteSpace(original)) return;

                string path;
                try
                {
                    path = Path.GetFullPath(Path.IsPathFullyQualified(original)
                        ? original
                        : Path.Combine(dataDirectory, original.Replace('/', Path.DirectorySeparatorChar)));
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    issues.Add($"图片路径格式无效：{original}");
                    return;
                }

                var assetPrefix = assetDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!path.StartsWith(assetPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (!File.Exists(path)) issues.Add($"引用图片不存在：{original}");
                    return;
                }

                if (!ImageExtensions.Contains(Path.GetExtension(path)))
                {
                    issues.Add($"引用了当前格式清单以外的图片，已保留：{Path.GetFileName(path)}");
                    return;
                }

                if (!File.Exists(path))
                    issues.Add($"引用图片不存在：{Path.GetFileName(path)}");
                else
                    references.Add(path);
                break;
        }
    }
}
