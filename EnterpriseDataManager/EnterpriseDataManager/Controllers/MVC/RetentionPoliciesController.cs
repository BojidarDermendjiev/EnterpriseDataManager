namespace EnterpriseDataManager.Controllers.MVC
{
    using Microsoft.AspNetCore.Mvc;

    public class RetentionPoliciesController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Breadcrumb"] = "Retention Policies";
            return View();
        }

        public IActionResult Create()
        {
            ViewData["Breadcrumb"] = "Retention Policies / Create";
            return View();
        }

        public IActionResult Details(Guid id)
        {
            ViewData["Breadcrumb"] = "Retention Policies / Details";
            return View();
        }
    }
}
