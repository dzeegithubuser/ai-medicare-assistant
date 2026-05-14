using Application.DTOs;
using Domain.Documents;
using Domain.Exceptions;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class ProfileService
{
    private readonly IProfileRepository _profileRepo;
    private readonly IUserRepository _userRepo;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(
        IProfileRepository profileRepo,
        IUserRepository userRepo,
        ILogger<ProfileService> logger)
    {
        _profileRepo = profileRepo;
        _userRepo = userRepo;
        _logger = logger;
    }

    public async Task<UserProfileResponse> GetProfileAsync(Guid userId)
    {
        try
        {
            _logger.LogInformation("Loading profile for user {UserId}", userId);

            var user = await _userRepo.GetByIdAsync(userId);
            var profile = await _profileRepo.GetByUserIdAsync(userId);
            var isComplete = profile is not null && profile.IsProfileComplete;

            return new UserProfileResponse
            {
                Profile = isComplete ? MapToDto(user!, profile!) : null,
                IsProfileComplete = isComplete,
                CurrentPrescriptionDocumentId = profile?.CurrentPrescriptionDocumentId
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

            var user = await _userRepo.GetByIdAsync(userId)
                ?? throw new NotFoundException("User", userId);

            // Names live on the login doc; the profile screen can edit them.
            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.ModifiedBy = userId.ToString();
            await _userRepo.UpdateAsync(user);

            var profile = await _profileRepo.GetByUserIdAsync(userId) ?? new ProfileDocument { UserId = userId };

            profile.CoverageYear = dto.CoverageYear;
            profile.HealthCondition = dto.HealthCondition;
            profile.TaxFilingStatus = dto.TaxFilingStatus;
            profile.MagiTier = dto.MagiTier;
            profile.Gender = dto.Gender;
            profile.TobaccoStatus = dto.TobaccoStatus;
            profile.DateOfBirth = dto.DateOfBirth;
            profile.Concierge = dto.Concierge;
            profile.ConciergeAmount = dto.ConciergeAmount;
            profile.AlternateEmail = dto.AlternateEmail;
            profile.AlternateMobile = dto.AlternateMobile;
            profile.LifeExpectancy = dto.LifeExpectancy;

            profile.AddressLine1 = dto.AddressLine1;
            profile.City = dto.City;
            profile.State = dto.State;
            profile.ZipCode = dto.ZipCode;
            profile.County = dto.County;
            profile.CountyCode = dto.CountyCode;
            profile.Latitude = dto.Latitude;
            profile.Longitude = dto.Longitude;

            profile.IsProfileComplete = true;
            profile.ModifiedBy = userId.ToString();

            await _profileRepo.UpdateAsync(profile);

            _logger.LogInformation("Profile saved for user {UserId}", userId);
            return MapToDto(user, profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving profile for user {UserId}", userId);
            throw;
        }
    }

    public static ProfileDto MapToDto(UserDocument user, ProfileDocument profile) => new()
    {
        FirstName = user.FirstName,
        LastName = user.LastName,
        CoverageYear = profile.CoverageYear,
        HealthCondition = profile.HealthCondition,
        TaxFilingStatus = profile.TaxFilingStatus,
        MagiTier = profile.MagiTier,
        Gender = profile.Gender,
        TobaccoStatus = profile.TobaccoStatus,
        DateOfBirth = profile.DateOfBirth ?? "",
        Concierge = profile.Concierge,
        ConciergeAmount = profile.ConciergeAmount,
        AlternateEmail = profile.AlternateEmail,
        AlternateMobile = profile.AlternateMobile,
        LifeExpectancy = profile.LifeExpectancy,
        AddressLine1 = profile.AddressLine1,
        City = profile.City,
        State = profile.State,
        ZipCode = profile.ZipCode,
        County = profile.County,
        CountyCode = profile.CountyCode,
        Latitude = profile.Latitude,
        Longitude = profile.Longitude
    };
}
