using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RockDMZ.Infrastructure;

namespace RockDMZ.Features.Home
{
    public class HomeController : Controller
    {
        private readonly ToolsContext _db;

        public HomeController(ToolsContext db)
        {
            _db = db;
        }
        public async Task<IActionResult> Index()
        {
            
            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}