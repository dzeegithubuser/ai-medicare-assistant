using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReferenceDataController : ControllerBase
{
    /// <summary>
    /// Returns all master/reference data for profile forms.
    /// Public endpoint — no authentication required.
    /// </summary>
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(new
        {
            genders = new[]
            {
                new { value = "Male", label = "Male" },
                new { value = "Female", label = "Female" }
            },
            taxFilingStatuses = new[]
            {
                new { value = "MARRIED_FILING_JOINTLY", label = "Jointly" },
                new { value = "FILING_INDIVIDUALLY", label = "Individually" }            },
            usStates = new[]
            {
                new { value = "AL", label = "Alabama" },
                new { value = "AK", label = "Alaska" },
                new { value = "AZ", label = "Arizona" },
                new { value = "AR", label = "Arkansas" },
                new { value = "CA", label = "California" },
                new { value = "CO", label = "Colorado" },
                new { value = "CT", label = "Connecticut" },
                new { value = "DE", label = "Delaware" },
                new { value = "FL", label = "Florida" },
                new { value = "GA", label = "Georgia" },
                new { value = "HI", label = "Hawaii" },
                new { value = "ID", label = "Idaho" },
                new { value = "IL", label = "Illinois" },
                new { value = "IN", label = "Indiana" },
                new { value = "IA", label = "Iowa" },
                new { value = "KS", label = "Kansas" },
                new { value = "KY", label = "Kentucky" },
                new { value = "LA", label = "Louisiana" },
                new { value = "ME", label = "Maine" },
                new { value = "MD", label = "Maryland" },
                new { value = "MA", label = "Massachusetts" },
                new { value = "MI", label = "Michigan" },
                new { value = "MN", label = "Minnesota" },
                new { value = "MS", label = "Mississippi" },
                new { value = "MO", label = "Missouri" },
                new { value = "MT", label = "Montana" },
                new { value = "NE", label = "Nebraska" },
                new { value = "NV", label = "Nevada" },
                new { value = "NH", label = "New Hampshire" },
                new { value = "NJ", label = "New Jersey" },
                new { value = "NM", label = "New Mexico" },
                new { value = "NY", label = "New York" },
                new { value = "NC", label = "North Carolina" },
                new { value = "ND", label = "North Dakota" },
                new { value = "OH", label = "Ohio" },
                new { value = "OK", label = "Oklahoma" },
                new { value = "OR", label = "Oregon" },
                new { value = "PA", label = "Pennsylvania" },
                new { value = "RI", label = "Rhode Island" },
                new { value = "SC", label = "South Carolina" },
                new { value = "SD", label = "South Dakota" },
                new { value = "TN", label = "Tennessee" },
                new { value = "TX", label = "Texas" },
                new { value = "UT", label = "Utah" },
                new { value = "VT", label = "Vermont" },
                new { value = "VA", label = "Virginia" },
                new { value = "WA", label = "Washington" },
                new { value = "WV", label = "West Virginia" },
                new { value = "WI", label = "Wisconsin" },
                new { value = "WY", label = "Wyoming" },
                new { value = "DC", label = "District of Columbia" }
            },
            medigapDataSources = new[]
            {
                new { value = (string?)null, label = "CSG" },
                new { value = (string?)"AIVANTE", label = "Aivante" },
                new { value = (string?)"MEDICARE_GOV", label = "Medicare" }
            },
            medigapPlanTypes = new[]
            {
                new { value = "G", label = "Plan G" },
                new { value = "N", label = "Plan N" },
                new { value = "HDG", label = "Plan HDG" }
            }
        });
    }
}
