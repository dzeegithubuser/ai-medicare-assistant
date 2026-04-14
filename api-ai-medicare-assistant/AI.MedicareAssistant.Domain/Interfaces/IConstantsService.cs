using Domain.Models;

namespace Domain.Interfaces;

public interface IConstantsService
{
    /// <summary>
    /// Returns all constants from the Financial Planner API. Results are cached.
    /// </summary>
    Task<List<ConstantItem>> GetAllAsync();

    /// <summary>
    /// Returns a single constant by its label (case-insensitive), or null if not found.
    /// </summary>
    Task<ConstantItem?> GetByLabelAsync(string label);

    /// <summary>
    /// Returns all constants matching the predicate filter.
    /// </summary>
    Task<List<ConstantItem>> GetByFilterAsync(Func<ConstantItem, bool> predicate);

    /// <summary>
    /// Returns the value string for a given label, or null if not found.
    /// </summary>
    Task<string?> GetValueByLabelAsync(string label);

    /// <summary>
    /// Returns the value as a comma-split list for a given label, or empty list.
    /// </summary>
    Task<List<string>> GetValueListByLabelAsync(string label);

    /// <summary>
    /// Returns the value as key-value pairs [{Label, Value(1-based index)}] for a given label.
    /// </summary>
    Task<List<LabelValuePair>> GetValuePairsByLabelAsync(string label);
}
