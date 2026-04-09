using IntuneTools.Utilities;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class AuditLogHelper
    {
        /// <summary>
        /// Retrieves Intune audit events from the last specified number of days.
        /// Uses the deviceManagement/auditEvents endpoint with date filtering and pagination.
        /// </summary>
        public static async Task<List<AuditEvent>> GetAuditEventsAsync(GraphServiceClient graphServiceClient, int days)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, $"Retrieving audit events for the last {days} day(s).");

                var fromDate = DateTime.UtcNow.AddDays(-days).ToString("yyyy-MM-ddTHH:mm:ssZ");

                var result = await graphServiceClient.DeviceManagement.AuditEvents.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Filter = $"activityDateTime ge {fromDate}";
                    requestConfiguration.QueryParameters.Orderby = new[] { "activityDateTime desc" };
                    requestConfiguration.QueryParameters.Top = 500;
                });

                var auditEvents = new List<AuditEvent>();

                var pageIterator = PageIterator<AuditEvent, AuditEventCollectionResponse>
                    .CreatePageIterator(graphServiceClient, result, (auditEvent) =>
                    {
                        auditEvents.Add(auditEvent);
                        return true;
                    });

                await pageIterator.IterateAsync();

                LogToFunctionFile(appFunction.Main, $"Retrieved {auditEvents.Count} audit event(s).");

                return auditEvents;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while retrieving audit events.", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
                throw;
            }
        }
    }
}
