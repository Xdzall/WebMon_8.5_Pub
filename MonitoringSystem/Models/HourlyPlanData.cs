using System;
using System.ComponentModel.DataAnnotations;

namespace MonitoringSystem.Models
{
    public class HourlyPlanData
    {
        [Key]
        public int Id { get; set; }
        public string MachineCode { get; set; } // CU (MCH1-01) atau CS (MCH1-02)
        public DateTime SelectedDate { get; set; }
        public int TotalPlan { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}