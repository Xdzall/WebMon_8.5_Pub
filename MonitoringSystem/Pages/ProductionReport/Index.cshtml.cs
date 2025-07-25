using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using OfficeOpenXml;
using System.Globalization;
using System.Text;

namespace MonitoringSystem.Pages.ProductionReport
{
    public class IndexModel : PageModel
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IConfiguration _configuration;

        // Deklarasi connectionString di sini
        private string connectionString;

        public int DaysInMonth { get; private set; }
        public List<string> ChartLabels { get; private set; } = new List<string>();
        public List<decimal> NormalData { get; private set; } = new List<decimal>();
        public List<decimal> OvertimeData { get; private set; } = new List<decimal>();
        public List<int> PlanData { get; private set; } = new List<int>();
        public List<int> EstimateData { get; private set; } = new List<int>();
        public List<int> NoOfDirectWorkers { get; private set; } = new List<int>();
        public List<int> DailyWorkTime { get; private set; } = new List<int>();
        public List<int> OvertimeOperators { get; private set; } = new List<int>();
        public List<int> OvertimeMinutes { get; private set; } = new List<int>();
        public List<int> DailyLossTime { get; private set; } = new List<int>();

        private class DailyData { public int Day { get; set; } public decimal LastNormalReading { get; set; } = 0; public decimal LastOvertimeReading { get; set; } = 0; public int Plan { get; set; } = 0; public int Estimate { get; set; } = 0; public int NoOfOperator { get; set; } = 0; public int OtOperatorCount { get; set; } = 0; public TimeSpan LastOtTime { get; set; } = TimeSpan.Zero; }
        public class RestTime { public int Duration { get; set; } public TimeSpan StartTime { get; set; } public TimeSpan EndTime { get; set; } }

        public IndexModel(IWebHostEnvironment webHostEnvironment, IConfiguration configuration)
        {
            _webHostEnvironment = webHostEnvironment;
            _configuration = configuration;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        [BindProperty(SupportsGet = true)] public int SelectedMonth { get; set; } = DateTime.Now.Month;
        [BindProperty(SupportsGet = true)] public int SelectedYear { get; set; } = DateTime.Now.Year;
        [BindProperty(SupportsGet = true)] public string MachineLine { get; set; } = "MCH1-01";
        [BindProperty(SupportsGet = true)] public List<string> SelectedShifts { get; set; } = new List<string>();

        public void OnGet()
        {
            if (!SelectedShifts.Any())
            {
                SelectedShifts = new List<string> { "1" };
            }
            LoadChartData();
        }

        public IActionResult OnPost(string submitButton)
        {
            if (submitButton == "reset")
            {
                return RedirectToPage(new { SelectedYear = DateTime.Now.Year, SelectedMonth = DateTime.Now.Month, MachineLine = "MCH1-01" });
            }
            return RedirectToPage(new
            {
                SelectedYear = this.SelectedYear,
                SelectedMonth = this.SelectedMonth,
                MachineLine = this.MachineLine,
                SelectedShifts = this.SelectedShifts
            });
        }

        private void LoadChartData()
        {
            // Mengisi variabel connectionString menggunakan konfigurasi yang sudah ada
            this.connectionString = _configuration.GetConnectionString("DefaultConnection");

            var dailyLosses = GetDailyLossTimeTotals();
            bool isCurrentMonthView = (SelectedYear == DateTime.Now.Year && SelectedMonth == DateTime.Now.Month);
            string dateFilter = isCurrentMonthView ? "AND CAST(SDate AS DATE) <= @TodayDate" : "";
            string machineType = MachineLine == "MCH1-01" ? "cu" : "cs";
            string monthName = CultureInfo.GetCultureInfo("en-US").DateTimeFormat.GetMonthName(SelectedMonth);
            this.DaysInMonth = DateTime.DaysInMonth(SelectedYear, SelectedMonth);

            TimeSpan shift1Start = new TimeSpan(7, 0, 0);
            TimeSpan shift1End = new TimeSpan(16, 0, 0);
            for (int day = 1; day <= this.DaysInMonth; day++)
            {
                var currentDate = new DateTime(SelectedYear, SelectedMonth, day);
                if (isCurrentMonthView && currentDate.Date > DateTime.Now.Date) { DailyWorkTime.Add(0); continue; }
                var dayType = DetermineTypeOfDay(currentDate.DayOfWeek);
                int workTimeForThisDay = 0;
                if (dayType != "WEEKEND")
                {
                    var allRestTimes = GetRestTime(dayType);
                    if (currentDate.Date == DateTime.Now.Date)
                    {
                        var currentTime = DateTime.Now.TimeOfDay;
                        if (currentTime > shift1Start)
                        {
                            var effectiveEndTime = (currentTime > shift1End) ? shift1End : currentTime;
                            int totalDuration = (int)(effectiveEndTime - shift1Start).TotalMinutes;
                            int totalRest = GetTotalRestTime(allRestTimes, shift1Start, shift1End, currentTime);
                            workTimeForThisDay = totalDuration - totalRest;
                        }
                    }
                    else if (currentDate.Date < DateTime.Now.Date)
                    {
                        int totalDuration = (int)(shift1End - shift1Start).TotalMinutes;
                        int totalRest = GetTotalRestTime(allRestTimes, shift1Start, shift1End, shift1End);
                        workTimeForThisDay = totalDuration - totalRest;
                    }
                }
                DailyWorkTime.Add(workTimeForThisDay < 0 ? 0 : workTimeForThisDay);
            }

            var combinedData = Enumerable.Range(1, this.DaysInMonth).Select(day => new DailyData { Day = day }).ToList();
            var planFilePath = Path.Combine(_webHostEnvironment.WebRootPath, "data", machineType, "plan", "csv", $"{monthName}_{SelectedYear}_plan.csv");
            if (System.IO.File.Exists(planFilePath)) { var planValues = ReadDataFromCsv(planFilePath, this.DaysInMonth); for (int i = 0; i < planValues.Count; i++) { combinedData[i].Plan = planValues[i]; } }
            var estimateFilePath = Path.Combine(_webHostEnvironment.WebRootPath, "data", machineType, "estimate", "csv", $"{monthName}_{SelectedYear}_estimate.csv");
            if (System.IO.File.Exists(estimateFilePath)) { var estimateValues = ReadDataFromCsv(estimateFilePath, this.DaysInMonth); for (int i = 0; i < estimateValues.Count; i++) { combinedData[i].Estimate = estimateValues[i]; } }

            string shiftsForSql = SelectedShifts.Any() ? string.Join(",", SelectedShifts.Select(s => $"'{s}'")) : "'0'";
            var sql = $@"
                WITH DailyAggregates AS (
                    SELECT ReportDate, MAX(CASE WHEN Shift = 1 THEN TotalUnit END) as Shift1_EndReading, MAX(CASE WHEN Shift IN (2, 3) THEN TotalUnit END) as OT_EndReading, MAX(NoOfOperator) as MaxOp, MAX(CASE WHEN Shift IN (2, 3) THEN NoOfOperator END) as OT_OperatorCount, MAX(CASE WHEN Shift IN (2, 3) THEN SDate END) as OT_LastSDate
                    FROM ( SELECT CAST(SDate AS DATE) AS ReportDate, SDate, TotalUnit, NoOfOperator, CASE WHEN CAST(SDate AS TIME) >= '07:00:00' AND CAST(SDate AS TIME) < '16:00:00' THEN 1 WHEN CAST(SDate AS TIME) >= '16:00:00' AND CAST(SDate AS TIME) < '23:15:00' THEN 2 ELSE 3 END as Shift FROM oeesn WHERE YEAR(SDate) = @SelectedYear AND MONTH(SDate) = @SelectedMonth AND MachineCode = @MachineLine {dateFilter} ) as ShiftData
                    GROUP BY ReportDate
                )
                SELECT DAY(ReportDate) as Day, CASE WHEN '1' IN ({shiftsForSql}) THEN ISNULL(Shift1_EndReading, 0) ELSE 0 END as LastNormalReading, CASE WHEN ('2' IN ({shiftsForSql}) OR '3' IN ({shiftsForSql})) THEN ISNULL(OT_EndReading, 0) ELSE CASE WHEN '1' IN ({shiftsForSql}) THEN ISNULL(Shift1_EndReading, 0) ELSE 0 END END as LastOvertimeReading, ISNULL(MaxOp, 0) as NoOfOperator, ISNULL(OT_OperatorCount, 0) as OtOperatorCount, ISNULL(CAST(OT_LastSDate AS TIME), '00:00:00') as LastOtTime
                FROM DailyAggregates;";

            try
            {
                using (var connection = new SqlConnection(this.connectionString))
                {
                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@SelectedYear", SelectedYear); command.Parameters.AddWithValue("@SelectedMonth", SelectedMonth); command.Parameters.AddWithValue("@MachineLine", MachineLine);
                        if (isCurrentMonthView) { command.Parameters.AddWithValue("@TodayDate", DateTime.Now.Date); }
                        connection.Open();
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int day = (int)reader["Day"]; var dataForDay = combinedData.FirstOrDefault(d => d.Day == day);
                                if (dataForDay != null) { dataForDay.LastNormalReading = (decimal)reader["LastNormalReading"]; dataForDay.LastOvertimeReading = (decimal)reader["LastOvertimeReading"]; dataForDay.NoOfOperator = Convert.ToInt32(reader["NoOfOperator"]); dataForDay.OtOperatorCount = Convert.ToInt32(reader["OtOperatorCount"]); dataForDay.LastOtTime = (TimeSpan)reader["LastOtTime"]; }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }

            foreach (var data in combinedData)
            {
                ChartLabels.Add(data.Day.ToString()); PlanData.Add(data.Plan); EstimateData.Add(data.Estimate); NormalData.Add(data.LastNormalReading);
                var overtimeValue = data.LastOvertimeReading - data.LastNormalReading;
                OvertimeData.Add(overtimeValue > 0 ? overtimeValue : 0);
                NoOfDirectWorkers.Add(data.NoOfOperator); OvertimeOperators.Add(data.OtOperatorCount);
                int netOvertimeMinutes = 0; TimeSpan otStartTime = new TimeSpan(16, 0, 0);
                if (data.LastOtTime > otStartTime)
                {
                    var currentDate = new DateTime(SelectedYear, SelectedMonth, data.Day); var dayType = DetermineTypeOfDay(currentDate.DayOfWeek); var allRestTimes = GetRestTime(dayType);
                    int grossMinutes = (int)(data.LastOtTime - otStartTime).TotalMinutes; int restMinutesInOt = GetTotalRestTime(allRestTimes, otStartTime, data.LastOtTime, data.LastOtTime);
                    netOvertimeMinutes = grossMinutes - restMinutesInOt;
                }
                OvertimeMinutes.Add(netOvertimeMinutes > 0 ? netOvertimeMinutes : 0);
                dailyLosses.TryGetValue(data.Day, out int totalSeconds);
                DailyLossTime.Add(totalSeconds / 60);
            }
        }

        // --- Kumpulan Metode Helper ---
        private readonly List<(TimeSpan Start, TimeSpan End)> FixedBreakTimes = new List<(TimeSpan, TimeSpan)> { (new TimeSpan(7, 0, 0), new TimeSpan(7, 0, 5)), (new TimeSpan(9, 30, 0), new TimeSpan(9, 35, 0)), (new TimeSpan(12, 0, 0), new TimeSpan(12, 45, 0)), (new TimeSpan(15, 30, 0), new TimeSpan(15, 35, 0)), (new TimeSpan(18, 15, 0), new TimeSpan(18, 45, 0)) };
        private bool IsInBreakTime(TimeSpan startTime, TimeSpan endTime, List<(TimeSpan Start, TimeSpan End)> breakTimes) { foreach (var (breakStart, breakEnd) in breakTimes) { if (startTime < breakEnd && endTime > breakStart) { return true; } } return false; }
        private Dictionary<int, int> GetDailyLossTimeTotals()
        {
            var dailyTotals = new Dictionary<int, int>();
            var breakCache = new Dictionary<DateTime, List<(TimeSpan Start, TimeSpan End)>>();

            string query = @"
        SELECT 
            CAST(Date AS DATE) as FullDate, 
            CAST(Time AS TIME) as StartTime, 
            CAST(EndDateTime AS TIME) as EndTime, 
            LossTime as Duration
        FROM AssemblyLossTime
        WHERE YEAR(Date) = @Year AND MONTH(Date) = @Month AND MachineCode = @Machine";

            try
            {
                using (var connection = new SqlConnection(this.connectionString))
                {
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Year", SelectedYear);
                        command.Parameters.AddWithValue("@Month", SelectedMonth);
                        command.Parameters.AddWithValue("@Machine", MachineLine);

                        connection.Open();
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var fullDate = (DateTime)reader["FullDate"];
                                var day = fullDate.Day;
                                var startTime = (TimeSpan)reader["StartTime"];
                                var endTime = (TimeSpan)reader["EndTime"];
                                var duration = Convert.ToInt32(reader["Duration"]);

                                // Cek cache, jika belum ada break untuk tanggal ini, ambil & simpan
                                if (!breakCache.ContainsKey(fullDate.Date))
                                {
                                    var additionalBreaks = GetAdditionalBreakTimesForDate(fullDate);
                                    var allBreaksForDay = new List<(TimeSpan Start, TimeSpan End)>();
                                    allBreaksForDay.AddRange(this.FixedBreakTimes);
                                    allBreaksForDay.AddRange(additionalBreaks);
                                    breakCache[fullDate.Date] = allBreaksForDay;
                                }

                                // Gunakan break yang sudah dicache untuk pengecekan
                                if (!IsInBreakTime(startTime, endTime, breakCache[fullDate.Date]))
                                {
                                    if (!dailyTotals.ContainsKey(day))
                                    {
                                        dailyTotals[day] = 0;
                                    }
                                    dailyTotals[day] += duration;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching loss time: {ex.Message}");
            }
            return dailyTotals;
        }
        private List<(TimeSpan Start, TimeSpan End)> GetAdditionalBreakTimesForDate(DateTime date)
        {
            var additionalBreaks = new List<(TimeSpan, TimeSpan)>();
            try
            {
                using (var connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();
                    string sql = @"
                SELECT TOP 1 BreakTime1Start, BreakTime1End, BreakTime2Start, BreakTime2End 
                FROM AdditionalBreakTimes 
                WHERE CAST(Date AS DATE) = @Date
                ORDER BY CreatedAt DESC";
                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@Date", date.Date);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                                    additionalBreaks.Add((reader.GetTimeSpan(0), reader.GetTimeSpan(1)));
                                if (!reader.IsDBNull(2) && !reader.IsDBNull(3))
                                    additionalBreaks.Add((reader.GetTimeSpan(2), reader.GetTimeSpan(3)));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting additional breaks: {ex.Message}");
            }
            return additionalBreaks;
        }
        public int GetTotalRestTime(List<RestTime> listRestTime, TimeSpan StartTime, TimeSpan EndTime, TimeSpan CurrentTime) { int TotalRestTime = 0; bool isToday = (SelectedYear == DateTime.Now.Year && SelectedMonth == DateTime.Now.Month); TotalRestTime = listRestTime.Sum(rest => { if (isToday && rest.StartTime > CurrentTime) { return 0; } TimeSpan effectiveRestStart = rest.StartTime < StartTime ? StartTime : rest.StartTime; TimeSpan effectiveRestEnd = rest.EndTime > EndTime ? EndTime : rest.EndTime; if (isToday && effectiveRestEnd > CurrentTime) { effectiveRestEnd = CurrentTime; } return effectiveRestStart < effectiveRestEnd ? (int)(effectiveRestEnd - effectiveRestStart).TotalMinutes : 0; }); return TotalRestTime; }
        public string DetermineTypeOfDay(DayOfWeek day) { return day switch { DayOfWeek.Monday or DayOfWeek.Tuesday or DayOfWeek.Wednesday or DayOfWeek.Thursday => "REGULAR", DayOfWeek.Friday => "FRIDAY", DayOfWeek.Saturday or DayOfWeek.Sunday => "WEEKEND", _ => "REGULAR" }; }
        public List<RestTime> GetRestTime(string dayTipe) { List<RestTime> listRestTime = new List<RestTime>(); try { using (SqlConnection connection = new SqlConnection(this.connectionString)) { connection.Open(); string GetRestTime = @"SELECT Duration, StartTime, EndTime FROM RestTime WHERE DayType = @DayTipe"; using (SqlCommand command = new SqlCommand(GetRestTime, connection)) { command.Parameters.AddWithValue("@DayTipe", dayTipe); using (SqlDataReader dataReader = command.ExecuteReader()) { while (dataReader.Read()) { if (!dataReader.IsDBNull(0)) { listRestTime.Add(new RestTime { Duration = dataReader.GetInt32(0), StartTime = dataReader.GetTimeSpan(1), EndTime = dataReader.GetTimeSpan(2) }); } } } } } } catch (Exception ex) { Console.WriteLine("Exception GetRestTime: " + ex.ToString()); } return listRestTime; }
        private List<int> ReadDataFromCsv(string filePath, int DaysInMonth) { var dailyTotals = new List<int>(new int[DaysInMonth]); try { var lines = System.IO.File.ReadAllLines(filePath); foreach (var line in lines) { if (string.IsNullOrWhiteSpace(line)) continue; var valuesForThisLine = line.Split(',').Skip(1).Select(field => int.TryParse(field.Trim(), out int num) ? num : 0).ToList(); for (int i = 0; i < valuesForThisLine.Count && i < DaysInMonth; i++) { dailyTotals[i] += valuesForThisLine[i]; } } } catch { return new List<int>(new int[DaysInMonth]); } return dailyTotals; }
    }
}