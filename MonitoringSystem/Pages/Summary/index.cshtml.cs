using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using MonitoringSystem.Data;
using Microsoft.EntityFrameworkCore;
using MonitoringSystem.Models;



namespace MonitoringSystem.Pages.Summary
{
    public class SummaryModel : PageModel
    {
        public string connectionString = "Server=10.83.33.103;trusted_connection=false;Database=PROMOSYS;User Id=sa;Password=sa;Persist Security Info=False;Encrypt=False";
        //public string connectionString = "Data Source=DESKTOP-NBPATD6\\MSSQLSERVERR;trusted_connection=true;trustservercertificate=True;Database=PROMOSYS;Integrated Security=True;Encrypt=False";
        public string errorMessage = "";

        private readonly ApplicationDbContext _context;

        public SummaryModel(ApplicationDbContext context)
        {
            _context = context;  // Dependency injection
        }

        public List<PlanQty> plansQty = new List<PlanQty>();
        public List<ManPower> noOfOperator = new List<ManPower>();

        [BindProperty]
        public DateTime StartSelectedDate { get; set; } = DateTime.Today;
        public List<ProductionAchievement> listProdAchieve = new List<ProductionAchievement>();

        [BindProperty]
        public DateTime EndSelectedDate { get; set; } = DateTime.Today;
        public List<HourlyPlanData> HourlyPlans { get; set; }

        public TimeSpan? BreakTime1Start { get; set; }
        public TimeSpan? BreakTime1End { get; set; }
        public TimeSpan? BreakTime2Start { get; set; }
        public TimeSpan? BreakTime2End { get; set; }


        private readonly List<(TimeSpan Start, TimeSpan End)> fixedBreakTimes = new List<(TimeSpan, TimeSpan)>
        {
            (TimeSpan.FromHours(7), TimeSpan.FromHours(7).Add(TimeSpan.FromMinutes(5))),
            (TimeSpan.FromHours(9).Add(TimeSpan.FromMinutes(30)), TimeSpan.FromHours(9).Add(TimeSpan.FromMinutes(35))),
            (TimeSpan.FromHours(15).Add(TimeSpan.FromMinutes(30)), TimeSpan.FromHours(15).Add(TimeSpan.FromMinutes(35))),
            (TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(15)), TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(45)))
        };

        public void OnGet()
        {
            StartSelectedDate = DateTime.Today;
            EndSelectedDate = DateTime.Today;
            GetProductionPlan();
            GetManPower();
        }

        public void OnPost()
        {
            var StartDate = Request.Form["StartSelectedDate"];
            var EndDate = Request.Form["EndSelectedDate"];

            if (EndSelectedDate < StartSelectedDate)
            {
                EndSelectedDate = DateTime.Today;
            }

            if (string.IsNullOrEmpty(StartDate) && string.IsNullOrEmpty(EndDate))
            {
                StartSelectedDate = DateTime.Today;
                EndSelectedDate = DateTime.Today;
            } 
            else if (string.IsNullOrEmpty(StartDate) && !string.IsNullOrEmpty(EndDate))
            {
                StartSelectedDate = DateTime.Today;
            }
            else if (!string.IsNullOrEmpty(StartDate) && string.IsNullOrEmpty(EndDate))
            {
                EndSelectedDate = DateTime.Today;
            }

            GetProductionPlan();
            GetManPower();
        }

        private List<(DateTime Start, DateTime End)> GetFixedBreakTimes_DateTime(DateTime date)
        {
            return new List<(DateTime, DateTime)>
            {
                (date.Date.AddHours(7), date.Date.AddHours(7).AddMinutes(5)),
                (date.Date.AddHours(9).AddMinutes(30), date.Date.AddHours(9).AddMinutes(35)),
                (date.Date.AddHours(15).AddMinutes(30), date.Date.AddHours(15).AddMinutes(35)),
                (date.Date.AddHours(18).AddMinutes(15), date.Date.AddHours(18).AddMinutes(45))
            };
        }
        private List<(TimeSpan Start, TimeSpan End)> GetAdditionalBreakTimes()
        {
            List<(TimeSpan Start, TimeSpan End)> additionalBreaks = new List<(TimeSpan, TimeSpan)>();
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string sql = @"SELECT TOP 1 BreakTime1Start, BreakTime1End, BreakTime2Start, BreakTime2End 
                           FROM AdditionalBreakTimes 
                           WHERE Date = @Date
                           ORDER BY CreatedAt DESC";
                    using (SqlCommand cmd = new SqlCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@Date", DateTime.Today); // fix hari ini
                        using (SqlDataReader reader = cmd.ExecuteReader())
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
                Console.WriteLine("GetAdditionalBreakTimes error: " + ex.Message);
            }
            return additionalBreaks;
        }


        private List<(DateTime Start, DateTime End)> GetAdditionalBreakTimes_DateTime(DateTime date)
        {
            List<(DateTime Start, DateTime End)> additionalBreaks = new List<(DateTime, DateTime)>();
            try {
                using (SqlConnection connection = new SqlConnection(connectionString)) {
                    connection.Open();
                    string sql = @"SELECT TOP 1 BreakTime1Start, BreakTime1End, BreakTime2Start, BreakTime2End 
                           FROM AdditionalBreakTimes 
                           WHERE Date = @Date
                           ORDER BY CreatedAt DESC";
                    using (SqlCommand cmd = new SqlCommand(sql, connection)) {
                        cmd.Parameters.AddWithValue("@Date", date.Date);
                        using (SqlDataReader reader = cmd.ExecuteReader()) {
                            if (reader.Read())
                            {
                                if (!reader.IsDBNull(0) && !reader.IsDBNull(1)){
                                    additionalBreaks.Add((date.Date.Add(reader.GetTimeSpan(0)), date.Date.Add(reader.GetTimeSpan(1))));
                                }
                                if (!reader.IsDBNull(2) && !reader.IsDBNull(3)) {
                                    additionalBreaks.Add((date.Date.Add(reader.GetTimeSpan(2)), date.Date.Add(reader.GetTimeSpan(3))));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) {
                Console.WriteLine("GetAdditionalBreakTimes_DateTime error: " + ex.Message);
            }
            return additionalBreaks;
        }

        private List<(DateTime Start, DateTime End)> GetTodayBreakTimes_DateTime(DateTime date)
        {
            var fixedBreaks = GetFixedBreakTimes_DateTime(date);
            var additionalBreaks = GetAdditionalBreakTimes_DateTime(date);
            return fixedBreaks.Concat(additionalBreaks).OrderBy(b => b.Start).ToList();
        }

        private List<(DateTime Start, DateTime End)> SplitIntervalExcludingBreaks_DateTime((DateTime Start, DateTime End) workInterval, List<(DateTime Start, DateTime End)> breaks)
        {
            var resultIntervals = new List<(DateTime, DateTime)>();
            var currentStart = workInterval.Start;

            foreach (var brk in breaks)
            {
                if (brk.End <= currentStart) continue;
                if (brk.Start >= workInterval.End) break;

                if (brk.Start > currentStart) {
                    resultIntervals.Add((currentStart, brk.Start));
                }
                currentStart = brk.End > currentStart ? brk.End : currentStart;
            }

            if (currentStart < workInterval.End) {
                resultIntervals.Add((currentStart, workInterval.End));
            }

            return resultIntervals;
        }


        public void GetProductionPlan()
        {
            try {
                using (SqlConnection connection = new SqlConnection(connectionString)) {
                    connection.Open();
                    string getTotalProduction = @"SELECT SUM(Quantity), ProductionRecords.MachineCode FROM ProductionRecords 
                                                  JOIN ProductionPlan ON ProductionRecords.PlanId = ProductionPlan.Id
                                                  WHERE ProductionPlan.CurrentDate BETWEEN @StartSelectedDate AND @EndSelectedDate
                                                  GROUP BY ProductionRecords.MachineCode;";
                    using (SqlCommand command = new SqlCommand(getTotalProduction, connection)) {
                        command.Parameters.AddWithValue("@StartSelectedDate", StartSelectedDate);
                        command.Parameters.AddWithValue("@EndSelectedDate", EndSelectedDate);
                        using (SqlDataReader dataReader = command.ExecuteReader()) {
                            while (dataReader.Read()) {
                                PlanQty plan = new PlanQty();
                                plan.Quantity = dataReader.GetInt32(0);
                                plan.MachineCode = dataReader.GetString(1);
                                plansQty.Add(plan);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) {
                Console.WriteLine("Exception: " + ex.ToString());
            }
        }

        public List<ActualQty> GetActualData(TimeSpan StartTime, TimeSpan EndTime)
        {
            List<ActualQty> listActualQty = new List<ActualQty>();

            var breakTimes = GetTodayBreakTimes();

            // pecah interval kerja jadi subinterval tidak overlap break time
            var intervals = SplitIntervalExcludingBreaks((StartTime, EndTime), breakTimes);

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    foreach (var interval in intervals)
                    {
                        string getActualData = @"SELECT COUNT(*) AS Qty, MachineCode FROM OEESN
                                         WHERE CAST(SDate AS Date) BETWEEN @StartSelectedDate AND @EndSelectedDate
                                               AND CAST(SDate AS Time) >= @StartTime
                                               AND CAST(SDate AS Time) < @EndTime
                                         GROUP BY MachineCode;";

                        using (SqlCommand command = new SqlCommand(getActualData, connection))
                        {
                            command.Parameters.AddWithValue("@StartSelectedDate", StartSelectedDate);
                            command.Parameters.AddWithValue("@EndSelectedDate", EndSelectedDate);
                            command.Parameters.AddWithValue("@StartTime", interval.Start);
                            command.Parameters.AddWithValue("@EndTime", interval.End);

                            using (SqlDataReader dataReader = command.ExecuteReader())
                            {
                                while (dataReader.Read())
                                {
                                    var machineCode = dataReader.GetString(1);
                                    var qty = dataReader.GetInt32(0);

                                    // cek apakah sudah ada di list, jika ada tambahkan qty-nya
                                    var existing = listActualQty.FirstOrDefault(a => a.MachineCode == machineCode);
                                    if (existing != null)
                                    {
                                        existing.Quantity += qty;
                                    }
                                    else
                                    {
                                        listActualQty.Add(new ActualQty { MachineCode = machineCode, Quantity = qty });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }

            return listActualQty;
        }
        // Untuk CU (MCH1-01)
        public int GetPlanForSummaryCU(DateTime selectedDate)
        {
            return _context.HourlyPlanData
                .Where(x => x.SelectedDate == selectedDate && x.MachineCode == "MCH1-01")  // Filter berdasarkan machine code CU
                .Sum(x => x.TotalPlan);  // Mengembalikan jumlah total Plan untuk CU
        }

        // Untuk CS (MCH1-02)
        public int GetPlanForSummaryCS(DateTime selectedDate)
        {
            return _context.HourlyPlanData
                .Where(x => x.SelectedDate == selectedDate && x.MachineCode == "MCH1-02")  // Filter berdasarkan machine code CS
                .Sum(x => x.TotalPlan);  // Mengembalikan jumlah total Plan untuk CS
        }

        private List<(TimeSpan Start, TimeSpan End)> GetTodayBreakTimes()
        {
            var additional = GetAdditionalBreakTimes();
            return fixedBreakTimes.Concat(additional).OrderBy(b => b.Start).ToList();
        }

        private List<(TimeSpan Start, TimeSpan End)> SplitIntervalExcludingBreaks((TimeSpan Start, TimeSpan End) workInterval, List<(TimeSpan Start, TimeSpan End)> breaks)
        {
            var resultIntervals = new List<(TimeSpan, TimeSpan)>();
            var currentStart = workInterval.Start;

            foreach (var brk in breaks)
            {
                if (brk.End <= currentStart) continue; // break sebelum current start
                if (brk.Start >= workInterval.End) break; // break setelah work interval

                if (brk.Start > currentStart)
                {
                    // interval sebelum break
                    resultIntervals.Add((currentStart, brk.Start));
                }
                // update current start setelah break end
                currentStart = brk.End > currentStart ? brk.End : currentStart;
            }

            if (currentStart < workInterval.End)
            {
                resultIntervals.Add((currentStart, workInterval.End));
            }

            return resultIntervals;
        }


        public List<SUTModel> GetSUTModel(TimeSpan StartTime, TimeSpan EndTime)
        {
            List<SUTModel> listSUTModel = new List<SUTModel>();
            var breakTimes = GetTodayBreakTimes();
            var intervals = SplitIntervalExcludingBreaks((StartTime, EndTime), breakTimes);

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    foreach (var interval in intervals)
                    {
                        string sql = @"SELECT MasterData.SUT, OEESN.MachineCode FROM OEESN
                               JOIN MasterData ON OEESN.Product_Id = MasterData.Product_Id
                               WHERE CAST(OEESN.SDate AS DATE) BETWEEN @StartSelectedDate AND @EndSelectedDate
                                     AND CAST(SDate AS Time) >= @StartTime 
                                     AND CAST(SDate AS Time) < @EndTime
                               ORDER BY SDate DESC;";

                        using (SqlCommand cmd = new SqlCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@StartSelectedDate", StartSelectedDate);
                            cmd.Parameters.AddWithValue("@EndSelectedDate", EndSelectedDate);
                            cmd.Parameters.AddWithValue("@StartTime", interval.Start);
                            cmd.Parameters.AddWithValue("@EndTime", interval.End);

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var sut = reader.GetInt32(0);
                                    var machineCode = reader.GetString(1);

                                    // Cek jika sudah ada data yg sama, supaya tidak duplicate
                                    if (!listSUTModel.Any(s => s.SUT == sut && s.MachineCode == machineCode))
                                    {
                                        listSUTModel.Add(new SUTModel { SUT = sut, MachineCode = machineCode });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }

            return listSUTModel;
        }


        public List<RestTime> GetRestTime(string dayTipe)
        {
            List<RestTime> listRestTime = new List<RestTime>();
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string GetRestTime = @"SELECT Duration, StartTime, EndTime FROM RestTime WHERE DayType = @DayTipe";
                    using (SqlCommand command = new SqlCommand(GetRestTime, connection))
                    {
                        command.Parameters.AddWithValue("@DayTipe", dayTipe);
                        using (SqlDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                if (!dataReader.IsDBNull(0))
                                {
                                    RestTime restTime = new RestTime();
                                    restTime.Duration = dataReader.GetInt32(0);
                                    restTime.StartTime = dataReader.GetTimeSpan(1);
                                    restTime.EndTime = dataReader.GetTimeSpan(2);
                                    listRestTime.Add(restTime);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }
            return listRestTime;
        }

        public int GetTotalRestTime(List<RestTime> listRestTime, TimeSpan StartTime, TimeSpan EndTime, TimeSpan CurrentTime)
        {
            int TotalRestTime = 0;
            TotalRestTime = listRestTime.Sum(rest =>
            {
                TimeSpan restStart = rest.StartTime < StartTime ? StartTime : rest.StartTime;
                TimeSpan restEnd = rest.EndTime > EndTime ? EndTime : rest.EndTime;
                if (StartSelectedDate == DateTime.Now.Date)
                {
                    restStart = restStart > CurrentTime ? CurrentTime : restStart;
                    restEnd = restEnd > CurrentTime ? CurrentTime : restEnd;
                }
                return restStart < restEnd ? (int)(restEnd - restStart).TotalMinutes : 0;
            });

            return TotalRestTime;
        }

        public string DetermineTypeOfDay(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday or DayOfWeek.Tuesday or DayOfWeek.Wednesday or DayOfWeek.Thursday => "REGULAR",
                DayOfWeek.Friday => "FRIDAY",
                DayOfWeek.Saturday or DayOfWeek.Sunday => "WEEKEND",
                _ => throw new NotImplementedException()
            };
        }

        public List<AssemblyTime> GetAssemblyTime(TimeSpan StartTime, TimeSpan EndTime)
        {
            List<AssemblyTime> listAssemblyTime = new List<AssemblyTime>();
            var breakTimes = GetTodayBreakTimes();
            var intervals = SplitIntervalExcludingBreaks((StartTime, EndTime), breakTimes);

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    foreach (var interval in intervals)
                    {
                        string sql = @"SELECT MasterData.ProductName As Model, OEESN.MachineCode As MachineCode, 
                                      MasterData.SUT As SUT, CAST(OEESN.SDate AS Time) As ProductionTime
                               FROM OEESN JOIN Masterdata ON OEESN.Product_Id = MasterData.Product_Id
                               WHERE CAST(OEESN.SDate AS DATE) BETWEEN @StartSelectedDate AND @EndSelectedDate
                                     AND CAST(SDate AS Time) >= @StartTime 
                                     AND CAST(SDate AS Time) < @EndTime
                               ORDER BY CAST(OEESN.SDate AS TIME) ASC";

                        using (SqlCommand cmd = new SqlCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@StartSelectedDate", StartSelectedDate);
                            cmd.Parameters.AddWithValue("@EndSelectedDate", EndSelectedDate);
                            cmd.Parameters.AddWithValue("@StartTime", interval.Start);
                            cmd.Parameters.AddWithValue("@EndTime", interval.End);

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    listAssemblyTime.Add(new AssemblyTime
                                    {
                                        Model = reader.GetString(0),
                                        MachineCode = reader.GetString(1),
                                        SUT = reader.GetInt32(2),
                                        ProductionTime = reader.GetTimeSpan(3)
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }

            return listAssemblyTime;
        }


        public List<ProductionAchievement> GetHourlyAchievement(TimeSpan StartTime, TimeSpan EndTime)
        {
            List<ProductionAchievement> listProdAchieve = new List<ProductionAchievement>();
            var breakTimes = GetTodayBreakTimes();
            var intervals = SplitIntervalExcludingBreaks((StartTime, EndTime), breakTimes);

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    foreach (var interval in intervals)
                    {
                        string sql = @"SELECT MIN(OEESN.SDate) As FirstTime,
                                      CAST(DATEADD(HOUR, DATEDIFF(HOUR, 0, OEESN.SDate), 0) AS TIME) AS StartTime,
                                      CAST(DATEADD(HOUR, DATEDIFF(HOUR, 0, OEESN.SDate) + 1, 0) AS TIME) As EndTime,
                                      Masterdata.ProductName As Model, 
                                      OEESN.MachineCode As MachineCode,
                                      MasterData.QtyHour As Target,
                                      MasterData.SUT AS SUT,
                                      COUNT(*) AS Actual
                               FROM OEESN
                                    JOIN Masterdata ON OEESN.Product_Id = MasterData.Product_Id
                               WHERE CAST(SDate As DATE) BETWEEN @StartSelectedDate AND @EndSelectedDate
                                     AND CAST(SDate As TIME) >= @StartTime
                                     AND CAST(SDate As TIME) < @EndTime   
                               GROUP BY DATEDIFF(HOUR, 0, SDate), Masterdata.ProductName, MasterData.QtyHour, MasterData.SUT, OEESN.MachineCode
                               ORDER BY MIN(OEESN.SDate);";

                        using (SqlCommand cmd = new SqlCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@StartSelectedDate", StartSelectedDate);
                            cmd.Parameters.AddWithValue("@EndSelectedDate", EndSelectedDate);
                            cmd.Parameters.AddWithValue("@StartTime", interval.Start);
                            cmd.Parameters.AddWithValue("@EndTime", interval.End);

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    TimeSpan startTimeHour = reader.GetTimeSpan(1);
                                    TimeSpan endTimeHour = reader.GetTimeSpan(2);
                                    var time = $"{startTimeHour:hh\\:mm} - {endTimeHour:hh\\:mm}";

                                    var existing = listProdAchieve.FirstOrDefault(p => p.StartTime == startTimeHour
                                                                                      && p.EndTime == endTimeHour
                                                                                      && p.Model == reader.GetString(3)
                                                                                      && p.MachineCode == reader.GetString(4));
                                    if (existing == null)
                                    {
                                        listProdAchieve.Add(new ProductionAchievement
                                        {
                                            FirstTime = reader.GetDateTime(0),
                                            StartTime = startTimeHour,
                                            EndTime = endTimeHour,
                                            Time = time,
                                            Model = reader.GetString(3),
                                            MachineCode = reader.GetString(4),
                                            Plan = reader.GetInt32(5),
                                            SUT = reader.GetInt32(6),
                                            Actual = reader.GetInt32(7)
                                        });
                                    }
                                    else
                                    {
                                        // jika data sudah ada, tambahkan Actual-nya
                                        existing.Actual += reader.GetInt32(7);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }

            return listProdAchieve;
        }
        private bool IsOverlappingWithBreakTime(TimeSpan start, TimeSpan end, int toleranceSeconds = 60)
        {
            // tambah toleransi 1 menit (60 detik)
            TimeSpan tolerance = TimeSpan.FromSeconds(toleranceSeconds);

            bool Overlaps(TimeSpan bStart, TimeSpan bEnd)
            {
                if (bStart == null || bEnd == null) return false;

                // Check overlap dengan toleransi
                return start < (bEnd + tolerance) && (end + tolerance) > bStart;
            }

            return (BreakTime1Start != null && BreakTime1End != null && Overlaps(BreakTime1Start.Value, BreakTime1End.Value))
                || (BreakTime2Start != null && BreakTime2End != null && Overlaps(BreakTime2Start.Value, BreakTime2End.Value));
        }
        public List<TotalChangesModel> GetTotalChangeModel(TimeSpan StartTime, TimeSpan EndTime)
        {
            List<TotalChangesModel> listTotalChangeModel = new List<TotalChangesModel>();
            var breakTimes = GetTodayBreakTimes();
            var intervals = SplitIntervalExcludingBreaks((StartTime, EndTime), breakTimes);

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    foreach (var interval in intervals)
                    {
                        string sql = @"SELECT COUNT(DISTINCT OEESN.Product_Id), OEESN.MachineCode FROM OEESN
                               WHERE CAST(SDate AS Date) BETWEEN @StartSelectedDate AND @EndSelectedDate
                                     AND CAST(SDate AS Time) >= @StartTime
                                     AND CAST(SDate AS Time) < @EndTime
                               GROUP BY OEESN.Product_Id, OEESN.MachineCode;";

                        using (SqlCommand cmd = new SqlCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@StartSelectedDate", StartSelectedDate);
                            cmd.Parameters.AddWithValue("@EndSelectedDate", EndSelectedDate);
                            cmd.Parameters.AddWithValue("@StartTime", interval.Start);
                            cmd.Parameters.AddWithValue("@EndTime", interval.End);

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var productId = reader.GetInt32(0);
                                    var machineCode = reader.GetString(1);

                                    if (!listTotalChangeModel.Any(t => t.ProductId == productId && t.MachineCode == machineCode))
                                    {
                                        listTotalChangeModel.Add(new TotalChangesModel
                                        {
                                            ProductId = productId,
                                            MachineCode = machineCode
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }

            return listTotalChangeModel;
        }


        public List<ModelTime> GetModelTime(TimeSpan StartTime, TimeSpan EndTime)
        {
            List<ModelTime> listModelTime = new List<ModelTime>();
            var breakTimes = GetTodayBreakTimes();
            var intervals = SplitIntervalExcludingBreaks((StartTime, EndTime), breakTimes);

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    foreach (var interval in intervals)
                    {
                        string sql = @"SELECT MIN(CAST(OEESN.SDate AS Time)) AS StartTime, MAX(CAST(OEESN.SDate AS Time)) AS EndTime, OEESN.MachineCode FROM OEESN 
                               JOIN Masterdata ON OEESN.Product_Id = MasterData.Product_Id
                               WHERE CAST (OEESN.SDate AS DATE) BETWEEN @StartSelectedDate AND @EndSelectedDate
                                     AND CAST(SDate AS Time) >= @StartTime 
                                     AND CAST(SDate AS Time) < @EndTime
                               GROUP BY OEESN.MachineCode";

                        using (SqlCommand cmd = new SqlCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@StartSelectedDate", StartSelectedDate);
                            cmd.Parameters.AddWithValue("@EndSelectedDate", EndSelectedDate);
                            cmd.Parameters.AddWithValue("@StartTime", interval.Start);
                            cmd.Parameters.AddWithValue("@EndTime", interval.End);

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    listModelTime.Add(new ModelTime
                                    {
                                        StartTime = reader.GetTimeSpan(0),
                                        EndTime = reader.GetTimeSpan(1),
                                        MachineCode = reader.GetString(2)
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }

            return listModelTime;
        }


        public List<TotalDefect> GetTotalDefect(TimeSpan StartTime, TimeSpan EndTime)
        {
            List<TotalDefect> listTotalDefect = new List<TotalDefect>();
            var breakTimes = GetTodayBreakTimes();
            var intervals = SplitIntervalExcludingBreaks((StartTime, EndTime), breakTimes);

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    foreach (var interval in intervals)
                    {
                        string sql = @"SELECT MachineCode, COUNT(*) AS TotalDefect FROM NG_RPTS 
                               WHERE CAST(SDate AS DATE) BETWEEN @StartSelectedDate AND @EndSelectedDate
                                     AND CAST(SDate AS Time) >= @StartTime 
                                     AND CAST(SDate AS Time) < @EndTime
                               GROUP BY MachineCode";

                        using (SqlCommand cmd = new SqlCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@StartSelectedDate", StartSelectedDate);
                            cmd.Parameters.AddWithValue("@EndSelectedDate", EndSelectedDate);
                            cmd.Parameters.AddWithValue("@StartTime", interval.Start);
                            cmd.Parameters.AddWithValue("@EndTime", interval.End);

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var machineCode = reader.GetString(0);
                                    var defectQty = reader.GetInt32(1);

                                    var existing = listTotalDefect.FirstOrDefault(d => d.MachineCode == machineCode);
                                    if (existing != null)
                                    {
                                        existing.DefectQty += defectQty;
                                    }
                                    else
                                    {
                                        listTotalDefect.Add(new TotalDefect
                                        {
                                            MachineCode = machineCode,
                                            DefectQty = defectQty
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }

            return listTotalDefect;
        }


        public List<StartEndModel> GetStartEndModel(string MachineCode, TimeSpan StartTime, TimeSpan EndTime)
        {
            List<StartEndModel> ListStartEnd = new List<StartEndModel>();
            var breakTimes = GetTodayBreakTimes();
            var intervals = SplitIntervalExcludingBreaks((StartTime, EndTime), breakTimes);

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    foreach (var interval in intervals)
                    {
                        string sql = @"WITH GroupedData AS (
                                   SELECT 
                                       MasterData.ProductName AS Model,
                                       CAST(OEESN.SDate AS Time) AS StartTime,
                                       CAST(OEESN.SDate AS Time) AS EndTime,
                                       ROW_NUMBER() OVER (ORDER BY CAST(OEESN.SDate AS Time)) 
                                       - ROW_NUMBER() OVER (PARTITION BY MasterData.ProductName ORDER BY CAST(OEESN.SDate AS Time)) AS GroupID
                                   FROM OEESN
                                   JOIN MasterData ON OEESN.Product_Id = MasterData.Product_Id
                                   WHERE 
                                       OEESN.MachineCode = @MachineCode
                                       AND CAST(OEESN.SDate AS Time) >= @StartTime
                                       AND CAST(OEESN.SDate AS Time) < @EndTime
                               ),
                               MergedData AS (
                                   SELECT 
                                       Model,
                                       MIN(StartTime) AS StartTime,
                                       MAX(EndTime) AS EndTime
                                   FROM GroupedData
                                   GROUP BY Model, GroupID
                               )
                               SELECT Model, StartTime, EndTime FROM MergedData ORDER BY StartTime;";

                        using (SqlCommand cmd = new SqlCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@MachineCode", MachineCode);
                            cmd.Parameters.AddWithValue("@StartTime", interval.Start);
                            cmd.Parameters.AddWithValue("@EndTime", interval.End);

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    ListStartEnd.Add(new StartEndModel
                                    {
                                        Model = reader.GetString(0),
                                        StartTime = reader.GetTimeSpan(1),
                                        EndTime = reader.GetTimeSpan(2)
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }

            return ListStartEnd;
        }


        public void GetManPower()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getManPower = @"SELECT DISTINCT TOP 2 NoOfOperator, MachineCode FROM OEESN WHERE CAST(SDate AS DATE) BETWEEN @StartSelectedDate AND @EndSelectedDate";
                    using (SqlCommand command = new SqlCommand(getManPower, connection))
                    {
                        command.Parameters.AddWithValue("@StartSelectedDate", StartSelectedDate);
                        command.Parameters.AddWithValue("@EndSelectedDate", EndSelectedDate);
                        using (SqlDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                ManPower manPower = new ManPower();
                                manPower.NoOfOperator = dataReader.GetInt32(0);
                                manPower.MachineCode = dataReader.GetString(1);
                                noOfOperator.Add(manPower);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)    
            {
                Console.WriteLine(ex.Message);
            }
        }

        public int GetModelPlan(string model)
        {
            int QuantityPlan = 0;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getQuantityModel = @"SELECT ProductionRecords.Quantity FROM ProductionRecords 
                                                JOIN ProductionPlan ON ProductionRecords.PlanId = ProductionPlan.Id 
                                                WHERE ProductionRecords.ProductName = @Model 
                                                      AND ProductionPlan.CurrentDate BETWEEN @StartSelectedDate AND @EndSelectedDate";
                    using (SqlCommand command = new SqlCommand(getQuantityModel, connection))
                    {
                        command.Parameters.AddWithValue("@Model", model);
                        command.Parameters.AddWithValue("@StartSelectedDate", StartSelectedDate);
                        command.Parameters.AddWithValue("@EndSelectedDate", EndSelectedDate);

                        var Result = command.ExecuteScalar();
                        if (Result != null)
                        {
                            QuantityPlan = (int)Result;
                        }
                        else
                        {
                            QuantityPlan = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }
            return QuantityPlan;
        }

        public int CalculatePlan(
            TimeSpan startTime,
            TimeSpan endTime,
            int SUT,
            List<RestTime> restTime
        )
        {
            TimeSpan effectiveTime = endTime - startTime;
            foreach (var rest in restTime)
            {
                if (startTime < rest.EndTime && endTime > rest.StartTime)
                {
                    TimeSpan overlapStart = TimeSpan.FromTicks(Math.Max(startTime.Ticks, rest.StartTime.Ticks));
                    TimeSpan overlapEnd = TimeSpan.FromTicks(Math.Min(endTime.Ticks, rest.EndTime.Ticks));
                    effectiveTime -= (overlapEnd - overlapStart);
                }
            }
            return Convert.ToInt32(effectiveTime.TotalSeconds / SUT);
        }

        public int CalculatePlanPerSUT(
            int DailyPlan, 
            List<ProductionAchievement> ProdAchievement,
            List<RestTime> listRestTime
        )
        {
            int TotalPlanPerSUT = 0;
            int PlanPerSUT = 0;
            var PreviousModel = "";
            var CurrentDate = DateTime.Now.Date;
            var CurrentWorkingTime = DateTime.Now.TimeOfDay;

            if (ProdAchievement.Count > 0)
            {
                for (int i = 0; i < ProdAchievement.Count; i++)
                {
                    var CurrentModel = ProdAchievement[i].Model;
                    var SUT = ProdAchievement[i].SUT;
                    var StartTime = ProdAchievement[i].StartTime;
                    var EndTime = ProdAchievement[i].EndTime;

                    var FirstTime_Model = TimeSpan.Zero;
                    var LastTime_Model = TimeSpan.Zero;
                    var QuantityPlan = 0;

                    if (CurrentModel != null)
                    {
                        FirstTime_Model = GetFirstTimeModel(StartTime, EndTime, CurrentModel);
                        LastTime_Model = GetLastTimeModel(StartTime, EndTime, CurrentModel);
                        QuantityPlan = GetModelPlan(CurrentModel);
                    }

                    if (StartSelectedDate == CurrentDate)
                    {
                        if (CurrentWorkingTime >= StartTime && CurrentWorkingTime <= EndTime)
                        {
                            if (CurrentModel == PreviousModel)
                            {
                                PlanPerSUT = CalculatePlan(StartTime, CurrentWorkingTime, SUT, listRestTime);
                            }
                            else
                            {
                                PlanPerSUT = 0;
                                PlanPerSUT = CalculatePlan(FirstTime_Model, EndTime, SUT, listRestTime);
                            }
                        }
                        else
                        {
                            if (CurrentModel == PreviousModel)
                            {
                                PlanPerSUT = CalculatePlan(StartTime, EndTime, SUT, listRestTime);
                            }
                            else
                            {
                                PlanPerSUT = 0;
                                PlanPerSUT = CalculatePlan(FirstTime_Model, EndTime, SUT, listRestTime);
                            }
                        }
                    }
                    else
                    {
                        PlanPerSUT = 0;
                        if (CurrentModel == PreviousModel)
                        {
                            PlanPerSUT = CalculatePlan(StartTime, EndTime, SUT, listRestTime);
                        }
                        else
                        {
                            PlanPerSUT = 0;
                            PlanPerSUT = CalculatePlan(FirstTime_Model, EndTime, SUT, listRestTime);
                        }
                    }
                    PlanPerSUT = Math.Min(PlanPerSUT, QuantityPlan);
                    PreviousModel = CurrentModel;
                    TotalPlanPerSUT += PlanPerSUT;
                    TotalPlanPerSUT = Math.Min(TotalPlanPerSUT, DailyPlan);
                }
            }
            return TotalPlanPerSUT;
        }

        public int CalculateTotalLossTime(
            List<AssemblyTime> assemblyTimes,
            List<RestTime> restTimes
        )
        {
            int totalLossTime = 0;
            var sortedAssemblyTimes = assemblyTimes.OrderBy(p => p.ProductionTime).ToList();

            for (int i = 0; i < sortedAssemblyTimes.Count; i++)
            {
                var current = sortedAssemblyTimes[i];
                var expectedEndTime = current.ProductionTime.Add(TimeSpan.FromSeconds(current.SUT * 3));
                var actualEndTime = i < sortedAssemblyTimes.Count - 1 ? sortedAssemblyTimes[i + 1].ProductionTime : expectedEndTime;

                foreach (var rest in restTimes)
                {
                    if (current.ProductionTime < rest.EndTime && actualEndTime > rest.StartTime)
                    {
                        if (current.ProductionTime >= rest.StartTime && current.ProductionTime < rest.EndTime)
                        {
                            current.ProductionTime = rest.EndTime;
                            expectedEndTime = current.ProductionTime.Add(TimeSpan.FromSeconds(current.SUT * 3));
                        }
                        if (actualEndTime > rest.EndTime && current.ProductionTime <= rest.StartTime)
                        {
                            actualEndTime = rest.StartTime;
                        }
                        if (actualEndTime > rest.StartTime && actualEndTime <= rest.EndTime)
                        {
                            actualEndTime = rest.StartTime;
                        }
                    }
                }

                int lossTime = 0;
                if (expectedEndTime < actualEndTime)
                {
                    lossTime = Math.Max(0, (int)(actualEndTime - current.ProductionTime).TotalSeconds);
                }

                totalLossTime += lossTime;
            }
            return totalLossTime;
        }

        public TimeSpan GetLastWorkingTime(string MachineCode, TimeSpan StartTime)
        {
            TimeSpan lastWorkingTime = TimeSpan.Zero;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getLastWorkingTime = @"SELECT MAX(CAST(SDate AS Time)) FROM OEESN 
                                                  WHERE CAST (SDate AS Date) BETWEEN @StartSelectedDate AND @EndSelectedDate 
                                                        AND CAST(SDate AS Time) >= @StartTime";
                    using (SqlCommand command = new SqlCommand(getLastWorkingTime, connection))
                    {
                        command.Parameters.AddWithValue("@StartSelectedDate", StartSelectedDate);
                        command.Parameters.AddWithValue("@EndSelectedDate", EndSelectedDate);
                        command.Parameters.AddWithValue("@MachineCode", MachineCode);
                        command.Parameters.AddWithValue("@StartTime", StartTime);

                        var Result = command.ExecuteScalar();
                        if (Result != null)
                        {
                            lastWorkingTime = (TimeSpan)Result;
                        }
                        else
                        {
                            lastWorkingTime = TimeSpan.Zero;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return lastWorkingTime;
        }

        public TimeSpan GetFirstTimeModel(TimeSpan StartTime, TimeSpan EndTime, string model)
        {
            TimeSpan FirstTime = TimeSpan.Zero;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getFirstTime = @"SELECT CAST(MIN(OEESN.SDate) AS Time) FROM OEESN 
                                            JOIN MasterData ON OEESN.Product_Id = MasterData.Product_Id 
                                            WHERE MasterData.ProductName = @Model AND CAST(OEESN.SDate AS Time) >= @StartTime 
                                                AND CAST(OEESN.SDate AS Time) <= @EndTime 
                                                AND CAST(OEESN.SDate AS DATE) BETWEEN @StartSelectedDate AND @EndSelectedDate";

                    using (SqlCommand command = new SqlCommand(getFirstTime, connection))
                    {
                        command.Parameters.AddWithValue("@Model", model);
                        command.Parameters.AddWithValue("@StartTime", StartTime);
                        command.Parameters.AddWithValue("@EndTime", EndTime);
                        command.Parameters.AddWithValue("@StartSelectedDate", StartSelectedDate);
                        command.Parameters.AddWithValue("@EndSelectedDate", EndSelectedDate);

                        var Result = command.ExecuteScalar();
                        if (Result != null)
                        {
                            FirstTime = (TimeSpan)Result;
                        }
                        else
                        {
                            FirstTime = TimeSpan.Zero;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }
            return FirstTime;
        }

        public TimeSpan GetLastTimeModel(TimeSpan StartTime, TimeSpan EndTime, string model)
        {
            TimeSpan LastTime = TimeSpan.Zero;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getLastTime = @"SELECT CAST(MAX(OEESN.SDate) AS Time) FROM OEESN 
                                            JOIN MasterData ON OEESN.Product_Id = MasterData.Product_Id 
                                            WHERE MasterData.ProductName = @Model AND CAST(OEESN.SDate AS Time) >= @StartTime 
                                                AND CAST(OEESN.SDate AS Time) <= @EndTime 
                                                AND CAST(OEESN.SDate AS DATE) BETWEEN @StartSelectedDate AND @EndSelectedDate";

                    using (SqlCommand command = new SqlCommand(getLastTime, connection))
                    {
                        command.Parameters.AddWithValue("@Model", model);
                        command.Parameters.AddWithValue("@StartTime", StartTime);
                        command.Parameters.AddWithValue("@EndTime", EndTime);
                        command.Parameters.AddWithValue("@StartSelectedDate", StartSelectedDate);
                        command.Parameters.AddWithValue("@EndSelectedDate", EndSelectedDate);

                        var Result = command.ExecuteScalar();
                        if (Result != null)
                        {
                            LastTime = (TimeSpan)Result;
                        }
                        else
                        {
                            LastTime = TimeSpan.Zero;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }
            return LastTime;
        }

        //---------------------------------------------------------------------------
        //------------------------ KHUSUS UNTUK SHIFT 3 -----------------------------
        //---------------------------------------------------------------------------

        public DateTime GetLastWorkingTime_03(string MachineCode, DateTime StartTime, DateTime EndTime)
        {
            DateTime lastWorkingTime = StartSelectedDate.AddDays(1).AddHours(7);
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getLastWorkingTime = @"SELECT MAX(SDate) FROM OEESN 
                                                  WHERE MachineCode = @MachineCode AND SDate BETWEEN @StartTime AND @EndTime;";
                    using (SqlCommand command = new SqlCommand(getLastWorkingTime, connection))
                    {
                        command.Parameters.AddWithValue("@MachineCode", MachineCode);
                        command.Parameters.AddWithValue("@StartTime", StartTime);
                        command.Parameters.AddWithValue("@EndTime", EndTime);

                        var Result = command.ExecuteScalar();
                        if (Result != null)
                        {
                            lastWorkingTime = (DateTime)Result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return lastWorkingTime;
        }

        public List<ActualQty> GetActualData_03(DateTime StartTime, DateTime EndTime)
        {
            List<ActualQty> listActualQty = new List<ActualQty>();

            var breakTimes = GetTodayBreakTimes_DateTime(StartTime.Date);
            var intervals = SplitIntervalExcludingBreaks_DateTime((StartTime, EndTime), breakTimes);

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    foreach (var interval in intervals)
                    {
                        string sql = @"SELECT COUNT(*) AS Qty, MachineCode FROM OEESN 
                               WHERE SDate BETWEEN @StartTime AND @EndTime
                               GROUP BY MachineCode;";

                        using (SqlCommand cmd = new SqlCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@StartTime", interval.Start);
                            cmd.Parameters.AddWithValue("@EndTime", interval.End);

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var machineCode = reader.GetString(1);
                                    var qty = reader.GetInt32(0);

                                    var existing = listActualQty.FirstOrDefault(a => a.MachineCode == machineCode);
                                    if (existing != null)
                                    {
                                        existing.Quantity += qty;
                                    }
                                    else
                                    {
                                        listActualQty.Add(new ActualQty { MachineCode = machineCode, Quantity = qty });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }

            return listActualQty;
        }


        public List<SUTModel> GetSUTModel_03(DateTime StartTime, DateTime EndTime)
        {
            List<SUTModel> listSUTModel = new List<SUTModel>();
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string GetSUT = @"SELECT MasterData.SUT, OEESN.MachineCode FROM OEESN
	                                         JOIN MasterData ON OEESN.Product_Id = MasterData.Product_Id
                                      WHERE OEESN.SDate BETWEEN @StartTime AND @EndTime
                                      ORDER BY SDate DESC;";
                    using (SqlCommand command = new SqlCommand(GetSUT, connection))
                    {
                        command.Parameters.AddWithValue("@StartTime", StartTime);
                        command.Parameters.AddWithValue("@EndTime", EndTime);
                        using (SqlDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                SUTModel sutModel = new SUTModel();
                                sutModel.SUT = dataReader.GetInt32(0);
                                sutModel.MachineCode = dataReader.GetString(1);
                                listSUTModel.Add(sutModel);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }
            return listSUTModel;
        }

        public List<ProductionAchievement> GetHourlyAchievement_03(DateTime StartTime, DateTime EndTime)
        {
            List<ProductionAchievement> listProdAchieve = new List<ProductionAchievement>();
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string SelectHourlyData = @"SELECT MIN(OEESN.SDate) As FirstTime,
                                                       CAST(DATEADD(HOUR, DATEDIFF(HOUR, 0, OEESN.SDate), 0) AS TIME) AS StartTime,
                                                       CAST(DATEADD(HOUR, DATEDIFF(HOUR, 0, OEESN.SDate) + 1, 0) AS TIME) As EndTime,
                                                       Masterdata.ProductName As Model, 
                                                       OEESN.MachineCode As MachineCode,
                                                       MasterData.QtyHour As Target,
                                                       MasterData.SUT AS SUT,
                                                       COUNT(*) AS Actual
                                                FROM OEESN
                                                       JOIN Masterdata ON OEESN.Product_Id = MasterData.Product_Id
                                                WHERE OEESN.SDate BETWEEN @StartTime AND @EndTime
                                                GROUP BY DATEDIFF(HOUR, 0, SDate), Masterdata.ProductName, 
                                                         MasterData.QtyHour, MasterData.SUT, OEESN.MachineCode
                                                ORDER BY MIN(OEESN.SDate);";

                    using (SqlCommand Command = new SqlCommand(SelectHourlyData, connection))
                    {
                        Command.Parameters.AddWithValue("@StartTime", StartTime);
                        Command.Parameters.AddWithValue("@EndTime", EndTime);
                        using (SqlDataReader dataReader = Command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                TimeSpan startTime = dataReader.GetTimeSpan(1);
                                TimeSpan endTime = dataReader.GetTimeSpan(2);
                                var time = $"{startTime:hh\\:mm} - {endTime:hh\\:mm}";

                                ProductionAchievement achievement = new ProductionAchievement();

                                achievement.FirstTime = dataReader.GetDateTime(0);
                                achievement.StartTime = startTime;
                                achievement.EndTime = endTime;
                                achievement.Time = time;
                                achievement.Model = dataReader.GetString(3);
                                achievement.MachineCode = dataReader.GetString(4);
                                achievement.Plan = dataReader.GetInt32(5);
                                achievement.SUT = dataReader.GetInt32(6);
                                achievement.Actual = dataReader.GetInt32(7);

                                listProdAchieve.Add(achievement);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }
            return listProdAchieve;
        }

        public int GetTotalRestTime_03(List<RestTime> listRestTime, DateTime StartTime, DateTime EndTime, TimeSpan CurrentTime)
        {
            int TotalRestTime = 0;
            TotalRestTime = listRestTime.Sum(rest =>
            {
                TimeSpan restStart = rest.StartTime < StartTime.TimeOfDay ? StartTime.TimeOfDay : rest.StartTime;
                TimeSpan restEnd = rest.EndTime > EndTime.TimeOfDay ? EndTime.TimeOfDay : rest.EndTime;
                if (StartSelectedDate == DateTime.Now.Date)
                {
                    restStart = restStart > CurrentTime ? CurrentTime : restStart;
                    restEnd = restEnd > CurrentTime ? CurrentTime : restEnd;
                }
                return restStart < restEnd ? (int)(restEnd - restStart).TotalMinutes : 0;
            });

            return TotalRestTime;
        }

        public List<AssemblyTime> GetAssemblyTime_03(DateTime StartTime, DateTime EndTime)
        {
            List<AssemblyTime> listAssemblyTime = new List<AssemblyTime>();
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string GetAssemblyProductionTime = @"SELECT MasterData.ProductName As Model, OEESN.MachineCode As MachineCode, 
                                                                MasterData.SUT As SUT, CAST(OEESN.SDate AS Time) As ProductionTime
                                                         FROM OEESN JOIN Masterdata ON OEESN.Product_Id = MasterData.Product_Id
                                                         WHERE OEESN.SDate BETWEEN @StartTime AND @EndTime
                                                         ORDER BY CAST(OEESN.SDate AS TIME) ASC";
                    using (SqlCommand command = new SqlCommand(GetAssemblyProductionTime, connection))
                    {
                        command.Parameters.AddWithValue("@StartTime", StartTime);
                        command.Parameters.AddWithValue("@EndTime", EndTime);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                AssemblyTime assemblyTime = new AssemblyTime();
                                assemblyTime.Model = reader.GetString(0);
                                assemblyTime.MachineCode = reader.GetString(1);
                                assemblyTime.SUT = reader.GetInt32(2);
                                assemblyTime.ProductionTime = reader.GetTimeSpan(3);
                                listAssemblyTime.Add(assemblyTime);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return listAssemblyTime;
        }
        
        public List<ModelTime> GetModelTime_03(DateTime StartTime, DateTime EndTime)
        {
            List<ModelTime> listModelTime = new List<ModelTime>();
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string GetModelTime = @"SELECT MIN(CAST(OEESN.SDate AS Time)) AS StartTime, MAX(CAST(OEESN.SDate AS Time)) AS EndTime, OEESN.MachineCode FROM OEESN 
                                            JOIN Masterdata ON OEESN.Product_Id = MasterData.Product_Id
                                            WHERE OEESN.SDate BETWEEN @StartTime AND @EndTime
                                            GROUP BY OEESN.MachineCode";

                    using (SqlCommand command = new SqlCommand(GetModelTime, connection))
                    {
                        command.Parameters.AddWithValue("@StartTime", StartTime);
                        command.Parameters.AddWithValue("@EndTime", EndTime);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                ModelTime modelTime = new ModelTime();
                                modelTime.StartTime = reader.GetTimeSpan(0);
                                modelTime.EndTime = reader.GetTimeSpan(1);
                                modelTime.MachineCode = reader.GetString(2);
                                listModelTime.Add(modelTime);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return listModelTime;
        }

        public List<TotalChangesModel> GetTotalChangeModel_03(DateTime StartTime, DateTime EndTime)
        {
            List<TotalChangesModel> listTotalChangeModel = new List<TotalChangesModel>();
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getTotalChangeModel = @"SELECT COUNT(DISTINCT OEESN.Product_Id), OEESN.MachineCode FROM OEESN
                                                  WHERE OEESN.SDate BETWEEN @StartTime AND @EndTime
                                                  GROUP BY OEESN.Product_Id, OEESN.MachineCode;";
                    using (SqlCommand command = new SqlCommand(getTotalChangeModel, connection))
                    {
                        command.Parameters.AddWithValue("@StartTime", StartTime);
                        command.Parameters.AddWithValue("@EndTime", EndTime);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                TotalChangesModel totalChange = new TotalChangesModel();
                                totalChange.ProductId = reader.GetInt32(0);
                                totalChange.MachineCode = reader.GetString(1);
                                listTotalChangeModel.Add(totalChange);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return listTotalChangeModel;
        }

        public int GetModelPlan_03(string model)
        {
            int QuantityPlan = 0;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getQuantityModel = @"SELECT ProductionRecords.Quantity FROM ProductionRecords 
                                                JOIN ProductionPlan ON ProductionRecords.PlanId = ProductionPlan.Id 
                                                WHERE ProductionRecords.ProductName = @Model AND ProductionPlan.CurrentDate BETWEEN @StartSelectedDate AND @EndSelectedDate";
                    using (SqlCommand command = new SqlCommand(getQuantityModel, connection))
                    {
                        command.Parameters.AddWithValue("@Model", model);
                        command.Parameters.AddWithValue("@StartSelectedDate", StartSelectedDate);
                        command.Parameters.AddWithValue("@EndSelectedDate", EndSelectedDate);
                        var Result = command.ExecuteScalar();
                        if (Result != null)
                        {
                            QuantityPlan = (int)Result;
                        }
                        else
                        {
                            QuantityPlan = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }
            return QuantityPlan;
        }

        public List<TotalDefect> GetTotalDefect_03(DateTime StartTime, DateTime EndTime)
        {
            List<TotalDefect> listTotalDefect = new List<TotalDefect>();
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getTotalDefect = @"SELECT MachineCode, COUNT(*) AS TotalDefect FROM NG_RPTS 
                                              WHERE OEESN.SDate BETWEEN @StartTime AND @EndTime
                                              GROUP BY MachineCode";
                    using (SqlCommand command = new SqlCommand(getTotalDefect, connection))
                    {
                        command.Parameters.AddWithValue("@StartTime", StartTime);
                        command.Parameters.AddWithValue("@EndTime", EndTime);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                TotalDefect totalDefect = new TotalDefect();
                                totalDefect.MachineCode = reader.GetString(0);
                                totalDefect.DefectQty = reader.GetInt32(1);
                                listTotalDefect.Add(totalDefect);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }
            return listTotalDefect;
        }

        public List<StartEndModel> GetStartEndModel_03(string MachineCode, DateTime StartTime, DateTime EndTime)
        {
            List<StartEndModel> ListStartEnd = new List<StartEndModel>();

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getStartEndModel = @"WITH GroupedData AS (
                                                    SELECT 
                                                        MasterData.ProductName AS Model,
                                                        CAST(OEESN.SDate AS Time) AS StartTime,
                                                        CAST(OEESN.SDate AS Time) AS EndTime,
                                                        ROW_NUMBER() OVER (ORDER BY CAST(OEESN.SDate AS Time)) 
                                                        - ROW_NUMBER() OVER (PARTITION BY MasterData.ProductName ORDER BY CAST(OEESN.SDate AS Time)) AS GroupID
                                                    FROM OEESN
                                                    JOIN MasterData ON OEESN.Product_Id = MasterData.Product_Id
                                                    WHERE 
                                                        OEESN.MachineCode = @MachineCode
                                                        AND OEESN.SDate BETWEEN @StartTime AND @EndTime
                                                ),
                                                MergedData AS (
                                                    SELECT 
                                                        Model,
                                                        MIN(StartTime) AS StartTime,
                                                        MAX(EndTime) AS EndTime
                                                    FROM GroupedData
                                                    GROUP BY Model, GroupID
                                                )
                                                SELECT Model, StartTime, EndTime FROM MergedData ORDER BY StartTime;";

                    using (SqlCommand command = new SqlCommand(getStartEndModel, connection))
                    {
                        command.Parameters.AddWithValue("@MachineCode", MachineCode);
                        command.Parameters.AddWithValue("@StartTime", StartTime);
                        command.Parameters.AddWithValue("@EndTime", EndTime);

                        using (SqlDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                StartEndModel StartEnd = new StartEndModel();
                                StartEnd.Model = dataReader.GetString(0);
                                StartEnd.StartTime = dataReader.GetTimeSpan(1);
                                StartEnd.EndTime = dataReader.GetTimeSpan(2);
                                ListStartEnd.Add(StartEnd);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return ListStartEnd;
        }

        // Model Data
        public class PlanQty
        {
            public int Quantity { get; set; }
            public string? MachineCode { get; set; }
        }

        public class ActualQty
        {
            public int Quantity { get; set; }
            public string? MachineCode { get; set; }
        }

        public class SUTModel
        {
            public int SUT { get; set; }
            public string? MachineCode { get; set; }
        }

        public class RestTime
        {
            public int Duration { get; set; }
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
        }

        public class AssemblyTime
        {
            public string? Model { get; set; }
            public string? MachineCode { get; set; }
            public int SUT { get; set; }
            public TimeSpan ProductionTime { get; set; }
        }

        public class TotalDefect
        {
            public string? MachineCode { get; set; }
            public int DefectQty { get; set; }
        }

		public class ManPower
		{
			public int NoOfOperator { get; set; }
			public string? MachineCode { get; set; }
		}

		public class TotalChangesModel
        {
            public int ProductId { get; set; }
            public string? MachineCode { get; set; }
        }

        public class ModelTime
        {
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
            public string? MachineCode { get; set; }
        }

        public class StartEndModel
        {
            public string? Model { get; set; }
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
        }
        public class ProductionAchievement
        {
            public DateTime FirstTime { get; set; }
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
            public string? Time { get; set; }
            public string? Model { get; set; }
            public string? MachineCode { get; set; }
            public int Plan { get; set; }
            public int SUT { get; set; }
            public int Actual { get; set; }
        }
    }
}