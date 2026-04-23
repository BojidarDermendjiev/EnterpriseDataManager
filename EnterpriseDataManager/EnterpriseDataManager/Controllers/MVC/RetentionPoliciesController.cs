namespace EnterpriseDataManager.Controllers.MVC;

using EnterpriseDataManager.Data;
using EnterpriseDataManager.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize]
public class RetentionPoliciesController : Controller
{
    private readonly EnterpriseDataManagerDbContext _db;

    public RetentionPoliciesController(EnterpriseDataManagerDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        ViewData["Breadcrumb"] = "Retention Policies";
        var policies = await _db.RetentionPolicies.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
        return View(policies);
    }

    public IActionResult Create()
    {
        ViewData["Breadcrumb"] = "Retention Policies / Create";
        return View(new RetentionPolicyFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RetentionPolicyFormViewModel model, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return View(model);

        var policy = RetentionPolicy.Create(model.Name, TimeSpan.FromDays(model.RetentionDays));
        policy.UpdateDetails(model.Name, model.Description);
        policy.SetScope(model.Scope);
        if (model.IsLegalHold) policy.EnableLegalHold();
        if (model.IsImmutable) policy.MakeImmutable();

        _db.RetentionPolicies.Add(policy);
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Retention policy created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken ct = default)
    {
        ViewData["Breadcrumb"] = "Retention Policies / Edit";
        var policy = await _db.RetentionPolicies.FindAsync(new object[] { id }, ct);
        if (policy == null) return NotFound();

        var model = new RetentionPolicyFormViewModel
        {
            Id = policy.Id,
            Name = policy.Name,
            Description = policy.Description,
            RetentionDays = (int)policy.RetentionPeriod.TotalDays,
            IsLegalHold = policy.IsLegalHold,
            IsImmutable = policy.IsImmutable,
            Scope = policy.Scope
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, RetentionPolicyFormViewModel model, CancellationToken ct = default)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var policy = await _db.RetentionPolicies.FindAsync(new object[] { id }, ct);
        if (policy == null) return NotFound();

        policy.UpdateDetails(model.Name, model.Description);
        policy.SetScope(model.Scope);

        if (!policy.IsImmutable)
            policy.SetRetentionPeriod(TimeSpan.FromDays(model.RetentionDays));

        if (model.IsLegalHold && !policy.IsLegalHold) policy.EnableLegalHold();
        if (!model.IsLegalHold && policy.IsLegalHold) policy.DisableLegalHold();

        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Retention policy updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var policy = await _db.RetentionPolicies.FindAsync(new object[] { id }, ct);
        if (policy != null)
        {
            policy.Delete();
            await _db.SaveChangesAsync(ct);
        }
        TempData["Success"] = "Retention policy deleted.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct = default)
    {
        ViewData["Breadcrumb"] = "Retention Policies / Details";
        var policy = await _db.RetentionPolicies.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (policy == null) return NotFound();
        return View(policy);
    }
}

public class RetentionPolicyFormViewModel
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int RetentionDays { get; set; } = 90;
    public bool IsLegalHold { get; set; }
    public bool IsImmutable { get; set; }
    public string? Scope { get; set; }
}
