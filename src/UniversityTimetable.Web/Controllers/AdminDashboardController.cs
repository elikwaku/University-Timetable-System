using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversityTimetable.Infrastructure.Data;

namespace UniversityTimetable.Web.Controllers
{
    [Route("Admin")]
    [Route("Admin/Dashboard")]
    [Route("Admin/[controller]")]
    public class AdminDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminDashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalDepartments = await _context.Departments.CountAsync();
            ViewBag.TotalProgrammes = await _context.Programmes.CountAsync();
            ViewBag.TotalCourses = await _context.Courses.CountAsync();
            ViewBag.TotalLecturers = await _context.Lecturers.CountAsync();
            ViewBag.TotalStudents = await _context.Students.CountAsync();
            ViewBag.TotalClassrooms = await _context.Classrooms.CountAsync();

            var activeTimetable = await _context.Timetables
                .Include(t => t.Entries)
                .Include(t => t.ClashReports)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            ViewBag.ActiveTimetable = activeTimetable;
            ViewBag.TotalClashes = activeTimetable?.ClashReports.Count(c => !c.IsResolved) ?? 0;
            ViewBag.TotalScheduledEntries = activeTimetable?.Entries.Count ?? 0;

            // Chart data preparation
            var roomTypes = await _context.Classrooms
                .GroupBy(c => c.RoomType)
                .Select(g => new { RoomType = g.Key.ToString(), Count = g.Count() })
                .ToListAsync();

            ViewBag.RoomTypesJson = System.Text.Json.JsonSerializer.Serialize(roomTypes);

            var logs = await _context.AISchedulingLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(8)
                .ToListAsync();

            return View("~/Views/AdminDashboard/Index.cshtml", logs);
        }
    }
}
