
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AI;

/// <summary>
/// Loads and assembles prompt files using a convention-based naming scheme.
/// Each prompt name maps to optional files under the Prompts directory:
///   system/{name}-system.txt, tasks/{name}.txt,
///   schemas/{name}-schema.txt, templates/{name}.txt
/// File contents are cached after the first load to avoid repeated disk I/O.
/// </summary>
public class PromptBuilder
{
    private readonly string _basePath = "Prompts";
    private readonly ILogger<PromptBuilder> _logger;
    private readonly ConcurrentDictionary<string, string> _fileCache = new();

    public PromptBuilder(ILogger<PromptBuilder> logger)
    {
        _logger = logger;
    }

    // ── Public API ──────────────────────────────────────────────────────

    /// <summary>
    /// Builds a system + user prompt pair using convention-based file resolution.
    /// File convention for a given <paramref name="promptName"/>:
    ///   system/{promptName}-system.txt  (required)
    ///   tasks/{promptName}.txt          (optional)
    ///   schemas/{promptName}-schema.txt (optional)
    ///   templates/{promptName}.txt      (optional)
    /// </summary>
    public (string system, string user) Build(string promptName, Dictionary<string, string> substitutions)
    {
        var system = Load($"system/{promptName}-system.txt");

        var task = TryLoad($"tasks/{promptName}.txt");
        var schema = TryLoad($"schemas/{promptName}-schema.txt");
        var template = TryLoad($"templates/{promptName}.txt");

        var user = template != null ? ApplySubstitutions(template, substitutions) : "";

        var parts = new[] { task, schema, user }.Where(p => !string.IsNullOrEmpty(p));
        var finalUser = string.Join("\n", parts);

        return (system, finalUser);
    }

    /// <summary>
    /// Loads a single prompt file by its path relative to the Prompts directory.
    /// Used by extractor services that only need a system prompt.
    /// </summary>
    public string LoadPromptFile(string relativePath)
    {
        return Load(relativePath);
    }

    // ── Legacy convenience wrappers (preserve call-site compatibility) ───

    /// <summary>Drug normalization — non-standard file names.</summary>
    public (string system, string user) BuildDrugNormalization(string prescription)
    {
        var system = Load("system/pharma-system.txt");
        var task = Load("tasks/drug-normalization.txt");
        var schema = Load("schemas/drug-json-schema.txt");
        var template = Load("templates/prescription-analysis.txt");

        var user = template.Replace("{{PRESCRIPTION}}", prescription);
        var finalUser = $"{task}\n{schema}\n{user}";

        return (system, finalUser);
    }

    // ── Private helpers ─────────────────────────────────────────────────

    private string Load(string relativePath)
    {
        return _fileCache.GetOrAdd(relativePath, key =>
        {
            var fullPath = Path.Combine(_basePath, key);
            if (!File.Exists(fullPath))
            {
                _logger.LogError("Prompt file not found: {Path}", fullPath);
                throw new FileNotFoundException($"Prompt file not found: {fullPath}", fullPath);
            }
            return File.ReadAllText(fullPath);
        });
    }

    private string? TryLoad(string relativePath)
    {
        var fullPath = Path.Combine(_basePath, relativePath);
        if (!File.Exists(fullPath))
            return null;
        return Load(relativePath);
    }

    private static string ApplySubstitutions(string template, Dictionary<string, string> substitutions)
    {
        foreach (var (key, value) in substitutions)
            template = template.Replace(key, value);
        return template;
    }
}
