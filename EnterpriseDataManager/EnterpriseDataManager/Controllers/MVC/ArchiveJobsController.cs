namespace EnterpriseDataManager.Controllers.MVC
{
    using Microsoft.AspNetCore.Mvc;

    public class ArchiveJobsController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Breadcrumb"] = "Archive Jobs";
            return View();
        }

        public IActionResult Details(Guid id)
        {
            ViewData["Breadcrumb"] = "Archive Jobs / Details";
            return View();
        }
    }
}
