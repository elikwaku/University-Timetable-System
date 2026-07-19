using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversityTimetable.Domain.Entities;
using UniversityTimetable.Domain.Enums;
using UniversityTimetable.Infrastructure.Data;
using UniversityTimetable.Infrastructure.Identity;

namespace UniversityTimetable.Web.Controllers
{
    public class StudentDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public StudentDashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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
                    .FirstOrDefaultAsync() ?? new Student { FullName = "Demo Student", IndexNumber = "CS/2025/0001", Level = DegreeLevel.Level200 };
            }

            ViewBag.Student = student;

            var publishedTimetable = await _context.Timetables
                .Include(t => t.Entries).ThenInclude(e => e.Course)
                .Include(t => t.Entries).ThenInclude(e => e.Lecturer)
                .Include(t => t.Entries).ThenInclude(e => e.Classroom)
                .Where(t => t.Status == TimetableStatus.Published || t.Status == TimetableStatus.Validated)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            var today = DateTime.Today.DayOfWeek;
            var todayEntries = publishedTimetable?.Entries
                .Where(e => e.DayOfWeek == today &&
                            e.Course != null &&
                            e.Course.ProgrammeId == student.ProgrammeId &&
                            e.Course.Level == student.Level &&
                            (e.StudentGroupId == null || e.StudentGroupId == student.StudentGroupId))
                .OrderBy(e => e.StartTime)
                .ToList() ?? new System.Collections.Generic.List<TimetableEntry>();

            ViewBag.TodayEntries = todayEntries;
            ViewBag.TotalEnrolledCourses = await _context.Courses.CountAsync(c => c.ProgrammeId == student.ProgrammeId && c.Level == student.Level);

            return View(student);
        }
    }
}
