namespace IntuneTools.Utilities
{
    public class TimeSaved
    {
        public static int UpdateTotalTimeSaved(int seconds, appFunction function)
        {
            totalTimeSavedInSeconds += seconds;

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
                case appFunction.FindUnassigned:
                    Variables.numberOfItemsCheckedForAssignments++;
                    break;
                case appFunction.JsonExport:
                    Variables.numberOfItemsJsonExported++;
                    break;
                case appFunction.AuditLog:
                    Variables.numberOfAuditLogsRetrieved++;
                    break;
                case appFunction.ManageAssignment:
                    Variables.numberOfAssignmentsManaged++;
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
