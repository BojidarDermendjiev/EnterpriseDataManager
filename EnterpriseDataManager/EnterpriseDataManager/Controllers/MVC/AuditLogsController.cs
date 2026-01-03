namespace EnterpriseDataManager.Controllers.MVC
{
    using Microsoft.AspNetCore.Mvc;

    public class AuditLogsController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Breadcrumb"] = "Audit Logs";
            return View();
        }

        public IActionResult Details(Guid id)
        {
            ViewData["Breadcrumb"] = "Audit Logs / Details";
            return View();
        }
    }
}
