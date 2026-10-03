using System.Text;
using System.Text.Json;

namespace TypingPet;

// Installs an optional shared profile once, without replacing a user's existing settings.
internal static class StarterProfileImporter
{
    public static string? TryInstall(string programDirectory, string dataDirectory)
    {
        var bundleDirectory = Path.Combine(programDirectory, "starter-profile");
        var bundleSettings = Path.Combine(bundleDirectory, "settings.json");
        var targetSettings = Path.Combine(dataDirectory, "settings.json");
        if (!File.Exists(bundleSettings) || File.Exists(targetSettings)) return null;

        var copiedAssets = new List<string>();
        var pendingSettings = targetSettings + ".starter.tmp";
        try
        {
            Directory.CreateDirectory(dataDirectory);
            var targetAssets = Path.Combine(dataDirectory, "assets");
            Directory.CreateDirectory(targetAssets);

            var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var bundleAssets = Path.Combine(bundleDirectory, "assets");
            if (Directory.Exists(bundleAssets))
            {
                foreach (var source in Directory.GetFiles(bundleAssets, "*", SearchOption.TopDirectoryOnly))
                {
                    var relative = "assets/" + Path.GetFileName(source);
                    var destination = Path.Combine(targetAssets, Guid.NewGuid().ToString("N") + Path.GetExtension(source));
                    File.Copy(source, destination);
                    copiedAssets.Add(destination);
                    replacements.Add(relative, destination);
                }
            }

            using var document = JsonDocument.Parse(File.ReadAllText(bundleSettings));
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                WriteWithLocalPaths(document.RootElement, writer, replacements);
            File.WriteAllText(pendingSettings, Encoding.UTF8.GetString(stream.ToArray()));
            File.Move(pendingSettings, targetSettings);
            return null;
        }
        catch (Exception error)
        {
            try { if (File.Exists(pendingSettings)) File.Delete(pendingSettings); } catch { }
            foreach (var asset in copiedAssets)
                try { if (File.Exists(asset)) File.Delete(asset); } catch { }
            return error.Message;
        }
    }

    private static void WriteWithLocalPaths(JsonElement element, Utf8JsonWriter writer, IReadOnlyDictionary<string, string> replacements)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(Replace(property.Name, replacements));
                    WriteWithLocalPaths(property.Value, writer, replacements);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteWithLocalPaths(item, writer, replacements);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(Replace(element.GetString()!, replacements));
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static string Replace(string value, IReadOnlyDictionary<string, string> replacements)
    {
        if (replacements.TryGetValue(value, out var localPath)) return localPath;
        if (value.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("分享包缺少图片：" + Path.GetFileName(value));
        return value;
    }
}
