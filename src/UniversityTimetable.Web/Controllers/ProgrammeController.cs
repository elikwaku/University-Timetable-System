using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversityTimetable.Domain.Entities;
using UniversityTimetable.Infrastructure.Data;

namespace UniversityTimetable.Web.Controllers
{
    [Route("Admin/[controller]")]
    public class ProgrammeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProgrammeController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            ViewBag.Departments = await _context.Departments.ToListAsync();
            var progs = await _context.Programmes
                .Include(p => p.Department)
                .Include(p => p.Courses)
                .Include(p => p.StudentGroups)
                .ToListAsync();
            return View("~/Views/Programme/Index.cshtml", progs);
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create(Programme prog)
        {
            if (ModelState.IsValid)
            {
                _context.Programmes.Add(prog);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Programme '{prog.Name}' created successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Edit")]
        public async Task<IActionResult> Edit(Programme prog)
        {
            var existing = await _context.Programmes.FindAsync(prog.Id);
            if (existing != null)
            {
                existing.Code = prog.Code;
                existing.Name = prog.Name;
                existing.DepartmentId = prog.DepartmentId;
                existing.UpdatedAt = System.DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Programme '{prog.Name}' updated successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.Programmes.FindAsync(id);
            if (existing != null)
            {
                existing.IsDeleted = true;
                existing.DeletedAt = System.DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Programme deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
