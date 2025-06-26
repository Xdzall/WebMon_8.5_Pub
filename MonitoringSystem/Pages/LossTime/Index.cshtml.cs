using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MonitoringSystem.Data;

namespace MonitoringSystem.Pages.LossTime
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<LossTimeRecord> LossTimeData { get; set; } = new List<LossTimeRecord>();
        public int TotalDuration { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
        public int TotalRecords { get; set; }
        public bool HasDataToDisplay => TotalRecords > 0;

        [BindProperty]
        public DateTime StartSelectedDate { get; set; } = DateTime.Today;

        [BindProperty]
        public DateTime EndSelectedDate { get; set; } = DateTime.Today;

        [BindProperty]
        public string MachineLine { get; set; } = "All";

        [BindProperty]
        public List<string> SelectedShifts { get; set; } = new List<string> { "1", "2", "3" };

        [BindProperty]
        public int SelectedPageSize { get; set; } = 10;

        // Properti untuk break time
        [BindProperty]
        public string AdditionalBreakTime1Start { get; set; } = "";
        [BindProperty]
        public string AdditionalBreakTime1End { get; set; } = "";
        [BindProperty]
        public string AdditionalBreakTime2Start { get; set; } = "";
        [BindProperty]
        public string AdditionalBreakTime2End { get; set; } = "";

        public bool IsFiltering { get; set; } = false;
        public Dictionary<string, int> CategorySummary { get; set; } = new Dictionary<string, int>();
        public string ChartDataJson { get; set; }

        public List<string> AllCategories { get; set; } = new List<string>
        {
            "Change Model",
            "Material Shortage External",
            "MP Adjustment",
            "Material Shortage Internal",
            "Material Shortage Inhouse",
            "Quality Trouble",
            "Machine Trouble",
            "Rework"
        };

        private readonly List<(TimeSpan Start, TimeSpan End)> FixedBreakTimes = new List<(TimeSpan, TimeSpan)>
        {
            (new TimeSpan(7, 0, 0), new TimeSpan(7, 5, 0)),
            (new TimeSpan(9, 30, 0), new TimeSpan(9, 35, 0)),
            (new TimeSpan(15, 30, 0), new TimeSpan(15, 35, 0)),
            (new TimeSpan(18, 15, 0), new TimeSpan(18, 45, 0))
        };

        public string connectionString = "Server=10.83.33.103;trusted_connection=false;Database=PROMOSYS;User Id=sa;Password=sa;Persist Security Info=False;Encrypt=False";
        //public string connectionString = "Data Source=DESKTOP-NBPATD6\\MSSQLSERVERR;trusted_connection=true;trustservercertificate=True;Database=LatestPROMOSYS;Integrated Security=True;Encrypt=False";

        public void OnGet(int pageNumber = 1, int pageSize = 10)
        {
            CurrentPage = pageNumber;
            PageSize = pageSize;
            SelectedPageSize = pageSize;

            LoadBreakTimeForToday(); 

            LoadData();
        }

        public IActionResult OnPostFilter()
        {
            CurrentPage = 1;
            PageSize = SelectedPageSize;

            if (EndSelectedDate < StartSelectedDate)
            {
                EndSelectedDate = DateTime.Today;
            }

            if (SelectedShifts == null || !SelectedShifts.Any())
            {
                SelectedShifts = new List<string> { "1", "2", "3" };
            }

            LoadBreakTimeForToday(); 

            IsFiltering = true;
            LoadData();
            return Page();
        }

        public IActionResult OnPostChangePage(int pageNumber, int pageSize, DateTime startSelectedDate,
            DateTime endSelectedDate, string machineLine, List<string> selectedShifts,
            string additionalBreakTime1Start, string additionalBreakTime1End,
            string additionalBreakTime2Start, string additionalBreakTime2End)
        {
            CurrentPage = pageNumber;
            PageSize = pageSize;
            StartSelectedDate = startSelectedDate;
            EndSelectedDate = endSelectedDate;
            MachineLine = machineLine;
            SelectedShifts = selectedShifts ?? new List<string> { "1", "2", "3" };
            AdditionalBreakTime1Start = additionalBreakTime1Start;
            AdditionalBreakTime1End = additionalBreakTime1End;
            AdditionalBreakTime2Start = additionalBreakTime2Start;
            AdditionalBreakTime2End = additionalBreakTime2End;

            LoadData();
            return Page();
        }

        public IActionResult OnPostReset()
        {
            StartSelectedDate = DateTime.Today;
            EndSelectedDate = DateTime.Today;
            MachineLine = "All";
            SelectedShifts = new List<string> { "1", "2", "3" };
            SelectedPageSize = 10;
            PageSize = 10;

            IsFiltering = false;
            CurrentPage = 1;

            LoadBreakTimeForToday(); 

            LoadData();
            return Page();
        }

        private void LoadBreakTimeForToday()
        {
            var today = DateTime.Today; 

            
            var latestBreakTime = _context.AdditionalBreakTimes
                .Where(bt => bt.Date == today)
                .OrderByDescending(bt => bt.CreatedAt) 
                .FirstOrDefault();

            if (latestBreakTime != null)
            {
                AdditionalBreakTime1Start = latestBreakTime.BreakTime1Start?.ToString(@"hh\:mm");
                AdditionalBreakTime1End = latestBreakTime.BreakTime1End?.ToString(@"hh\:mm");
                AdditionalBreakTime2Start = latestBreakTime.BreakTime2Start?.ToString(@"hh\:mm");
                AdditionalBreakTime2End = latestBreakTime.BreakTime2End?.ToString(@"hh\:mm");
            }
        }

        private List<(TimeSpan Start, TimeSpan End)> GetAllBreakTimes()
        {
            var breakTimes = new List<(TimeSpan Start, TimeSpan End)>();

            breakTimes.AddRange(FixedBreakTimes);

            if (!string.IsNullOrEmpty(AdditionalBreakTime1Start) && !string.IsNullOrEmpty(AdditionalBreakTime1End))
            {
                if (TryParseTimeSpan(AdditionalBreakTime1Start, out TimeSpan start1) && TryParseTimeSpan(AdditionalBreakTime1End, out TimeSpan end1))
                {
                    breakTimes.Add((start1, end1));
                }
            }

            if (!string.IsNullOrEmpty(AdditionalBreakTime2Start) && !string.IsNullOrEmpty(AdditionalBreakTime2End))
            {
                if (TryParseTimeSpan(AdditionalBreakTime2Start, out TimeSpan start2) && TryParseTimeSpan(AdditionalBreakTime2End, out TimeSpan end2))
                {
                    breakTimes.Add((start2, end2));
                }
            }

            return breakTimes;
        }

        private bool TryParseTimeSpan(string timeString, out TimeSpan result)
        {
            string[] formats = { "HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss" };

            if (TimeSpan.TryParseExact(timeString, formats, null, out result))
            {
                return true;
            }

            if (DateTime.TryParse(timeString, out DateTime dateTime))
            {
                result = dateTime.TimeOfDay;
                return true;
            }

            result = TimeSpan.Zero;
            return false;
        }

        private bool IsInBreakTime(TimeSpan startTime, TimeSpan endTime, List<(TimeSpan Start, TimeSpan End)> breakTimes)
        {
            foreach (var (breakStart, breakEnd) in breakTimes)
            {
                if ((startTime >= breakStart && startTime <= breakEnd) ||
                    (endTime >= breakStart && endTime <= breakEnd) ||
                    (startTime <= breakStart && endTime >= breakEnd))
                {
                    return true;
                }
            }
            return false;
        }

        private void LoadData()
        {
            var breakTimes = GetAllBreakTimes();

            CalculateAllDataSummary(breakTimes);
            PrepareChartDataJson(breakTimes);

            LoadPaginatedData(breakTimes);
        }

        private void LoadPaginatedData(List<(TimeSpan Start, TimeSpan End)> breakTimes)
        {
            TotalRecords = GetTotalRecords(breakTimes);
            EnsureValidCurrentPage();

            string query = BuildQueryBase();
            query += " ORDER BY [Date] DESC, Time OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    AddQueryParameters(command);
                    command.Parameters.AddWithValue("@Offset", (CurrentPage - 1) * PageSize);
                    command.Parameters.AddWithValue("@PageSize", PageSize);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        LossTimeData.Clear();

                        while (reader.Read())
                        {
                            TimeSpan startTime = reader.GetTimeSpan(reader.GetOrdinal("StartTime"));
                            TimeSpan endTime = reader.GetTimeSpan(reader.GetOrdinal("EndTime"));

                            if (IsInBreakTime(startTime, endTime, breakTimes))
                            {
                                continue;
                            }

                            LossTimeRecord record = new LossTimeRecord
                            {
                                Nomor = reader.GetInt32(reader.GetOrdinal("Id")),
                                Date = reader.GetDateTime(reader.GetOrdinal("Date")),
                                LossTime = reader.GetString(reader.GetOrdinal("Reason")),
                                Start = startTime,
                                End = endTime,
                                Duration = reader.GetInt32(reader.GetOrdinal("LossTime")),
                                Location = reader.GetString(reader.GetOrdinal("MachineCode")),
                                Shift = reader.GetString(reader.GetOrdinal("Shift")),
                                Category = CategorizeReason(reader.GetString(reader.GetOrdinal("Reason")))
                            };

                            LossTimeData.Add(record);
                        }
                    }
                }
            }
        }

        private void CalculateAllDataSummary(List<(TimeSpan Start, TimeSpan End)> breakTimes)
        {
            CategorySummary.Clear();
            TotalDuration = 0;

            foreach (var category in AllCategories)
            {
                CategorySummary[category] = 0;
            }

            if (!CategorySummary.ContainsKey("Other"))
            {
                CategorySummary["Other"] = 0;
            }

            string allDataQuery = BuildQueryBase();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(allDataQuery, connection))
                {
                    AddQueryParameters(command);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            TimeSpan startTime = reader.GetTimeSpan(reader.GetOrdinal("StartTime"));
                            TimeSpan endTime = reader.GetTimeSpan(reader.GetOrdinal("EndTime"));

                            if (IsInBreakTime(startTime, endTime, breakTimes))
                            {
                                continue;
                            }

                            string reason = reader.GetString(reader.GetOrdinal("Reason"));
                            int duration = reader.GetInt32(reader.GetOrdinal("LossTime"));
                            string category = CategorizeReason(reason);

                            CategorySummary[category] += duration;
                            TotalDuration += duration;
                        }
                    }
                }
            }
        }

        private string BuildQueryBase()
        {
            string query = @"
                SELECT Id, Date, Reason, 
                       CAST(Time AS TIME) AS StartTime,
                       CAST(EndDateTime AS TIME) AS EndTime,
                       LossTime, MachineCode, 
                       CASE 
                           WHEN (DATEPART(HOUR, Time) = 7 AND DATEPART(MINUTE, Time) >= 0) OR 
                                (DATEPART(HOUR, Time) > 7 AND DATEPART(HOUR, Time) < 15) OR 
                                (DATEPART(HOUR, Time) = 15 AND DATEPART(MINUTE, Time) <= 45) THEN '1'
                           WHEN (DATEPART(HOUR, Time) = 15 AND DATEPART(MINUTE, Time) > 45) OR 
                                (DATEPART(HOUR, Time) > 15 AND DATEPART(HOUR, Time) < 23) OR 
                                (DATEPART(HOUR, Time) = 23 AND DATEPART(MINUTE, Time) <= 15) THEN '2'
                           ELSE '3'
                       END AS Shift
                FROM AssemblyLossTime 
                WHERE [Date] >= @StartDate AND [Date] <= @EndDate";

            if (!string.IsNullOrEmpty(MachineLine) && MachineLine != "All")
            {
                query += " AND MachineCode = @MachineLine";
            }

            if (SelectedShifts != null && SelectedShifts.Any() && SelectedShifts.Count < 3)
            {
                query += " AND (";
                List<string> shiftConditions = new List<string>();

                foreach (var shift in SelectedShifts)
                {
                    if (shift == "1")
                        shiftConditions.Add("((DATEPART(HOUR, Time) = 7 AND DATEPART(MINUTE, Time) >= 0) OR (DATEPART(HOUR, Time) > 7 AND DATEPART(HOUR, Time) < 15) OR (DATEPART(HOUR, Time) = 15 AND DATEPART(MINUTE, Time) <= 45))");
                    else if (shift == "2")
                        shiftConditions.Add("((DATEPART(HOUR, Time) = 15 AND DATEPART(MINUTE, Time) > 45) OR (DATEPART(HOUR, Time) > 15 AND DATEPART(HOUR, Time) < 23) OR (DATEPART(HOUR, Time) = 23 AND DATEPART(MINUTE, Time) <= 15))");
                    else if (shift == "3")
                        shiftConditions.Add("((DATEPART(HOUR, Time) = 23 AND DATEPART(MINUTE, Time) > 15) OR (DATEPART(HOUR, Time) >= 0 AND DATEPART(HOUR, Time) < 7))");
                }

                query += string.Join(" OR ", shiftConditions);
                query += ")";
            }

            return query;
        }

        private void AddQueryParameters(SqlCommand command)
        {
            command.Parameters.AddWithValue("@StartDate", StartSelectedDate.Date);
            command.Parameters.AddWithValue("@EndDate", EndSelectedDate.Date);

            if (!string.IsNullOrEmpty(MachineLine) && MachineLine != "All")
            {
                command.Parameters.AddWithValue("@MachineLine", MachineLine);
            }
        }

        private int GetTotalRecords(List<(TimeSpan Start, TimeSpan End)> breakTimes)
        {
            int count = 0;
            string query = BuildQueryBase();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    AddQueryParameters(command);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            TimeSpan startTime = reader.GetTimeSpan(reader.GetOrdinal("StartTime"));
                            TimeSpan endTime = reader.GetTimeSpan(reader.GetOrdinal("EndTime"));

                            // Only count records that are NOT in break time
                            if (!IsInBreakTime(startTime, endTime, breakTimes))
                            {
                                count++;
                            }
                        }
                    }
                }
            }
            return count;
        }

        private void PrepareChartDataJson(List<(TimeSpan Start, TimeSpan End)> breakTimes)
        {
            try
            {
                string allDataQuery = BuildQueryBase();

                var allData = new List<LossTimeRecord>();

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(allDataQuery, connection))
                    {
                        AddQueryParameters(command);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                TimeSpan startTime = reader.GetTimeSpan(reader.GetOrdinal("StartTime"));
                                TimeSpan endTime = reader.GetTimeSpan(reader.GetOrdinal("EndTime"));

                                if (IsInBreakTime(startTime, endTime, breakTimes))
                                {
                                    continue;
                                }

                                string reason = reader.GetString(reader.GetOrdinal("Reason"));
                                LossTimeRecord record = new LossTimeRecord
                                {
                                    LossTime = reason,
                                    Duration = reader.GetInt32(reader.GetOrdinal("LossTime")),
                                    Shift = reader.GetString(reader.GetOrdinal("Shift")),
                                    Category = CategorizeReason(reason)
                                };
                                allData.Add(record);
                            }
                        }
                    }
                }

                var shift1Data = new Dictionary<string, double>();
                var shift2Data = new Dictionary<string, double>();
                var shift3Data = new Dictionary<string, double>();

                foreach (var category in AllCategories)
                {
                    shift1Data[category] = 0;
                    shift2Data[category] = 0;
                    shift3Data[category] = 0;
                }

                foreach (var record in allData)
                {
                    double durationInMinutes = record.Duration / 60.0;

                    if (record.Shift == "1")
                        shift1Data[record.Category] += durationInMinutes;
                    else if (record.Shift == "2")
                        shift2Data[record.Category] += durationInMinutes;
                    else if (record.Shift == "3")
                        shift3Data[record.Category] += durationInMinutes;
                }

                var chartData = new
                {
                    labels = AllCategories.ToArray(),
                    shift1Data = AllCategories.Select(c => Math.Round(shift1Data[c], 2)).ToArray(),
                    shift2Data = AllCategories.Select(c => Math.Round(shift2Data[c], 2)).ToArray(),
                    shift3Data = AllCategories.Select(c => Math.Round(shift3Data[c], 2)).ToArray()
                };

                ChartDataJson = JsonSerializer.Serialize(chartData);
            }
            catch (Exception ex)
            {
                ChartDataJson = "{ \"labels\": [], \"shift1Data\": [], \"shift2Data\": [], \"shift3Data\": [] }";
            }
        }

        private void EnsureValidCurrentPage()
        {
            if (TotalRecords == 0)
            {
                CurrentPage = 1;
                return;
            }

            int maxPages = (int)Math.Ceiling((double)TotalRecords / PageSize);

            if (CurrentPage > maxPages)
            {
                CurrentPage = maxPages;
            }
            else if (CurrentPage < 1)
            {
                CurrentPage = 1;
            }
        }

        private string CategorizeReason(string reason)
        {
            reason = reason?.ToLower() ?? "";
            if (reason.Contains("change model"))
                return "Change Model";
            else if (reason.Contains("material shortage external"))
                return "Material Shortage External";
            else if (reason.Contains("mp adjustment"))
                return "MP Adjustment";
            else if (reason.Contains("material shortage internal"))
                return "Material Shortage Internal";
            else if (reason.Contains("material shortage inhouse"))
                return "Material Shortage Inhouse";
            else if (reason.Contains("quality trouble"))
                return "Quality Trouble";
            else if (reason.Contains("machine trouble"))
                return "Machine Trouble";
            else if (reason.Contains("rework"))
                return "Rework";
            else
                return "Other";  // Default category
        }

        public int GetTotalDurationAllCategories()
        {
            return CategorySummary.Values.Sum();
        }

        public double SecondsToMinutes(int seconds)
        {
            return Math.Round(seconds / 60.0, 2);
        }

        public List<int> GetPageSizeOptions()
        {
            return new List<int> { 10 };
        }
    }

    public class LossTimeRecord
    {
        public int Nomor { get; set; }
        public DateTime Date { get; set; }
        public string LossTime { get; set; }
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        public int Duration { get; set; }
        public string Location { get; set; }
        public string Shift { get; set; }
        public string Category { get; set; }
    }
}