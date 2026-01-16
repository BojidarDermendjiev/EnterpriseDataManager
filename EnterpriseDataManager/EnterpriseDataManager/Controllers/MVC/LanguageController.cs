namespace EnterpriseDataManager.Controllers.MVC;

using EnterpriseDataManager.Resources;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

public class LanguageController : Controller
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    public LanguageController(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }
    [HttpGet]
    public IActionResult SetLanguage(string culture, string returnUrl)
    {
        if (string.IsNullOrEmpty(culture))
        {
            culture = "en";
        }

        // Set the culture cookie
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                HttpOnly = false,
                Path = "/"
            }
        );

        // Redirect back
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult GetCurrentLanguage()
    {
        var culture = HttpContext.Features.Get<IRequestCultureFeature>()?.RequestCulture.Culture.Name ?? "en";
        return Json(new { culture });
    }

    [HttpGet]
    public IActionResult Debug()
    {
        var requestCulture = HttpContext.Features.Get<IRequestCultureFeature>();
        var cultureCookie = Request.Cookies[CookieRequestCultureProvider.DefaultCookieName];

        // Test localizer
        var mainText = _localizer["Main"];
        var dashboardText = _localizer["Dashboard"];

        return Json(new
        {
            CurrentCulture = requestCulture?.RequestCulture.Culture.Name,
            CurrentUICulture = requestCulture?.RequestCulture.UICulture.Name,
            CookieValue = cultureCookie,
            CookieName = CookieRequestCultureProvider.DefaultCookieName,
            LocalizerMain = mainText.Value,
            LocalizerMainFound = !mainText.ResourceNotFound,
            LocalizerDashboard = dashboardText.Value,
            LocalizerDashboardFound = !dashboardText.ResourceNotFound,
            LocalizerSearchedLocation = mainText.SearchedLocation
        });
    }
}
