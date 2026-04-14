namespace Domain.Interfaces;

public interface IFdaNdcService
{
    /// <summary>
    /// Fetches package info for the given NDC code from the FDA NDC Directory.
    /// Returns null if not found or API unavailable.
    /// </summary>
    Task<NdcPackageInfo?> GetPackageInfo(string ndcCode);

    /// <summary>
    /// Fetches package info for multiple NDC codes in parallel.
    /// </summary>
    Task<List<NdcPackageInfo>> GetPackageInfoBatch(List<string> ndcCodes);
}

public class NdcPackageInfo
{
    public string NdcCode { get; set; } = "";
    public string PackageDescription { get; set; } = "";
    public int PackageSize { get; set; }
    public string PackageType { get; set; } = "";
}
