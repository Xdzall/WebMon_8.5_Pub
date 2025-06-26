using System;
using System.ComponentModel.DataAnnotations;

namespace MonitoringSystem.Models
{
    public class AdditionalBreakTime
    {
        [Key]
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan? BreakTime1Start { get; set; }
        public TimeSpan? BreakTime1End { get; set; }
        public TimeSpan? BreakTime2Start { get; set; }
        public TimeSpan? BreakTime2End { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}