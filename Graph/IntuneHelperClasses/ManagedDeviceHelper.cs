using Microsoft.Graph;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class ManagedDeviceHelper
    {
        private static readonly string[] SelectFields =
        {
            "id", "deviceName", "operatingSystem", "osVersion", "lastSyncDateTime",
            "userPrincipalName", "complianceState", "manufacturer", "model", "serialNumber"
        };

        /// <summary>
        /// Retrieves every Intune managed device in the tenant. Graph does not support reliable
        /// server-side filtering on lastSyncDateTime, so staleness is determined client-side.
        /// </summary>
        public static async Task<List<ManagedDevice>> GetAllManagedDevicesAsync(GraphServiceClient graphServiceClient)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.ManagedDevices.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Top = 1000;
                    requestConfiguration.QueryParameters.Select = SelectFields;
                });

                if (result == null)
                {
                    throw new InvalidOperationException("The result from the Graph API is null.");
                }

                var devices = new List<ManagedDevice>();
                var pageIterator = PageIterator<ManagedDevice, ManagedDeviceCollectionResponse>.CreatePageIterator(graphServiceClient, result, (device) =>
                {
                    devices.Add(device);
                    return true;
                });
                await pageIterator.IterateAsync();

                return devices;
            }
            catch (Microsoft.Graph.Beta.Models.ODataErrors.ODataError me)
            {
                AppLogger.Warning($"ODataError retrieving managed devices: {me.Message}", appFunction.FindStaleDevices);
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"An unexpected error occurred while retrieving managed devices: {ex.Message}", appFunction.FindStaleDevices);
            }

            return new List<ManagedDevice>();
        }

        /// <summary>
        /// Retrieves all managed devices whose last sync is older than <paramref name="staleDays"/>
        /// (or that have never synced), mapped for display in the Cleanup page's stale-devices grid.
        /// </summary>
        public static async Task<List<Utilities.StaleManagedDeviceInfo>> GetStaleManagedDevicesAsync(GraphServiceClient graphServiceClient, int staleDays)
        {
            var devices = await GetAllManagedDevicesAsync(graphServiceClient);
            var now = DateTimeOffset.UtcNow;

            return devices
                .Where(d => Utilities.UserInterfaceHelper.IsDeviceStale(d.LastSyncDateTime, staleDays, now))
                .Select(d => new Utilities.StaleManagedDeviceInfo
                {
                    DeviceId = d.Id,
                    DeviceName = d.DeviceName,
                    OperatingSystem = d.OperatingSystem,
                    OsVersion = d.OsVersion,
                    UserPrincipalName = d.UserPrincipalName,
                    ComplianceState = d.ComplianceState?.ToString(),
                    Manufacturer = d.Manufacturer,
                    Model = d.Model,
                    SerialNumber = d.SerialNumber,
                    LastSyncDateTime = d.LastSyncDateTime,
                })
                .ToList();
        }

        /// <summary>
        /// Deletes an Intune managed device record. This removes the device from Intune
        /// management only — it does not retire or wipe the physical device, which can
        /// re-enroll on its own.
        /// </summary>
        public static async Task DeleteManagedDeviceAsync(GraphServiceClient graphServiceClient, string deviceId)
        {
            if (graphServiceClient == null)
            {
                throw new ArgumentNullException(nameof(graphServiceClient));
            }

            if (string.IsNullOrEmpty(deviceId))
            {
                throw new InvalidOperationException("Device ID cannot be null or empty.");
            }

            await graphServiceClient.DeviceManagement.ManagedDevices[deviceId].DeleteAsync();
        }
    }
}
