using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversityTimetable.Domain.Entities;
using UniversityTimetable.Infrastructure.Data;

namespace UniversityTimetable.Web.Controllers
{
    [Route("Admin/[controller]")]
    public class SystemSettingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SystemSettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var settings = await _context.SystemSettings.ToListAsync();
            return View("~/Views/SystemSettings/Index.cshtml", settings);
        }

        [HttpPost("SaveSetting")]
        public async Task<IActionResult> SaveSetting(string key, string value)
        {
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (setting != null)
            {
                setting.Value = value;
                setting.UpdatedAt = System.DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Setting '{setting.Key}' updated to '{value}'.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
