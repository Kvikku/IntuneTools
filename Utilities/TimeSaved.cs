using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntuneTools.Utilities
{
    public class TimeSaved
    {
        public static int UpdateTotalTimeSaved(int minutes)
        {
            totalTimeSavedInSeconds += minutes;
            return totalTimeSavedInSeconds;
        }

        public static int GetTotalTimeSaved()
        {
            return totalTimeSavedInSeconds;
        }


    }
}
