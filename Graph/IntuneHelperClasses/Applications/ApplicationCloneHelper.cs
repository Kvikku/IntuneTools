using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Graph.Beta.Models;

namespace IntuneTools.Graph.IntuneHelperClasses.Applications
{
    /// <summary>
    /// Reflection-based clone shared by every handler — it strips the
    /// server-managed properties Graph rejects on POST and copies every
    /// other writable property from the source instance to a fresh
    /// instance of the same concrete derived type. Mirrors what the legacy
    /// <c>ApplicationHelper.ImportMultipleApplications</c> does for
    /// metadata-only types so cloneable apps and binary apps share one
    /// well-tested code path.
    /// </summary>
    internal static class ApplicationCloneHelper
    {
        /// <summary>
        /// Properties Graph rejects on POST because they are server-managed,
        /// navigation-only, or require a separate workflow (assignments are
        /// added afterwards, content versions need a binary upload, etc.).
        /// Keep this aligned with <see cref="MobileApp"/> and its derived types.
        /// </summary>
        internal static readonly HashSet<string> StripOnImport = new(StringComparer.Ordinal)
        {
            "Id",
            "CreatedDateTime",
            "LastModifiedDateTime",
            "PublishingState",
            "UploadState",
            "IsAssigned",
            "DependentAppCount",
            "SupersedingAppCount",
            "SupersededAppCount",
            "CommittedContentVersion",
            "Size",
            "Assignments",
            "Categories",
            "Relationships",
            "ContentVersions",
        };

        /// <summary>
        /// Returns a writable clone of <paramref name="source"/> with the
        /// server-managed properties dropped. Preserves <c>OdataType</c> so
        /// Graph routes the POST to the correct derived collection.
        /// </summary>
        public static MobileApp Clone(MobileApp source)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));

            var sourceType = source.GetType();
            var clone = (MobileApp?)Activator.CreateInstance(sourceType)
                ?? throw new InvalidOperationException($"Failed to create an instance of '{sourceType.FullName}' for cloning.");

            foreach (var property in sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanWrite || StripOnImport.Contains(property.Name))
                {
                    continue;
                }

                var value = property.GetValue(source);
                if (value != null)
                {
                    property.SetValue(clone, value);
                }
            }

            if (string.IsNullOrEmpty(clone.OdataType))
            {
                clone.OdataType = source.OdataType;
            }

            return clone;
        }
    }
}
