using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using UniversityTimetable.Domain.Entities;
using UniversityTimetable.Domain.Enums;

namespace UniversityTimetable.Infrastructure.Services
{
    public class StudentImportRecord
    {
        public string IndexNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ProgrammeCode { get; set; } = string.Empty;
        public int Level { get; set; } = 100;
    }

    public class ImportResult
    {
        public int TotalImported { get; set; }
        public int TotalSkipped { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public List<StudentGroupSuggestion> GroupSuggestions { get; set; } = new List<StudentGroupSuggestion>();
    }

    public class StudentGroupSuggestion
    {
        public string ProgrammeCode { get; set; } = string.Empty;
        public DegreeLevel Level { get; set; }
        public int TotalEnrolled { get; set; }
        public int RecommendedGroupsCount { get; set; }
        public int RecommendedStudentsPerGroup { get; set; }
        public string Rationale { get; set; } = string.Empty;
    }

    public class StudentCsvImporter
    {
        public async Task<ImportResult> ImportStudentsFromCsvAsync(Stream csvStream, Func<string, Task<Programme?>> getProgrammeByCodeFunc)
        {
            var result = new ImportResult();
            var records = new List<StudentImportRecord>();

            using (var reader = new StreamReader(csvStream))
            using (var csv = new CsvReader(reader, new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture) { HeaderValidated = null, MissingFieldFound = null }))
            {
                records = csv.GetRecords<StudentImportRecord>().ToList();
            }

            var groupedByProgLevel = records.GroupBy(r => new { r.ProgrammeCode, Level = (DegreeLevel)r.Level });

            foreach (var grp in groupedByProgLevel)
            {
                int enrolled = grp.Count();
                int targetRoomCap = 60; // Standard room capacity threshold
                int groupsNeeded = (int)Math.Ceiling((double)enrolled / targetRoomCap);
                if (groupsNeeded < 1) groupsNeeded = 1;

                int perGroup = (int)Math.Ceiling((double)enrolled / groupsNeeded);

                result.GroupSuggestions.Add(new StudentGroupSuggestion
                {
                    ProgrammeCode = grp.Key.ProgrammeCode,
                    Level = grp.Key.Level,
                    TotalEnrolled = enrolled,
                    RecommendedGroupsCount = groupsNeeded,
                    RecommendedStudentsPerGroup = perGroup,
                    Rationale = enrolled > targetRoomCap
                        ? $"Class size ({enrolled}) exceeds standard room capacity ({targetRoomCap}). Recommending split into {groupsNeeded} groups."
                        : $"Class size ({enrolled}) fits within room capacity. Single group recommended."
                });
            }

            result.TotalImported = records.Count;
            return result;
        }
    }
}
