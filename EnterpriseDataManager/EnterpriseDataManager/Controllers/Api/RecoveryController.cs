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
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SimulationResult>>> Simulate(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var source = await _recoveryJobRepository.GetByIdAsync(id, cancellationToken);
        if (source == null)
            return NotFoundResponse<SimulationResult>($"Recovery job {id} not found.");

        var simJob = RecoveryJob.Create(source.ArchiveJobId, source.DestinationPath);
        simJob.IsSimulation = true;

        _db.RecoveryJobs.Add(simJob);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created simulation job {SimJobId} from {SourceJobId}", simJob.Id, id);

        var result = new SimulationResult(
            SimulationJobId: simJob.Id,
            Status: "Simulating",
            Message: $"Simulation job created from recovery job {id}. No files will be written.");

        return Success(result, "Simulation started.");
    }
}

public record SimulationResult(Guid SimulationJobId, string Status, string Message);
