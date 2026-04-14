namespace Domain.Interfaces;

/// <summary>
/// Pharmacy lookup via Financial Planner getPharmacies API.
/// Returns pharmacies near the user's lat/lng with pagination and filtering.
/// </summary>
public interface IPharmacyLookupService
{
    Task<PharmacyLookupResponse> GetPharmaciesAsync(
        PharmacyLookupRequest request,
        CancellationToken cancellationToken = default);
}

public class PharmacyLookupRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double SearchRadiusInMiles { get; set; }
    public string PharmacyName { get; set; } = "";
    public int Page { get; set; } = 1;
    public int Size { get; set; } = 20;
}

public class PharmacyLookupResponse
{
    public string WebServiceTransactionId { get; set; } = "";
    public string WebServiceStatus { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double SearchRadiusInMiles { get; set; }
    public string PharmacyName { get; set; } = "";
    public int Page { get; set; }
    public int Size { get; set; }
    public int TotalPages { get; set; }
    public int TotalPharmacies { get; set; }
    public List<PharmacyLookupEntry> Pharmacies { get; set; } = [];
}

public class PharmacyLookupEntry
{
    public long PharmacyNumber { get; set; }
    public string PharmacyName { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Address { get; set; } = "";
    public double Distance { get; set; }
    public int Zipcode { get; set; }
}
