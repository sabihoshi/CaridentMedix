using CaridentMedix.Server.Models;
using Compunet.YoloV8;
using Compunet.YoloV8.Plotting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using YoloDotNet;
using YoloDotNet.Extensions;
using Image = SixLabors.ImageSharp.Image;

namespace CaridentMedix.Server.Controllers;

/// <inheritdoc />
[ApiController]
[Route("[controller]/[action]")]
public class ImageController(
    IConfiguration configuration,
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db) : ControllerBase
{
    /// <summary>
    ///     Analyzes an uploaded image using the YOLO model using an alternative library and returns the analysis result.
    /// </summary>
    /// <param name="file">The image file to be analyzed.</param>
    /// <param name="confidence">The confidence to use when analyzing the image.</param>
    /// <param name="iou">The intersection over union to use when analyzing the image.</param>
    /// <returns>
    ///     Returns an IActionResult:
    ///     - BadRequest if the YOLO model is not found.
    ///     - Unauthorized if the user is not found.
    ///     - Ok with the image analysis result if the image is successfully analyzed.
    /// </returns>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> AnalyzeImageAlternativeAsync(
        IFormFile file, float confidence = 0.3f, float iou = 0.45f)
    {
        if (string.IsNullOrEmpty(configuration["YOLO:Model"]))
            return BadRequest("YOLO model not found.");

        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var userIdPath = Path.Combine("images", user.Id);
        var basePath = Path.Combine("wwwroot", userIdPath);
        Directory.CreateDirectory(basePath);

        var fileName = $"{DateTimeOffset.Now.ToUnixTimeSeconds()}_{file.FileName}";

        var path = Path.Combine(basePath, fileName);
        await using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream);

        using var predictor = YoloV8Predictor.Create(configuration["YOLO:Model"]!);
        predictor.Configuration.Confidence = confidence;
        predictor.Configuration.IoU = iou;

        using var image = await Image.LoadAsync(file.OpenReadStream());

        var result = await predictor.DetectAsync(image);
        var plot = await result.PlotImageAsync(image);

        var plottedPath = Path.Combine(basePath, $"plotted_{fileName}");
        await using var plotStream = new FileStream(plottedPath, FileMode.Create);
        await plot.SaveAsPngAsync(plotStream);

        var imageResult = new Models.Image
        {
            Name = fileName,
            OriginalImagePath = $"images/{user.Id}/{fileName}",
            PlottedImagePath = $"images/{user.Id}/plotted_{fileName}",
            CreatedAt = DateTimeOffset.UtcNow,
            Width = result.Image.Width,
            Height = result.Image.Height,
            Detections = result.Boxes.Select(box => new Detection
            {
                ClassId = box.Class.Id,
                ClassName = box.Class.Name,
                Confidence = box.Confidence,
                Height = box.Bounds.Height,
                Width = box.Bounds.Width,
                X = box.Bounds.X,
                Y = box.Bounds.Y
            }).ToList()
        };

        await db.Images.AddAsync(imageResult);
        await db.SaveChangesAsync();

        return Ok(imageResult);
    }

    /// <summary>
    ///     Analyzes an uploaded image using the YOLO model and returns the analysis result.
    /// </summary>
    /// <param name="file">The image file to be analyzed.</param>
    /// <param name="threshold">The threshold to use when analyzing the image.</param>
    /// <returns>
    ///     Returns an IActionResult:
    ///     - BadRequest if the YOLO model is not found.
    ///     - Unauthorized if the user is not found.
    ///     - Ok with the image analysis result if the image is successfully analyzed.
    /// </returns>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> AnalyzeImageAsync(IFormFile file, double threshold = 0.25)
    {
        if (string.IsNullOrEmpty(configuration["YOLO:Model"]))
            return BadRequest("YOLO model not found.");

        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var userIdPath = Path.Combine("images", user.Id);
        var basePath = Path.Combine("wwwroot", userIdPath);
        Directory.CreateDirectory(basePath);

        var fileName = $"{DateTimeOffset.Now.ToUnixTimeSeconds()}_{file.FileName}";

        var path = Path.Combine(basePath, fileName);
        var plottedPath = Path.Combine(basePath, $"plotted_{fileName}");

        await using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream);

        using var yolo = new Yolo(configuration["YOLO:Model"]!, false);
        using var image = await Image.LoadAsync(file.OpenReadStream());
        var results = yolo.RunObjectDetection(image, threshold);

        image.Draw(results);
        await image.SaveAsync(plottedPath);

        var imageResult = new Models.Image
        {
            User = user,
            Name = fileName,
            OriginalImagePath = $"images/{user.Id}/{fileName}",
            PlottedImagePath = $"images/{user.Id}/plotted_{fileName}",
            CreatedAt = DateTimeOffset.UtcNow,
            Width = image.Width,
            Height = image.Height,
            Detections = results.Select(x => new Detection
            {
                ClassId = x.Label.Index,
                ClassName = x.Label.Name,
                Confidence = x.Confidence,
                Height = x.BoundingBox.Height,
                Width = x.BoundingBox.Width,
                X = x.BoundingBox.X,
                Y = x.BoundingBox.Y
            }).ToList()
        };
        await db.Images.AddAsync(imageResult);
        await db.SaveChangesAsync();

        return Ok(imageResult);
    }

    /// <summary>
    ///     Deletes a specific report based on the provided id.
    /// </summary>
    /// <param name="id">The id of the report to delete.</param>
    /// <returns>
    ///     Returns an IActionResult:
    ///     - Ok if the report is successfully deleted.
    ///     - Unauthorized if the user is not found or does not match the user associated with the report.
    ///     - NotFound if the report is not found.
    /// </returns>
    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteReport(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var report = await db.DataReports
            .Include(report => report.Images)
            .Include(report => report.User)
            .FirstOrDefaultAsync(report => report.Id == id);

        if (report is null)
            return NotFound();

        if (report.User != user)
            return Unauthorized();

        db.DataReports.Remove(report);
        await db.SaveChangesAsync();

        return Ok();
    }

    /// <summary>
    ///     Retrieves a specific report based on the provided id.
    /// </summary>
    /// <param name="id">The id of the report to retrieve.</param>
    /// <returns>
    ///     Returns an IActionResult:
    ///     - Ok with the report data if the report is found and the user is authorized.
    ///     - Unauthorized if the user is not found or does not match the user associated with the report.
    ///     - NotFound if the report is not found.
    /// </returns>
    [Authorize]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetReport(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var report = await db.DataReports
            .Include(report => report.Images)
            .Include(report => report.User)
            .FirstOrDefaultAsync(report => report.Id == id);

        if (report is null)
            return NotFound();

        if (report.User != user)
            return Unauthorized();

        return Ok(report);
    }

    /// <summary>
    ///     Retrieves reports based on the provided filter.
    /// </summary>
    /// <param name="filter">
    ///     The filter to apply when retrieving reports. This could be a keyword or phrase that is present in
    ///     the report data.
    /// </param>
    /// <returns>
    ///     Returns an IActionResult:
    ///     - Unauthorized if the user is not found.
    ///     - Ok with the list of reports that match the filter if the reports are successfully retrieved.
    /// </returns>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> GetReportsAsync(string filter)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var reports = await db.DataReports
            .Where(report => report.User == user)
            .ToListAsync();

        return Ok(reports);
    }

    /// <summary>
    ///     Uploads a result image to the server.
    /// </summary>
    /// <param name="file">The image file to be uploaded.</param>
    /// <returns>
    ///     Returns an IActionResult:
    ///     - Unauthorized if the user is not found.
    ///     - Ok with the image data if the image is successfully uploaded.
    /// </returns>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> UploadImageResultAsync(IFormFile file)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var path = Path.Combine("wwwroot", "images", file.FileName);
        await using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream);

        var image = new Models.Image
        {
            Name = file.FileName,
            OriginalImagePath = path,
            CreatedAt = DateTimeOffset.Now
        };

        await db.Images.AddAsync(image);
        await db.SaveChangesAsync();

        return Ok(image);
    }
}