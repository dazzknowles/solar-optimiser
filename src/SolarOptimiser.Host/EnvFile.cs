namespace SolarOptimiser.Host;

/// <summary>
/// Reads the repo-root .env file so local dev/test DB settings have one source of truth shared
/// across tooling instead of being duplicated into user-secrets.
/// </summary>
internal static class EnvFile
{
    public static IReadOnlyDictionary<string, string> Load()
    {
        string? repositoryRoot = FindRepositoryRoot();
        if (repositoryRoot is null)
        {
            return new Dictionary<string, string>();
        }

        string envPath = Path.Combine(repositoryRoot, ".env");
        if (!File.Exists(envPath))
        {
            return new Dictionary<string, string>();
        }

        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines(envPath))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            int separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            string key = trimmed[..separatorIndex].Trim();
            string value = trimmed[(separatorIndex + 1)..].Trim();
            values[key] = value;
        }

        return values;
    }

    private static string? FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SolarOptimiser.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
