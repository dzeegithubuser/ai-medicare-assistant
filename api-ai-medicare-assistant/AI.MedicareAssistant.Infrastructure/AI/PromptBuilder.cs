
namespace Infrastructure.AI;

public class PromptBuilder
{
    private readonly string _basePath="Prompts";

    private string Load(string p)
    {
        return File.ReadAllText(Path.Combine(_basePath,p));
    }

    public (string system,string user) Build(string prescription)
    {
        var system=Load("system/pharma-system.txt");
        var task=Load("tasks/drug-normalization.txt");
        var schema=Load("schemas/drug-json-schema.txt");
        var template=Load("templates/prescription-analysis.txt");

        var user=template.Replace("{{PRESCRIPTION}}",prescription);

        var finalUser=$"{task}\n{schema}\n{user}";

        return (system,finalUser);
    }

    public (string system, string user) BuildDrugNameSuggestion(string input)
    {
        var system = Load("system/drug-name-suggestion-system.txt");
        var task = Load("tasks/drug-name-suggestion.txt");
        var schema = Load("schemas/drug-name-suggestion-schema.txt");
        var template = Load("templates/drug-name-suggestion.txt");

        var user = template.Replace("{{INPUT}}", input);

        var finalUser = $"{task}\n{schema}\n{user}";

        return (system, finalUser);
    }

    public (string system, string user) BuildPlanScoring(Dictionary<string, string> substitutions)
    {
        var system = Load("system/plan-scoring-system.txt");
        var task = Load("tasks/plan-scoring.txt");
        var schema = Load("schemas/plan-scoring-schema.txt");
        var template = Load("templates/plan-scoring.txt");

        var user = ApplySubstitutions(template, substitutions);

        var finalUser = $"{task}\n{schema}\n{user}";

        return (system, finalUser);
    }

    public (string system, string user) BuildCostEvaluation(Dictionary<string, string> substitutions)
    {
        var system = Load("system/cost-evaluation-system.txt");
        var task = Load("tasks/cost-evaluation.txt");
        var schema = Load("schemas/cost-evaluation-schema.txt");
        var template = Load("templates/cost-evaluation.txt");

        var user = ApplySubstitutions(template, substitutions);

        var finalUser = $"{task}\n{schema}\n{user}";

        return (system, finalUser);
    }

    public (string system, string user) BuildLtcEvaluation(Dictionary<string, string> substitutions)
    {
        var system = Load("system/ltc-evaluation-system.txt");
        var task = Load("tasks/ltc-evaluation.txt");
        var schema = Load("schemas/ltc-evaluation-schema.txt");
        var template = Load("templates/ltc-evaluation.txt");

        var user = ApplySubstitutions(template, substitutions);

        var finalUser = $"{task}\n{schema}\n{user}";

        return (system, finalUser);
    }

    private static string ApplySubstitutions(string template, Dictionary<string, string> substitutions)
    {
        foreach (var (key, value) in substitutions)
            template = template.Replace(key, value);
        return template;
    }
}
