using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using MonitoringSystem.Data;
using MonitoringSystem.Models;
using static MonitoringSystem.Pages.Summary.SummaryModel;

namespace MonitoringSystem.Pages.Performance
{
	public class PerformanceModel : PageModel
	{
		public string connectionString = "Server=10.83.33.103;trusted_connection=false;Database=PROMOSYS;User Id=sa;Password=sa;Persist Security Info=False;Encrypt=False";
		//public string connectionString = "Data Source=DESKTOP-NBPATD6\\MSSQLSERVERR;trusted_connection=true;trustservercertificate=True;Database=PROMOSYS;Integrated Security=True;Encrypt=False";
		public string errorMessage = "";

        private readonly ApplicationDbContext _context;
        private readonly IServiceProvider _serviceProvider;
        public List<PlanQty> plansQty = new List<PlanQty>();
        public PerformanceModel(ApplicationDbContext context, IServiceProvider serviceProvider)
        {
            _context = context;  // Dependency injection untuk ApplicationDbContext
            _serviceProvider = serviceProvider;  // Dependency injection untuk IServiceProvider
        }

        // Properti untuk menampung data HourlyPlanData
        public int TotalPlanForSummaryCU { get; set; }
        public int TotalPlanForSummaryCS { get; set; }

        [BindProperty]
		public DateTime SelectedDate { get; set; } = DateTime.Now.Date;

		[BindProperty]
		public string MachineCode { get; set; } = "MCH1-01";

        public TimeSpan? BreakTime1Start { get; set; }
        public TimeSpan? BreakTime1End { get; set; }
        public TimeSpan? BreakTime2Start { get; set; }
        public TimeSpan? BreakTime2End { get; set; }


        public List<ProductionAchievement> listProdAchieve = new List<ProductionAchievement>();
		public List<AssemblyTime> assemblyTimes = new List<AssemblyTime>();

		public int Plan { get; set; }
		public int Actual { get; set; }

		public void OnGet()
		{
			SelectedDate = DateTime.Today;
			MachineCode = MachineCode;
			GetHourlyAchievement();
			GetAssemblyTime();
        }
        public void GetProductionPlanDaily()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getTotalProduction = @"SELECT SUM(Quantity), ProductionRecords.MachineCode FROM ProductionRecords 
                                                  JOIN ProductionPlan ON ProductionRecords.PlanId = ProductionPlan.Id
                                                  WHERE ProductionPlan.CurrentDate = @SelectionDate
                                                  GROUP BY ProductionRecords.MachineCode;";
                    using (SqlCommand command = new SqlCommand(getTotalProduction, connection))
                    {
                        command.Parameters.AddWithValue("@SelectionDate", SelectedDate);
                        using (SqlDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                PlanQty plan = new PlanQty();
                                plan.Quantity = dataReader.GetInt32(0);
                                plan.MachineCode = dataReader.GetString(1);
                                plansQty.Add(plan);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }
        }
        public void OnPost()
		{
			MachineCode = MachineCode ?? "MCH1-01";
			if (SelectedDate == null || SelectedDate == DateTime.MinValue)
			{
				SelectedDate = DateTime.Today;
			}
            LoadBreakTimesFromDb();
            GetHourlyAchievement();
			GetAssemblyTime();
		}
        public int GetPlanForSummary(DateTime selectedDate, string machineCode)
        {
            return _context.HourlyPlanData
                .Where(x => x.SelectedDate == selectedDate && x.MachineCode == machineCode)
                .Sum(x => x.TotalPlan);  // Mengambil total Plan untuk machineCode tertentu
        }

        public void SavePlanToDatabase(int plan, string machineCode)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var existingRecord = context.HourlyPlanData
                    .FirstOrDefault(x => x.SelectedDate == DateTime.Today && x.MachineCode == machineCode);

                if (existingRecord != null)
                {
                    // Update data jika sudah ada
                    existingRecord.TotalPlan = plan;
                    existingRecord.UpdatedAt = DateTime.Now;
                    context.Update(existingRecord);
                }
                else
                {
                    // Insert data baru jika belum ada
                    context.HourlyPlanData.Add(new HourlyPlanData
                    {
                        MachineCode = machineCode,
                        SelectedDate = DateTime.Today,
                        TotalPlan = plan,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    });
                }

                context.SaveChanges();
            }
        }

        private int CalculatePlanPerHourForSummary(string currentModel, string previousModel, TimeSpan startTime, TimeSpan endTime,
                                  TimeSpan currentTime, DateTime currentDate, int sut,
                                  List<Performance.PerformanceModel.RestTime> listRestTime)
        {
            var firstTimeModel = TimeSpan.Zero;
            var lastTimeModel = TimeSpan.Zero;
            var qtyPlan = 1;
            int planPerHour = 1;

            if (currentModel != null)
            {
                firstTimeModel = GetFirstTimeModel(startTime, endTime, currentModel);
                lastTimeModel = GetLastTimeModel(startTime, endTime, currentModel);
                qtyPlan = GetModelPlan(currentModel);
            }

            if (SelectedDate == currentDate)
            {
                if (currentTime >= startTime && currentTime <= endTime)
                {
                    planPerHour = currentModel == previousModel
                        ? CalculatePlan(startTime, currentTime, sut, listRestTime)
                        : CalculatePlan(firstTimeModel, lastTimeModel, sut, listRestTime);
                }
                else
                {
                    planPerHour = currentModel == previousModel
                        ? CalculatePlan(startTime, endTime, sut, listRestTime)
                        : CalculatePlan(firstTimeModel, lastTimeModel, sut, listRestTime);
                }
            }
            else
            {
                planPerHour = currentModel == previousModel
                    ? CalculatePlan(startTime, endTime, sut, listRestTime)
                    : CalculatePlan(firstTimeModel, lastTimeModel, sut, listRestTime);
            }

            // Ensure plan doesn't exceed quantity plan
            planPerHour = Math.Min(planPerHour, qtyPlan);

            return planPerHour > 0 ? planPerHour : 1;
        }

        private void LoadBreakTimesFromDb()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"SELECT TOP 1 BreakTime1Start, BreakTime1End, BreakTime2Start, BreakTime2End 
                                 FROM AdditionalBreakTime 
                                 WHERE Date = @Date
                                 ORDER BY CreatedAt DESC";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Date", SelectedDate.Date);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                BreakTime1Start = reader.IsDBNull(0) ? (TimeSpan?)null : reader.GetTimeSpan(0);
                                BreakTime1End = reader.IsDBNull(1) ? (TimeSpan?)null : reader.GetTimeSpan(1);
                                BreakTime2Start = reader.IsDBNull(2) ? (TimeSpan?)null : reader.GetTimeSpan(2);
                                BreakTime2End = reader.IsDBNull(3) ? (TimeSpan?)null : reader.GetTimeSpan(3);
                            }
                            else
                            {
                                BreakTime1Start = null;
                                BreakTime1End = null;
                                BreakTime2Start = null;
                                BreakTime2End = null;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading break times: " + ex.Message);
            }
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


        [HttpGet]
        [Route("/OnGetUpdatedData")]
        public IActionResult OnGetUpdatedData()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("OnGetUpdatedData called");

                var actualData = GetActualPerHour();
                var labels = actualData.Select(data => data.EndTime).ToList();
                var efficiencyData = CalculateCumulativeEfficiencyForChart(actualData);

                System.Diagnostics.Debug.WriteLine($"Actual Data Count: {actualData.Count}");
                System.Diagnostics.Debug.WriteLine($"Labels: {string.Join(", ", labels)}");
                System.Diagnostics.Debug.WriteLine($"Efficiency: {string.Join(", ", efficiencyData)}");

                return new JsonResult(new
                {
                    Labels = labels ?? new List<string>(),
                    Efficiency = efficiencyData ?? new List<double>()
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnGetUpdatedData: {ex.Message}");
                return StatusCode(500, new { error = "Failed to fetch data", details = ex.Message });
            }
        }

        public List<double> CalculateCumulativeEfficiencyForChart(List<ActualData> actualData)
        {
            List<double> efficiencyData = new List<double> { 0 }; // Start with 0 at 07:00

            var cumulativeActual = 0;
            var cumulativePlan = 0;

            for (int i = 1; i < actualData.Count; i++)
            {
                cumulativeActual += actualData[i].Actual;

                // Calculate cumulative plan based on hourly achievement data
                var matchingAchievement = listProdAchieve
                    .Where(achievement => achievement.EndTime.ToString(@"hh\:mm") == actualData[i].EndTime)
                    .ToList();

                if (matchingAchievement.Any())
                {
                    foreach (var achievement in matchingAchievement)
                    {
                        var planPerHour = CalculateHourlyPlan(achievement);
                        cumulativePlan += planPerHour;
                    }
                }
                else
                {
                    cumulativePlan += 1; // Default plan if no matching achievement
                }

                // Calculate efficiency
                double efficiency = 0;
                if (cumulativePlan > 0)
                {
                    efficiency = Math.Round((double)cumulativeActual / cumulativePlan * 100, 2);
                }

                efficiencyData.Add(efficiency);
            }

            return efficiencyData;
        }

        private int CalculateHourlyPlan(ProductionAchievement achievement)
        {
            var currentTime = DateTime.Now.TimeOfDay;
            var currentDate = DateTime.Now.Date;
            var listRestTime = GetRestTime(DetermineTypeOfDay(DateTime.Today.DayOfWeek));
            var previousModel = "";

            var firstTimeModel = TimeSpan.Zero;
            var lastTimeModel = TimeSpan.Zero;
            var quantityPlan = 1;

            if (achievement.Model != null)
            {
                firstTimeModel = GetFirstTimeModel(achievement.StartTime, achievement.EndTime, achievement.Model);
                lastTimeModel = GetLastTimeModel(achievement.StartTime, achievement.EndTime, achievement.Model);
                quantityPlan = GetModelPlan(achievement.Model);
            }

            int planPerHour = 1;

            if (SelectedDate == currentDate)
            {
                if (currentTime >= achievement.StartTime && currentTime <= achievement.EndTime)
                {
                    planPerHour = achievement.Model == previousModel
                        ? CalculatePlan(achievement.StartTime, currentTime, achievement.SUT, listRestTime)
                        : CalculatePlan(firstTimeModel, lastTimeModel, achievement.SUT, listRestTime);
                }
                else
                {
                    planPerHour = achievement.Model == previousModel
                        ? CalculatePlan(achievement.StartTime, achievement.EndTime, achievement.SUT, listRestTime)
                        : CalculatePlan(firstTimeModel, achievement.EndTime, achievement.SUT, listRestTime);
                }
            }
            else
            {
                planPerHour = achievement.Model == previousModel
                    ? CalculatePlan(achievement.StartTime, achievement.EndTime, achievement.SUT, listRestTime)
                    : CalculatePlan(firstTimeModel, achievement.EndTime, achievement.SUT, listRestTime);
            }

            planPerHour = Math.Min(planPerHour, quantityPlan);
            return planPerHour > 0 ? planPerHour : 1;
        }

        public List<int> CalculateCumulativePlan()
		{
			List<int> cumulativePlan = new List<int> { 0 };

			foreach (var achievement in listProdAchieve)
			{
				int plan = CalculateSinglePlan(
					achievement.StartTime,
					achievement.EndTime,
					achievement.SUT,
					GetRestTime("REGULAR")
				);
				cumulativePlan.Add(plan + cumulativePlan.Last());
			}

			return cumulativePlan;
		}

		public int CalculateSinglePlan(TimeSpan startTime, TimeSpan endTime, int SUT, List<RestTime> restTimes)
		{
			TimeSpan effectiveTime = endTime - startTime;

			foreach (var rest in restTimes)
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

        public int GetProductionPlan()
        {
            int totalPlan = 0;

            // Calculate total plan based on hourly achievement data
            foreach (var achievement in listProdAchieve)
            {
                var planPerHour = CalculateHourlyPlan(achievement);
                totalPlan += planPerHour;
            }

            return totalPlan;
        }

        public int GetActualProduction()
		{
			int actual = 0;
			try
			{
				using (SqlConnection connection = new SqlConnection(connectionString))
				{
					connection.Open();
					string getActualProduction = @"SELECT Count(*) FROM OEESN WHERE CAST(SDate AS DATE) = @SelectedDate
                                                   AND MachineCode = @MachineCode;";
					using (SqlCommand command = new SqlCommand(getActualProduction, connection))
					{
						command.Parameters.AddWithValue("@SelectedDate", SelectedDate);
						command.Parameters.AddWithValue("@MachineCode", MachineCode);
						var Result = command.ExecuteScalar();
						if (Result != null) 
						{
							actual = (int)Result;
						}
						else
						{
							actual = 0;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("Exception: " + ex.ToString());
			}
			return actual;
		}

		public int GetPlanTaktTime()
		{
			int planTaktTime = 0;
			try
			{
				using (SqlConnection connection = new SqlConnection(connectionString))
				{
					connection.Open();
					string getPlanTaktTime = @"SELECT TOP 1 MasterData.SUT FROM OEESN
	                                                  JOIN MasterData ON OEESN.Product_Id = MasterData.Product_Id
                                               WHERE CAST(OEESN.SDate AS DATE) = @SelectedDate AND OEESN.MachineCode = @MachineCode
                                               ORDER BY SDate DESC;";
					using (SqlCommand command = new SqlCommand(getPlanTaktTime, connection))
					{
						command.Parameters.AddWithValue("@SelectedDate", SelectedDate);
						command.Parameters.AddWithValue("@MachineCode", MachineCode);
						var Result = command.ExecuteScalar();
						if (Result != null)
						{
							planTaktTime = (int)Result;
						}
						else
						{
							planTaktTime = 0;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("Exception: " + ex.ToString());
			}
			return planTaktTime;
		}

        public void GetHourlyAchievement()
        {
            listProdAchieve.Clear();

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                    SELECT MIN(OEESN.SDate) As FirstTime,
                           CAST(DATEADD(HOUR, DATEDIFF(HOUR, 0, OEESN.SDate), 0) AS TIME) AS StartTime,
                           CAST(DATEADD(HOUR, DATEDIFF(HOUR, 0, OEESN.SDate) + 1, 0) AS TIME) As EndTime,
                           Masterdata.ProductName As Model, 
                           MasterData.QtyHour As Target,
                           MasterData.SUT AS SUT,
                           COUNT(*) AS Actual
                    FROM OEESN
                    JOIN Masterdata ON OEESN.Product_Id = MasterData.Product_Id
                    WHERE CAST(SDate As DATE) = @Date AND OEESN.MachineCode = @MachineCode
                    GROUP BY DATEDIFF(HOUR, 0, SDate), Masterdata.ProductName, MasterData.QtyHour, MasterData.SUT
                    ORDER BY MIN(OEESN.SDate);";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Date", SelectedDate);
                        command.Parameters.AddWithValue("@MachineCode", MachineCode);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var startTime = reader.GetTimeSpan(1);
                                var endTime = reader.GetTimeSpan(2);

                                // cek overlap dengan break time
                                if (IsOverlappingWithBreakTime(startTime, endTime))
                                {
                                    // skip data ini karena masuk break time
                                    continue;
                                }

                                listProdAchieve.Add(new ProductionAchievement
                                {
                                    FirstTime = reader.GetDateTime(0),
                                    StartTime = startTime,
                                    EndTime = endTime,
                                    Time = $"{startTime:hh\\:mm} - {endTime:hh\\:mm}",
                                    Model = reader.GetString(3),
                                    Plan = reader.GetInt32(4),
                                    SUT = reader.GetInt32(5),
                                    Actual = reader.GetInt32(6)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in GetHourlyAchievement: " + ex.Message);
            }
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
                                            AND CAST(OEESN.SDate AS Time) <= @EndTime AND CAST(OEESN.SDate AS DATE) = @CurrentDate";

					using (SqlCommand command = new SqlCommand(getFirstTime, connection))
					{
						command.Parameters.AddWithValue("@Model", model);
						command.Parameters.AddWithValue("@StartTime", StartTime);
						command.Parameters.AddWithValue("@EndTime", EndTime);
						command.Parameters.AddWithValue("@CurrentDate", SelectedDate);
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
                                            AND CAST(OEESN.SDate AS Time) <= @EndTime AND CAST(OEESN.SDate AS DATE) = @CurrentDate";

					using (SqlCommand command = new SqlCommand(getLastTime, connection))
					{
						command.Parameters.AddWithValue("@Model", model);
						command.Parameters.AddWithValue("@StartTime", StartTime);
						command.Parameters.AddWithValue("@EndTime", EndTime);
						command.Parameters.AddWithValue("@CurrentDate", SelectedDate);
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

		public int CalculateTotalPlanPerSUT(
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

					if (SelectedDate == CurrentDate)
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
                                                WHERE ProductionRecords.ProductName = @Model AND ProductionPlan.CurrentDate = @CurrentDate";
					using (SqlCommand command = new SqlCommand(getQuantityModel, connection))
					{
						command.Parameters.AddWithValue("@Model", model);
						command.Parameters.AddWithValue("@CurrentDate", SelectedDate);
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

		public int GetTotalDefect()
		{
			int TotalDefect = 0;
			try
			{
				using (SqlConnection connection = new SqlConnection(connectionString))
				{
					connection.Open();
					string getTotalDefect = @"SELECT COUNT(*) AS TotalDefect FROM NG_RPTS WHERE CAST(SDate AS DATE) = @SelectedDate AND MachineCode = @MachineCode";
					using (SqlCommand command = new SqlCommand(getTotalDefect, connection))
					{
						command.Parameters.AddWithValue("@SelectedDate", SelectedDate);
						command.Parameters.AddWithValue("@MachineCode", MachineCode);
						var Result = command.ExecuteScalar();
						if (Result != null)
						{
							TotalDefect = (int)Result;
						}
						else
						{
							TotalDefect = 0;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("Exception: " + ex.ToString());
			}
			return TotalDefect;
		}

		public int GetCurrentSUT()
		{
			int SUT = 0;
			try
			{
				using (SqlConnection connection = new SqlConnection(connectionString))
				{
					connection.Open();
					string getTotalDefect = @"SELECT TOP 1 MasterData.SUT FROM OEESN 
										      JOIN MasterData ON OEESN.Product_Id = MasterData.Product_Id 
											  WHERE CAST(OEESN.SDate AS DATE) = @SelectedDate AND OEESN.MachineCode = @MachineCode
											  ORDER BY SDate DESC;";
					using (SqlCommand command = new SqlCommand(getTotalDefect, connection))
					{
						command.Parameters.AddWithValue("@SelectedDate", SelectedDate);
						command.Parameters.AddWithValue("@MachineCode", MachineCode);
						var Result = command.ExecuteScalar();
						if (Result != null)
						{
							SUT = (int)Result;
						}
						else
						{
							SUT = 0;
						};
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("Exception: " + ex.ToString());
			}
			return SUT;
		}

		public List<RestTime> GetRestTime(string dayTipe)
		{
			List<RestTime> listRestTime = new List<RestTime>();
			try
			{
				using (SqlConnection connection = new SqlConnection(connectionString))
				{
					connection.Open();
					string getRestTime = @"SELECT Duration, StartTime, EndTime FROM RestTime WHERE DayType = @DayTipe";
					using (SqlCommand command = new SqlCommand(getRestTime, connection))
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

		public void GetAssemblyTime()
		{
			try
			{
				using (SqlConnection connection = new SqlConnection(connectionString))
				{
					connection.Open();
					string getAssemblyProductionTime = @"SELECT MasterData.ProductName As Model, OEESN.MachineCode As MachineCode, 
                                                                MasterData.SUT As SUT, CAST(OEESN.SDate AS Time) As ProductionTime
                                                        FROM OEESN JOIN Masterdata ON OEESN.Product_Id = MasterData.Product_Id
                                                        WHERE CAST(OEESN.SDate AS DATE) = @Date AND OEESN.MachineCode = @MachineCode
                                                        ORDER BY CAST(OEESN.SDate AS TIME) ASC";
					using (SqlCommand command = new SqlCommand(getAssemblyProductionTime, connection))
					{
						command.Parameters.AddWithValue("@Date", SelectedDate);
						command.Parameters.AddWithValue("@MachineCode", MachineCode);
						using (SqlDataReader reader = command.ExecuteReader())
						{
							while (reader.Read())
							{
								AssemblyTime assemblyTime = new AssemblyTime();
								assemblyTime.Model = reader.GetString(0);
								assemblyTime.MachineCode = reader.GetString(1);
								assemblyTime.SUT = reader.GetInt32(2);
								assemblyTime.ProductionTime = reader.GetTimeSpan(3);
								assemblyTimes.Add(assemblyTime);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}
		}

		public int CalculateTotalLossTime(List<AssemblyTime> assemblyTimes, List<RestTime> restTimes)
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
					Console.WriteLine(lossTime.ToString());
				}

				totalLossTime += lossTime;
			}
			return totalLossTime;
		}

		public int GetManPower()
		{
			int NoOfOperator = 0;
			try
			{
				using (SqlConnection connection = new SqlConnection(connectionString))
				{
					connection.Open();
					string getManPower = @"SELECT DISTINCT TOP 1 NoOfOperator FROM OEESN WHERE CAST(SDate AS DATE) = @SelectedDate AND MachineCode = @MachineCode;";
					using (SqlCommand command = new SqlCommand(getManPower, connection))
					{
						command.Parameters.AddWithValue("@SelectedDate", SelectedDate);
						command.Parameters.AddWithValue("@MachineCode", MachineCode);
						var Result = command.ExecuteScalar();
						if (Result != null)
						{
							NoOfOperator = (int)Result;
						}
						else
						{
							NoOfOperator = 0;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
			return NoOfOperator;
		}

		public TimeSpan GetLastWorkingTime(string MachineCode)
		{
			TimeSpan lastWorkingTime = TimeSpan.Zero;
			try
			{
				using (SqlConnection connection = new SqlConnection(connectionString))
				{
					connection.Open();
					string getLastWorkingTime = @"SELECT MAX(CAST(SDate AS Time)) FROM OEESN 
                                                  WHERE CAST (SDate AS Date) = @SelectedDate AND MachineCode = @MachineCode";
					using (SqlCommand command = new SqlCommand(getLastWorkingTime, connection))
					{
						command.Parameters.AddWithValue("@SelectedDate", SelectedDate);
						command.Parameters.AddWithValue("@MachineCode", MachineCode);
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

	    public List<ActualData> GetActualPerHour()
        {
            List<ActualData> actualData = new List<ActualData>();
            actualData.Add(new ActualData
            {
                StartTime = "07:00",
                EndTime = "07:00",
                Actual = 0
            });

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                    SELECT StartTime, EndTime, SUM(Actual) AS Actual
                    FROM (
                        SELECT 
                            CAST(DATEADD(HOUR, DATEDIFF(HOUR, 0, OEESN.SDate), 0) AS TIME) AS StartTime,
                            CAST(DATEADD(HOUR, DATEDIFF(HOUR, 0, OEESN.SDate) + 1, 0) AS TIME) As EndTime,
                            COUNT(*) AS Actual
                        FROM OEESN
                        WHERE CAST(SDate As DATE) = @Date AND OEESN.MachineCode = @MachineCode
                        GROUP BY DATEDIFF(HOUR, 0, SDate)
                    ) AS GroupedData
                    GROUP BY StartTime, EndTime
                    ORDER BY StartTime;";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Date", SelectedDate);
                        command.Parameters.AddWithValue("@MachineCode", MachineCode);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var startTime = reader.GetTimeSpan(0);
                                var endTime = reader.GetTimeSpan(1);

                                // skip jika overlap dengan break time
                                if (IsOverlappingWithBreakTime(startTime, endTime))
                                {
                                    continue;
                                }

                                ActualData data = new ActualData();
                                data.StartTime = $"{startTime:hh\\:mm}";
                                data.EndTime = $"{endTime:hh\\:mm}";
                                data.Actual = reader.GetInt32(2);

                                actualData.Add(data);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in GetActualPerHour: " + ex.Message);
            }

            return actualData;
        }


        public class ProductionAchievement
		{
			public DateTime FirstTime { get; set; }
			public TimeSpan StartTime { get; set; }
			public TimeSpan EndTime { get; set; }
			public string? Time { get; set; }
			public string? Model { get; set; }
			public int Plan { get; set; }
			public int SUT { get; set; }
			public int Actual { get; set; }
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

		public class ActualData
		{
			public string StartTime { get; set; }
			public string EndTime { get; set; }
			public int Actual { get; set; }
		}
	}
}
public class PlanQty
{
    public int Quantity { get; set; }
    public string? MachineCode { get; set; }
}
