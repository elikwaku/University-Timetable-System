using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversityTimetable.Domain.Entities;
using UniversityTimetable.Infrastructure.Data;
using UniversityTimetable.Infrastructure.Services;

namespace UniversityTimetable.Web.Controllers
{
    [Route("Admin/[controller]")]
    public class StudentGroupController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudentGroupController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            ViewBag.Programmes = await _context.Programmes.ToListAsync();
            var groups = await _context.StudentGroups
                .Include(g => g.Programme)
                .ToListAsync();
            return View("~/Views/StudentGroup/Index.cshtml", groups);
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create(StudentGroup group)
        {
            if (group.StudentCount > 80 && !group.IgnoreSplit)
            {
                int half = group.StudentCount / 2;
                int rem = group.StudentCount - half;

                var grpA = new StudentGroup
                {
                    Name = $"{group.Name} (Group A)",
                    ProgrammeId = group.ProgrammeId,
                    Level = group.Level,
                    StudentCount = half,
                    CreatedAt = System.DateTime.UtcNow
                };

                var grpB = new StudentGroup
                {
                    Name = $"{group.Name} (Group B)",
                    ProgrammeId = group.ProgrammeId,
                    Level = group.Level,
                    StudentCount = rem,
                    CreatedAt = System.DateTime.UtcNow
                };

                _context.StudentGroups.AddRange(grpA, grpB);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Student Count ({group.StudentCount}) exceeds 80. Automatically split into '{grpA.Name}' ({half}) and '{grpB.Name}' ({rem}).";
            }
            else
            {
                _context.StudentGroups.Add(group);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Student Group '{group.Name}' created successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("AutoSplit")]
        public async Task<IActionResult> AutoSplit(int id)
        {
            var existing = await _context.StudentGroups.FindAsync(id);
            if (existing != null && existing.StudentCount > 80)
            {
                int half = existing.StudentCount / 2;
                int rem = existing.StudentCount - half;

                string baseName = existing.Name.Replace(" (Group A)", "").Replace(" (Group B)", "").Trim();

                existing.Name = $"{baseName} (Group A)";
                existing.StudentCount = half;
                existing.IgnoreSplit = false;
                existing.UpdatedAt = System.DateTime.UtcNow;

                var grpB = new StudentGroup
                {
                    Name = $"{baseName} (Group B)",
                    ProgrammeId = existing.ProgrammeId,
                    Level = existing.Level,
                    StudentCount = rem,
                    IgnoreSplit = false,
                    CreatedAt = System.DateTime.UtcNow
                };

                _context.StudentGroups.Add(grpB);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Group split into 2 groups: '{existing.Name}' ({half} students) and '{grpB.Name}' ({rem} students). Both will have equal weekly lecturer meeting times and course contact hours.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("ToggleIgnoreSplit")]
        public async Task<IActionResult> ToggleIgnoreSplit(int id)
        {
            var existing = await _context.StudentGroups.FindAsync(id);
            if (existing != null)
            {
                existing.IgnoreSplit = !existing.IgnoreSplit;
                existing.UpdatedAt = System.DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = existing.IgnoreSplit
                    ? $"Group '{existing.Name}' set to ignore >80 auto-split."
                    : $"Group '{existing.Name}' auto-split rule re-enabled.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Edit")]
        public async Task<IActionResult> Edit(StudentGroup group)
        {
            var existing = await _context.StudentGroups.FindAsync(group.Id);
            if (existing != null)
            {
                existing.Name = group.Name;
                existing.ProgrammeId = group.ProgrammeId;
                existing.Level = group.Level;
                existing.StudentCount = group.StudentCount;
                existing.IgnoreSplit = group.IgnoreSplit;
                existing.UpdatedAt = System.DateTime.UtcNow;

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Student Group '{group.Name}' updated successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.StudentGroups.FindAsync(id);
            if (existing != null)
            {
                existing.IsDeleted = true;
                existing.DeletedAt = System.DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Student Group deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
