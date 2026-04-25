namespace EnterpriseDataManager.Controllers.Api;

using Asp.Versioning;
using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Data;
using Microsoft.AspNetCore.Mvc;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/recovery")]
public class RecoveryController : ApiBaseController
{
    private readonly IRecoveryJobRepository _recoveryJobRepository;
    private readonly EnterpriseDataManagerDbContext _db;
    private readonly ILogger<RecoveryController> _logger;

    public RecoveryController(
        IRecoveryJobRepository recoveryJobRepository,
        EnterpriseDataManagerDbContext db,
        ILogger<RecoveryController> logger)
    {
        _recoveryJobRepository = recoveryJobRepository;
        _db = db;
        _logger = logger;
    }

    [HttpPost("{id:guid}/simulate")]
    [ProducesResponseType(typeof(ApiResponse<SimulationResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SimulationResult>>> Simulate(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var source = await _recoveryJobRepository.GetByIdAsync(id, cancellationToken);
        if (source == null)
            return NotFoundResponse<SimulationResult>($"Recovery job {id} not found.");

        if (string.IsNullOrWhiteSpace(source.DestinationPath))
            return BadRequestResponse<SimulationResult>("Source recovery job has no destination path.");

        var normalizedSimDest = Path.GetFullPath(source.DestinationPath.Trim());
        var activeJobs = _db.RecoveryJobs
            .Where(j => !j.IsDeleted && !j.IsSimulation && j.Id != source.Id)
            .Select(j => j.DestinationPath);

        foreach (var liveDest in activeJobs)
        {
            if (!string.IsNullOrWhiteSpace(liveDest) &&
                string.Equals(Path.GetFullPath(liveDest.Trim()), normalizedSimDest, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Simulation job rejected: destination path {Path} conflicts with a live recovery job.",
                    normalizedSimDest);
                return BadRequestResponse<SimulationResult>(
                    "Simulation destination path conflicts with an active live recovery job destination. Use a separate path for simulations.");
            }
        }

        var simJob = RecoveryJob.Create(source.ArchiveJobId, source.DestinationPath);
        simJob.IsSimulation = true;

        _db.RecoveryJobs.Add(simJob);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "SIMULATION JOB {SimJobId} created from source {SourceJobId}. This is a dry-run only — no files will be written or restored.",
            simJob.Id, id);

        var result = new SimulationResult(
            SimulationJobId: simJob.Id,
            Status: "Simulating",
            Message: $"Simulation job created from recovery job {id}. No files will be written.");

        return Success(result, "Simulation started.");
    }
}

public record SimulationResult(Guid SimulationJobId, string Status, string Message);
