using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniversityTimetable.Domain.Entities;
using UniversityTimetable.Domain.Enums;

namespace UniversityTimetable.Core.Scheduling
{
    public class SchedulingOptions
    {
        public TimeSpan DayStartTime { get; set; } = new TimeSpan(6, 30, 0);
        public TimeSpan DayEndTime { get; set; } = new TimeSpan(19, 0, 0);
        public TimeSpan BreakStartTime { get; set; } = new TimeSpan(12, 30, 0);
        public TimeSpan BreakEndTime { get; set; } = new TimeSpan(13, 0, 0);
        public bool BalanceWorkload { get; set; } = true;
        public bool RegenerateOnlyUnlocked { get; set; } = false;
    }

    public class SchedulingResult
    {
        public bool Success { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public int TotalCoursesScheduled { get; set; }
        public int TotalUnscheduledCourses { get; set; }
        public double ClassroomUtilizationPercentage { get; set; }
        public List<TimetableEntry> GeneratedEntries { get; set; } = new List<TimetableEntry>();
        public List<AISchedulingLog> ExplainabilityLogs { get; set; } = new List<AISchedulingLog>();
        public List<ClashReport> ClashReports { get; set; } = new List<ClashReport>();
        public string SummaryMessage { get; set; } = string.Empty;
    }

    public interface ISchedulingEngine
    {
        Task<SchedulingResult> GenerateTimetableAsync(
            Timetable timetable,
            List<Course> courses,
            List<Lecturer> lecturers,
            List<Classroom> classrooms,
            List<StudentGroup> studentGroups,
            SchedulingOptions options);
    }

    public class AISchedulingEngine : ISchedulingEngine
    {
        private readonly IAIExplainabilityService _explainabilityService;

        public AISchedulingEngine(IAIExplainabilityService explainabilityService)
        {
            _explainabilityService = explainabilityService;
        }

        public async Task<SchedulingResult> GenerateTimetableAsync(
            Timetable timetable,
            List<Course> courses,
            List<Lecturer> lecturers,
            List<Classroom> classrooms,
            List<StudentGroup> studentGroups,
            SchedulingOptions options)
        {
            var startTime = DateTime.UtcNow;
            var result = new SchedulingResult();
            var logs = new List<AISchedulingLog>();
            var entries = new List<TimetableEntry>();

            // 1. Preserve Locked Entries if partial regeneration
            if (options.RegenerateOnlyUnlocked && timetable.Entries != null)
            {
                var locked = timetable.Entries.Where(e => e.IsLocked).ToList();
                entries.AddRange(locked);
                logs.Add(new AISchedulingLog
                {
                    TimetableId = timetable.Id,
                    StepName = "INITIALIZE_LOCKED_ENTRIES",
                    Action = "PRESERVE",
                    Rationale = $"Preserved {locked.Count} locked entries from overwrite.",
                    Details = $"Locked entries will remain fixed during partial re-scheduling."
                });
            }

            // 2. Generate Available Time Slots (1h & 2h windows, excluding break)
            var availableSlots = TimeSlotGenerator.GenerateAvailableTimeSlots(options.DayStartTime, options.DayEndTime, options.BreakStartTime, options.BreakEndTime);

            // 3. Build Demands for each Course & Sort: Give Computer Lab required courses highest priority, then sort by Student Count & Contact Hours
            var courseDemands = BuildCourseDemands(courses, studentGroups, classrooms)
                .OrderByDescending(d => d.Course.ComputerLabRequired)
                .ThenByDescending(d => d.Course.LabRequired)
                .ThenByDescending(d => d.StudentGroup?.StudentCount ?? d.EstimatedCombinedSize)
                .ThenByDescending(d => d.Course.WeeklyContactHours)
                .ToList();

            int scheduledCount = 0;
            int unscheduledCount = 0;

            foreach (var demand in courseDemands)
            {
                // Skip if course demands already satisfied by locked entries
                int existingHours = entries.Where(e => e.CourseId == demand.Course.Id &&
                                                      (demand.StudentGroup == null || e.StudentGroupId == demand.StudentGroup.Id)).Sum(e => e.DurationHours);
                int targetHours = demand.RequiredHours - existingHours;
                if (targetHours <= 0) continue;

                var combinations = TimeSlotGenerator.GetValidSessionCombinations(targetHours);
                var selectedCombination = combinations.FirstOrDefault() ?? new List<int> { 2, 2 };

                bool demandSuccess = true;

                foreach (var sessionDuration in selectedCombination)
                {
                    var validRoomTypes = GetRequiredRoomTypes(demand.Course, demand.SessionType);
                    var candidateRooms = classrooms.Where(r => r.IsActive && validRoomTypes.Contains(r.RoomType)).ToList();

                    // Prioritize room allocation based on student count
                    int demandSize = demand.StudentGroup?.StudentCount ?? demand.EstimatedCombinedSize;
                    if (demandSize > 80)
                    {
                        // Large classes (>80) get priority to use large capacity classrooms
                        candidateRooms = candidateRooms.Where(r => r.Capacity >= demandSize)
                            .OrderByDescending(r => r.Capacity).ToList();
                    }
                    else
                    {
                        // Standard classes use tightest fit rooms to preserve large rooms for large classes
                        candidateRooms = candidateRooms.Where(r => r.Capacity >= demandSize)
                            .OrderBy(r => r.Capacity).ToList();
                    }

                    if (!candidateRooms.Any())
                    {
                        // Fallback to any suitable room if capacity constraint is tight
                        candidateRooms = classrooms.Where(r => r.IsActive && validRoomTypes.Contains(r.RoomType)).OrderByDescending(r => r.Capacity).ToList();
                    }

                    var lecturer = demand.AssignedLecturer;
                    var candidateSlots = availableSlots.Where(s => s.DurationHours == sessionDuration).ToList();

                    // Score and select best (slot, room) pair based on AI Priority Heuristics
                    var bestPair = candidateSlots
                        .SelectMany(slot => candidateRooms.Select(room => new { Slot = slot, Room = room }))
                        .Where(sr => !HasCollision(sr.Slot, sr.Room.Id, lecturer.Id, demand.StudentGroup?.Id, demand.Course.ProgrammeId, demand.Course.Level, entries, options))
                        .OrderByDescending(sr => CalculateSlotScore(sr.Slot, sr.Room, lecturer, demand, entries))
                        .FirstOrDefault();

                    bool assigned = false;

                    if (bestPair != null)
                    {
                        var effectiveSessionType = (bestPair.Room.Capacity > 100) ? SessionType.CombinedClass : demand.SessionType;

                        var newEntry = new TimetableEntry
                        {
                            TimetableId = timetable.Id,
                            CourseId = demand.Course.Id,
                            LecturerId = lecturer.Id,
                            ClassroomId = bestPair.Room.Id,
                            StudentGroupId = demand.StudentGroup?.Id,
                            DayOfWeek = bestPair.Slot.DayOfWeek,
                            StartTime = bestPair.Slot.StartTime,
                            EndTime = bestPair.Slot.EndTime,
                            DurationHours = sessionDuration,
                            SessionType = effectiveSessionType,
                            Remarks = bestPair.Room.Capacity > 100
                                ? "Combined Class Session (Room Cap > 100)"
                                : (!string.IsNullOrEmpty(demand.GroupNameLabel) ? $"{demand.GroupNameLabel} Session" : null),
                            IsLocked = false
                        };

                        entries.Add(newEntry);
                        assigned = true;

                        string context = demand.StudentGroup != null
                            ? $"Assigned to {demand.StudentGroup.Name} in {bestPair.Room.RoomNumber} (Cap: {bestPair.Room.Capacity}, Type: {effectiveSessionType})."
                            : $"Combined Session for Course {demand.Course.Code} in {bestPair.Room.RoomNumber}.";

                        logs.Add(_explainabilityService.ExplainAssignment(timetable.Id, demand.Course, lecturer, bestPair.Room, demand.StudentGroup, bestPair.Slot, effectiveSessionType, context));
                    }

                    if (!assigned)
                    {
                        demandSuccess = false;
                        logs.Add(new AISchedulingLog
                        {
                            TimetableId = timetable.Id,
                            StepName = $"UNSCHEDULED_{demand.Course.Code}",
                            Action = "FAILED_PLACEMENT",
                            Rationale = $"Could not place {sessionDuration}h session for {demand.Course.Code}.",
                            Details = $"All candidate rooms and time slots resulted in lecturer/room/group clashes."
                        });
                    }
                }

                if (demandSuccess) scheduledCount++; else unscheduledCount++;
            }

            // 4. Calculate Utilization Metrics
            int totalSlotsPossible = classrooms.Count * availableSlots.Count;
            double utilization = totalSlotsPossible > 0 ? (double)entries.Count / totalSlotsPossible * 100.0 : 0.0;

            // 5. Run Clash Scanner for validation
            var clashResult = ClashDetectionEngine.DetectAllClashes(timetable.Id, entries, courses, classrooms, studentGroups, options.BreakStartTime, options.BreakEndTime);

            var endTime = DateTime.UtcNow;
            result.Success = !clashResult.HasCriticalClashes;
            result.ExecutionTime = endTime - startTime;
            result.TotalCoursesScheduled = scheduledCount;
            result.TotalUnscheduledCourses = unscheduledCount;
            result.ClassroomUtilizationPercentage = Math.Round(utilization, 2);
            result.GeneratedEntries = entries;
            result.ExplainabilityLogs = logs;
            result.ClashReports = clashResult.Reports;
            result.SummaryMessage = $"AI Timetable Generation completed in {result.ExecutionTime.TotalSeconds:F2}s. {scheduledCount} courses scheduled, {unscheduledCount} unscheduled. {clashResult.Reports.Count} total clashes detected.";

            return result;
        }

        private class SchedulingDemand
        {
            public Course Course { get; set; } = null!;
            public Lecturer AssignedLecturer { get; set; } = null!;
            public StudentGroup? StudentGroup { get; set; }
            public string? GroupNameLabel { get; set; }
            public int RequiredHours { get; set; }
            public SessionType SessionType { get; set; }
            public int EstimatedCombinedSize { get; set; }
        }

        private List<SchedulingDemand> BuildCourseDemands(List<Course> courses, List<StudentGroup> studentGroups, List<Classroom> classrooms)
        {
            var demands = new List<SchedulingDemand>();

            foreach (var course in courses)
            {
                var lecturer = course.AssignedLecturer ?? new Lecturer { Id = 1, FullName = "Department Staff" };

                var validRoomTypes = GetRequiredRoomTypes(course, course.ComputerLabRequired ? SessionType.ComputerLab : course.LabRequired ? SessionType.Lab : SessionType.Lecture);
                var suitableRooms = classrooms.Where(r => r.IsActive && validRoomTypes.Contains(r.RoomType)).ToList();
                int maxCapacity = suitableRooms.Any() ? suitableRooms.Max(c => c.Capacity) : 100;

                var progGroups = studentGroups.Where(g => g.ProgrammeId == course.ProgrammeId && g.Level == course.Level).ToList();
                int totalEnrolled = progGroups.Sum(g => g.StudentCount);
                if (totalEnrolled == 0) totalEnrolled = 60; // fallback default

                bool isExplicitlyIgnored = progGroups.Any(g => g.IgnoreSplit);
                bool exceedsThreshold = totalEnrolled > 80 && !isExplicitlyIgnored;
                bool needsCapacitySplit = exceedsThreshold || (totalEnrolled > maxCapacity && !isExplicitlyIgnored) || progGroups.Count > 1;

                if (needsCapacitySplit)
                {
                    if (progGroups.Count > 1)
                    {
                        // Multiple real DB student groups exist (e.g. Group A, Group B)
                        foreach (var group in progGroups)
                        {
                            var sessionType = course.ComputerLabRequired ? SessionType.ComputerLab
                                : course.LabRequired ? SessionType.Lab
                                : course.FieldWorkRequired ? SessionType.FieldWork
                                : SessionType.Lecture;

                            demands.Add(new SchedulingDemand
                            {
                                Course = course,
                                AssignedLecturer = lecturer,
                                StudentGroup = group, // Real DB entity
                                GroupNameLabel = group.Name,
                                RequiredHours = course.WeeklyContactHours,
                                SessionType = sessionType,
                                EstimatedCombinedSize = group.StudentCount
                            });
                        }
                    }
                    else
                    {
                        // Single real group or no group in DB, but enrollment > 80 or > room max capacity
                        // Auto-split into two equal groups (or more if capacity demands)
                        var realGroup = progGroups.FirstOrDefault();
                        int numGroups = exceedsThreshold ? 2 : (int)Math.Ceiling((double)totalEnrolled / maxCapacity);
                        if (numGroups < 2) numGroups = 2;
                        int baseSize = totalEnrolled / numGroups;

                        for (int i = 0; i < numGroups; i++)
                        {
                            char letter = (char)('A' + i);
                            int groupSize = (i == numGroups - 1) ? (totalEnrolled - (baseSize * (numGroups - 1))) : baseSize;

                            var sessionType = course.ComputerLabRequired ? SessionType.ComputerLab
                                : course.LabRequired ? SessionType.Lab
                                : course.FieldWorkRequired ? SessionType.FieldWork
                                : SessionType.Lecture;

                            demands.Add(new SchedulingDemand
                            {
                                Course = course,
                                AssignedLecturer = lecturer,
                                StudentGroup = realGroup, // Real DB entity (or null)
                                GroupNameLabel = $"Group {letter}",
                                RequiredHours = course.WeeklyContactHours, // Fulfills full required weekly contact hours for each group
                                SessionType = sessionType,
                                EstimatedCombinedSize = groupSize
                            });
                        }
                    }
                }
                else
                {
                    var realGroup = progGroups.FirstOrDefault();
                    var sessionType = course.ComputerLabRequired ? SessionType.ComputerLab
                        : course.LabRequired ? SessionType.Lab
                        : course.FieldWorkRequired ? SessionType.FieldWork
                        : SessionType.Lecture;

                    demands.Add(new SchedulingDemand
                    {
                        Course = course,
                        AssignedLecturer = lecturer,
                        StudentGroup = realGroup, // Real DB entity (or null)
                        GroupNameLabel = realGroup?.Name ?? "Main Group",
                        RequiredHours = course.WeeklyContactHours,
                        SessionType = sessionType,
                        EstimatedCombinedSize = totalEnrolled
                    });
                }
            }

            return demands;
        }

        private double CalculateSlotScore(
            TimeSlot slot,
            Classroom room,
            Lecturer lecturer,
            SchedulingDemand demand,
            List<TimetableEntry> entries)
        {
            double score = 0;
            int demandSize = demand.StudentGroup?.StudentCount ?? demand.EstimatedCombinedSize;

            // 1. Room Fit Efficiency (Prefer tighter fit so large rooms stay available)
            int excessCap = room.Capacity - demandSize;
            if (excessCap >= 0)
            {
                score += Math.Max(0, 50 - (excessCap / 2.0));
            }

            // 2. Lecturer Workload Balancing (Prefer days with fewer scheduled hours for lecturer)
            int lecturerHoursOnDay = entries
                .Where(e => e.LecturerId == lecturer.Id && e.DayOfWeek == slot.DayOfWeek)
                .Sum(e => e.DurationHours);
            score -= (lecturerHoursOnDay * 15.0);

            // 3. Student Workload Balancing (Prefer days with fewer scheduled hours for student group)
            int groupHoursOnDay = entries
                .Where(e => e.DayOfWeek == slot.DayOfWeek &&
                            ((demand.StudentGroup != null && e.StudentGroupId == demand.StudentGroup.Id) ||
                             (e.Course != null && e.Course.ProgrammeId == demand.Course.ProgrammeId && e.Course.Level == demand.Course.Level)))
                .Sum(e => e.DurationHours);
            score -= (groupHoursOnDay * 10.0);

            // 4. Compact Scheduling / Gap Minimization
            bool lecturerContiguous = entries.Any(e => e.LecturerId == lecturer.Id && e.DayOfWeek == slot.DayOfWeek &&
                                                      (e.EndTime == slot.StartTime || e.StartTime == slot.EndTime));
            if (lecturerContiguous) score += 20.0;

            bool groupContiguous = entries.Any(e => e.DayOfWeek == slot.DayOfWeek &&
                                                   demand.StudentGroup != null && e.StudentGroupId == demand.StudentGroup.Id &&
                                                   (e.EndTime == slot.StartTime || e.StartTime == slot.EndTime));
            if (groupContiguous) score += 20.0;

            // 5. Prefer standard core periods (08:30 to 16:00)
            if (slot.StartTime >= new TimeSpan(8, 30, 0) && slot.EndTime <= new TimeSpan(16, 0, 0))
            {
                score += 10.0;
            }

            // 6. Day Distribution: Strongly penalize scheduling multiple sessions of the same course on the same day to split them across the week
            bool sameCourseSameDay = entries.Any(e => e.CourseId == demand.Course.Id &&
                                                      ((demand.StudentGroup != null && e.StudentGroupId == demand.StudentGroup.Id) || demand.StudentGroup == null) &&
                                                      e.DayOfWeek == slot.DayOfWeek);
            if (sameCourseSameDay)
            {
                score -= 80.0; // Encourage splitting course sessions across different days
            }

            // 7. Room Capacity Priority Alignment: Bonus for large classes in large capacity rooms, penalty for small classes taking large rooms
            if (demandSize > 80 && room.Capacity >= 100)
            {
                score += 60.0; // High priority bonus for large classes using large capacity rooms
            }
            else if (demandSize <= 80 && room.Capacity > 100)
            {
                score -= 40.0; // Reserve large capacity rooms for large classes
            }

            // 8. Computer Lab Priority Alignment:
            // Priority is given to courses that require a computer lab. Computer labs can be treated as normal lecture rooms/halls,
            // but normal courses receive a soft penalty so standard lecture halls are preferred first.
            if (room.RoomType == RoomType.ComputerLab)
            {
                if (demand.Course.ComputerLabRequired || demand.SessionType == SessionType.ComputerLab)
                {
                    score += 80.0; // High priority bonus for Computer Lab required courses
                }
                else
                {
                    score -= 30.0; // Soft penalty for non-computer-lab courses so normal lecture halls/rooms are preferred first
                }
            }

            return score;
        }

        private List<RoomType> GetRequiredRoomTypes(Course course, SessionType sessionType)
        {
            if (sessionType == SessionType.ComputerLab || course.ComputerLabRequired)
                return new List<RoomType> { RoomType.ComputerLab };
            if (sessionType == SessionType.Lab || course.LabRequired)
                return new List<RoomType> { RoomType.Lab };
            if (sessionType == SessionType.CombinedClass)
                return new List<RoomType> { RoomType.LectureHall, RoomType.SeminarRoom, RoomType.ComputerLab };

            // Computer labs can be treated as normal lecture rooms or halls for general courses as well
            return new List<RoomType> { RoomType.LectureHall, RoomType.SeminarRoom, RoomType.ComputerLab };
        }

        private bool HasCollision(
            TimeSlot slot,
            int classroomId,
            int lecturerId,
            int? studentGroupId,
            int programmeId,
            DegreeLevel level,
            List<TimetableEntry> existingEntries,
            SchedulingOptions options)
        {
            // 1. Break overlap check
            if (TimeSlotGenerator.OverlapsWithBreak(slot.StartTime, slot.EndTime, options.BreakStartTime, options.BreakEndTime))
                return true;

            foreach (var entry in existingEntries)
            {
                if (entry.DayOfWeek != slot.DayOfWeek) continue;

                // Time overlap check
                bool overlap = slot.StartTime < entry.EndTime && slot.EndTime > entry.StartTime;
                if (!overlap) continue;

                // Lecturer clash
                if (entry.LecturerId == lecturerId) return true;

                // Classroom clash
                if (entry.ClassroomId == classroomId) return true;

                // Group clash
                if (studentGroupId.HasValue && entry.StudentGroupId.HasValue && entry.StudentGroupId == studentGroupId)
                    return true;

                // Combined class overlap for same programme & level
                if (!studentGroupId.HasValue || !entry.StudentGroupId.HasValue)
                {
                    if (entry.Course != null && entry.Course.ProgrammeId == programmeId && entry.Course.Level == level)
                        return true;
                }
            }

            return false;
        }
    }
}
