namespace Domain.Models;

/// <summary>
/// Represents a constant item from the Financial Planner API.
/// Uniquely identified by the Label field.
/// </summary>
public class ConstantItem
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "";
    public int Year { get; set; }

    /// <summary>
    /// Returns the value as a list, splitting on commas if the value contains multiple entries.
    /// e.g. "abc,xyz,pqr" → ["abc", "xyz", "pqr"]
    /// </summary>
    public List<string> GetValueList()
    {
        if (string.IsNullOrWhiteSpace(Value)) return [];
        return Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    /// <summary>
    /// Returns the value as key-value pairs with 1-based index.
    /// e.g. "abc,xyz,pqr" → [{Label="abc", Value=1}, {Label="xyz", Value=2}, {Label="pqr", Value=3}]
    /// </summary>
    public List<LabelValuePair> GetValuePairs()
    {
        var items = GetValueList();
        return items.Select((label, index) => new LabelValuePair
        {
            Label = label,
            Value = index + 1
        }).ToList();
    }
}

public class LabelValuePair
{
    public string Label { get; set; } = "";
    public int Value { get; set; }
}
