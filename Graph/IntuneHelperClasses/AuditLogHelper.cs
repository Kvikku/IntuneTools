using Microsoft.Graph;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class AuditLogHelper
    {
        /// <summary>
        /// Retrieves Intune audit events from the last specified number of days.
        /// Uses the deviceManagement/auditEvents endpoint with date filtering and pagination.
        /// </summary>
        /// <param name="graphServiceClient">Authenticated Graph client.</param>
        /// <param name="days">Number of days to look back.</param>
        /// <param name="cancellationToken">Token to cancel the long-running retrieval.</param>
        /// <param name="onProgress">Optional callback invoked after each event is received with the running count.</param>
        public static async Task<List<AuditEvent>> GetAuditEventsAsync(
            GraphServiceClient graphServiceClient,
            int days,
            CancellationToken cancellationToken = default,
            Action<int>? onProgress = null)
        {
            try
            {
                AppLogger.Info($"Retrieving audit events for the last {days} day(s).", appFunction.AuditLog);

                var fromDate = DateTime.UtcNow.AddDays(-days).ToString("yyyy-MM-ddTHH:mm:ssZ");

                var result = await graphServiceClient.DeviceManagement.AuditEvents.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Filter = $"activityDateTime ge {fromDate}";
                    requestConfiguration.QueryParameters.Orderby = new[] { "activityDateTime desc" };
                    requestConfiguration.QueryParameters.Top = 500;
                }, cancellationToken: cancellationToken);

                var auditEvents = new List<AuditEvent>();

                var pageIterator = PageIterator<AuditEvent, AuditEventCollectionResponse>
                    .CreatePageIterator(graphServiceClient, result, (auditEvent) =>
                    {
                        auditEvents.Add(auditEvent);
                        onProgress?.Invoke(auditEvents.Count);
                        return true;
                    });

                await pageIterator.IterateAsync(cancellationToken);

                AppLogger.Info($"Retrieved {auditEvents.Count} audit event(s).", appFunction.AuditLog);

                return auditEvents;
            }
            catch (OperationCanceledException)
            {
                AppLogger.Warning("Audit event retrieval was cancelled by the user.", appFunction.AuditLog);
                throw;
            }
            catch (Exception ex)
            {
                AppLogger.Warning("An error occurred while retrieving audit events.", appFunction.AuditLog);
                AppLogger.Error(ex.Message, appFunction.AuditLog);
                throw;
            }
        }
    }
}
