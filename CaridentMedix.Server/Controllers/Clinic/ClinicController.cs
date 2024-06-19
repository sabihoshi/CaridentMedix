﻿using System.Net;
using AutoMapper;
using CaridentMedix.Server.Controllers.Image;
using CaridentMedix.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace CaridentMedix.Server.Controllers.Clinic;

/// <summary>
///     ClinicController is a controller that handles operations related to clinics.
/// </summary>
[Authorize]
[ApiController]
[Route("[controller]/[action]")]
public class ClinicController(
    IConfiguration configuration,
    IMapper mapper,
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db) : ControllerBase
{
    /// <summary>
    ///     This method is responsible for registering a new dentist.
    /// </summary>
    /// <param name="request">A model containing the dentist's email, name, password, phone number, and username.</param>
    /// <returns>
    ///     An IActionResult that represents the result of the RegisterDentist action.
    ///     If the registration is successful, it returns an OkResult with a success message.
    ///     If the registration fails, it returns a BadRequestObjectResult with the errors.
    /// </returns>
    [HttpPost]
    [SwaggerResponse(Status200OK, "A success message", typeof(DentistModel))]
    [SwaggerResponse(Status400BadRequest, "A bad request response", typeof(ErrorResponse))]
    [SwaggerResponse(Status401Unauthorized, "The user is not authorized to perform this action", typeof(ErrorResponse))]
    [SwaggerResponse(Status404NotFound, "The clinic was not found", typeof(ErrorResponse))]
    public async Task<IActionResult> AddDentistAsync([FromBody] RegisterDentistRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to perform this action.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User not found." }]
            });
        }

        var clinic = request.ClinicId is null ? user.Clinic : await db.Clinics.FirstOrDefaultAsync(c => c.Id == request.ClinicId);
        if (clinic is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "You are not associated with a clinic. Please provide a clinic ID if you are not associated with a clinic.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(request.ClinicId), Message = "Clinic not found." }]
            });
        }

        if (clinic.Admins.All(x => x.Id != user.Id) && !await userManager.IsInRoleAsync(user, "Admin"))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to perform this action.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User is not a clinic admin." }]
            });
        }

        var dentist = new Dentist
        {
            Email = request.Email,
            Name = request.Name,
            PhoneNumber = request.PhoneNumber
        };

        clinic.Dentists.Add(dentist);
        await userManager.UpdateAsync(user);

        var response = mapper.Map<DentistModel>(dentist);

        return Ok(response);
    }

    /// <summary>
    ///     Asynchronously adds a user to the clinic admin role.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="clinicId">The clinic's unique identifier.</param>
    /// <returns>An IActionResult that represents the result of the AddUserToClinicAdmin action.</returns>
    [HttpPost]
    [SwaggerResponse(Status200OK, "The user was successfully added to the clinic admin role")]
    [SwaggerResponse(Status401Unauthorized, "The user is not authorized to perform this action", typeof(ErrorResponse))]
    [SwaggerResponse(Status404NotFound, "The user or clinic was not found", typeof(ErrorResponse))]
    public async Task<IActionResult> AddUserToClinicAdminAsync(string userId, int? clinicId = null)
    {
        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to perform this action.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User is not a clinic admin." }]
            });
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "The user was not found.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(userId), Message = "The user was not found." }]
            });
        }

        var clinic = clinicId is null ? currentUser.Clinic : await db.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId);
        if (clinic is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "You are not associated with a clinic.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(clinicId), Message = "Clinic not found." }]
            });
        }

        if (clinic.Admins.All(x => x.Id != user.Id) && !await userManager.IsInRoleAsync(currentUser, "Admin"))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to add this user to the clinic admin role.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User is not authorized." }]
            });
        }

        clinic.Admins.Add(user);
        await db.SaveChangesAsync();

        return Ok(new BaseResponse { Message = "User added to clinic admin role successfully!" });
    }

    /// <summary>
    ///     Asynchronously adds a user to the clinic admin role using the user's email.
    /// </summary>
    /// <param name="userEmail">The user's email.</param>
    /// <param name="clinicId">The clinic's unique identifier.</param>
    /// <returns>An IActionResult that represents the result of the AddUserToClinicAdminByEmailAsync action.</returns>
    [HttpPost]
    [SwaggerResponse(Status200OK, "The user was successfully added to the clinic admin role")]
    [SwaggerResponse(Status401Unauthorized, "The user is not authorized to perform this action", typeof(ErrorResponse))]
    [SwaggerResponse(Status404NotFound, "The user or clinic was not found", typeof(ErrorResponse))]
    public async Task<IActionResult> AddUserToClinicAdminByEmailAsync(string userEmail, int? clinicId = null)
    {
        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to perform this action.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User is not a clinic admin." }]
            });
        }

        var user = await userManager.FindByEmailAsync(userEmail);
        if (user is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "The user was not found.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(userEmail), Message = "The user was not found." }]
            });
        }

        var clinic = clinicId is null ? currentUser.Clinic : await db.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId);
        if (clinic is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "You are not associated with a clinic.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(clinicId), Message = "Clinic not found." }]
            });
        }

        if (clinic.Admins.All(x => x.Id != user.Id) && !await userManager.IsInRoleAsync(currentUser, "Admin"))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to add this user to the clinic admin role.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User is not authorized." }]
            });
        }

        clinic.Admins.Add(user);
        await db.SaveChangesAsync();

        return Ok();
    }

    /// <summary>
    ///     Asynchronously deletes a clinic.
    /// </summary>
    /// <param name="clinicId">The unique identifier of the clinic.</param>
    /// <returns>An IActionResult that represents the result of the DeleteClinic action.</returns>
    [HttpDelete("{clinicId:int}")]
    [SwaggerResponse(Status200OK, "The clinic was successfully deleted")]
    [SwaggerResponse(Status404NotFound, "The clinic was not found", typeof(ErrorResponse))]
    public async Task<IActionResult> DeleteClinicAsync(int clinicId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to perform this action.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User not found." }]
            });
        }

        var clinic = await db.Clinics.Include(clinic => clinic.Admins).FirstOrDefaultAsync(c => c.Id == clinicId);
        if (clinic is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "You are not associated with a clinic. Please provide a clinic ID if you are not associated with a clinic.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(clinicId), Message = "Clinic not found." }]
            });
        }

        if (clinic.Admins.All(x => x.Id != user.Id) && !await userManager.IsInRoleAsync(user, "Admin"))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to perform this action.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User is not a clinic admin." }]
            });
        }

        db.Clinics.Remove(clinic);
        await db.SaveChangesAsync();

        return Ok();
    }

    /// <summary>
    ///     Asynchronously deletes a shared report.
    /// </summary>
    /// <param name="reportId">The unique identifier of the shared report.</param>
    /// <param name="clinicId">The clinic's unique identifier.</param>
    /// <returns>
    ///     An IActionResult that represents the result of the DeleteSharedReportAsync action.
    ///     If the deletion is successful, it returns an OkResult.
    ///     If the deletion fails, it returns a BadRequestObjectResult with an error message.
    /// </returns>
    [HttpDelete("{reportId:int}")]
    [SwaggerResponse(Status200OK, "The shared report was successfully deleted")]
    [SwaggerResponse(Status401Unauthorized, "The user is not authorized to perform this action", typeof(ErrorResponse))]
    [SwaggerResponse(Status404NotFound, "The report was not found", typeof(ErrorResponse))]
    [SwaggerResponse(Status400BadRequest, "A bad request response", typeof(ErrorResponse))]
    public async Task<IActionResult> DeleteSharedReportAsync(int reportId, int? clinicId = null)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to perform this action.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User not found." }]
            });
        }

        var clinic = clinicId is null ? user.Clinic : await db.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId);
        if (clinic is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "You are not associated with a clinic. Please provide a clinic ID if you are not associated with a clinic.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(clinicId), Message = "Clinic not found." }]
            });
        }

        if (clinic.Admins.All(x => x.Id != user.Id) && !await userManager.IsInRoleAsync(user, "Admin"))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to perform this action.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User is not a clinic admin." }]
            });
        }

        var report = clinic.DataReports.FirstOrDefault(report => report.Id == reportId);
        if (report is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "The report was not found.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(reportId), Message = "Report not found." }]
            });
        }

        clinic.DataReports.Remove(report);
        await db.SaveChangesAsync();

        return Ok(new BaseResponse { Message = "Report deleted successfully!" });
    }

    /// <summary>
    ///     Asynchronously retrieves a list of all clinics.
    /// </summary>
    /// <param name="latitude">The latitude of the user's location.</param>
    /// <param name="longitude">The longitude of the user's location.</param>
    /// <param name="radiusKm">The radius in kilometers from the user's location.</param>
    /// <returns>The list of clinics ordered by distance from the user's location.</returns>
    [HttpGet]
    [AllowAnonymous]
    [SwaggerResponse(Status200OK, "A list of clinics", typeof(List<ClinicModel>))]
    [SwaggerResponse(Status400BadRequest, "A bad request response", typeof(ErrorResponse))]
    public async Task<IActionResult> FindNearbyClinics(float latitude, float longitude, float radiusKm = 50)
    {
        var clinics = await db.Clinics.ToListAsync();
        var clinicModels = clinics.Select(mapper.Map<ClinicModel>).ToList();

        foreach (var clinic in clinicModels)
        {
            clinic.Distance = Distance(clinic.Longitude, clinic.Latitude);
        }

        clinicModels = clinicModels
           .Where(clinic => clinic.Distance <= radiusKm)
           .OrderBy(clinic => clinic.Distance)
           .ToList();

        return Ok(clinicModels);

        double Distance(float clinicLongitude, float clinicLatitude)
        {
            const double earthRadiusKm = 6371;
            var dLat = DegreesToRadians(clinicLatitude - latitude);
            var dLon = DegreesToRadians(clinicLongitude - longitude);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(DegreesToRadians(latitude)) * Math.Cos(DegreesToRadians(clinicLatitude)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadiusKm * c;

            static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
        }
    }

    /// <summary>
    ///     Asynchronously retrieves a list of clinic admins associated with the user.
    /// </summary>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains an IActionResult that can be one of the
    ///     following:
    ///     - A result that represents a status code 200 (OK) with a list of clinic admins.
    ///     - A result that represents a status code 500 (Internal Server Error) if an exception was thrown.
    /// </returns>
    [HttpGet]
    [SwaggerResponse(Status200OK, "A list of clinic admins", typeof(List<ApplicationUser>))]
    [SwaggerResponse(Status401Unauthorized, "The user is not authorized to perform this action", typeof(ErrorResponse))]
    [SwaggerResponse(Status400BadRequest, "A bad request response", typeof(ErrorResponse))]
    public async Task<IActionResult> GetClinicAdminsAsync(int? clinicId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to perform this action.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User not found." }]
            });
        }

        var clinic = clinicId is null ? user.Clinic : await db.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId);
        if (clinic is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "You are not associated with a clinic. Please provide a clinic ID if you are not associated with a clinic.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(clinicId), Message = "Clinic not found." }]
            });
        }

        if (clinic.Admins.All(x => x.Id != user.Id) && !await userManager.IsInRoleAsync(user, "Admin"))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to perform this action.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User is not a clinic admin." }]
            });
        }

        var result = clinic.Admins
           .Select(mapper.Map<AdminModel>)
           .OrderBy(admin => admin.Email)
           .ToList();

        return Ok(result);
    }

    /// <summary>
    ///     Asynchronously retrieves a clinic based on the provided clinic identifier.
    /// </summary>
    /// <param name="clinicId">The unique identifier of the clinic.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains an IActionResult that can be one of the
    ///     following:
    ///     - A result that represents a status code 200 (OK) with a clinic.
    ///     - A result that represents a status code 404 (Not Found) if the clinic was not found.
    /// </returns>
    [HttpGet]
    [AllowAnonymous]
    [SwaggerResponse(Status200OK, "A clinic object", typeof(ClinicModel))]
    [SwaggerResponse(Status404NotFound, "The clinic was not found", typeof(ErrorResponse))]
    public async Task<IActionResult> GetClinicAsync(int clinicId)
    {
        var clinic = await db.Clinics
           .Include(clinic => clinic.Dentists)
           .FirstOrDefaultAsync(clinic => clinic.Id == clinicId);
        if (clinic is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "The clinic was not found.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(clinicId), Message = "Clinic not found." }]
            });
        }

        var clinicModel = mapper.Map<ClinicModel>(clinic);
        return Ok(clinicModel);
    }

    /// <summary>
    ///     Asynchronously retrieves a list of clinics based on the provided search parameters.
    /// </summary>
    /// <param name="generalSearch">A general search term that is used to search across multiple fields.</param>
    /// <param name="name">A specific search term for the name of a clinic or dentist.</param>
    /// <param name="email">A specific search term for the email of a clinic or dentist.</param>
    /// <param name="phoneNumber">A specific search term for the phone number of a clinic.</param>
    /// <param name="address">A specific search term for the address of a clinic.</param>
    /// <param name="description">A specific search term for the description of a clinic.</param>
    /// <param name="website">A specific search term for the website of a clinic.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains an IActionResult that can be one of the
    ///     following:
    ///     - A result that represents a status code 200 (OK) with a list of clinics.
    ///     - A result that represents a status code 500 (Internal Server Error) if an exception was thrown.
    /// </returns>
    [HttpGet]
    [AllowAnonymous]
    [SwaggerResponse(Status200OK, "A list of clinics", typeof(List<ClinicModel>))]
    [SwaggerResponse(Status400BadRequest, "A bad request response", typeof(ErrorResponse))]
    public Task<IActionResult> GetClinicsAsync(
        string? generalSearch, string? name, string? email, string? phoneNumber,
        string? address, string? description, string? website)
    {
        const int maxDistance = 3;

        var clinicsQuery = db.Clinics
           .Include(clinic => clinic.Dentists)
           .AsEnumerable();

        if (!string.IsNullOrEmpty(generalSearch))
        {
            clinicsQuery = clinicsQuery.Where(clinic
                => clinic.Name.StartsWith(generalSearch)
                || LevenshteinDistance(clinic.Name, generalSearch) <= maxDistance
                || clinic.Email!.StartsWith(generalSearch)
                || LevenshteinDistance(clinic.Email!, generalSearch) <= maxDistance
                || clinic.PhoneNumber!.StartsWith(generalSearch)
                || LevenshteinDistance(clinic.PhoneNumber!, generalSearch) <= maxDistance
                || clinic.Dentists.Any(dentist
                       => dentist.Name.StartsWith(generalSearch)
                       || LevenshteinDistance(dentist.Name, generalSearch) <= maxDistance)
                || clinic.Dentists.Any(dentist
                       => dentist.Email!.StartsWith(generalSearch)
                       || LevenshteinDistance(dentist.Email!, generalSearch) <= maxDistance)
                || clinic.Dentists.Any(dentist
                       => dentist.PhoneNumber!.StartsWith(generalSearch)
                       || LevenshteinDistance(dentist.PhoneNumber!, generalSearch) <= maxDistance)
                || clinic.Address.StartsWith(generalSearch)
                || LevenshteinDistance(clinic.Address, generalSearch) <= maxDistance
                || clinic.Description!.StartsWith(generalSearch)
                || LevenshteinDistance(clinic.Description!, generalSearch) <= maxDistance
                || clinic.Website!.StartsWith(generalSearch)
                || LevenshteinDistance(clinic.Website!, generalSearch) <= maxDistance);
        }

        if (!string.IsNullOrEmpty(name))
        {
            clinicsQuery = clinicsQuery.Where(clinic =>
                clinic.Name.StartsWith(name) || LevenshteinDistance(clinic.Name, name) <= maxDistance
                                             || clinic.Dentists.Any(dentist
                                                    => dentist.Name.StartsWith(name) ||
                                                       LevenshteinDistance(dentist.Name, name) <= maxDistance));
        }

        if (!string.IsNullOrEmpty(email))
        {
            clinicsQuery = clinicsQuery.Where(clinic =>
                clinic.Email!.StartsWith(email) || LevenshteinDistance(clinic.Email!, email) <= maxDistance
                                                || clinic.Dentists.Any(dentist
                                                       => dentist.Email!.StartsWith(email) ||
                                                          LevenshteinDistance(dentist.Email!, email) <= maxDistance));
        }

        if (!string.IsNullOrEmpty(phoneNumber))
        {
            clinicsQuery = clinicsQuery.Where(clinic =>
                clinic.PhoneNumber!.StartsWith(phoneNumber) ||
                LevenshteinDistance(clinic.PhoneNumber!, phoneNumber) <= maxDistance
             || clinic.Dentists.Any(dentist
                    => dentist.PhoneNumber!.StartsWith(phoneNumber) ||
                       LevenshteinDistance(dentist.PhoneNumber!, phoneNumber) <= maxDistance));
        }

        if (!string.IsNullOrEmpty(address))
        {
            clinicsQuery = clinicsQuery.Where(clinic =>
                clinic.Address.StartsWith(address) ||
                LevenshteinDistance(clinic.Address, address) <= maxDistance);
        }

        if (!string.IsNullOrEmpty(description))
        {
            clinicsQuery = clinicsQuery.Where(clinic =>
                clinic.Description!.StartsWith(description) ||
                LevenshteinDistance(clinic.Description!, description) <= maxDistance);
        }

        if (!string.IsNullOrEmpty(website))
        {
            clinicsQuery = clinicsQuery.Where(clinic =>
                clinic.Website!.StartsWith(website) ||
                LevenshteinDistance(clinic.Website!, website) <= maxDistance);
        }

        var clinics = clinicsQuery.Select(mapper.Map<ClinicModel>).ToList();
        var clinicModels = string.IsNullOrEmpty(generalSearch)
            ? clinics
            : clinics
               .Select(mapper.Map<ClinicModel>)
               .OrderBy(clinic =>
                    WeightedLevenshteinDistance(clinic.Name, generalSearch, 0.5)
                  + WeightedLevenshteinDistance(clinic.Email!, generalSearch, 1.5)
                  + WeightedLevenshteinDistance(clinic.PhoneNumber!, generalSearch, 1.5)
                  + WeightedLevenshteinDistance(clinic.Address, generalSearch, 1.5)
                  + WeightedLevenshteinDistance(clinic.Description!, generalSearch, 1.5)
                  + WeightedLevenshteinDistance(clinic.Website!, generalSearch, 1.5)
                  + WeightedLevenshteinDistance(clinic.Name, name, 0.5)
                  + WeightedLevenshteinDistance(clinic.Email!, email, 1.5)
                  + WeightedLevenshteinDistance(clinic.PhoneNumber!, phoneNumber, 1.5)
                  + WeightedLevenshteinDistance(clinic.Address, address, 1.5)
                  + WeightedLevenshteinDistance(clinic.Description!, description, 1.5)
                  + WeightedLevenshteinDistance(clinic.Website!, website, 1.5))
               .ToList();

        return Task.FromResult<IActionResult>(Ok(clinicModels));

        static int WeightedLevenshteinDistance(string source, string? target, double weight)
        {
            if (string.IsNullOrEmpty(target)) return 0;

            var distance = LevenshteinDistance(source, target);
            return (int) (distance * weight);
        }
    }

    /// <summary>
    ///     Asynchronously retrieves a list of dentists associated with the user.
    /// </summary>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains an IActionResult that can be one of the
    ///     following:
    ///     - A result that represents a status code 200 (OK) with a list of dentists.
    ///     - A result that represents a status code 500 (Internal Server Error) if an exception was thrown.
    /// </returns>
    [HttpGet]
    [AllowAnonymous]
    [SwaggerResponse(Status200OK, "A list of dentists", typeof(List<DentistModel>))]
    [SwaggerResponse(Status401Unauthorized, "The user is not authorized to perform this action", typeof(ErrorResponse))]
    [SwaggerResponse(Status400BadRequest, "A bad request response", typeof(ErrorResponse))]
    public async Task<IActionResult> GetDentistsAsync(int? clinicId = null)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to perform this action.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User not found." }]
            });
        }

        var clinic = clinicId is null ? user.Clinic : await db.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId);
        if (clinic is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "You are not associated with a clinic. Please provide a clinic ID if you are not associated with a clinic.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(clinicId), Message = "Clinic not found." }]
            });
        }

        var result = clinic.Dentists
           .Select(mapper.Map<DentistModel>)
           .OrderBy(dentist => dentist.Name)
           .ToList();

        return Ok(result);
    }

    /// <summary>
    ///     Asynchronously retrieves a list of clinics associated with the user.
    /// </summary>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains an IActionResult that can be one of the
    ///     following:
    ///     - A result that represents a status code 200 (OK) with a list of clinics.
    ///     - A result that represents a status code 500 (Internal Server Error) if an exception was thrown.
    /// </returns>
    [HttpGet]
    [SwaggerResponse(Status200OK, "A list of clinics", typeof(List<DataReportModel>))]
    [SwaggerResponse(Status401Unauthorized, "The user is not authorized to perform this action", typeof(ErrorResponse))]
    [SwaggerResponse(Status400BadRequest, "A bad request response", typeof(ErrorResponse))]
    public async Task<IActionResult> GetSharedReportsAsync(int? clinicId = null)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to perform this action.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User not found." }]
            });
        }

        var clinic = clinicId is null ? user.Clinic : await db.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId);
        if (clinic is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "You are not associated with a clinic. Please provide a clinic ID if you are not associated with a clinic.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(clinicId), Message = "Clinic not found." }]
            });
        }

        if (clinic.Admins.All(x => x.Id != user.Id) && !await userManager.IsInRoleAsync(user, "Admin"))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to perform this action.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User is not a clinic admin." }]
            });
        }

        var result = clinic.DataReports
           .Select(mapper.Map<DataReportModel>)
           .OrderByDescending(report => report.CreatedAt)
           .ToList();

        return Ok(result);
    }

    /// <summary>
    ///     Asynchronously removes a dentist from the clinic.
    /// </summary>
    /// <param name="dentistId">The unique identifier of the dentist.</param>
    /// <param name="clinicId">The clinic's unique identifier.</param>
    /// <returns>
    ///     An IActionResult that represents the result of the RemoveDentistAsync action.
    ///     If the removal is successful, it returns an OkResult with a success message.
    ///     If the removal fails, it returns a BadRequestObjectResult with an error message.
    /// </returns>
    [HttpDelete]
    [SwaggerResponse(Status200OK, "A success message", typeof(BaseResponse))]
    [SwaggerResponse(Status400BadRequest, "A bad request response", typeof(ErrorResponse))]
    [SwaggerResponse(Status401Unauthorized, "The user is not authorized to perform this action", typeof(ErrorResponse))]
    [SwaggerResponse(Status404NotFound, "The dentist was not found", typeof(ErrorResponse))]
    public async Task<IActionResult> RemoveDentistAsync(int dentistId, int? clinicId = null)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to perform this action.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User not found." }]
            });
        }

        var clinic = clinicId is null ? user.Clinic : await db.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId);
        if (clinic is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "You are not associated with a clinic. Please provide a clinic ID if you are not associated with a clinic.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(clinicId), Message = "Clinic not found." }]
            });
        }

        if (clinic.Admins.All(x => x.Id != user.Id) && !await userManager.IsInRoleAsync(user, "Admin"))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to perform this action.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User is not a clinic admin." }]
            });
        }

        var dentist = clinic.Dentists.FirstOrDefault(dentist => dentist.Id == dentistId);
        if (dentist is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "The dentist was not found.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(dentistId), Message = "Dentist not found." }]
            });
        }

        clinic.Dentists.Remove(dentist);
        await userManager.UpdateAsync(user);

        return Ok(new BaseResponse { Message = "Dentist removed successfully!" });
    }

    /// <summary>
    ///     Asynchronously removes a user from the clinic admin role.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="clinicId">The clinic's unique identifier.</param>
    /// <returns>An IActionResult that represents the result of the RemoveUserFromClinicAdminAsync action.</returns>
    [HttpPost]
    [SwaggerResponse(Status200OK, "The user was successfully removed from the clinic admin role", typeof(BaseResponse))]
    [SwaggerResponse(Status401Unauthorized, "The user is not authorized to perform this action", typeof(ErrorResponse))]
    [SwaggerResponse(Status404NotFound, "The user or clinic was not found", typeof(ErrorResponse))]
    public async Task<IActionResult> RemoveUserFromClinicAdminAsync(string userId, int? clinicId = null)
    {
        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to perform this action.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User is not a clinic admin." }]
            });
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "The user was not found.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(userId), Message = "The user was not found." }]
            });
        }

        var clinic = clinicId is null ? currentUser.Clinic : await db.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId);
        if (clinic is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "You are not associated with a clinic.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(clinicId), Message = "Clinic not found." }]
            });
        }

        if (clinic.Admins.All(x => x.Id != user.Id) && !await userManager.IsInRoleAsync(currentUser, "Admin"))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to add this user to the clinic admin role.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User is not authorized." }]
            });
        }

        clinic.Admins.Remove(user);
        await db.SaveChangesAsync();

        return Ok(new BaseResponse { Message = "User removed successfully!" });
    }

    /// <summary>
    ///     Asynchronously removes a user from the clinic admin role using the user's email.
    /// </summary>
    /// <param name="userEmail">The user's email.</param>
    /// <param name="clinicId">The clinic's unique identifier.</param>
    /// <returns>An IActionResult that represents the result of the RemoveUserFromClinicAdminByEmailAsync action.</returns>
    [HttpPost]
    [SwaggerResponse(Status200OK, "The user was successfully removed from the clinic admin role")]
    [SwaggerResponse(Status401Unauthorized, "The user is not authorized to perform this action", typeof(ErrorResponse))]
    [SwaggerResponse(Status404NotFound, "The user or clinic was not found", typeof(ErrorResponse))]
    public async Task<IActionResult> RemoveUserFromClinicAdminByEmailAsync(string userEmail, int? clinicId = null)
    {
        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to perform this action.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User is not a clinic admin." }]
            });
        }

        var user = await userManager.FindByEmailAsync(userEmail);
        if (user is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "The user was not found.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(userEmail), Message = "The user was not found." }]
            });
        }

        var clinic = clinicId is null ? currentUser.Clinic : await db.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId);
        if (clinic is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "You are not associated with a clinic.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(clinicId), Message = "Clinic not found." }]
            });
        }

        if (clinic.Admins.All(x => x.Id != user.Id) && !await userManager.IsInRoleAsync(currentUser, "Admin"))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to add this user to the clinic admin role.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User is not authorized." }]
            });
        }

        clinic.Admins.Remove(user);
        await db.SaveChangesAsync();

        return Ok(new BaseResponse { Message = "User removed successfully!" });
    }

    /// <summary>
    ///     Asynchronously updates a clinic.
    /// </summary>
    /// <param name="clinicId">The unique identifier of the clinic.</param>
    /// <param name="clinic">The clinic model that contains the clinic's information.</param>
    /// <returns>An IActionResult that represents the result of the UpdateClinic action.</returns>
    [HttpPatch("{clinicId:int}")]
    [SwaggerResponse(Status200OK, "The clinic was successfully updated")]
    [SwaggerResponse(Status404NotFound, "The clinic was not found", typeof(ErrorResponse))]
    [SwaggerResponse(Status500InternalServerError, "An exception was thrown", typeof(ErrorResponse))]
    public async Task<IActionResult> UpdateClinicAsync(int clinicId, ClinicEditRequest clinic)
    {
        var clinicEntity = await db.Clinics.FindAsync(clinicId);
        if (clinicEntity is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "The clinic was not found.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(clinicId), Message = "Clinic not found." }]
            });
        }

        clinicEntity.Address = clinic.Address ?? clinicEntity.Address;
        clinicEntity.Description = clinic.Description ?? clinicEntity.Description;
        clinicEntity.Email = clinic.Email ?? clinicEntity.Email;
        clinicEntity.ImagePath = clinic.ImagePath ?? clinicEntity.ImagePath;
        clinicEntity.Latitude = clinic.Latitude ?? clinicEntity.Latitude;
        clinicEntity.Longitude = clinic.Longitude ?? clinicEntity.Longitude;
        clinicEntity.Name = clinic.Name ?? clinicEntity.Name;
        clinicEntity.PhoneNumber = clinic.PhoneNumber ?? clinicEntity.PhoneNumber;
        clinicEntity.Website = clinic.Website ?? clinicEntity.Website;

        await db.SaveChangesAsync();

        var result = mapper.Map<ClinicModel>(clinicEntity);

        return Ok(result);
    }

    /// <summary>
    ///     Asynchronously updates a dentist.
    /// </summary>
    /// <param name="dentistId">The unique identifier of the dentist.</param>
    /// <param name="request">The request that contains the updated dentist information.</param>
    /// <returns>
    ///     An IActionResult that represents the result of the UpdateDentistAsync action.
    ///     If the update is successful, it returns an OkResult with a success message.
    ///     If the update fails, it returns a BadRequestObjectResult with an error message.
    /// </returns>
    [HttpPatch("{dentistId:int}")]
    [SwaggerResponse(Status200OK, "The dentist was successfully updated", typeof(DentistModel))]
    [SwaggerResponse(Status400BadRequest, "A bad request response", typeof(ErrorResponse))]
    [SwaggerResponse(Status401Unauthorized, "The user is not authorized to perform this action", typeof(ErrorResponse))]
    [SwaggerResponse(Status404NotFound, "The dentist was not found", typeof(ErrorResponse))]
    public async Task<IActionResult> UpdateDentistAsync(int dentistId, UpdateDentistRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to perform this action.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User not found." }]
            });
        }

        var clinic = request.ClinicId is null ? user.Clinic : await db.Clinics.FirstOrDefaultAsync(c => c.Id == request.ClinicId);
        if (clinic is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "You are not associated with a clinic. Please provide a clinic ID if you are not associated with a clinic.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(request.ClinicId), Message = "Clinic not found." }]
            });
        }

        if (clinic.Admins.All(x => x.Id != user.Id) && !await userManager.IsInRoleAsync(user, "Admin"))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "You are not authorized to perform this action.",
                StatusCode = HttpStatusCode.Unauthorized,
                Details = [new ErrorDetail { PropertyName = nameof(User), Message = "User is not a clinic admin." }]
            });
        }

        var dentist = clinic.Dentists.FirstOrDefault(dentist => dentist.Id == dentistId);
        if (dentist is null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "The dentist was not found.",
                StatusCode = HttpStatusCode.NotFound,
                Details = [new ErrorDetail { PropertyName = nameof(dentistId), Message = "Dentist not found." }]
            });
        }

        dentist.Email = request.Email ?? dentist.Email;
        dentist.Name = request.Name ?? dentist.Name;
        dentist.PhoneNumber = request.PhoneNumber ?? dentist.PhoneNumber;

        await db.SaveChangesAsync();

        var result = mapper.Map<DentistModel>(dentist);

        return Ok(result);
    }

    private static int LevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source)) return string.IsNullOrEmpty(target) ? 0 : target.Length;
        if (string.IsNullOrEmpty(target)) return source.Length;

        var distance = new int[source.Length + 1, target.Length + 1];

        for (var i = 0; i <= source.Length; distance[i, 0] = i++) { }
        for (var j = 0; j <= target.Length; distance[0, j] = j++) { }

        for (var i = 1; i <= source.Length; i++)
        {
            for (var j = 1; j <= target.Length; j++)
            {
                var cost = target[j - 1] == source[i - 1] ? 0 : 1;
                distance[i, j] = Math.Min(
                    Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                    distance[i - 1, j - 1] + cost);
            }
        }

        return distance[source.Length, target.Length];
    }
}