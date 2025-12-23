using Declarify.Services;
using Microsoft.AspNetCore.Mvc;

namespace Declarify.Controllers
{
    public class ExecutiveController : Controller
    {
        private readonly IEmployeeDOIService _doiService;
        public ExecutiveController(IEmployeeDOIService doiService)
        {
            _doiService = doiService;
        }

        [HttpGet]
        public async Task<IActionResult> ExecutiveDashboard()
        {
            var model = await _doiService.GetExecutiveDashboardAsync();
            return View(model);
        }
        public IActionResult Index()
        {
            return View();
        }
    }
}
