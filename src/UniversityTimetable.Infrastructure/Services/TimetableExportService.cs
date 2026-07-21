using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using UniversityTimetable.Domain.Entities;

namespace UniversityTimetable.Infrastructure.Services
{
    public class TimetableExportService
    {
        public byte[] ExportTimetableToExcel(Timetable timetable, string? universityName = null, string? departmentName = null)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Master Timetable");

                // 1. Header Information
                // A2: University Name
                worksheet.Cell("A2").Value = string.IsNullOrWhiteSpace(universityName) ? "UNIVERSITY OF GHANA" : universityName.Trim();
                worksheet.Cell("A2").Style.Font.Bold = true;
                worksheet.Cell("A2").Style.Font.FontSize = 14;
                worksheet.Cell("A2").Style.Font.FontColor = XLColor.FromHtml("#0F172A");

                // A3: Department
                worksheet.Cell("A3").Value = string.IsNullOrWhiteSpace(departmentName) ? "DEPARTMENT OF COMPUTER SCIENCE & ACADEMICS" : departmentName.Trim();
                worksheet.Cell("A3").Style.Font.Bold = true;
                worksheet.Cell("A3").Style.Font.FontSize = 11;
                worksheet.Cell("A3").Style.Font.FontColor = XLColor.FromHtml("#475569");

                // A4: Semester Information
                string yearName = timetable.AcademicYear?.YearName ?? "2025/2026";
                int semNumber = timetable.Semester?.SemesterNumber ?? 1;
                worksheet.Cell("A4").Value = $"ACADEMIC YEAR {yearName} - SEMESTER {semNumber} TIMETABLE ({timetable.Title})";
                worksheet.Cell("A4").Style.Font.Bold = true;
                worksheet.Cell("A4").Style.Font.FontSize = 10;
                worksheet.Cell("A4").Style.Font.FontColor = XLColor.FromHtml("#0284C7");

                // 2. Table Headers (Row 7)
                worksheet.Cell(7, 1).Value = "DAY / TIME";
                worksheet.Cell(7, 2).Value = "06:30-07:30";
                worksheet.Cell(7, 3).Value = "07:30-08:30";
                worksheet.Cell(7, 4).Value = "08:30-09:30";
                worksheet.Cell(7, 5).Value = "09:30-10:30";
                worksheet.Cell(7, 6).Value = "10:30-11:30";
                worksheet.Cell(7, 7).Value = "11:30-12:30";
                worksheet.Cell(7, 8).Value = "BREAK (12:30-13:00)";
                worksheet.Cell(7, 9).Value = "13:00-14:00";
                worksheet.Cell(7, 10).Value = "14:00-15:00";
                worksheet.Cell(7, 11).Value = "15:00-16:00";
                worksheet.Cell(7, 12).Value = "16:00-17:00";
                worksheet.Cell(7, 13).Value = "17:00-18:00";
                worksheet.Cell(7, 14).Value = "18:00-19:00";

                var headerRange = worksheet.Range(7, 1, 7, 14);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Font.FontColor = XLColor.White;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F172A");
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                worksheet.Row(7).Height = 26;

                // 3. Days Setup (Column A, Rows 8-17)
                var daysMap = new Dictionary<DayOfWeek, (int startRow, int endRow, string label)>
                {
                    { DayOfWeek.Monday, (8, 9, "MONDAY") },
                    { DayOfWeek.Tuesday, (10, 11, "TUESDAY") },
                    { DayOfWeek.Wednesday, (12, 13, "WEDNESDAY") },
                    { DayOfWeek.Thursday, (14, 15, "THURSDAY") },
                    { DayOfWeek.Friday, (16, 17, "FRIDAY") }
                };

                foreach (var kvp in daysMap)
                {
                    int rStart = kvp.Value.startRow;
                    int rEnd = kvp.Value.endRow;
                    var dayRange = worksheet.Range(rStart, 1, rEnd, 1);
                    dayRange.Merge();
                    dayRange.Value = kvp.Value.label;
                    dayRange.Style.Font.Bold = true;
                    dayRange.Style.Font.FontSize = 11;
                    dayRange.Style.Font.FontColor = XLColor.White;
                    dayRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
                    dayRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    dayRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    worksheet.Row(rStart).Height = 24;
                    worksheet.Row(rEnd).Height = 24;
                }

                // 4. Column H Break (12:30 - 13:00)
                var breakRange = worksheet.Range(8, 8, 17, 8);
                breakRange.Merge();
                breakRange.Value = "OFFICIAL\nDAILY\nBREAK\n\n(12:30 - 13:00)";
                breakRange.Style.Font.Bold = true;
                breakRange.Style.Font.FontSize = 10;
                breakRange.Style.Font.FontColor = XLColor.FromHtml("#92400E");
                breakRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF3C7");
                breakRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                breakRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                breakRange.Style.Alignment.WrapText = true;

                // Table Borders
                var tableRange = worksheet.Range(7, 1, 17, 14);
                tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                tableRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#CBD5E1");
                tableRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#0F172A");

                // 5. Populate Timetable Entries
                if (timetable.Entries != null)
                {
                    foreach (var entry in timetable.Entries)
                    {
                        if (!daysMap.TryGetValue(entry.DayOfWeek, out var dayInfo)) continue;

                        int startCol = GetExcelColumnIndexForTime(entry.StartTime);
                        if (startCol < 2 || startCol > 14 || startCol == 8) continue;

                        int durationCols = entry.DurationHours > 1 ? entry.DurationHours : 1;
                        int endCol = startCol + durationCols - 1;

                        if (startCol < 8 && endCol >= 8)
                        {
                            endCol = 7; // Cap before Break Column H
                        }
                        if (endCol > 14) endCol = 14;

                        var cellRange = worksheet.Range(dayInfo.startRow, startCol, dayInfo.endRow, endCol);
                        cellRange.Merge();

                        string courseCode = entry.Course?.Code ?? "COURSE";
                        string courseTitle = entry.Course?.ShortForm ?? entry.Course?.Title ?? "";
                        string room = entry.Classroom?.RoomNumber ?? "Unassigned";
                        string lecturer = entry.Lecturer?.FullName ?? "Staff";
                        string group = entry.StudentGroup?.Name ?? "Combined";

                        cellRange.Value = $"{courseCode}\n{courseTitle}\nRm: {room} | {lecturer}\n[{group}]";
                        cellRange.Style.Alignment.WrapText = true;
                        cellRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        cellRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                        if (entry.SessionType == Domain.Enums.SessionType.ComputerLab || entry.SessionType == Domain.Enums.SessionType.Lab)
                        {
                            cellRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCFCE7");
                            cellRange.Style.Font.FontColor = XLColor.FromHtml("#166534");
                            cellRange.Style.Font.Bold = true;
                            cellRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            cellRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#86EFAC");
                        }
                        else if (entry.SessionType == Domain.Enums.SessionType.CombinedClass)
                        {
                            cellRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F3E8FF");
                            cellRange.Style.Font.FontColor = XLColor.FromHtml("#6B21A8");
                            cellRange.Style.Font.Bold = true;
                            cellRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            cellRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D8B4FE");
                        }
                        else
                        {
                            cellRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E0F2FE");
                            cellRange.Style.Font.FontColor = XLColor.FromHtml("#0369A1");
                            cellRange.Style.Font.Bold = true;
                            cellRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            cellRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#7DD3FC");
                        }
                    }
                }

                worksheet.Column(1).Width = 16;
                for (int col = 2; col <= 14; col++)
                {
                    if (col == 8) worksheet.Column(col).Width = 14;
                    else worksheet.Column(col).Width = 18;
                }

                worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
                worksheet.PageSetup.FitToPages(1, 1);

                using (var ms = new MemoryStream())
                {
                    workbook.SaveAs(ms);
                    return ms.ToArray();
                }
            }
        }

        private int GetExcelColumnIndexForTime(TimeSpan time)
        {
            if (time >= new TimeSpan(6, 30, 0) && time < new TimeSpan(7, 30, 0)) return 2;  // B
            if (time >= new TimeSpan(7, 30, 0) && time < new TimeSpan(8, 30, 0)) return 3;  // C
            if (time >= new TimeSpan(8, 30, 0) && time < new TimeSpan(9, 30, 0)) return 4;  // D
            if (time >= new TimeSpan(9, 30, 0) && time < new TimeSpan(10, 30, 0)) return 5;  // E
            if (time >= new TimeSpan(10, 30, 0) && time < new TimeSpan(11, 30, 0)) return 6; // F
            if (time >= new TimeSpan(11, 30, 0) && time < new TimeSpan(12, 30, 0)) return 7; // G
            if (time >= new TimeSpan(12, 30, 0) && time < new TimeSpan(13, 0, 0)) return 8;  // H (Break)
            if (time >= new TimeSpan(13, 0, 0) && time < new TimeSpan(14, 0, 0)) return 9;  // I
            if (time >= new TimeSpan(14, 0, 0) && time < new TimeSpan(15, 0, 0)) return 10; // J
            if (time >= new TimeSpan(15, 0, 0) && time < new TimeSpan(16, 0, 0)) return 11; // K
            if (time >= new TimeSpan(16, 0, 0) && time < new TimeSpan(17, 0, 0)) return 12; // L
            if (time >= new TimeSpan(17, 0, 0) && time < new TimeSpan(18, 0, 0)) return 13; // M
            if (time >= new TimeSpan(18, 0, 0) && time <= new TimeSpan(19, 0, 0)) return 14; // N
            return -1;
        }

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
                    sb.AppendLine($"<td>{entry.Classroom?.RoomNumber}</td>");
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
