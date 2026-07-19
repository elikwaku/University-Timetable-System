using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversityTimetable.Domain.Entities;
using UniversityTimetable.Infrastructure.Data;

namespace UniversityTimetable.Web.Controllers
{
    [Route("Admin/[controller]")]
    public class LecturerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LecturerController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            ViewBag.Departments = await _context.Departments.ToListAsync();
            var lecturers = await _context.Lecturers
                .Include(l => l.Department)
                .Include(l => l.AssignedCourses)
                .ToListAsync();
            return View("~/Views/Lecturer/Index.cshtml", lecturers);
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create(Lecturer lecturer)
        {
            _context.Lecturers.Add(lecturer);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Lecturer '{lecturer.FullName}' registered successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Edit")]
        public async Task<IActionResult> Edit(Lecturer lecturer)
        {
            var existing = await _context.Lecturers.FindAsync(lecturer.Id);
            if (existing != null)
            {
                existing.EmployeeId = lecturer.EmployeeId;
                existing.FullName = lecturer.FullName;
                existing.Email = lecturer.Email;
                existing.Phone = lecturer.Phone;
                existing.DepartmentId = lecturer.DepartmentId;
                existing.MaxWeeklyWorkloadHours = lecturer.MaxWeeklyWorkloadHours;
                existing.AvailableDays = lecturer.AvailableDays;
                existing.PreferredTimeSlots = lecturer.PreferredTimeSlots;
                existing.UpdatedAt = System.DateTime.UtcNow;

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Lecturer '{lecturer.FullName}' updated successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.Lecturers.FindAsync(id);
            if (existing != null)
            {
                existing.IsDeleted = true;
                existing.DeletedAt = System.DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Lecturer deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
