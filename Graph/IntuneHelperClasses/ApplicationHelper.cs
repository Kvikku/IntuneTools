using Microsoft.Graph;
using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static IntuneTools.Utilities.HelperClass;
using static IntuneTools.Utilities.Variables;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class ApplicationHelper
    {
        public static async Task<List<MobileApp>> GetAllMobileApps(GraphServiceClient graphServiceClient)
        {
            try
            {
                LogToImportStatusFile("Retrieving all Mobile Apps.");

                var result = await graphServiceClient.DeviceAppManagement.MobileApps.GetAsync();

                List<MobileApp> mobileApps = new List<MobileApp>();

                if (result?.Value != null)
                {
                    var pageIterator = PageIterator<MobileApp, MobileAppCollectionResponse>.CreatePageIterator(graphServiceClient, result, (app) =>
                    {
                        mobileApps.Add(app);
                        return true;
                    });
                    await pageIterator.IterateAsync();
                    LogToImportStatusFile($"Found {mobileApps.Count} Mobile Apps.");
                }
                else
                {
                    LogToImportStatusFile("No Mobile Apps found or the result was null.");
                }

                return mobileApps;
            }
            catch (Exception)
            {
                LogToImportStatusFile("An error occurred while retrieving all Mobile Apps", LogLevels.Error);
                return new List<MobileApp>();
            }
        }
    }
}
