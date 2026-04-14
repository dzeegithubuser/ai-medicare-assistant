namespace Domain.Interfaces;

public interface IDrugAiService
{
    Task<string> AnalyzePrescription(string prescription);
    Task<string> SuggestDrugNames(string input);
}
