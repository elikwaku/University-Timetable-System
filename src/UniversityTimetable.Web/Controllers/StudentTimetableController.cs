using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversityTimetable.Domain.Entities;
using UniversityTimetable.Domain.Enums;
using UniversityTimetable.Infrastructure.Data;
using UniversityTimetable.Infrastructure.Identity;
using UniversityTimetable.Infrastructure.Services;

namespace UniversityTimetable.Web.Controllers
{
    public class StudentTimetableController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly TimetableExportService _exportService;

        public StudentTimetableController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            TimetableExportService exportService)
        {
            _context = context;
            _userManager = userManager;
            _exportService = exportService;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            int studentId = user?.AssociatedStudentId ?? 1;

            var student = await _context.Students
                .Include(s => s.Programme)
                .Include(s => s.StudentGroup)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null)
            {
                student = await _context.Students
                    .Include(s => s.Programme)
                    .Include(s => s.StudentGroup)
                    .FirstOrDefaultAsync() ?? new Student { FullName = "Demo Student", Level = DegreeLevel.Level200 };
            }

            ViewBag.Student = student;

            var publishedTimetable = await _context.Timetables
                .Include(t => t.AcademicYear)
                .Include(t => t.Semester)
                .Include(t => t.Entries).ThenInclude(e => e.Course)
                .Include(t => t.Entries).ThenInclude(e => e.Lecturer)
                .Include(t => t.Entries).ThenInclude(e => e.Classroom)
                .Where(t => t.Status == TimetableStatus.Published || t.Status == TimetableStatus.Validated)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            var studentEntries = publishedTimetable?.Entries
                .Where(e => e.Course != null &&
                            e.Course.ProgrammeId == student.ProgrammeId &&
                            e.Course.Level == student.Level &&
                            (e.StudentGroupId == null || e.StudentGroupId == student.StudentGroupId))
                .OrderBy(e => e.DayOfWeek).ThenBy(e => e.StartTime)
                .ToList() ?? new System.Collections.Generic.List<TimetableEntry>();

            ViewBag.Timetable = publishedTimetable;
            return View(studentEntries);
        }

        public async Task<IActionResult> ExportPrintHtml()
        {
            var user = await _userManager.GetUserAsync(User);
            int studentId = user?.AssociatedStudentId ?? 1;

            var student = await _context.Students.Include(s => s.Programme).FirstOrDefaultAsync(s => s.Id == studentId);

            var publishedTimetable = await _context.Timetables
                .Include(t => t.AcademicYear)
                .Include(t => t.Semester)
                .Include(t => t.Entries).ThenInclude(e => e.Course)
                .Include(t => t.Entries).ThenInclude(e => e.Lecturer)
                .Include(t => t.Entries).ThenInclude(e => e.Classroom)
                .Include(t => t.Entries).ThenInclude(e => e.StudentGroup)
                .Where(t => t.Status == TimetableStatus.Published || t.Status == TimetableStatus.Validated)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (publishedTimetable != null && student != null)
            {
                var filteredEntries = publishedTimetable.Entries
                    .Where(e => e.Course != null &&
                                e.Course.ProgrammeId == student.ProgrammeId &&
                                e.Course.Level == student.Level &&
                                (e.StudentGroupId == null || e.StudentGroupId == student.StudentGroupId))
                    .ToList();

                publishedTimetable.Entries = filteredEntries;
            }

            string html = _exportService.GeneratePrintableHtml(publishedTimetable ?? new Timetable(), $"Personal Schedule - {student?.FullName ?? "Student"}");
            return Content(html, "text/html");
        }
    }
}
