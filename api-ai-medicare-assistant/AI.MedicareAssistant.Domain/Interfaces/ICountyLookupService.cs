namespace Domain.Interfaces;

public interface ICountyLookupService
{
    /// <summary>
    /// Returns the county code for the given 5-digit ZIP code,
    /// or null if the ZIP is not found.
    /// </summary>
    Task<string?> GetCountyCode(string zipCode);

    /// <summary>
    /// Returns the county name for the given 5-digit ZIP code, or null if not found.
    /// </summary>
    Task<string?> GetCountyName(string zipCode);

    /// <summary>
    /// Returns the 2-letter state code for the given 5-digit ZIP code, or null if not found.
    /// </summary>
    Task<string?> GetStateCode(string zipCode);

    /// <summary>
    /// Returns all city/county entries for the given 5-digit ZIP code.
    /// </summary>
    Task<List<CountyCodeEntry>> GetCountyCodeList(string zipCode);
}

public class CountyCodeEntry
{
    public string City { get; set; } = "";
    public string CountyName { get; set; } = "";
    public string CountyCode { get; set; } = "";
    public string State { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
