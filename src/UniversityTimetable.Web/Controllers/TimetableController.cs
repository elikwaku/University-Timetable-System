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
    [Route("Admin/[controller]")]
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

        [HttpGet("")]
        [HttpGet("Index")]
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

            return View("~/Views/Timetable/Index.cshtml", timetables);
        }

        [HttpPost("GenerateAI")]
        public async Task<IActionResult> GenerateAI(
            int academicYearId,
            int semesterId,
            string title,
            string startTime = "06:30",
            string endTime = "19:00",
            bool regenerateUnlockedOnly = false,
            bool createNewVersion = true)
        {
            var year = await _context.AcademicYears.FindAsync(academicYearId);
            var semester = await _context.Semesters.FindAsync(semesterId);

            Timetable? timetable = null;
            if (!createNewVersion)
            {
                timetable = await _context.Timetables
                    .Include(t => t.Entries)
                    .FirstOrDefaultAsync(t => t.AcademicYearId == academicYearId && t.SemesterId == semesterId);
            }

            if (timetable == null)
            {
                timetable = new Timetable
                {
                    Title = string.IsNullOrWhiteSpace(title) ? $"Master Timetable {year?.YearName} Sem {semester?.SemesterNumber}" : title.Trim(),
                    AcademicYearId = academicYearId,
                    SemesterId = semesterId,
                    Status = TimetableStatus.Draft,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Timetables.Add(timetable);
                await _context.SaveChangesAsync();
            }
            else if (!string.IsNullOrWhiteSpace(title))
            {
                timetable.Title = title.Trim();
            }

            var courses = await _context.Courses
                .Include(c => c.Department)
                .Include(c => c.Programme)
                .Include(c => c.AssignedLecturer)
                .ToListAsync();

            var lecturers = await _context.Lecturers.ToListAsync();
            var classrooms = await _context.Classrooms.Include(c => c.Building).ToListAsync();
            var studentGroups = await _context.StudentGroups.Include(g => g.Programme).ToListAsync();

            // Fetch day start/end & break times from SystemSettings or parameters
            var breakStartSetting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "BreakStartTime");
            var breakEndSetting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "BreakEndTime");

            TimeSpan dayStart = TimeSpan.TryParse(startTime, out var ds) ? ds : new TimeSpan(6, 30, 0);
            TimeSpan dayEnd = TimeSpan.TryParse(endTime, out var de) ? de : new TimeSpan(19, 0, 0);
            TimeSpan breakStart = breakStartSetting != null ? TimeSpan.Parse(breakStartSetting.Value) : new TimeSpan(12, 30, 0);
            TimeSpan breakEnd = breakEndSetting != null ? TimeSpan.Parse(breakEndSetting.Value) : new TimeSpan(13, 0, 0);

            var options = new SchedulingOptions
            {
                DayStartTime = dayStart,
                DayEndTime = dayEnd,
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

        [HttpPost("Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var timetable = await _context.Timetables
                .Include(t => t.Entries)
                .Include(t => t.ClashReports)
                .Include(t => t.AISchedulingLogs)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (timetable != null)
            {
                timetable.IsDeleted = true;
                timetable.DeletedAt = DateTime.UtcNow;

                foreach (var entry in timetable.Entries)
                {
                    entry.IsDeleted = true;
                    entry.DeletedAt = DateTime.UtcNow;
                }

                foreach (var report in timetable.ClashReports)
                {
                    report.IsDeleted = true;
                    report.DeletedAt = DateTime.UtcNow;
                }

                foreach (var log in timetable.AISchedulingLogs)
                {
                    log.IsDeleted = true;
                    log.DeletedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Timetable '{timetable.Title}' deleted successfully.";
            }
            else
            {
                TempData["Error"] = "Timetable not found.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("ResolveClashes")]
        public async Task<IActionResult> ResolveClashes(int id)
        {
            var timetable = await _context.Timetables
                .Include(t => t.AcademicYear)
                .Include(t => t.Semester)
                .Include(t => t.Entries).ThenInclude(e => e.Course)
                .Include(t => t.Entries).ThenInclude(e => e.Lecturer)
                .Include(t => t.Entries).ThenInclude(e => e.Classroom)
                .Include(t => t.Entries).ThenInclude(e => e.StudentGroup)
                .Include(t => t.ClashReports)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (timetable == null)
            {
                TempData["Error"] = "Timetable not found.";
                return RedirectToAction(nameof(Index));
            }

            var activeClashes = timetable.ClashReports.Where(c => !c.IsResolved).ToList();
            if (!activeClashes.Any())
            {
                TempData["Success"] = "No active clashes found in this timetable!";
                return RedirectToAction(nameof(Editor), new { id = timetable.Id });
            }

            var clashingEntryIds = new HashSet<int>();
            foreach (var clash in activeClashes)
            {
                if (!string.IsNullOrEmpty(clash.AffectedEntryIds))
                {
                    var parts = clash.AffectedEntryIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var p in parts)
                    {
                        if (int.TryParse(p.Trim(), out int entryId))
                        {
                            clashingEntryIds.Add(entryId);
                        }
                    }
                }
            }

            var originalLockStates = new Dictionary<int, bool>();
            foreach (var entry in timetable.Entries)
            {
                originalLockStates[entry.Id] = entry.IsLocked;
                if (clashingEntryIds.Contains(entry.Id))
                {
                    entry.IsLocked = false;
                }
                else
                {
                    entry.IsLocked = true;
                }
            }

            var courses = await _context.Courses
                .Include(c => c.Department)
                .Include(c => c.Programme)
                .Include(c => c.AssignedLecturer)
                .ToListAsync();

            var lecturers = await _context.Lecturers.ToListAsync();
            var classrooms = await _context.Classrooms.Include(c => c.Building).ToListAsync();
            var studentGroups = await _context.StudentGroups.Include(g => g.Programme).ToListAsync();

            var breakStartSetting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "BreakStartTime");
            var breakEndSetting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "BreakEndTime");

            TimeSpan dayStart = new TimeSpan(6, 30, 0);
            TimeSpan dayEnd = new TimeSpan(19, 0, 0);
            TimeSpan breakStart = breakStartSetting != null ? TimeSpan.Parse(breakStartSetting.Value) : new TimeSpan(12, 30, 0);
            TimeSpan breakEnd = breakEndSetting != null ? TimeSpan.Parse(breakEndSetting.Value) : new TimeSpan(13, 0, 0);

            var options = new SchedulingOptions
            {
                DayStartTime = dayStart,
                DayEndTime = dayEnd,
                BreakStartTime = breakStart,
                BreakEndTime = breakEnd,
                RegenerateOnlyUnlocked = true
            };

            var result = await _schedulingEngine.GenerateTimetableAsync(timetable, courses, lecturers, classrooms, studentGroups, options);

            var unlockedEntries = _context.TimetableEntries.Where(e => e.TimetableId == timetable.Id && !e.IsLocked);
            _context.TimetableEntries.RemoveRange(unlockedEntries);

            foreach (var entry in result.GeneratedEntries.Where(e => e.Id == 0))
            {
                entry.TimetableId = timetable.Id;
                _context.TimetableEntries.Add(entry);
            }

            foreach (var entry in timetable.Entries)
            {
                if (originalLockStates.TryGetValue(entry.Id, out bool wasLocked))
                {
                    entry.IsLocked = wasLocked;
                }
            }

            var oldClashes = _context.ClashReports.Where(c => c.TimetableId == timetable.Id);
            _context.ClashReports.RemoveRange(oldClashes);
            _context.ClashReports.AddRange(result.ClashReports);

            _context.AISchedulingLogs.AddRange(result.ExplainabilityLogs);

            int remainingClashes = result.ClashReports.Count(c => !c.IsResolved);
            timetable.Status = remainingClashes == 0 ? TimetableStatus.Validated : TimetableStatus.Draft;
            timetable.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            if (remainingClashes < activeClashes.Count)
            {
                TempData["Success"] = $"Automated Clash Resolution: Reduced active clashes from {activeClashes.Count} to {remainingClashes}.";
            }
            else
            {
                TempData["Success"] = result.SummaryMessage;
            }

            return RedirectToAction(nameof(Editor), new { id = timetable.Id });
        }

        [HttpGet("Editor/{id}")]
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

            return View("~/Views/Timetable/Editor.cshtml", timetable);
        }

        [HttpPost("ToggleLock")]
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

        [HttpPost("MoveEntry")]
        public async Task<IActionResult> MoveEntry(int entryId, DayOfWeek day, string startTimeStr, int roomId)
        {
            var entry = await _context.TimetableEntries.FindAsync(entryId);
            if (entry == null) return Json(new { success = false, message = "Entry not found" });

            var targetRoom = await _context.Classrooms.FindAsync(roomId);

            TimeSpan startTime = TimeSpan.Parse(startTimeStr);
            TimeSpan endTime = startTime.Add(TimeSpan.FromHours(entry.DurationHours));

            entry.DayOfWeek = day;
            entry.StartTime = startTime;
            entry.EndTime = endTime;
            entry.ClassroomId = roomId;

            if (targetRoom != null && targetRoom.Capacity > 100)
            {
                entry.SessionType = SessionType.CombinedClass;
                entry.Remarks = "Combined Class Session (Room Cap > 100)";
            }

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

        [HttpPost("Publish")]
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

        [HttpGet("ExportCsv/{id}")]
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

        [HttpGet("ExportPrintHtml/{id}")]
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

        [HttpGet("ExportExcel/{id}")]
        public async Task<IActionResult> ExportExcel(int id)
        {
            var timetable = await _context.Timetables
                .Include(t => t.AcademicYear)
                .Include(t => t.Semester)
                .Include(t => t.Entries).ThenInclude(e => e.Course).ThenInclude(c => c!.Department)
                .Include(t => t.Entries).ThenInclude(e => e.Lecturer)
                .Include(t => t.Entries).ThenInclude(e => e.Classroom)
                .Include(t => t.Entries).ThenInclude(e => e.StudentGroup)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (timetable == null) return NotFound();

            string deptName = timetable.Entries?.FirstOrDefault(e => e.Course?.Department != null)?.Course?.Department?.Name
                              ?? "DEPARTMENT OF COMPUTER SCIENCE & ACADEMICS";

            byte[] excelData = _exportService.ExportTimetableToExcel(timetable, "UNIVERSITY OF GHANA", deptName);
            string sanitizedTitle = string.Concat(timetable.Title.Split(Path.GetInvalidFileNameChars()));
            string fileName = $"{sanitizedTitle}_Timetable.xlsx";

            return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}
