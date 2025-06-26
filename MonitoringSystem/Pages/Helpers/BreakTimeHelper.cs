using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;

namespace MonitoringSystem.Helpers
{
    public static class BreakTimeHelper
    {
        private static readonly List<(TimeSpan Start, TimeSpan End)> FixedBreaks = new()
        {
            (new TimeSpan(7, 0, 0), new TimeSpan(7, 5, 0)),
            (new TimeSpan(9, 30, 0), new TimeSpan(9, 35, 0)),
            (new TimeSpan(15, 30, 0), new TimeSpan(15, 35, 0)),
            (new TimeSpan(18, 15, 0), new TimeSpan(18, 45, 0))
        };

        public static List<(TimeSpan Start, TimeSpan End)> GetBreakTimes(HttpContext context)
        {
            var breaks = new List<(TimeSpan Start, TimeSpan End)>();

            breaks.AddRange(FixedBreaks);

            TryAdd(context, "AdditionalBreakTime1Start", "AdditionalBreakTime1End", breaks);
            TryAdd(context, "AdditionalBreakTime2Start", "AdditionalBreakTime2End", breaks);

            return breaks;
        }

        private static void TryAdd(HttpContext context, string startKey, string endKey, List<(TimeSpan, TimeSpan)> list)
        {
            string start = context.Request.Cookies[startKey];
            string end = context.Request.Cookies[endKey];

            if (TimeSpan.TryParse(start, out TimeSpan s) && TimeSpan.TryParse(end, out TimeSpan e))
                list.Add((s, e));
        }   
    }
}
