using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversityTimetable.Core.Scheduling;
using UniversityTimetable.Domain.Entities;
using UniversityTimetable.Domain.Enums;
using UniversityTimetable.Infrastructure.Data;
using UniversityTimetable.Infrastructure.Services;

namespace UniversityTimetable.Web.Controllers
{
    public class TimetableController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISchedulingEngine _schedulingEngine;
        private readonly TimetableExportService _exportService;

        public TimetableController(
            ApplicationDbContext context,
            ISchedulingEngine schedulingEngine,
            TimetableExportService exportService)
        {
            _context = context;
            _schedulingEngine = schedulingEngine;
            _exportService = exportService;
        }

        public async Task<IActionResult> Index()
        {
            var timetables = await _context.Timetables
                .Include(t => t.AcademicYear)
                .Include(t => t.Semester)
                .Include(t => t.Entries)
                .Include(t => t.ClashReports)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            ViewBag.ActiveAcademicYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            ViewBag.ActiveSemester = await _context.Semesters.FirstOrDefaultAsync(s => s.IsActive);

            return View(timetables);
        }

        [HttpPost]
        public async Task<IActionResult> GenerateAI(int academicYearId, int semesterId, string title, bool regenerateUnlockedOnly = false)
        {
            var year = await _context.AcademicYears.FindAsync(academicYearId);
            var semester = await _context.Semesters.FindAsync(semesterId);

            var timetable = await _context.Timetables
                .Include(t => t.Entries)
                .FirstOrDefaultAsync(t => t.AcademicYearId == academicYearId && t.SemesterId == semesterId);

            if (timetable == null)
            {
                timetable = new Timetable
                {
                    Title = string.IsNullOrEmpty(title) ? $"Master Timetable {year?.YearName} Sem {semester?.SemesterNumber}" : title,
                    AcademicYearId = academicYearId,
                    SemesterId = semesterId,
                    Status = TimetableStatus.Draft
                };
                _context.Timetables.Add(timetable);
                await _context.SaveChangesAsync();
            }

            var courses = await _context.Courses
                .Include(c => c.Department)
                .Include(c => c.Programme)
                .Include(c => c.AssignedLecturer)
                .ToListAsync();

            var lecturers = await _context.Lecturers.ToListAsync();
            var classrooms = await _context.Classrooms.Include(c => c.Building).ToListAsync();
            var studentGroups = await _context.StudentGroups.Include(g => g.Programme).ToListAsync();

            // Fetch break times from SystemSettings
            var breakStartSetting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "BreakStartTime");
            var breakEndSetting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "BreakEndTime");

            TimeSpan breakStart = breakStartSetting != null ? TimeSpan.Parse(breakStartSetting.Value) : new TimeSpan(12, 30, 0);
            TimeSpan breakEnd = breakEndSetting != null ? TimeSpan.Parse(breakEndSetting.Value) : new TimeSpan(13, 0, 0);

            var options = new SchedulingOptions
            {
                BreakStartTime = breakStart,
                BreakEndTime = breakEnd,
                RegenerateOnlyUnlocked = regenerateUnlockedOnly
            };

            var result = await _schedulingEngine.GenerateTimetableAsync(timetable, courses, lecturers, classrooms, studentGroups, options);

            // Update Database with Generated Entries, Logs, and Clash Reports
            if (!regenerateUnlockedOnly)
            {
                var existingEntries = _context.TimetableEntries.Where(e => e.TimetableId == timetable.Id);
                _context.TimetableEntries.RemoveRange(existingEntries);
            }
            else
            {
                var unlockedEntries = _context.TimetableEntries.Where(e => e.TimetableId == timetable.Id && !e.IsLocked);
                _context.TimetableEntries.RemoveRange(unlockedEntries);
            }

            foreach (var entry in result.GeneratedEntries.Where(e => e.Id == 0))
            {
                entry.TimetableId = timetable.Id;
                _context.TimetableEntries.Add(entry);
            }

            // Remove previous clash reports & add new
            var oldClashes = _context.ClashReports.Where(c => c.TimetableId == timetable.Id);
            _context.ClashReports.RemoveRange(oldClashes);
            _context.ClashReports.AddRange(result.ClashReports);

            _context.AISchedulingLogs.AddRange(result.ExplainabilityLogs);

            timetable.Status = result.Success ? TimetableStatus.Validated : TimetableStatus.Draft;
            timetable.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = result.SummaryMessage;
            return RedirectToAction(nameof(Editor), new { id = timetable.Id });
        }

        public async Task<IActionResult> Editor(int id)
        {
            var timetable = await _context.Timetables
                .Include(t => t.AcademicYear)
                .Include(t => t.Semester)
                .Include(t => t.Entries).ThenInclude(e => e.Course)
                .Include(t => t.Entries).ThenInclude(e => e.Lecturer)
                .Include(t => t.Entries).ThenInclude(e => e.Classroom)
                .Include(t => t.Entries).ThenInclude(e => e.StudentGroup)
                .Include(t => t.ClashReports)
                .Include(t => t.AISchedulingLogs)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (timetable == null) return NotFound();

            ViewBag.Courses = await _context.Courses.ToListAsync();
            ViewBag.Lecturers = await _context.Lecturers.ToListAsync();
            ViewBag.Classrooms = await _context.Classrooms.ToListAsync();
            ViewBag.StudentGroups = await _context.StudentGroups.ToListAsync();

            return View(timetable);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleLock(int entryId)
        {
            var entry = await _context.TimetableEntries.FindAsync(entryId);
            if (entry != null)
            {
                entry.IsLocked = !entry.IsLocked;
                await _context.SaveChangesAsync();
                return Json(new { success = true, isLocked = entry.IsLocked });
            }
            return Json(new { success = false });
        }

        [HttpPost]
        public async Task<IActionResult> MoveEntry(int entryId, DayOfWeek day, string startTimeStr, int roomId)
        {
            var entry = await _context.TimetableEntries.FindAsync(entryId);
            if (entry == null) return Json(new { success = false, message = "Entry not found" });

            TimeSpan startTime = TimeSpan.Parse(startTimeStr);
            TimeSpan endTime = startTime.Add(TimeSpan.FromHours(entry.DurationHours));

            entry.DayOfWeek = day;
            entry.StartTime = startTime;
            entry.EndTime = endTime;
            entry.ClassroomId = roomId;
            entry.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Re-scan clashes
            var timetable = await _context.Timetables.Include(t => t.Entries).FirstOrDefaultAsync(t => t.Id == entry.TimetableId);
            var courses = await _context.Courses.ToListAsync();
            var classrooms = await _context.Classrooms.ToListAsync();
            var groups = await _context.StudentGroups.ToListAsync();

            var clashResult = ClashDetectionEngine.DetectAllClashes(timetable!.Id, timetable.Entries.ToList(), courses, classrooms, groups, new TimeSpan(12, 30, 0), new TimeSpan(13, 0, 0));

            var oldClashes = _context.ClashReports.Where(c => c.TimetableId == timetable.Id);
            _context.ClashReports.RemoveRange(oldClashes);
            _context.ClashReports.AddRange(clashResult.Reports);
            await _context.SaveChangesAsync();

            return Json(new { success = true, clashCount = clashResult.Reports.Count });
        }

        [HttpPost]
        public async Task<IActionResult> Publish(int id)
        {
            var timetable = await _context.Timetables.FindAsync(id);
            if (timetable != null)
            {
                timetable.Status = TimetableStatus.Published;
                timetable.PublishedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Timetable published successfully! Now visible on Student Portal.";
            }
            return RedirectToAction(nameof(Editor), new { id });
        }

        public async Task<IActionResult> ExportCsv(int id)
        {
            var timetable = await _context.Timetables
                .Include(t => t.AcademicYear)
                .Include(t => t.Semester)
                .Include(t => t.Entries).ThenInclude(e => e.Course)
                .Include(t => t.Entries).ThenInclude(e => e.Lecturer)
                .Include(t => t.Entries).ThenInclude(e => e.Classroom)
                .Include(t => t.Entries).ThenInclude(e => e.StudentGroup)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (timetable == null) return NotFound();

            byte[] csvData = _exportService.ExportTimetableToCsv(timetable);
            return File(csvData, "text/csv", $"Timetable_Export_{timetable.Id}.csv");
        }

        public async Task<IActionResult> ExportPrintHtml(int id)
        {
            var timetable = await _context.Timetables
                .Include(t => t.AcademicYear)
                .Include(t => t.Semester)
                .Include(t => t.Entries).ThenInclude(e => e.Course)
                .Include(t => t.Entries).ThenInclude(e => e.Lecturer)
                .Include(t => t.Entries).ThenInclude(e => e.Classroom)
                .Include(t => t.Entries).ThenInclude(e => e.StudentGroup)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (timetable == null) return NotFound();

            string html = _exportService.GeneratePrintableHtml(timetable, timetable.Title);
            return Content(html, "text/html");
        }
    }
}
