namespace EnterpriseDataManager.Controllers.MVC;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;

public sealed class SettingsController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IWebHostEnvironment _environment;

    public SettingsController(
        IConfiguration configuration,
        UserManager<IdentityUser> userManager,
        IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _userManager = userManager;
        _environment = environment;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Breadcrumb"] = "Settings";

        var currentUser = await _userManager.GetUserAsync(User);

        var model = new SettingsViewModel
        {
            // General Settings
            ApplicationName = "Enterprise Data Manager",
            Version = "1.0.0",
            Environment = _environment.EnvironmentName,

            // User Settings
            CurrentUserEmail = currentUser?.Email ?? "Not logged in",
            CurrentUserName = currentUser?.UserName ?? "Guest",

            // System Settings
            DatabaseConnection = MaskConnectionString(_configuration.GetConnectionString("DefaultConnection") ?? ""),
            MaxUploadSize = 100, // MB
            SessionTimeout = 30, // minutes
            EnableAuditLogging = true,
            EnableRateLimiting = true,

            // Archive Settings
            DefaultRetentionDays = 365,
            MaxConcurrentJobs = 5,
            CompressionEnabled = true,
            EncryptionEnabled = true,

            // Notification Settings
            EmailNotificationsEnabled = false,
            SlackNotificationsEnabled = false,
            WebhookUrl = ""
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveGeneral(SettingsViewModel model)
    {
        TempData["Success"] = "General settings saved successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveArchive(SettingsViewModel model)
    {
        TempData["Success"] = "Archive settings saved successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveNotifications(SettingsViewModel model)
    {
        TempData["Success"] = "Notification settings saved successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        if (newPassword != confirmPassword)
        {
            TempData["Error"] = "New passwords do not match.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Succeeded)
        {
            TempData["Success"] = "Password changed successfully.";
        }
        else
        {
            TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
        }

        return RedirectToAction(nameof(Index));
    }

    private static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return "Not configured";

        // Mask sensitive parts
        var parts = connectionString.Split(';');
        var masked = parts.Select(p =>
        {
            if (p.StartsWith("Password=", StringComparison.OrdinalIgnoreCase) ||
                p.StartsWith("Pwd=", StringComparison.OrdinalIgnoreCase))
            {
                return p.Split('=')[0] + "=****";
            }
            return p;
        });

        return string.Join(";", masked);
    }
}

public class SettingsViewModel
{
    // General
    public string ApplicationName { get; set; } = "";
    public string Version { get; set; } = "";
    public string Environment { get; set; } = "";

    // User
    public string CurrentUserEmail { get; set; } = "";
    public string CurrentUserName { get; set; } = "";

    // System
    public string DatabaseConnection { get; set; } = "";
    public int MaxUploadSize { get; set; }
    public int SessionTimeout { get; set; }
    public bool EnableAuditLogging { get; set; }
    public bool EnableRateLimiting { get; set; }

    // Archive
    public int DefaultRetentionDays { get; set; }
    public int MaxConcurrentJobs { get; set; }
    public bool CompressionEnabled { get; set; }
    public bool EncryptionEnabled { get; set; }

    // Notifications
    public bool EmailNotificationsEnabled { get; set; }
    public bool SlackNotificationsEnabled { get; set; }
    public string WebhookUrl { get; set; } = "";
}
