namespace IntuneTools.Utilities
{
    public class TimeSaved
    {
        public static int UpdateTotalTimeSaved(int minutes, appFunction function)
        {
            totalTimeSavedInSeconds += minutes;

            switch (function)
            {
                case appFunction.Rename:
                    Variables.numberOfItemsRenamed++;
                    break;
                case appFunction.Delete:
                    Variables.numberOfItemsDeleted++;
                    break;
                case appFunction.Import:
                    Variables.numberOfItemsImported++;
                    break;
                case appFunction.Assignment:
                    Variables.numberOfItemsAssigned++;
                    break;
            }

            return totalTimeSavedInSeconds;
        }

        public static int GetTotalTimeSaved()
        {
            return totalTimeSavedInSeconds;
        }

        public static int GetTotalTimeSavedInMinutes()
        {
            return totalTimeSavedInSeconds / 60;
        }

    }
}
