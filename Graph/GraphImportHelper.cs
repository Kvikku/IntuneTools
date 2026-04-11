using Microsoft.Kiota.Serialization.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntuneTools.Graph
{
    /// <summary>
    /// Centralized helper for Graph API import operations.
    /// Provides reflection-based property copying and JSON deserialization utilities.
    /// </summary>
    public static class GraphImportHelper
    {
        /// <summary>
        /// Properties to skip during reflection-based property copy for import operations.
        /// These are server-generated and cannot be set on creation.
        /// </summary>
        private static readonly HashSet<string> DefaultSkipProperties = new(StringComparer.Ordinal)
        {
            "Id",
            "CreatedDateTime",
            "LastModifiedDateTime"
        };

        /// <summary>
        /// Copies all writable non-null properties from source to target using reflection,
        /// skipping server-generated properties (Id, CreatedDateTime, LastModifiedDateTime)
        /// and any additional properties specified in <paramref name="additionalSkipProperties"/>.
        /// Sets the Id to empty string on the target.
        /// </summary>
        /// <typeparam name="T">The policy type.</typeparam>
        /// <param name="source">The source object to copy from.</param>
        /// <param name="target">The target object to copy to.</param>
        /// <param name="additionalSkipProperties">Additional property names to skip.</param>
        public static void CopyProperties<T>(T source, T target, IEnumerable<string>? additionalSkipProperties = null)
            where T : class
        {
            var skipSet = new HashSet<string>(DefaultSkipProperties, StringComparer.Ordinal);
            if (additionalSkipProperties != null)
            {
                foreach (var prop in additionalSkipProperties)
                    skipSet.Add(prop);
            }

            foreach (var property in source.GetType().GetProperties())
            {
                if (!property.CanWrite || skipSet.Contains(property.Name))
                    continue;

                var value = property.GetValue(source);
                if (value != null)
                    property.SetValue(target, value);
            }

            // Set Id to empty string (Graph API requires it for creation)
            var idProp = typeof(T).GetProperty("Id");
            if (idProp != null && idProp.CanWrite)
                idProp.SetValue(target, "");
        }

        /// <summary>
        /// Creates a new instance of the same runtime type as source and copies properties.
        /// Useful for polymorphic types (e.g., DeviceCompliancePolicy subtypes).
        /// </summary>
        /// <typeparam name="T">The base policy type.</typeparam>
        /// <param name="source">The source object to clone.</param>
        /// <param name="additionalSkipProperties">Additional property names to skip.</param>
        /// <returns>A new instance with copied properties.</returns>
        public static T CloneForImport<T>(T source, IEnumerable<string>? additionalSkipProperties = null)
            where T : class
        {
            var type = source.GetType();
            var target = Activator.CreateInstance(type) as T
                ?? throw new InvalidOperationException($"Could not create instance of {type.Name}");
            CopyProperties(source, target, additionalSkipProperties);
            return target;
        }

        /// <summary>
        /// Deserializes a JsonElement into a typed Graph SDK object using Kiota's JsonParseNode.
        /// </summary>
        /// <typeparam name="T">The target type (must have CreateFromDiscriminatorValue).</typeparam>
        /// <param name="policyData">The JSON data to deserialize.</param>
        /// <param name="factory">The factory method from the Graph SDK model (e.g., DeviceCompliancePolicy.CreateFromDiscriminatorValue).</param>
        /// <returns>The deserialized object, or null if deserialization fails.</returns>
        public static T? DeserializeFromJson<T>(JsonElement policyData, Microsoft.Kiota.Abstractions.Serialization.ParsableFactory<T> factory)
            where T : Microsoft.Kiota.Abstractions.Serialization.IParsable
        {
            var json = policyData.GetRawText();
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            using var document = JsonDocument.Parse(stream);
            var parseNode = new JsonParseNode(document.RootElement);
            return parseNode.GetObjectValue(factory);
        }

        /// <summary>
        /// Runs a batch import loop with per-item error handling and logging.
        /// </summary>
        /// <param name="ids">List of item IDs to import.</param>
        /// <param name="resourceName">Human-readable resource name for logging.</param>
        /// <param name="importSingleAsync">The async function to import a single item.</param>
        public static async Task ImportBatchAsync(
            List<string> ids,
            string resourceName,
            Func<string, Task> importSingleAsync)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, $"Importing {ids.Count} {resourceName}.");

                foreach (var id in ids)
                {
                    try
                    {
                        await importSingleAsync(id);
                    }
                    catch (Exception ex)
                    {
                        GraphErrorHandler.HandleException(ex, $"importing {resourceName} item", id);
                    }
                }
            }
            catch (Exception ex)
            {
                GraphErrorHandler.HandleException(ex, "during import process for", resourceName);
            }
        }
    }
}
