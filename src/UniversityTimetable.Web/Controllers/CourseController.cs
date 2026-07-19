using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversityTimetable.Domain.Entities;
using UniversityTimetable.Infrastructure.Data;

namespace UniversityTimetable.Web.Controllers
{
    [Route("Admin/[controller]")]
    public class CourseController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CourseController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            ViewBag.Departments = await _context.Departments.ToListAsync();
            ViewBag.Programmes = await _context.Programmes.ToListAsync();
            ViewBag.Lecturers = await _context.Lecturers.ToListAsync();

            var courses = await _context.Courses
                .Include(c => c.Department)
                .Include(c => c.Programme)
                .Include(c => c.AssignedLecturer)
                .ToListAsync();
            return View("~/Views/Course/Index.cshtml", courses);
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create(Course course)
        {
            if (course.WeeklyContactHours < 4) course.WeeklyContactHours = 4;

            _context.Courses.Add(course);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Course '{course.Code} - {course.Title}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Edit")]
        public async Task<IActionResult> Edit(Course course)
        {
            var existing = await _context.Courses.FindAsync(course.Id);
            if (existing != null)
            {
                existing.Code = course.Code;
                existing.ShortForm = course.ShortForm;
                existing.Title = course.Title;
                existing.CreditHours = course.CreditHours;
                existing.WeeklyContactHours = course.WeeklyContactHours < 4 ? 4 : course.WeeklyContactHours;
                existing.DepartmentId = course.DepartmentId;
                existing.ProgrammeId = course.ProgrammeId;
                existing.Level = course.Level;
                existing.AssignedLecturerId = course.AssignedLecturerId;
                existing.ComputerLabRequired = course.ComputerLabRequired;
                existing.LabRequired = course.LabRequired;
                existing.PracticalRequired = course.PracticalRequired;
                existing.FieldWorkRequired = course.FieldWorkRequired;
                existing.UpdatedAt = System.DateTime.UtcNow;

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Course '{course.Code}' updated successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.Courses.FindAsync(id);
            if (existing != null)
            {
                existing.IsDeleted = true;
                existing.DeletedAt = System.DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Course deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
