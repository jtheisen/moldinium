using Microsoft.AspNetCore.Mvc;
using SampleApp.AspNetMvc.Models;
using System.Diagnostics;

namespace SampleApp.AspNetMvc.Controllers
{
    public class HomeController : Controller
    {
        private readonly JobList jobList;

        public HomeController(JobList jobList)
        {
            this.jobList = jobList;
        }

        public IActionResult AddSimpleJob()
        {
            jobList.AddSimpleJobCommand.Execute(null);

            return RedirectToAction(nameof(Index));
        }

        public IActionResult AddComplexJob()
        {
            jobList.AddComplexJobCommand.Execute(null);

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Cancel()
        {
            jobList.CancelCommand.Execute(null);

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Index()
        {
            return View(jobList);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}