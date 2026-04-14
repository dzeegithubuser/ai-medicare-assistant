using Application.DTOs;
using Domain.Documents;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace Application.Services;

public class ChatOrchestratorService
{
    private readonly ConvStateService _convState;
    private readonly RecommendationService _recommendation;
    private readonly OrchestratorIntentService _intentService;
    private readonly ProfileService _profileService;
    private readonly DeltaCalculationService _deltaCalc;
    private readonly CostProjectionService _costProjection;
    private readonly ICountyLookupService _countyLookup;
    private readonly IPharmacyLookupService _pharmacyLookup;
    private readonly IDrugAiService _drugAi;
    private readonly ILogger<ChatOrchestratorService> _logger;

    // Intent → handler method lookup
    private readonly Dictionary<string, Func<Guid, OrchestratorIntentResult, ConvStateDocument, Task<OrchestratorResponse>>> _handlers;

    public ChatOrchestratorService(
        ConvStateService convState,
        RecommendationService recommendation,
        OrchestratorIntentService intentService,
        ProfileService profileService,
        DeltaCalculationService deltaCalc,
        CostProjectionService costProjection,
        ICountyLookupService countyLookup,
        IPharmacyLookupService pharmacyLookup,
        IDrugAiService drugAi,
        ILogger<ChatOrchestratorService> logger)
    {
        _convState = convState;
        _recommendation = recommendation;
        _intentService = intentService;
        _profileService = profileService;
        _deltaCalc = deltaCalc;
        _costProjection = costProjection;
        _countyLookup = countyLookup;
        _pharmacyLookup = pharmacyLookup;
        _drugAi = drugAi;
        _logger = logger;

        _handlers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["create_recommendation"] = HandleCreateRecommendation,
            ["update_demographic"] = HandleUpdateDemographic,
            ["update_health_financial"] = HandleUpdateHealthFinancial,
            ["modify_drugs"] = HandleModifyDrugs,
            ["modify_pharmacy"] = HandleModifyPharmacy,
            ["modify_plans"] = HandleModifyPlans,
            ["delete_recommendation"] = HandleDeleteRecommendation,
            ["view_summary"] = HandleViewSummary,
            ["compare_plans"] = HandleComparePlans,
            ["view_projections"] = HandleViewProjections,
            ["update_tax_filing"] = HandleUpdateTaxFiling,
            ["update_magi"] = HandleUpdateMagi,
            ["update_life_expectancy"] = HandleUpdateLifeExpectancy,
            ["view_plan_details"] = HandleViewPlanDetails,
            ["filter_sort_plans"] = HandleFilterSortPlans,
            ["check_drug_coverage"] = HandleCheckDrugCoverage,
            ["update_concierge"] = HandleUpdateConcierge,
            ["view_funding"] = HandleViewFunding,
            ["help"] = HandleHelp,
        };
    }

    public async Task<OrchestratorResponse> ProcessMessageAsync(Guid userId, string message, string? currentPage = null, CancellationToken ct = default)
    {
        try
        {
            var state = await _convState.GetOrCreateAsync(userId);

            // ── FSM: confirmation / delete phrase states take priority ──
            if (state.State == ConversationState.AwaitingConfirmation)
                return await HandleConfirmation(userId, message, state);

            if (state.State == ConversationState.AwaitingDeletePhrase)
                return await HandleDeletePhrase(userId, message, state);

            // ── FSM: multi-turn collection states ──
            if (state.State == ConversationState.CollectingProfile)
                return await ContinueProfileCollection(userId, message, state);

            if (state.State == ConversationState.CollectingDrugs)
                return await ContinueDrugCollection(userId, message, state);

            if (state.State == ConversationState.CollectingPharmacy)
                return await ContinuePharmacyCollection(userId, message, state);

            if (state.State == ConversationState.CollectingPlans)
                return await ContinuePlanCollection(userId, message, state);

            // ── Default: classify intent and route to handler ──
            var intentResult = await _intentService.ClassifyAsync(message, currentPage, ct);

            if (_handlers.TryGetValue(intentResult.Intent, out var handler))
                return await handler(userId, intentResult, state);

            return Reply("I'm not sure how to help with that. Type **help** to see what I can do.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Orchestrator error for user {UserId}: {Message}", userId, message);
            return Reply("Sorry, something went wrong processing your request. Please try again.");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  FSM: Confirmation & Delete flows
    // ═══════════════════════════════════════════════════════════════

    private async Task<OrchestratorResponse> HandleConfirmation(Guid userId, string message, ConvStateDocument state)
    {
        var normalized = message.Trim().ToLowerInvariant();

        // Guard: if pending context is missing (e.g., TTL expired and was recreated), reset gracefully
        if (state.PendingChanges.ElementCount == 0 && state.ActiveIntent != "delete_recommendation")
        {
            await _convState.ResetAsync(userId);
            return Reply("Your previous action expired. Please start again — type **help** to see options.");
        }

        if (IsAffirmative(normalized))
        {
            // Route to delete phrase flow if confirming a delete
            if (state.ActiveIntent == "delete_recommendation")
            {
                await _convState.UpdateStateAsync(userId, ConversationState.AwaitingDeletePhrase, "delete_recommendation");
                return Reply("To confirm deletion, please type **DELETE MY RECOMMENDATION** exactly.");
            }

            // Commit pending changes
            var result = await CommitPendingChangesAsync(userId, state);
            await _convState.ClearPendingAsync(userId);
            return result;
        }

        if (IsNegative(normalized))
        {
            await _convState.ClearPendingAsync(userId);
            return Reply("No changes made.");
        }

        return Reply($"I need a **yes** or **no** to proceed.\n\n_{state.AwaitingConfirmationFor}_", requiresConfirmation: true);
    }

    private async Task<OrchestratorResponse> CommitPendingChangesAsync(Guid userId, ConvStateDocument state)
    {
        var pending = state.PendingChanges;
        var changeType = pending.GetValue("changeType", "").AsString;

        try
        {
            switch (changeType)
            {
                case "update_profile":
                {
                    var field = pending.GetValue("field", "").AsString;
                    var snapshot = System.Text.Json.JsonSerializer.Deserialize<ProfileSnapshot>(
                        pending.GetValue("snapshot", "{}").AsString);
                    if (snapshot is not null)
                    {
                        await _recommendation.UpdateProfileAsync(userId, snapshot);
                        return Reply($"✅ **{field}** updated successfully.");
                    }
                    break;
                }
                case "update_drugs":
                {
                    var drugsJson = pending.GetValue("drugs", "[]").AsString;
                    var drugs = System.Text.Json.JsonSerializer.Deserialize<List<SelectedDrugDoc>>(drugsJson);
                    if (drugs is not null)
                    {
                        await _recommendation.UpdateDrugsAsync(userId, drugs);
                        return Reply("✅ Drug list updated successfully.");
                    }
                    break;
                }
                case "update_pharmacy":
                {
                    var pharmacyJson = pending.GetValue("pharmacy", "{}").AsString;
                    var pharmacy = System.Text.Json.JsonSerializer.Deserialize<SelectedPharmacyDoc>(pharmacyJson);
                    var mailOrderJson = pending.GetValue("mailOrder", "").AsString;
                    var mailOrder = string.IsNullOrEmpty(mailOrderJson) ? null
                        : System.Text.Json.JsonSerializer.Deserialize<MailOrderPharmacyDoc>(mailOrderJson);
                    await _recommendation.UpdatePharmacyAsync(userId, pharmacy, mailOrder);
                    return Reply("✅ Pharmacy updated successfully.");
                }
                case "update_plans":
                {
                    var plansJson = pending.GetValue("plans", "[]").AsString;
                    var plans = System.Text.Json.JsonSerializer.Deserialize<List<SelectedPlanDoc>>(plansJson);
                    if (plans is not null)
                    {
                        await _recommendation.UpdatePlansAsync(userId, plans);
                        return Reply("✅ Plan selections updated successfully.");
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit pending changes for user {UserId}", userId);
            return Reply("Sorry, there was an error saving your changes. Please try again.");
        }

        return Reply("✅ Change confirmed and saved.");
    }

    private async Task<OrchestratorResponse> HandleDeletePhrase(Guid userId, string message, ConvStateDocument state)
    {
        if (message.Trim().Equals("DELETE MY RECOMMENDATION", StringComparison.OrdinalIgnoreCase))
        {
            var rec = await _recommendation.GetActiveAsync(userId);
            var name = rec?.Name ?? "Your recommendation";
            await _recommendation.DeleteAsync(userId);
            await _convState.ResetAsync(userId);
            return Reply($"🗑️ **{name}** has been permanently deleted.");
        }

        await _convState.ClearPendingAsync(userId);
        return Reply("Deletion cancelled. Your recommendation is safe.");
    }

    // ═══════════════════════════════════════════════════════════════
    //  Multi-turn collection stubs
    // ═══════════════════════════════════════════════════════════════

    private async Task<OrchestratorResponse> ContinueProfileCollection(Guid userId, string message, ConvStateDocument state)
    {
        var fields = state.CollectedFields;
        var step = fields.GetValue("_step", "name").AsString;
        var input = message.Trim();

        switch (step)
        {
            case "name":
            {
                var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    return Reply("Please enter your **first and last name** (e.g., \"John Smith\").");
                await _convState.SetCollectedFieldAsync(userId, "firstName", parts[0]);
                await _convState.SetCollectedFieldAsync(userId, "lastName", parts[1]);
                await _convState.SetCollectedFieldAsync(userId, "_step", "dob");
                return Reply($"Thanks, **{parts[0]} {parts[1]}**!\n\n**What is your date of birth?** (MM/DD/YYYY)");
            }

            case "dob":
            {
                if (!DateTime.TryParse(input, out var dob) || dob.Year < 1900 || dob > DateTime.UtcNow)
                    return Reply("Please enter a valid date of birth (e.g., 03/15/1955).");
                await _convState.SetCollectedFieldAsync(userId, "dob", dob.ToString("yyyy-MM-dd"));
                await _convState.SetCollectedFieldAsync(userId, "_step", "gender");
                return Reply("**What is your gender?** (Male or Female)");
            }

            case "gender":
            {
                var g = input.ToUpperInvariant();
                string gender;
                if (g.StartsWith('M')) gender = "M";
                else if (g.StartsWith('F')) gender = "F";
                else return Reply("Please enter **Male** or **Female**.");
                await _convState.SetCollectedFieldAsync(userId, "gender", gender);
                await _convState.SetCollectedFieldAsync(userId, "_step", "zip");
                return Reply("**What is your 5-digit ZIP code?**");
            }

            case "zip":
            {
                if (input.Length < 5 || !input.All(char.IsDigit))
                    return Reply("Please enter a valid 5-digit ZIP code.");
                var zip = input.PadLeft(5, '0');
                var entries = await _countyLookup.GetCountyCodeList(zip);
                if (entries.Count == 0)
                    return Reply($"ZIP code **{zip}** was not found. Please check and try again.");

                var entry = entries[0];
                await _convState.SetCollectedFieldAsync(userId, "zip", zip);
                await _convState.SetCollectedFieldAsync(userId, "county", entry.CountyName);
                await _convState.SetCollectedFieldAsync(userId, "countyCode", entry.CountyCode);
                await _convState.SetCollectedFieldAsync(userId, "state", entry.State);
                await _convState.SetCollectedFieldAsync(userId, "city", entry.City);
                await _convState.SetCollectedFieldAsync(userId, "lat", entry.Latitude.ToString());
                await _convState.SetCollectedFieldAsync(userId, "lng", entry.Longitude.ToString());
                await _convState.SetCollectedFieldAsync(userId, "_step", "health");

                return Reply(
                    $"📍 Found **{entry.City}, {entry.State} ({entry.CountyName} County)**.\n\n" +
                    "**How would you describe your health?**\n" +
                    "1. Best Health\n2. Good Health\n3. Fair Health\n4. Poor Health");
            }

            case "health":
            {
                var health = ParseHealthCondition(input);
                if (health == 0)
                    return Reply("Please enter **1** (Best), **2** (Good), **3** (Fair), or **4** (Poor).");
                await _convState.SetCollectedFieldAsync(userId, "health", health.ToString());
                await _convState.SetCollectedFieldAsync(userId, "_step", "lifeExpectancy");
                return Reply("**What age do you expect to live to?** (65–120, default is 95)");
            }

            case "lifeExpectancy":
            {
                if (!int.TryParse(input, out var le) || le < 65 || le > 120)
                    return Reply("Please enter a number between **65** and **120**.");
                await _convState.SetCollectedFieldAsync(userId, "lifeExpectancy", le.ToString());
                await _convState.SetCollectedFieldAsync(userId, "_step", "tobacco");
                return Reply("**Do you use tobacco?** (Yes or No)");
            }

            case "tobacco":
            {
                var lower = input.ToLowerInvariant();
                int tobacco;
                if (lower is "yes" or "y") tobacco = 1;
                else if (lower is "no" or "n") tobacco = 0;
                else return Reply("Please enter **Yes** or **No**.");
                await _convState.SetCollectedFieldAsync(userId, "tobacco", tobacco.ToString());
                await _convState.SetCollectedFieldAsync(userId, "_step", "taxFiling");
                return Reply(
                    "**What is your tax filing status?**\n" +
                    "1. Single\n2. Married Filing Jointly\n3. Married Filing Separately\n4. Head of Household");
            }

            case "taxFiling":
            {
                var status = ParseTaxFilingStatus(input);
                if (status is null)
                    return Reply("Please enter **1** (Single), **2** (Joint), **3** (Separate), or **4** (Head of Household).");
                await _convState.SetCollectedFieldAsync(userId, "taxFiling", status);
                await _convState.SetCollectedFieldAsync(userId, "_step", "magi");
                return Reply(
                    "**What is your MAGI tier (Modified Adjusted Gross Income)?**\n" +
                    "Enter your income amount or tier number (1–6). Tier 1 = ≤$103K single / ≤$206K joint.");
            }

            case "magi":
            {
                var magi = ParseMagiTier(input);
                if (magi is null)
                    return Reply("Please enter a tier number **1–6** or an income amount (e.g., 85000).");
                await _convState.SetCollectedFieldAsync(userId, "magi", magi);
                await _convState.SetCollectedFieldAsync(userId, "_step", "coverageYear");
                return Reply($"**What Medicare coverage year?** (default: {DateTime.UtcNow.Year})");
            }

            case "coverageYear":
            {
                var year = DateTime.UtcNow.Year;
                if (!string.IsNullOrWhiteSpace(input) && input != "default")
                {
                    if (!int.TryParse(input, out year) || year < DateTime.UtcNow.Year || year > DateTime.UtcNow.Year + 5)
                        return Reply($"Please enter a year between **{DateTime.UtcNow.Year}** and **{DateTime.UtcNow.Year + 5}**.");
                }
                await _convState.SetCollectedFieldAsync(userId, "coverageYear", year.ToString());
                await _convState.SetCollectedFieldAsync(userId, "_step", "concierge");
                return Reply("**Would you like concierge service?** (Yes or No)\nConcierge provides personalized Medicare support for a monthly fee.");
            }

            case "concierge":
            {
                var lower = input.ToLowerInvariant();
                if (lower is "yes" or "y")
                {
                    await _convState.SetCollectedFieldAsync(userId, "concierge", "1");
                    await _convState.SetCollectedFieldAsync(userId, "_step", "conciergeAmount");
                    return Reply("**How much per month for concierge?** (e.g., 200)");
                }
                if (lower is "no" or "n")
                {
                    await _convState.SetCollectedFieldAsync(userId, "concierge", "0");
                    return await FinalizeProfileCollection(userId, state);
                }
                return Reply("Please enter **Yes** or **No**.");
            }

            case "conciergeAmount":
            {
                if (!decimal.TryParse(input.Replace("$", ""), out var amount) || amount <= 0)
                    return Reply("Please enter a valid monthly amount (e.g., 200).");
                await _convState.SetCollectedFieldAsync(userId, "conciergeAmount", amount.ToString());
                return await FinalizeProfileCollection(userId, state);
            }

            default:
                await _convState.ResetAsync(userId);
                return Reply("Something went wrong. Let's start over — type **create recommendation**.");
        }
    }

    private async Task<OrchestratorResponse> FinalizeProfileCollection(Guid userId, ConvStateDocument state)
    {
        // Reload to get latest collected fields
        state = await _convState.GetOrCreateAsync(userId);
        var f = state.CollectedFields;

        var snapshot = new ProfileSnapshot
        {
            RecommendationName = $"Medicare Plan — {DateTime.UtcNow:MMM yyyy}",
            FirstName = f.GetValue("firstName", "").AsString,
            LastName = f.GetValue("lastName", "").AsString,
            DateOfBirth = DateOnly.TryParse(f.GetValue("dob", "").AsString, out var dob) ? dob : default,
            Gender = f.GetValue("gender", "").AsString,
            ZipCode = f.GetValue("zip", "").AsString,
            County = f.GetValue("county", "").AsString,
            CountyCode = f.GetValue("countyCode", "").AsString,
            State = f.GetValue("state", "").AsString,
            City = f.GetValue("city", "").AsString,
            HealthCondition = int.TryParse(f.GetValue("health", "1").AsString, out var h) ? h : 1,
            LifeExpectancy = int.TryParse(f.GetValue("lifeExpectancy", "95").AsString, out var le) ? le : 95,
            TobaccoStatus = int.TryParse(f.GetValue("tobacco", "0").AsString, out var t) ? t : 0,
            TaxFilingStatus = f.GetValue("taxFiling", "").AsString,
            MagiTier = f.GetValue("magi", "1").AsString,
            CoverageYear = int.TryParse(f.GetValue("coverageYear", DateTime.UtcNow.Year.ToString()).AsString, out var cy) ? cy : DateTime.UtcNow.Year,
            Concierge = int.TryParse(f.GetValue("concierge", "0").AsString, out var c) ? c : 0,
            ConciergeAmount = decimal.TryParse(f.GetValue("conciergeAmount", "0").AsString, out var ca) ? ca : null,
            Latitude = double.TryParse(f.GetValue("lat", "0").AsString, out var lat) ? lat : null,
            Longitude = double.TryParse(f.GetValue("lng", "0").AsString, out var lng) ? lng : null
        };

        var doc = new RecommendationDocument
        {
            UserId = userId,
            Name = snapshot.RecommendationName,
            Profile = snapshot
        };

        await _recommendation.CreateAsync(userId, doc);
        await _convState.ResetAsync(userId);

        return Reply(
            $"✅ Recommendation **\"{doc.Name}\"** created!\n\n" +
            $"**{snapshot.FirstName} {snapshot.LastName}** | {snapshot.Gender} | ZIP: {snapshot.ZipCode} — {snapshot.City}, {snapshot.State}\n" +
            $"Health: {HealthLabel(snapshot.HealthCondition)} | Life Expectancy: {snapshot.LifeExpectancy}\n\n" +
            "Your recommendation is ready. Next steps:\n" +
            "- **Add drugs** — tell me your prescriptions\n" +
            "- **Find pharmacies** — say \"find pharmacies\"\n" +
            "- **Compare plans** — say \"compare plans\"\n\n" +
            "Or type **help** to see all options.");
    }

    private async Task<OrchestratorResponse> ContinueDrugCollection(Guid userId, string message, ConvStateDocument state)
    {
        // Drug collection is simple: user lists drugs, we add them
        var rec = await _recommendation.GetActiveAsync(userId);
        if (rec is null)
        {
            await _convState.ResetAsync(userId);
            return NoRecommendationReply();
        }

        var lower = message.Trim().ToLowerInvariant();
        if (lower is "done" or "skip" or "no" or "no more")
        {
            await _convState.ResetAsync(userId);
            var count = rec.DrugList.Count;
            return Reply(count > 0
                ? $"Drug collection complete. You have **{count}** drug(s) in your list."
                : "No drugs added. You can add drugs later by saying **\"add [drug name]\"**.");
        }

        // Parse as drug names — add them to the list
        var drugs = new List<SelectedDrugDoc>(rec.DrugList);
        var names = message.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var name in names)
        {
            drugs.Add(new SelectedDrugDoc
            {
                DrugName = name.Trim(),
                Quantity = 30,
                RefillFrequency = "30 days"
            });
        }

        await _recommendation.UpdateDrugsAsync(userId, drugs);

        return Reply(
            $"Added **{names.Length}** drug(s). You now have **{drugs.Count}** total.\n\n" +
            "Enter more drug names separated by commas, or type **done** to finish.");
    }

    private async Task<OrchestratorResponse> ContinuePharmacyCollection(Guid userId, string message, ConvStateDocument state)
    {
        var rec = await _recommendation.GetActiveAsync(userId);
        if (rec is null)
        {
            await _convState.ResetAsync(userId);
            return NoRecommendationReply();
        }

        var lower = message.Trim().ToLowerInvariant();
        if (lower is "cancel" or "skip" or "nevermind")
        {
            await _convState.ResetAsync(userId);
            return Reply("Pharmacy selection cancelled.");
        }

        // Parse numeric selection from displayed pharmacy list
        if (int.TryParse(message.Trim(), out var selection))
        {
            var reloaded = await _convState.GetOrCreateAsync(userId);
            var optionsJson = reloaded.CollectedFields.GetValue("pharmacyOptions", "[]").AsString;
            var options = System.Text.Json.JsonSerializer.Deserialize<List<PharmacyLookupEntry>>(optionsJson);

            if (options is null || selection < 1 || selection > options.Count)
                return Reply($"Please enter a number between **1** and **{options?.Count ?? 0}**.");

            var selected = options[selection - 1];
            var pharmacy = new SelectedPharmacyDoc
            {
                Npi = selected.PharmacyNumber.ToString(),
                Name = selected.PharmacyName,
                Address = selected.Address,
                ZipCode = selected.Zipcode.ToString().PadLeft(5, '0'),
                Distance = selected.Distance
            };

            await _recommendation.UpdatePharmacyAsync(userId, pharmacy);
            await _convState.ResetAsync(userId);

            return Reply($"✅ Pharmacy set to **{selected.PharmacyName}** — {selected.Address} ({selected.Distance:F1} mi).");
        }

        return Reply("Please enter the **number** of the pharmacy you'd like to select, or type **cancel**.");
    }

    private async Task<OrchestratorResponse> ContinuePlanCollection(Guid userId, string message, ConvStateDocument state)
    {
        // Plan collection placeholder — actual plan searching requires plan recommendation API
        var lower = message.Trim().ToLowerInvariant();
        if (lower is "done" or "skip" or "cancel")
        {
            await _convState.ResetAsync(userId);
            return Reply("Plan selection cancelled. You can compare plans anytime by saying **\"compare plans\"**.");
        }

        await _convState.ResetAsync(userId);
        return Reply("Plan selection from chat is not yet supported. Please say **\"compare plans\"** to see available options.");
    }

    // ═══════════════════════════════════════════════════════════════
    //  Intent handlers — Phase 1 UCs (stubs for now)
    // ═══════════════════════════════════════════════════════════════

    private async Task<OrchestratorResponse> HandleCreateRecommendation(Guid userId, OrchestratorIntentResult intent, ConvStateDocument state)
    {
        // Load profile to pre-populate
        var profileResponse = await _profileService.GetProfileAsync(userId);
        if (profileResponse.IsProfileComplete && profileResponse.Profile is not null)
        {
            // Auto-populate from existing profile
            var p = profileResponse.Profile;
            var countyEntries = await _countyLookup.GetCountyCodeList(p.ZipCode);
            var county = countyEntries.FirstOrDefault();

            var snapshot = new ProfileSnapshot
            {
                RecommendationName = $"Medicare Plan — {DateTime.UtcNow:MMM yyyy}",
                FirstName = p.FirstName,
                LastName = p.LastName,
                DateOfBirth = DateOnly.TryParse(p.DateOfBirth, out var dob) ? dob : default,
                Gender = p.Gender,
                ZipCode = p.ZipCode,
                County = county?.CountyName ?? p.County ?? "",
                CountyCode = county?.CountyCode ?? p.CountyCode ?? "",
                State = county?.State ?? p.State,
                City = county?.City ?? p.City,
                AddressLine1 = p.AddressLine1,
                HealthCondition = p.HealthCondition,
                LifeExpectancy = p.LifeExpectancy,
                TobaccoStatus = p.TobaccoStatus,
                TaxFilingStatus = p.TaxFilingStatus,
                MagiTier = p.MagiTier,
                CoverageYear = p.CoverageYear,
                Concierge = p.Concierge,
                ConciergeAmount = p.ConciergeAmount,
                Latitude = county?.Latitude ?? p.Latitude,
                Longitude = county?.Longitude ?? p.Longitude
            };

            var doc = new RecommendationDocument
            {
                UserId = userId,
                Name = snapshot.RecommendationName,
                Profile = snapshot
            };

            await _recommendation.CreateAsync(userId, doc);

            var summary =
                $"✅ Recommendation **\"{doc.Name}\"** created using your existing profile.\n\n" +
                $"**{snapshot.FirstName} {snapshot.LastName}** | {snapshot.Gender} | ZIP: {snapshot.ZipCode} — {snapshot.City}, {snapshot.State}\n" +
                $"Health: {HealthLabel(snapshot.HealthCondition)} | Life Expectancy: {snapshot.LifeExpectancy}\n\n" +
                "Next steps:\n" +
                "- **Add drugs** — tell me your prescriptions\n" +
                "- **Find pharmacies** — say \"find pharmacies\"\n" +
                "- **View plans** — say \"compare plans\"\n\n" +
                "Or type **help** to see all options.";

            return Reply(summary);
        }

        // Profile incomplete — start multi-turn collection
        await _convState.UpdateStateAsync(userId, ConversationState.CollectingProfile, "create_recommendation");
        state.CollectedFields = new BsonDocument();
        await _convState.SetCollectedFieldAsync(userId, "_step", "name");

        return Reply(
            "Let's create your Medicare recommendation! I'll walk you through a few questions.\n\n" +
            "**What is your first and last name?**");
    }

    private async Task<OrchestratorResponse> HandleUpdateDemographic(Guid userId, OrchestratorIntentResult intent, ConvStateDocument state)
    {
        var rec = await _recommendation.GetActiveAsync(userId);
        if (rec is null) return NoRecommendationReply();

        var field = intent.Params.GetValueOrDefault("field", "")?.ToLowerInvariant() ?? "";
        var value = intent.Params.GetValueOrDefault("value");

        switch (field)
        {
            case "zip" or "zipcode" or "zip_code":
            {
                if (string.IsNullOrEmpty(value))
                    return Reply("What ZIP code would you like to change to?");

                var zip = value.PadLeft(5, '0');
                var entries = await _countyLookup.GetCountyCodeList(zip);
                if (entries.Count == 0)
                    return Reply($"ZIP code **{zip}** was not found. Please enter a valid 5-digit ZIP code.");

                var entry = entries[0];
                var updated = CloneProfile(rec.Profile);
                updated.ZipCode = zip;
                updated.County = entry.CountyName;
                updated.CountyCode = entry.CountyCode;
                updated.State = entry.State;
                updated.City = entry.City;
                updated.Latitude = entry.Latitude;
                updated.Longitude = entry.Longitude;

                return await ProposeProfileChange(userId, rec, updated, "ZIP code",
                    rec.Profile.ZipCode, $"{zip} — {entry.City}, {entry.State}");
            }

            case "dob" or "dateofbirth" or "date_of_birth" or "birthday":
            {
                if (string.IsNullOrEmpty(value) || !DateTime.TryParse(value, out var dob))
                    return Reply("What is your new date of birth? (MM/DD/YYYY)");

                var updated = CloneProfile(rec.Profile);
                updated.DateOfBirth = DateOnly.FromDateTime(dob);
                return await ProposeProfileChange(userId, rec, updated, "date of birth",
                    rec.Profile.DateOfBirth.ToString("MM/dd/yyyy"), dob.ToString("MM/dd/yyyy"));
            }

            case "gender":
            {
                if (string.IsNullOrEmpty(value))
                    return Reply("What gender would you like to update to? (Male or Female)");
                var g = value.ToUpperInvariant().StartsWith('M') ? "M" : value.ToUpperInvariant().StartsWith('F') ? "F" : null;
                if (g is null) return Reply("Please enter **Male** or **Female**.");

                var updated = CloneProfile(rec.Profile);
                updated.Gender = g;
                return await ProposeProfileChange(userId, rec, updated, "gender",
                    rec.Profile.Gender == "M" ? "Male" : "Female", g == "M" ? "Male" : "Female");
            }

            case "name":
            {
                if (string.IsNullOrEmpty(value))
                    return Reply("What would you like to change your name to?");
                var parts = value.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var updated = CloneProfile(rec.Profile);
                updated.FirstName = parts[0];
                if (parts.Length > 1) updated.LastName = parts[1];

                // Name change doesn't affect costs — no delta needed
                var snapshot = System.Text.Json.JsonSerializer.Serialize(updated);
                var pending = new BsonDocument
                {
                    ["changeType"] = "update_profile",
                    ["field"] = "name",
                    ["snapshot"] = snapshot
                };
                await _convState.SetPendingChangeAsync(userId,
                    $"Change name from \"{rec.Profile.FirstName} {rec.Profile.LastName}\" to \"{updated.FirstName} {updated.LastName}\"",
                    pending);
                return Reply(
                    $"Change name from **{rec.Profile.FirstName} {rec.Profile.LastName}** to **{updated.FirstName} {updated.LastName}**?\n\n" +
                    "This won't affect your cost projections.",
                    requiresConfirmation: true);
            }

            default:
                return Reply("Which demographic field would you like to update? I can change your **name**, **date of birth**, **gender**, or **ZIP code**.");
        }
    }

    private async Task<OrchestratorResponse> HandleUpdateHealthFinancial(Guid userId, OrchestratorIntentResult intent, ConvStateDocument state)
    {
        var rec = await _recommendation.GetActiveAsync(userId);
        if (rec is null) return NoRecommendationReply();

        var field = intent.Params.GetValueOrDefault("field", "")?.ToLowerInvariant() ?? "";
        var value = intent.Params.GetValueOrDefault("value");

        switch (field)
        {
            case "health" or "healthcondition" or "health_condition":
            {
                if (string.IsNullOrEmpty(value))
                    return Reply("How would you describe your health?\n1. Best Health\n2. Good Health\n3. Fair Health\n4. Poor Health");

                var health = ParseHealthCondition(value);
                if (health == 0)
                    return Reply("Please enter **1** (Best), **2** (Good), **3** (Fair), or **4** (Poor).");

                var updated = CloneProfile(rec.Profile);
                updated.HealthCondition = health;
                return await ProposeProfileChange(userId, rec, updated, "health condition",
                    HealthLabel(rec.Profile.HealthCondition), HealthLabel(health));
            }

            case "tobacco" or "smoking":
            {
                if (string.IsNullOrEmpty(value))
                    return Reply("Do you use tobacco? (**Yes** or **No**)");

                var lower = value.ToLowerInvariant();
                int tobacco;
                if (lower is "yes" or "y" or "1") tobacco = 1;
                else if (lower is "no" or "n" or "0") tobacco = 0;
                else return Reply("Please enter **Yes** or **No**.");

                var updated = CloneProfile(rec.Profile);
                updated.TobaccoStatus = tobacco;
                return await ProposeProfileChange(userId, rec, updated, "tobacco status",
                    rec.Profile.TobaccoStatus == 1 ? "Yes" : "No", tobacco == 1 ? "Yes" : "No");
            }

            default:
                return Reply("Which health/financial field would you like to update? I can change your **health condition** or **tobacco status**.\n\nFor tax filing, MAGI, life expectancy, or concierge — try those specific commands.");
        }
    }

    private async Task<OrchestratorResponse> HandleModifyDrugs(Guid userId, OrchestratorIntentResult intent, ConvStateDocument state)
    {
        var rec = await _recommendation.GetActiveAsync(userId);
        if (rec is null) return NoRecommendationReply();

        var action = intent.Params.GetValueOrDefault("action", "")?.ToLowerInvariant() ?? "";
        var drugName = intent.Params.GetValueOrDefault("drugName") ?? "";
        var dosage = intent.Params.GetValueOrDefault("dosage") ?? "";
        var quantity = intent.Params.GetValueOrDefault("quantity") ?? "";
        var frequency = intent.Params.GetValueOrDefault("frequency") ?? "";

        switch (action)
        {
            case "add":
            {
                if (string.IsNullOrEmpty(drugName))
                    return Reply("What drug would you like to add? (e.g., \"add Eliquis 5mg\")");

                var newDrug = new SelectedDrugDoc
                {
                    DrugName = drugName,
                    Dosage = string.IsNullOrEmpty(dosage) ? "" : dosage,
                    Quantity = int.TryParse(quantity, out var q) ? q : 30,
                    RefillFrequency = string.IsNullOrEmpty(frequency) ? "30 days" : frequency
                };

                var updatedDrugs = new List<SelectedDrugDoc>(rec.DrugList) { newDrug };
                var drugsJson = System.Text.Json.JsonSerializer.Serialize(updatedDrugs);

                var pending = new BsonDocument
                {
                    ["changeType"] = "update_drugs",
                    ["drugs"] = drugsJson
                };

                var desc = $"Add **{drugName}**" +
                    (string.IsNullOrEmpty(dosage) ? "" : $" {dosage}") +
                    $" (Qty {newDrug.Quantity}, {newDrug.RefillFrequency}) to your drug list";

                await _convState.SetPendingChangeAsync(userId, desc, pending);
                return Reply(
                    $"{desc}?\n\nYou currently have **{rec.DrugList.Count}** drug(s). This would make it **{updatedDrugs.Count}**.",
                    requiresConfirmation: true);
            }

            case "remove":
            {
                if (rec.DrugList.Count == 0)
                    return Reply("You don't have any drugs in your list to remove.");

                if (string.IsNullOrEmpty(drugName))
                {
                    var drugList = FormatDrugList(rec.DrugList);
                    return Reply($"Which drug would you like to remove?\n\n{drugList}");
                }

                var match = rec.DrugList.FirstOrDefault(d =>
                    d.DrugName.Contains(drugName, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                    return Reply($"**{drugName}** was not found in your drug list.\n\n{FormatDrugList(rec.DrugList)}");

                var updatedDrugs = rec.DrugList.Where(d => d != match).ToList();
                var drugsJson = System.Text.Json.JsonSerializer.Serialize(updatedDrugs);

                var pending = new BsonDocument
                {
                    ["changeType"] = "update_drugs",
                    ["drugs"] = drugsJson
                };
                await _convState.SetPendingChangeAsync(userId,
                    $"Remove **{match.DrugName} {match.Dosage}** from your drug list", pending);
                return Reply(
                    $"Remove **{match.DrugName} {match.Dosage}** (Qty {match.Quantity}, {match.RefillFrequency})?\n\n" +
                    $"You'll have **{updatedDrugs.Count}** drug(s) remaining.",
                    requiresConfirmation: true);
            }

            case "change" or "modify" or "edit":
            {
                if (string.IsNullOrEmpty(drugName))
                {
                    var drugList = FormatDrugList(rec.DrugList);
                    return Reply($"Which drug would you like to modify?\n\n{drugList}");
                }

                var match = rec.DrugList.FirstOrDefault(d =>
                    d.DrugName.Contains(drugName, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                    return Reply($"**{drugName}** was not found in your drug list.\n\n{FormatDrugList(rec.DrugList)}");

                var updatedDrug = new SelectedDrugDoc
                {
                    DrugName = match.DrugName,
                    Dosage = string.IsNullOrEmpty(dosage) ? match.Dosage : dosage,
                    Quantity = int.TryParse(quantity, out var q2) ? q2 : match.Quantity,
                    RefillFrequency = string.IsNullOrEmpty(frequency) ? match.RefillFrequency : frequency,
                    Rxcui = match.Rxcui,
                    NdcCode = match.NdcCode
                };

                var updatedDrugs = rec.DrugList.Select(d => d == match ? updatedDrug : d).ToList();
                var drugsJson = System.Text.Json.JsonSerializer.Serialize(updatedDrugs);

                var pending = new BsonDocument
                {
                    ["changeType"] = "update_drugs",
                    ["drugs"] = drugsJson
                };
                await _convState.SetPendingChangeAsync(userId,
                    $"Update **{match.DrugName}** — dosage: {updatedDrug.Dosage}, qty: {updatedDrug.Quantity}, frequency: {updatedDrug.RefillFrequency}", pending);
                return Reply(
                    $"Update **{match.DrugName}**:\n" +
                    $"- Dosage: {match.Dosage} → **{updatedDrug.Dosage}**\n" +
                    $"- Quantity: {match.Quantity} → **{updatedDrug.Quantity}**\n" +
                    $"- Frequency: {match.RefillFrequency} → **{updatedDrug.RefillFrequency}**",
                    requiresConfirmation: true);
            }

            default:
            {
                // No action specified — show current drugs and prompt
                if (rec.DrugList.Count == 0)
                    return Reply("Your drug list is empty. Say **\"add Eliquis 5mg\"** to add a drug.");

                var drugList = FormatDrugList(rec.DrugList);
                return Reply($"Your current drugs:\n\n{drugList}\n\nYou can **add**, **remove**, or **modify** a drug.");
            }
        }
    }

    private async Task<OrchestratorResponse> HandleModifyPharmacy(Guid userId, OrchestratorIntentResult intent, ConvStateDocument state)
    {
        var rec = await _recommendation.GetActiveAsync(userId);
        if (rec is null) return NoRecommendationReply();

        var action = intent.Params.GetValueOrDefault("action", "")?.ToLowerInvariant() ?? "";

        switch (action)
        {
            case "enable_mail_order":
            {
                var mailOrder = new MailOrderPharmacyDoc
                {
                    Enabled = true,
                    Name = "Mail Order Pharmacy"
                };
                var mailOrderJson = System.Text.Json.JsonSerializer.Serialize(mailOrder);
                var pharmacyJson = rec.Pharmacy is not null
                    ? System.Text.Json.JsonSerializer.Serialize(rec.Pharmacy)
                    : "";

                var pending = new BsonDocument
                {
                    ["changeType"] = "update_pharmacy",
                    ["pharmacy"] = pharmacyJson,
                    ["mailOrder"] = mailOrderJson
                };
                await _convState.SetPendingChangeAsync(userId, "Enable mail-order pharmacy", pending);
                return Reply(
                    "Enable **mail-order pharmacy**? This can lower costs for maintenance medications (typically 90-day supply at lower copay).",
                    requiresConfirmation: true);
            }

            case "disable_mail_order":
            {
                var mailOrder = new MailOrderPharmacyDoc { Enabled = false };
                var mailOrderJson = System.Text.Json.JsonSerializer.Serialize(mailOrder);
                var pharmacyJson = rec.Pharmacy is not null
                    ? System.Text.Json.JsonSerializer.Serialize(rec.Pharmacy)
                    : "";

                var pending = new BsonDocument
                {
                    ["changeType"] = "update_pharmacy",
                    ["pharmacy"] = pharmacyJson,
                    ["mailOrder"] = mailOrderJson
                };
                await _convState.SetPendingChangeAsync(userId, "Disable mail-order pharmacy", pending);
                return Reply("Disable **mail-order pharmacy**?", requiresConfirmation: true);
            }

            case "change":
            default:
            {
                // Start pharmacy search — need lat/lng from profile
                var lat = rec.Profile.Latitude ?? 0;
                var lng = rec.Profile.Longitude ?? 0;

                if (lat == 0 || lng == 0)
                    return Reply("Your profile doesn't have location data. Please update your **ZIP code** first.");

                var result = await _pharmacyLookup.GetPharmaciesAsync(new PharmacyLookupRequest
                {
                    Latitude = lat,
                    Longitude = lng,
                    SearchRadiusInMiles = 10,
                    Page = 1,
                    Size = 5
                });

                if (result.Pharmacies.Count == 0)
                    return Reply("No pharmacies found within 10 miles. Try updating your ZIP code.");

                // Store pharmacies in collected fields for selection
                await _convState.UpdateStateAsync(userId, ConversationState.CollectingPharmacy, "modify_pharmacy");
                var pharmacyListJson = System.Text.Json.JsonSerializer.Serialize(result.Pharmacies);
                await _convState.SetCollectedFieldAsync(userId, "pharmacyOptions", pharmacyListJson);

                var current = rec.Pharmacy is not null
                    ? $"Current: **{rec.Pharmacy.Name}** — {rec.Pharmacy.Address}\n\n"
                    : "No pharmacy currently selected.\n\n";

                var lines = new List<string> { current + "Nearby pharmacies:" };
                for (int i = 0; i < result.Pharmacies.Count; i++)
                {
                    var p = result.Pharmacies[i];
                    lines.Add($"{i + 1}. **{p.PharmacyName}** — {p.Address} ({p.Distance:F1} mi)");
                }
                lines.Add("\nEnter the **number** of the pharmacy you'd like to select.");

                return Reply(string.Join("\n", lines));
            }
        }
    }

    private async Task<OrchestratorResponse> HandleModifyPlans(Guid userId, OrchestratorIntentResult intent, ConvStateDocument state)
    {
        var rec = await _recommendation.GetActiveAsync(userId);
        if (rec is null) return NoRecommendationReply();

        if (rec.PlanSelections.Count == 0)
            return Reply("You don't have any plan selections yet. Say **\"compare plans\"** to see available options.");

        var planType = intent.Params.GetValueOrDefault("planType", "")?.ToLowerInvariant() ?? "";

        // Show current selections with option to change
        var lines = new List<string> { "### Current Plan Selections\n" };
        foreach (var plan in rec.PlanSelections)
        {
            var suffix = plan.MedigapPlanType is not null ? $" (Plan {plan.MedigapPlanType})" : "";
            lines.Add($"- **{plan.PlanType}:** {plan.PlanName} — {plan.Carrier}{suffix} — ${plan.MonthlyPremium}/mo");
        }

        lines.Add("\nTo change a plan, say **\"compare plans\"** to see alternatives, or specify which plan type to modify (PDP, MA, or Medigap).");

        return Reply(string.Join("\n", lines));
    }

    private async Task<OrchestratorResponse> HandleDeleteRecommendation(Guid userId, OrchestratorIntentResult intent, ConvStateDocument state)
    {
        var rec = await _recommendation.GetActiveAsync(userId);
        if (rec is null)
            return Reply("You don't have an active recommendation to delete.");

        await _convState.UpdateStateAsync(userId, ConversationState.AwaitingConfirmation, "delete_recommendation");

        return Reply(
            $"You are about to permanently delete **\"{rec.Name}\"**. " +
            "This will remove your full profile, all plan selections, prescription list, pharmacy, and all projections. " +
            "**This cannot be undone.** Are you sure?",
            requiresConfirmation: true);
    }

    private async Task<OrchestratorResponse> HandleViewSummary(Guid userId, OrchestratorIntentResult intent, ConvStateDocument state)
    {
        var rec = await _recommendation.GetActiveAsync(userId);
        if (rec is null)
            return Reply("You don't have an active recommendation yet. Would you like to create one?");

        var summary = FormatSummary(rec);
        return Reply(summary, displayData: new DisplayData { Type = "summary", Payload = rec });
    }

    private Task<OrchestratorResponse> HandleComparePlans(Guid userId, OrchestratorIntentResult intent, ConvStateDocument state)
    {
        // Plan comparison requires plan recommendation API calls that are integrated in the existing wizard flow.
        // For now, show current selections and guide user.
        return HandleModifyPlans(userId, intent, state);
    }

    private async Task<OrchestratorResponse> HandleViewProjections(Guid userId, OrchestratorIntentResult intent, ConvStateDocument state)
    {
        var rec = await _recommendation.GetActiveAsync(userId);
        if (rec is null) return NoRecommendationReply();

        if (rec.LastCostSnapshot is null)
            return Reply("No cost projections available yet. Your recommendation needs plan selections to calculate costs.");

        var c = rec.LastCostSnapshot;
        var p = rec.Profile;
        var yearsRemaining = p.LifeExpectancy - (DateTime.UtcNow.Year - p.DateOfBirth.Year);

        var projection =
            $"## Lifetime Cost Projections\n\n" +
            $"Coverage period: **{p.CoverageYear}** to **{p.LifeExpectancy}** ({yearsRemaining} years)\n\n" +
            $"| Category | Amount |\n|---|---|\n" +
            $"| **Lifetime Total** | **${c.LifetimeTotal:N0}** |\n" +
            $"| Premiums | ${c.LifetimePremiums:N0} |\n" +
            $"| Out-of-Pocket | ${c.LifetimeOop:N0} |\n" +
            $"| IRMAA Surcharges | ${c.LifetimeIrmaa:N0} |\n" +
            $"| **Present Value** | **${c.PresentValue:N0}** |\n" +
            $"| Current Year ({p.CoverageYear}) | ${c.CurrentYearTotal:N0} |\n\n" +
            $"_Calculated {c.CalculatedAt:MMM d, yyyy}_\n\n" +
            "💡 Try changing a profile field (ZIP, health, life expectancy) to see how it affects your costs.\n\n" +
            "---\n" +
            "_⚠️ These are estimates based on current CMS data and actuarial assumptions. " +
            "Actual costs may vary. Consult a licensed financial advisor or Medicare counselor before making decisions._";

        return Reply(projection, displayData: new DisplayData { Type = "projections", Payload = c });
    }

    // ═══════════════════════════════════════════════════════════════
    //  Intent handlers — Phase 2 UCs (stubs)
    // ═══════════════════════════════════════════════════════════════

    private async Task<OrchestratorResponse> HandleUpdateTaxFiling(Guid userId, OrchestratorIntentResult intent, ConvStateDocument state)
    {
        var rec = await _recommendation.GetActiveAsync(userId);
        if (rec is null) return NoRecommendationReply();

        var value = intent.Params.GetValueOrDefault("value");
        if (string.IsNullOrEmpty(value))
            return Reply("What tax filing status?\n1. Single\n2. Married Filing Jointly\n3. Married Filing Separately\n4. Head of Household");

        var status = ParseTaxFilingStatus(value);
        if (status is null)
            return Reply("Please enter **1** (Single), **2** (Joint), **3** (Separate), or **4** (Head of Household).");

        var updated = CloneProfile(rec.Profile);
        updated.TaxFilingStatus = status;
        return await ProposeProfileChange(userId, rec, updated, "tax filing status",
            rec.Profile.TaxFilingStatus, status);
    }

    private async Task<OrchestratorResponse> HandleUpdateMagi(Guid userId, OrchestratorIntentResult intent, ConvStateDocument state)
    {
        var rec = await _recommendation.GetActiveAsync(userId);
        if (rec is null) return NoRecommendationReply();

        var value = intent.Params.GetValueOrDefault("value");
        if (string.IsNullOrEmpty(value))
            return Reply("What is your MAGI tier? Enter a tier number **1–6** or an income amount (e.g., 85000).\nTier 1 = ≤$103K single / ≤$206K joint.");

        var magi = ParseMagiTier(value);
        if (magi is null)
            return Reply("Please enter a tier number **1–6** or an income amount.");

        var updated = CloneProfile(rec.Profile);
        updated.MagiTier = magi;
        return await ProposeProfileChange(userId, rec, updated, "MAGI tier",
            rec.Profile.MagiTier, magi);
    }

    private async Task<OrchestratorResponse> HandleUpdateLifeExpectancy(Guid userId, OrchestratorIntentResult intent, ConvStateDocument state)
    {
        var rec = await _recommendation.GetActiveAsync(userId);
        if (rec is null) return NoRecommendationReply();

        var value = intent.Params.GetValueOrDefault("value");
        if (string.IsNullOrEmpty(value) || !int.TryParse(value, out var le) || le < 65 || le > 120)
            return Reply("What life expectancy age? Enter a number between **65** and **120** (current: " + rec.Profile.LifeExpectancy + ").");

        var updated = CloneProfile(rec.Profile);
        updated.LifeExpectancy = le;
        return await ProposeProfileChange(userId, rec, updated, "life expectancy",
            rec.Profile.LifeExpectancy.ToString(), le.ToString());
    }

    private async Task<OrchestratorResponse> HandleViewPlanDetails(Guid userId, OrchestratorIntentResult intent, ConvStateDocument state)
    {
        var rec = await _recommendation.GetActiveAsync(userId);
        if (rec is null) return NoRecommendationReply();

        if (rec.PlanSelections.Count == 0)
            return Reply("You don't have any plan selections. Say **\"compare plans\"** to see options.");

        var planName = intent.Params.GetValueOrDefault("planName") ?? "";
        SelectedPlanDoc? target = null;

        if (!string.IsNullOrEmpty(planName))
            target = rec.PlanSelections.FirstOrDefault(p =>
                p.PlanName.Contains(planName, StringComparison.OrdinalIgnoreCase) ||
                p.Carrier.Contains(planName, StringComparison.OrdinalIgnoreCase));

        target ??= rec.PlanSelections[0];

        var suffix = target.MedigapPlanType is not null ? $" (Plan {target.MedigapPlanType})" : "";
        var detail =
            $"## {target.PlanName}{suffix}\n\n" +
            $"**Type:** {target.PlanType}\n" +
            $"**Carrier:** {target.Carrier}\n" +
            $"**Monthly Premium:** ${target.MonthlyPremium}/mo (${target.MonthlyPremium * 12:N0}/yr)\n" +
            $"**Plan ID:** {target.PlanId}\n\n" +
            "For complete formulary and benefit details, visit the plan's official website or say **\"check drug coverage\"** to see if your drugs are covered.";

        return Reply(detail, displayData: new DisplayData { Type = "plan_detail", Payload = target });
    }

    private async Task<OrchestratorResponse> HandleFilterSortPlans(Guid userId, OrchestratorIntentResult intent, ConvStateDocument state)
    {
        var rec = await _recommendation.GetActiveAsync(userId);
        if (rec is null) return NoRecommendationReply();

        return Reply("Plan filtering and sorting is available through the **Compare Plans** feature. Say **\"compare plans\"** to see all available options sorted by cost.");
    }

    private async Task<OrchestratorResponse> HandleCheckDrugCoverage(Guid userId, OrchestratorIntentResult intent, ConvStateDocument state)
    {
        var rec = await _recommendation.GetActiveAsync(userId);
        if (rec is null) return NoRecommendationReply();

        var drugName = intent.Params.GetValueOrDefault("drugName") ?? "";

        if (rec.DrugList.Count == 0)
            return Reply("You don't have any drugs in your list. Say **\"add [drug name]\"** to add one.");

        if (rec.PlanSelections.Count == 0)
            return Reply("You don't have any plan selections. Choose a plan first to check drug coverage.");

        if (!string.IsNullOrEmpty(drugName))
        {
            var match = rec.DrugList.FirstOrDefault(d =>
                d.DrugName.Contains(drugName, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                return Reply($"**{drugName}** is not in your drug list. Your drugs: {string.Join(", ", rec.DrugList.Select(d => d.DrugName))}");
        }

        return Reply(
            "Drug coverage check requires formulary data from the selected plan.\n\n" +
            "Your current drugs:\n" +
            FormatDrugList(rec.DrugList) + "\n\n" +
            "For detailed coverage information, check your plan's formulary at the carrier's website.");
    }

    private async Task<OrchestratorResponse> HandleUpdateConcierge(Guid userId, OrchestratorIntentResult intent, ConvStateDocument state)
    {
        var rec = await _recommendation.GetActiveAsync(userId);
        if (rec is null) return NoRecommendationReply();

        var enabled = intent.Params.GetValueOrDefault("enabled")?.ToLowerInvariant();
        var amountStr = intent.Params.GetValueOrDefault("amount");

        var updated = CloneProfile(rec.Profile);

        if (enabled == "false" || enabled == "no")
        {
            updated.Concierge = 0;
            updated.ConciergeAmount = null;
            return await ProposeProfileChange(userId, rec, updated, "concierge service",
                rec.Profile.Concierge == 1 ? $"${rec.Profile.ConciergeAmount}/mo" : "None", "Disabled");
        }

        if (enabled == "true" || enabled == "yes")
        {
            updated.Concierge = 1;
            if (decimal.TryParse(amountStr?.Replace("$", ""), out var amount) && amount > 0)
            {
                updated.ConciergeAmount = amount;
            }
            else if (rec.Profile.ConciergeAmount is > 0)
            {
                updated.ConciergeAmount = rec.Profile.ConciergeAmount;
            }
            else
            {
                return Reply("How much per month for concierge service? (e.g., 200)");
            }

            return await ProposeProfileChange(userId, rec, updated, "concierge service",
                rec.Profile.Concierge == 1 ? $"${rec.Profile.ConciergeAmount}/mo" : "None",
                $"${updated.ConciergeAmount}/mo");
        }

        return Reply("Would you like to **enable** or **disable** concierge service?");
    }

    private async Task<OrchestratorResponse> HandleViewFunding(Guid userId, OrchestratorIntentResult intent, ConvStateDocument state)
    {
        var rec = await _recommendation.GetActiveAsync(userId);
        if (rec is null) return NoRecommendationReply();

        if (rec.LastCostSnapshot is null)
            return Reply("No cost data available yet. You need plan selections and a cost calculation first.");

        var c = rec.LastCostSnapshot;
        var funding =
            $"## Medicare Funding Requirements\n\n" +
            $"Based on your current recommendation, you should plan to fund:\n\n" +
            $"| | Amount |\n|---|---|\n" +
            $"| **Lifetime Medicare Costs** | **${c.LifetimeTotal:N0}** |\n" +
            $"| Present Value (today's dollars) | ${c.PresentValue:N0} |\n" +
            $"| Annual average | ${(c.LifetimeTotal / Math.Max(1, rec.Profile.LifeExpectancy - (DateTime.UtcNow.Year - rec.Profile.DateOfBirth.Year))):N0} |\n\n" +
            "💡 These figures include premiums, out-of-pocket costs, and IRMAA surcharges.\n\n" +
            "---\n" +
            "_⚠️ These are estimates based on current CMS data and actuarial assumptions. " +
            "Actual costs may vary. Consult a licensed financial advisor or Medicare counselor before making decisions._";

        return Reply(funding);
    }

    private Task<OrchestratorResponse> HandleHelp(Guid userId, OrchestratorIntentResult intent, ConvStateDocument state)
    {
        var helpText =
            "I can help you with everything related to your Medicare recommendation.\n\n" +
            "**RECOMMENDATION**\n" +
            "Create | View | Delete\n\n" +
            "**PROFILE UPDATES**\n" +
            "Date of birth · ZIP · Gender · Health profile · Life expectancy\n" +
            "Tax filing status · MAGI tier · Concierge service\n\n" +
            "**DRUGS & PHARMACY**\n" +
            "Add/remove/edit drugs | Change pharmacy | Add mail-order\n\n" +
            "**MEDICARE PLANS**\n" +
            "Compare plans | View plan details | Filter & sort | Check drug coverage\n\n" +
            "**MEDICARE PROJECTIONS & FUNDING**\n" +
            "View lifetime costs | View funding requirements\n\n" +
            "What would you like to do?";

        return Task.FromResult(Reply(helpText, displayData: new DisplayData { Type = "help_menu" }));
    }

    // ═══════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════

    private static OrchestratorResponse Reply(
        string message,
        bool requiresConfirmation = false,
        DeltaResult? delta = null,
        DisplayData? displayData = null,
        string? nextIntent = null)
    {
        return new OrchestratorResponse
        {
            Message = message,
            RequiresConfirmation = requiresConfirmation,
            Delta = delta,
            DisplayData = displayData,
            NextIntent = nextIntent
        };
    }

    private static bool IsAffirmative(string input) =>
        input is "yes" or "y" or "confirm" or "ok" or "sure" or "go ahead" or "do it" or "yes please"
            or "yep" or "yeah" or "yea" or "absolutely" or "affirmative" or "correct" or "right"
            or "proceed" or "accept" or "approve" or "sounds good" or "let's do it" or "save it";

    private static bool IsNegative(string input) =>
        input is "no" or "n" or "cancel" or "never mind" or "nevermind" or "nope" or "stop"
            or "nah" or "no thanks" or "decline" or "reject" or "undo" or "go back" or "don't"
            or "discard" or "abort" or "skip" or "not now" or "forget it";

    private static string FormatSummary(RecommendationDocument rec)
    {
        var p = rec.Profile;
        var lines = new List<string>
        {
            $"## Summary of \"{rec.Name}\"\n",
            "### Profile",
            $"**{p.FirstName} {p.LastName}** | DOB: {p.DateOfBirth:MMM d, yyyy} | {p.Gender} | ZIP: {p.ZipCode} — {p.City}, {p.State}",
            $"Health: {HealthLabel(p.HealthCondition)} | Life Expectancy: {p.LifeExpectancy} | Tobacco: {(p.TobaccoStatus == 1 ? "Yes" : "No")}",
            $"Tax Filing: {p.TaxFilingStatus} | MAGI: {p.MagiTier} | Concierge: {(p.Concierge == 1 ? $"${p.ConciergeAmount}/mo" : "None")}",
        };

        if (rec.PlanSelections.Count > 0)
        {
            lines.Add("\n### Plan Selections");
            foreach (var plan in rec.PlanSelections)
            {
                var suffix = plan.MedigapPlanType is not null ? $" (Plan {plan.MedigapPlanType})" : "";
                lines.Add($"- **{plan.PlanType}:** {plan.PlanName} — {plan.Carrier}{suffix} — ${plan.MonthlyPremium}/mo");
            }
        }

        if (rec.DrugList.Count > 0)
        {
            lines.Add("\n### Prescription Drugs");
            for (int i = 0; i < rec.DrugList.Count; i++)
            {
                var d = rec.DrugList[i];
                lines.Add($"{i + 1}. {d.DrugName} {d.Dosage} | Qty {d.Quantity} | {d.RefillFrequency}");
            }
        }

        if (rec.Pharmacy is not null)
        {
            lines.Add($"\n### Pharmacy\n{rec.Pharmacy.Name} — {rec.Pharmacy.Address}, {rec.Pharmacy.City}, {rec.Pharmacy.State}");
            if (rec.MailOrderPharmacy?.Enabled == true)
                lines.Add($"Mail-order: {rec.MailOrderPharmacy.Name}");
        }

        if (rec.LastCostSnapshot is not null)
        {
            var c = rec.LastCostSnapshot;
            lines.Add($"\n### Cost Snapshot (as of {c.CalculatedAt:MMM d, yyyy})");
            lines.Add($"Lifetime Total: **${c.LifetimeTotal:N0}** | Present Value: **${c.PresentValue:N0}**");
            lines.Add($"Current Year: ${c.CurrentYearTotal:N0} | Premiums: ${c.LifetimePremiums:N0} | OOP: ${c.LifetimeOop:N0} | IRMAA: ${c.LifetimeIrmaa:N0}");
            lines.Add("\n_⚠️ Estimates based on current CMS data. Consult a licensed advisor._");
        }

        return string.Join("\n", lines);
    }

    private static string HealthLabel(int condition) => condition switch
    {
        1 => "Best Health",
        2 => "Good Health",
        3 => "Fair Health",
        4 => "Poor Health",
        _ => "Unknown"
    };

    private static OrchestratorResponse NoRecommendationReply() =>
        Reply("You don't have an active recommendation yet. Say **\"create recommendation\"** to get started.");

    private async Task<OrchestratorResponse> ProposeProfileChange(
        Guid userId, RecommendationDocument rec, ProfileSnapshot updated,
        string fieldName, string oldValue, string newValue)
    {
        var snapshot = System.Text.Json.JsonSerializer.Serialize(updated);
        var pending = new BsonDocument
        {
            ["changeType"] = "update_profile",
            ["field"] = fieldName,
            ["snapshot"] = snapshot
        };

        // Build delta if cost snapshot exists
        DeltaResult? delta = null;
        if (rec.LastCostSnapshot is not null)
        {
            delta = _deltaCalc.BuildPreviewDelta(rec.LastCostSnapshot, fieldName, oldValue, newValue);
        }

        await _convState.SetPendingChangeAsync(userId,
            $"Change {fieldName} from \"{oldValue}\" to \"{newValue}\"", pending);

        var message = $"Change **{fieldName}** from **{oldValue}** to **{newValue}**?";
        if (delta is not null && rec.LastCostSnapshot is not null)
        {
            message += "\n\n_Note: A full cost recalculation will be performed after confirmation._";
        }

        return Reply(message, requiresConfirmation: true, delta: delta);
    }

    private static ProfileSnapshot CloneProfile(ProfileSnapshot source) => new()
    {
        RecommendationName = source.RecommendationName,
        FirstName = source.FirstName,
        LastName = source.LastName,
        DateOfBirth = source.DateOfBirth,
        Gender = source.Gender,
        ZipCode = source.ZipCode,
        County = source.County,
        CountyCode = source.CountyCode,
        State = source.State,
        City = source.City,
        AddressLine1 = source.AddressLine1,
        HealthCondition = source.HealthCondition,
        LifeExpectancy = source.LifeExpectancy,
        TobaccoStatus = source.TobaccoStatus,
        TaxFilingStatus = source.TaxFilingStatus,
        MagiTier = source.MagiTier,
        CoverageYear = source.CoverageYear,
        Concierge = source.Concierge,
        ConciergeAmount = source.ConciergeAmount,
        AlternateEmail = source.AlternateEmail,
        AlternateMobile = source.AlternateMobile,
        Latitude = source.Latitude,
        Longitude = source.Longitude
    };

    private static int ParseHealthCondition(string input)
    {
        var lower = input.Trim().ToLowerInvariant();
        if (lower is "1" or "best" or "best health") return 1;
        if (lower is "2" or "good" or "good health") return 2;
        if (lower is "3" or "fair" or "fair health") return 3;
        if (lower is "4" or "poor" or "poor health") return 4;
        return 0;
    }

    private static string? ParseTaxFilingStatus(string input)
    {
        var lower = input.Trim().ToLowerInvariant();
        if (lower is "1" or "single") return "SINGLE";
        if (lower is "2" or "joint" or "married filing jointly" or "married jointly" or "mfj") return "MARRIED_FILING_JOINTLY";
        if (lower is "3" or "separate" or "married filing separately" or "mfs") return "MARRIED_FILING_SEPARATELY";
        if (lower is "4" or "head of household" or "hoh") return "HEAD_OF_HOUSEHOLD";
        return null;
    }

    private static string? ParseMagiTier(string input)
    {
        var trimmed = input.Trim().Replace("$", "").Replace(",", "");
        if (int.TryParse(trimmed, out var val))
        {
            if (val is >= 1 and <= 6) return val.ToString();
            // Interpret as income amount — map to tier
            return val switch
            {
                <= 103000 => "1",
                <= 129000 => "2",
                <= 161000 => "3",
                <= 193000 => "4",
                <= 500000 => "5",
                _ => "6"
            };
        }
        return null;
    }

    private static string FormatDrugList(List<SelectedDrugDoc> drugs)
    {
        var lines = new List<string>();
        for (int i = 0; i < drugs.Count; i++)
        {
            var d = drugs[i];
            lines.Add($"{i + 1}. **{d.DrugName}** {d.Dosage} | Qty {d.Quantity} | {d.RefillFrequency}");
        }
        return string.Join("\n", lines);
    }
}
