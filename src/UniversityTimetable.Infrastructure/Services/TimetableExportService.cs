using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UniversityTimetable.Domain.Entities;

namespace UniversityTimetable.Infrastructure.Services
{
    public class TimetableExportService
    {
        public byte[] ExportTimetableToCsv(Timetable timetable)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Course Code,Course Title,Day,Start Time,End Time,Duration,Session Type,Lecturer,Classroom,Student Group");

            if (timetable.Entries != null)
            {
                foreach (var entry in timetable.Entries.OrderBy(e => e.DayOfWeek).ThenBy(e => e.StartTime))
                {
                    string courseCode = entry.Course?.Code ?? "N/A";
                    string courseTitle = entry.Course?.Title ?? "N/A";
                    string day = entry.DayOfWeek.ToString();
                    string start = entry.StartTime.ToString(@"hh\:mm");
                    string end = entry.EndTime.ToString(@"hh\:mm");
                    string duration = $"{entry.DurationHours}h";
                    string sessionType = entry.SessionType.ToString();
                    string lecturer = entry.Lecturer?.FullName ?? "N/A";
                    string room = entry.Classroom?.RoomNumber ?? "N/A";
                    string group = entry.StudentGroup?.Name ?? "Combined All Groups";

                    sb.AppendLine($"\"{courseCode}\",\"{courseTitle}\",\"{day}\",\"{start}\",\"{end}\",\"{duration}\",\"{sessionType}\",\"{lecturer}\",\"{room}\",\"{group}\"");
                }
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public string GeneratePrintableHtml(Timetable timetable, string targetTitle)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head><meta charset='utf-8'><title>" + targetTitle + "</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 30px; color: #333; }");
            sb.AppendLine(".header { text-align: center; margin-bottom: 25px; border-bottom: 2px solid #004085; padding-bottom: 10px; }");
            sb.AppendLine("h1 { margin: 0; color: #004085; font-size: 24px; }");
            sb.AppendLine("h3 { margin: 5px 0 0 0; color: #6c757d; font-weight: normal; font-size: 16px; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 15px; }");
            sb.AppendLine("th, td { border: 1px solid #dee2e6; padding: 10px; text-align: left; font-size: 13px; }");
            sb.AppendLine("th { background-color: #f8f9fa; color: #495057; font-weight: 600; }");
            sb.AppendLine(".badge { display: inline-block; padding: 3px 7px; font-size: 11px; font-weight: bold; color: #fff; border-radius: 3px; background-color: #007bff; }");
            sb.AppendLine(".badge-lab { background-color: #28a745; }");
            sb.AppendLine(".badge-combined { background-color: #6f42c1; }");
            sb.AppendLine("</style></head><body>");

            sb.AppendLine("<div class='header'>");
            sb.AppendLine($"<h1>UNIVERSITY TIMETABLE SYSTEM</h1>");
            sb.AppendLine($"<h3>{targetTitle} | {timetable.AcademicYear?.YearName ?? "2025/2026"} - Semester {timetable.Semester?.SemesterNumber ?? 1}</h3>");
            sb.AppendLine("</div>");

            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr><th>Day</th><th>Time Slot</th><th>Course</th><th>Session</th><th>Classroom</th><th>Lecturer</th><th>Group</th></tr></thead>");
            sb.AppendLine("<tbody>");

            if (timetable.Entries != null && timetable.Entries.Any())
            {
                foreach (var entry in timetable.Entries.OrderBy(e => e.DayOfWeek).ThenBy(e => e.StartTime))
                {
                    string badgeClass = entry.SessionType == Domain.Enums.SessionType.CombinedClass ? "badge badge-combined"
                        : entry.SessionType == Domain.Enums.SessionType.ComputerLab || entry.SessionType == Domain.Enums.SessionType.Lab ? "badge badge-lab"
                        : "badge";

                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td><strong>{entry.DayOfWeek}</strong></td>");
                    sb.AppendLine($"<td>{entry.StartTime:hh\\:mm} - {entry.EndTime:hh\\:mm} ({entry.DurationHours}h)</td>");
                    sb.AppendLine($"<td><strong>{entry.Course?.Code}</strong><br/><small>{entry.Course?.Title}</small></td>");
                    sb.AppendLine($"<td><span class='{badgeClass}'>{entry.SessionType}</span></td>");
                    sb.AppendLine($"<td>{entry.Classroom?.RoomNumber} ({entry.Classroom?.Building?.Code})</td>");
                    sb.AppendLine($"<td>{entry.Lecturer?.FullName}</td>");
                    sb.AppendLine($"<td>{entry.StudentGroup?.Name ?? "Combined All"}</td>");
                    sb.AppendLine("</tr>");
                }
            }
            else
            {
                sb.AppendLine("<tr><td colspan='7' style='text-align:center;'>No scheduled entries found.</td></tr>");
            }

            sb.AppendLine("tbody></table>");
            sb.AppendLine("<div style='margin-top:40px; font-size:11px; text-align:center; color:#888;'>Generated by AI University Timetable System on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "</div>");
            sb.AppendLine("</body></html>");

            return sb.ToString();
        }
    }
}
