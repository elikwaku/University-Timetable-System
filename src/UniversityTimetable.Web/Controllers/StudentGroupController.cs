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
        private readonly StudentCsvImporter _csvImporter;

        public StudentGroupController(ApplicationDbContext context, StudentCsvImporter csvImporter)
        {
            _context = context;
            _csvImporter = csvImporter;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            ViewBag.Programmes = await _context.Programmes.ToListAsync();
            var groups = await _context.StudentGroups
                .Include(g => g.Programme)
                .Include(g => g.Students)
                .ToListAsync();
            return View("~/Views/StudentGroup/Index.cshtml", groups);
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create(StudentGroup group)
        {
            _context.StudentGroups.Add(group);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Student Group '{group.Name}' created successfully.";
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

        [HttpPost("ImportCsv")]
        public async Task<IActionResult> ImportCsv(IFormFile csvFile)
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                TempData["Error"] = "Please select a valid CSV file.";
                return RedirectToAction(nameof(Index));
            }

            using (var stream = csvFile.OpenReadStream())
            {
                var result = await _csvImporter.ImportStudentsFromCsvAsync(stream, async code =>
                {
                    return await _context.Programmes.FirstOrDefaultAsync(p => p.Code == code);
                });

                TempData["Success"] = $"Successfully parsed {result.TotalImported} student records across {result.GroupSuggestions.Count} programme groups.";
                ViewBag.ImportSuggestions = result.GroupSuggestions;
            }

            var groups = await _context.StudentGroups.Include(g => g.Programme).Include(g => g.Students).ToListAsync();
            ViewBag.Programmes = await _context.Programmes.ToListAsync();
            return View("~/Views/StudentGroup/Index.cshtml", groups);
        }
    }
}
