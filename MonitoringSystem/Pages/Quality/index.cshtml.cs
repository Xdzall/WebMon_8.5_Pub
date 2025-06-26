using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace MonitoringSystem.Pages.Quality
{
    public class QualityModel : PageModel
    {
		public string connectionString = "Server=10.83.33.103;trusted_connection=false;Database=PROMOSYS;User Id=sa;Password=sa;Persist Security Info=False;Encrypt=False";
        //public string connectionString = "Data Source=DESKTOP-NBPATD6\\MSSQLSERVERR;trusted_connection=true;trustservercertificate=True;Database=PROMOSYS;Integrated Security=True;Encrypt=False";
        public string errorMessage = "";

		public void OnGet()
        {
        }

        public int GetProductionPlan()
        {
            int plan = 0;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getTotalProduction = @"SELECT SUM(Quantity) FROM ProductionRecords 
	                                              JOIN ProductionPlan ON ProductionRecords.PlanId = ProductionPlan.Id
                                                  WHERE ProductionPlan.CurrentDate = @SelectedDate AND ProductionRecords.MachineCode = @MachineCode;";
                    using (SqlCommand command = new SqlCommand(getTotalProduction, connection))
                    {
                        command.Parameters.AddWithValue("@SelectedDate", DateTime.Now.Date);
                        command.Parameters.AddWithValue("@MachineCode", "MCH1-01");
                        var Result = command.ExecuteScalar();
                        if ( Result != null ) { plan = (int)Result; }
                        else { plan = 0; }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }
            return plan;
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
						command.Parameters.AddWithValue("@SelectedDate", DateTime.Now.Date);
						command.Parameters.AddWithValue("@MachineCode", "MCH1-01");
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

		public void GetDailyDefect ()
		{
			try
			{
				using (SqlConnection connection = new SqlConnection (connectionString))
				{
					connection.Open();
					string GetDailyDefect = @"SELECT Cause, COUNT(*) FROM NG_RPTS WHERE CAST(SDate AS DATE) = @Date GROUP BY Cause";
					using (SqlCommand command = new SqlCommand(GetDailyDefect, connection))
					{
						command.Parameters.AddWithValue("@Date", DateTime.Now.Date);
					}
				}
			} 
			catch (Exception ex)
			{
				Console.WriteLine("Exception: " + ex.ToString());
			}
		}

		public class DailyDefect
		{
			public string Cause { get; set; }
			public int Quantity { get; set; }
		}
	}
}
