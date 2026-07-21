using System;
using System.Linq;
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
            var settings = await _context.SystemSettings
                .OrderBy(s => s.Category)
                .ThenBy(s => s.Key)
                .ToListAsync();
            return View("~/Views/SystemSettings/Index.cshtml", settings);
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create(string key, string value, string category, string description)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                TempData["Error"] = "Rule Key is required.";
                return RedirectToAction(nameof(Index));
            }

            var trimmedKey = key.Trim();
            var existing = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key.ToLower() == trimmedKey.ToLower());

            if (existing != null)
            {
                TempData["Error"] = $"A scheduling rule with key '{trimmedKey}' already exists.";
                return RedirectToAction(nameof(Index));
            }

            var setting = new SystemSetting
            {
                Key = trimmedKey,
                Value = value?.Trim() ?? string.Empty,
                Category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim(),
                Description = description?.Trim() ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            _context.SystemSettings.Add(setting);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Scheduling rule '{setting.Key}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("SaveSetting")]
        public async Task<IActionResult> SaveSetting(string key, string value, string? category = null, string? description = null)
        {
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (setting != null)
            {
                setting.Value = value?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(category))
                {
                    setting.Category = category.Trim();
                }
                if (description != null)
                {
                    setting.Description = description.Trim();
                }
                setting.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Rule '{setting.Key}' updated successfully.";
            }
            else
            {
                TempData["Error"] = $"Rule with key '{key}' not found.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var setting = await _context.SystemSettings.FindAsync(id);
            if (setting != null)
            {
                setting.IsDeleted = true;
                setting.DeletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Scheduling rule '{setting.Key}' deleted successfully.";
            }
            else
            {
                TempData["Error"] = "Rule not found.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
