using Microsoft.Graph;

namespace IntuneTools.Graph.EntraHelperClasses
{
    public class EntraDeviceHelper
    {
        private static readonly string[] SelectFields =
        {
            "id", "displayName", "operatingSystem", "operatingSystemVersion",
            "trustType", "accountEnabled", "approximateLastSignInDateTime"
        };

        /// <summary>
        /// Retrieves every Entra ID device object in the tenant. Graph does not support reliable
        /// server-side filtering on approximateLastSignInDateTime, so staleness is determined
        /// client-side.
        /// </summary>
        public static async Task<List<Device>> GetAllEntraDevicesAsync(GraphServiceClient graphServiceClient)
        {
            try
            {
                var result = await graphServiceClient.Devices.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Top = 999;
                    requestConfiguration.QueryParameters.Select = SelectFields;
                });

                if (result == null)
                {
                    throw new InvalidOperationException("The result from the Graph API is null.");
                }

                var devices = new List<Device>();
                var pageIterator = PageIterator<Device, DeviceCollectionResponse>.CreatePageIterator(graphServiceClient, result, (device) =>
                {
                    devices.Add(device);
                    return true;
                });
                await pageIterator.IterateAsync();

                return devices;
            }
            catch (Microsoft.Graph.Beta.Models.ODataErrors.ODataError me)
            {
                AppLogger.Warning($"ODataError retrieving Entra ID devices: {me.Message}", appFunction.FindStaleDevices);
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"An unexpected error occurred while retrieving Entra ID devices: {ex.Message}", appFunction.FindStaleDevices);
            }

            return new List<Device>();
        }

        /// <summary>
        /// Retrieves all Entra ID device objects whose approximate last sign-in is older than
        /// <paramref name="staleDays"/> (or that have never signed in), mapped for display in the
        /// Cleanup page's stale-devices grid.
        /// </summary>
        public static async Task<List<Utilities.StaleEntraDeviceInfo>> GetStaleEntraDevicesAsync(GraphServiceClient graphServiceClient, int staleDays)
        {
            var devices = await GetAllEntraDevicesAsync(graphServiceClient);
            var now = DateTimeOffset.UtcNow;

            return devices
                .Where(d => Utilities.UserInterfaceHelper.IsDeviceStale(d.ApproximateLastSignInDateTime, staleDays, now))
                .Select(d => new Utilities.StaleEntraDeviceInfo
                {
                    DeviceObjectId = d.Id,
                    DisplayName = d.DisplayName,
                    OperatingSystem = d.OperatingSystem,
                    OperatingSystemVersion = d.OperatingSystemVersion,
                    TrustType = d.TrustType?.ToString(),
                    AccountEnabled = d.AccountEnabled,
                    ApproximateLastSignInDateTime = d.ApproximateLastSignInDateTime,
                })
                .ToList();
        }

        /// <summary>
        /// Deletes an Entra ID device object. The object is recoverable from Entra's deleted
        /// items for 30 days. Note that hybrid-joined devices (trustType "ServerAd") are synced
        /// from on-premises AD via AD Connect and will likely reappear unless also removed there.
        /// </summary>
        public static async Task DeleteEntraDeviceAsync(GraphServiceClient graphServiceClient, string deviceObjectId)
        {
            if (graphServiceClient == null)
            {
                throw new ArgumentNullException(nameof(graphServiceClient));
            }

            if (string.IsNullOrEmpty(deviceObjectId))
            {
                throw new InvalidOperationException("Device object ID cannot be null or empty.");
            }

            await graphServiceClient.Devices[deviceObjectId].DeleteAsync();
        }
    }
}
