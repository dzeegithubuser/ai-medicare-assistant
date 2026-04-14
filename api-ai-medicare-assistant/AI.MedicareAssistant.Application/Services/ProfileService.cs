using Application.DTOs;
using Domain.Entities;
using Domain.Exceptions;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class ProfileService
{
    private readonly IProfileRepository _repo;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(IProfileRepository repo, ILogger<ProfileService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<UserProfileResponse> GetProfileAsync(Guid userId)
    {
        try
        {
            _logger.LogInformation("Loading profile for user {UserId}", userId);
            var entity = await _repo.GetByUserIdAsync(userId);

            return new UserProfileResponse
            {
                Profile = entity is null ? null : MapToDto(entity),
                IsProfileComplete = entity is not null,
                CurrentPrescriptionDocumentId = entity?.CurrentPrescriptionDocumentId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading profile for user {UserId}", userId);
            throw;
        }
    }

    public async Task<ProfileDto> SaveAsync(Guid userId, ProfileDto dto)
    {
        try
        {
            _logger.LogInformation("Saving profile for user {UserId}", userId);

            var entity = await _repo.GetByUserIdAsync(userId);
            var isNew = entity is null;

            entity ??= new Profile
            {
                UserId = userId,
                CreatedBy = userId.ToString(),
                ModifiedBy = userId.ToString()
            };

            entity.FirstName = dto.FirstName;
            entity.LastName = dto.LastName;
            entity.CoverageYear = dto.CoverageYear;
            entity.HealthCondition = dto.HealthCondition;
            entity.TaxFilingStatus = dto.TaxFilingStatus;
            entity.MagiTier = dto.MagiTier;
            entity.Gender = dto.Gender;
            entity.TobaccoStatus = dto.TobaccoStatus;
            entity.DateOfBirth = DateOnly.Parse(dto.DateOfBirth);
            entity.Concierge = dto.Concierge;
            entity.ConciergeAmount = dto.ConciergeAmount;
            entity.AlternateEmail = dto.AlternateEmail;
            entity.AlternateMobile = dto.AlternateMobile;
            entity.LifeExpectancy = dto.LifeExpectancy;

            // Address fields
            entity.AddressLine1 = dto.AddressLine1;
            entity.City = dto.City;
            entity.State = dto.State;
            entity.ZipCode = dto.ZipCode;
            entity.County = dto.County;
            entity.CountyCode = dto.CountyCode;
            entity.Latitude = dto.Latitude;
            entity.Longitude = dto.Longitude;
            entity.ModifiedBy = userId.ToString();

            if (isNew)
                await _repo.CreateAsync(entity);
            else
                await _repo.UpdateAsync(entity);

            _logger.LogInformation("Profile {Action} for user {UserId}", isNew ? "created" : "updated", userId);
            return MapToDto(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving profile for user {UserId}", userId);
            throw;
        }
    }

    public static ProfileDto MapToDto(Profile e) => new()
    {
        FirstName = e.FirstName,
        LastName = e.LastName,
        CoverageYear = e.CoverageYear,
        HealthCondition = e.HealthCondition,
        TaxFilingStatus = e.TaxFilingStatus,
        MagiTier = e.MagiTier,
        Gender = e.Gender,
        TobaccoStatus = e.TobaccoStatus,
        DateOfBirth = e.DateOfBirth.ToString("yyyy-MM-dd"),
        Concierge = e.Concierge,
        ConciergeAmount = e.ConciergeAmount,
        AlternateEmail = e.AlternateEmail,
        AlternateMobile = e.AlternateMobile,
        LifeExpectancy = e.LifeExpectancy,
        AddressLine1 = e.AddressLine1,
        City = e.City,
        State = e.State,
        ZipCode = e.ZipCode,
        County = e.County,
        CountyCode = e.CountyCode,
        Latitude = e.Latitude,
        Longitude = e.Longitude
    };
}
