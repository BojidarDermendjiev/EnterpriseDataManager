namespace EnterpriseDataManager.Controllers.MVC
{
    using Microsoft.AspNetCore.Mvc;

    public class ArchivePlansController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Breadcrumb"] = "Archive Plans";
            return View();
        }

        public IActionResult Create()
        {
            ViewData["Breadcrumb"] = "Archive Plans / Create";
            return View();
        }

        public IActionResult Details(Guid id)
        {
            ViewData["Breadcrumb"] = "Archive Plans / Details";
            return View();
        }

        public IActionResult Edit(Guid id)
        {
            ViewData["Breadcrumb"] = "Archive Plans / Edit";
            return View();
        }
    }
}
