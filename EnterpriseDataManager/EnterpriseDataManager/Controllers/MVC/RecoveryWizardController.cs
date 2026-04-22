namespace EnterpriseDataManager.Controllers.MVC;

using EnterpriseDataManager.Core.Interfaces.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
public class RecoveryWizardController : Controller
{
    private readonly IRecoveryJobRepository _recoveryJobRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RecoveryWizardController> _logger;

    public RecoveryWizardController(
        IRecoveryJobRepository recoveryJobRepository,
        IHttpClientFactory httpClientFactory,
        ILogger<RecoveryWizardController> logger)
    {
        _recoveryJobRepository = recoveryJobRepository;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Test Recovery Wizard";
        ViewData["Breadcrumb"] = "Recovery Wizard";

        var jobs = await _recoveryJobRepository.GetAllAsync(cancellationToken);
        return View(jobs.OrderByDescending(j => j.CreatedAt).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> Simulate(Guid id, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Simulate Recovery";
        ViewData["Breadcrumb"] = "Recovery Wizard / Simulate";

        var job = await _recoveryJobRepository.GetByIdAsync(id, cancellationToken);
        if (job == null)
            return NotFound();

        return View(new SimulateViewModel { JobId = id, DestinationPath = job.DestinationPath });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Simulate(SimulateViewModel model, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Simulate Recovery";
        ViewData["Breadcrumb"] = "Recovery Wizard / Simulate";

        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var client = _httpClientFactory.CreateClient();
            var requestUri = $"{Request.Scheme}://{Request.Host}/api/recovery/{model.JobId}/simulate";

            var response = await client.PostAsync(requestUri, null, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty, $"Simulation request failed: {response.StatusCode}");
                return View(model);
            }

            model.ResultJson = content;
            model.Succeeded = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Simulation call failed for job {JobId}", model.JobId);
            ModelState.AddModelError(string.Empty, ex.Message);
        }

        return View(model);
    }
}

public class SimulateViewModel
{
    public Guid JobId { get; set; }
    public string DestinationPath { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public string? ResultJson { get; set; }
}
