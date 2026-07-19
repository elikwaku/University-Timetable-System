using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversityTimetable.Domain.Entities;
using UniversityTimetable.Infrastructure.Data;

namespace UniversityTimetable.Web.Controllers
{
    [Route("Admin/[controller]")]
    public class DepartmentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DepartmentController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var depts = await _context.Departments
                .Include(d => d.Programmes)
                .Include(d => d.Lecturers)
                .ToListAsync();
            return View("~/Views/Department/Index.cshtml", depts);
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create(Department dept)
        {
            if (ModelState.IsValid)
            {
                _context.Departments.Add(dept);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Department '{dept.Name}' created successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Edit")]
        public async Task<IActionResult> Edit(Department dept)
        {
            var existing = await _context.Departments.FindAsync(dept.Id);
            if (existing != null)
            {
                existing.Code = dept.Code;
                existing.Name = dept.Name;
                existing.Faculty = dept.Faculty;
                existing.UpdatedAt = System.DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Department '{dept.Name}' updated successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.Departments.FindAsync(id);
            if (existing != null)
            {
                existing.IsDeleted = true;
                existing.DeletedAt = System.DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Department deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
