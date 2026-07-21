using System;
using System.Collections.Generic;
using System.Linq;

namespace UniversityTimetable.Core.Scheduling
{
    public class TimeSlot
    {
        public DayOfWeek DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int DurationHours { get; set; }

        public string DisplayText => $"{DayOfWeek} {StartTime:hh\\:mm} - {EndTime:hh\\:mm} ({DurationHours}h)";
    }

    public static class TimeSlotGenerator
    {
        public static List<TimeSlot> GenerateAvailableTimeSlots(TimeSpan breakStart, TimeSpan breakEnd)
        {
            return GenerateAvailableTimeSlots(new TimeSpan(6, 30, 0), new TimeSpan(19, 0, 0), breakStart, breakEnd);
        }

        public static List<TimeSlot> GenerateAvailableTimeSlots(TimeSpan dayStart, TimeSpan dayEnd, TimeSpan breakStart, TimeSpan breakEnd)
        {
            var slots = new List<TimeSlot>();
            var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };

            foreach (var day in days)
            {
                // 1-Hour Slots
                var current = dayStart;
                while (current.Add(TimeSpan.FromHours(1)) <= dayEnd)
                {
                    var slotEnd = current.Add(TimeSpan.FromHours(1));

                    // Check if slot overlaps with official break
                    if (!OverlapsWithBreak(current, slotEnd, breakStart, breakEnd))
                    {
                        slots.Add(new TimeSlot
                        {
                            DayOfWeek = day,
                            StartTime = current,
                            EndTime = slotEnd,
                            DurationHours = 1
                        });
                    }

                    current = slotEnd;
                }

                // 2-Hour Slots
                current = dayStart;
                while (current.Add(TimeSpan.FromHours(2)) <= dayEnd)
                {
                    var slotEnd = current.Add(TimeSpan.FromHours(2));

                    // Check if slot overlaps with official break
                    if (!OverlapsWithBreak(current, slotEnd, breakStart, breakEnd))
                    {
                        slots.Add(new TimeSlot
                        {
                            DayOfWeek = day,
                            StartTime = current,
                            EndTime = slotEnd,
                            DurationHours = 2
                        });
                    }

                    current = current.Add(TimeSpan.FromHours(1)); // Step by 1 hour to find 2h windows
                }
            }

            return slots;
        }

        public static bool OverlapsWithBreak(TimeSpan start, TimeSpan end, TimeSpan breakStart, TimeSpan breakEnd)
        {
            return start < breakEnd && end > breakStart;
        }

        /// <summary>
        /// Partitions weekly contact hours (min 4, max 6) into allowed combinations of 1h and 2h sessions.
        /// </summary>
        public static List<List<int>> GetValidSessionCombinations(int totalContactHours)
        {
            var combinations = new List<List<int>>();

            if (totalContactHours == 6)
            {
                combinations.Add(new List<int> { 2, 2, 2 });          // 2h + 2h + 2h
                combinations.Add(new List<int> { 2, 2, 1, 1 });       // 2h + 2h + 1h + 1h
                combinations.Add(new List<int> { 2, 1, 1, 1, 1 });    // 2h + 1h + 1h + 1h + 1h
                combinations.Add(new List<int> { 1, 1, 1, 1, 1, 1 }); // 6 x 1h
            }
            else if (totalContactHours == 5)
            {
                combinations.Add(new List<int> { 2, 2, 1 });          // 2h + 2h + 1h
                combinations.Add(new List<int> { 2, 1, 1, 1 });       // 2h + 1h + 1h + 1h
                combinations.Add(new List<int> { 1, 1, 1, 1, 1 });    // 5 x 1h
            }
            else if (totalContactHours == 4)
            {
                combinations.Add(new List<int> { 2, 2 });          // 2h + 2h
                combinations.Add(new List<int> { 2, 1, 1 });       // 2h + 1h + 1h
                combinations.Add(new List<int> { 1, 1, 1, 1 });    // 1h + 1h + 1h + 1h
            }
            else if (totalContactHours == 3)
            {
                combinations.Add(new List<int> { 2, 1 });
                combinations.Add(new List<int> { 1, 1, 1 });
            }
            else if (totalContactHours == 2)
            {
                combinations.Add(new List<int> { 2 });
                combinations.Add(new List<int> { 1, 1 });
            }
            else
            {
                // Fallback greedy partitioning using 2h and 1h
                var list = new List<int>();
                int remaining = totalContactHours;
                while (remaining >= 2)
                {
                    list.Add(2);
                    remaining -= 2;
                }
                if (remaining == 1) list.Add(1);
                combinations.Add(list);
            }

            return combinations;
        }
    }
}
