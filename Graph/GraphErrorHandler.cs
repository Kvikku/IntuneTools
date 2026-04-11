using Microsoft.Graph.Beta.Models.ODataErrors;
using System;

namespace IntuneTools.Graph
{
    /// <summary>
    /// Centralized error handling for Graph API operations.
    /// Provides consistent logging and exception classification.
    /// </summary>
    public static class GraphErrorHandler
    {
        /// <summary>
        /// Logs a Graph API exception with consistent formatting and log levels.
        /// ODataError → Warning, unexpected Exception → Error.
        /// </summary>
        /// <param name="ex">The exception to handle.</param>
        /// <param name="operation">A short description of the operation (e.g., "retrieving", "deleting").</param>
        /// <param name="resourceName">The resource type or name (e.g., "settings catalog policies").</param>
        public static void HandleException(Exception ex, string operation, string resourceName)
        {
            if (ex is ODataError odataError)
            {
                LogToFunctionFile(appFunction.Main,
                    $"ODataError {operation} {resourceName}: {odataError.Error?.Message ?? odataError.Message}",
                    LogLevels.Warning);
            }
            else
            {
                LogToFunctionFile(appFunction.Main,
                    $"An error occurred while {operation} {resourceName}: {ex.Message}",
                    LogLevels.Error);
            }
        }
    }
}
