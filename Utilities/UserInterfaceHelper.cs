using CommunityToolkit.WinUI.UI.Controls;

namespace IntuneTools.Utilities
{
    public static class UserInterfaceHelper
    {
        public static void RebindDataGrid<T>(DataGrid dataGrid, ObservableCollection<T> items)
        {
            dataGrid.ItemsSource = null;
            dataGrid.ItemsSource = items;
        }

        public static async Task<int> PopulateCollectionAsync<TSource, TTarget>(
            ObservableCollection<TTarget> target,
            Func<Task<IEnumerable<TSource>>> loader,
            Func<TSource, TTarget> map)
        {
            var sourceItems = await loader();
            var count = 0;

            foreach (var item in sourceItems)
            {
                target.Add(map(item));
                count++;
            }

            return count;
        }

        public static async Task<int> PopulateCollectionAsync<T>(
            ObservableCollection<T> target,
            Func<Task<IEnumerable<T>>> loader)
        {
            var sourceItems = await loader();
            var count = 0;

            foreach (var item in sourceItems)
            {
                target.Add(item);
                count++;
            }

            return count;
        }

        public static async Task<int> SearchCollectionAsync<TSource, TTarget>(
            ObservableCollection<TTarget> target,
            Func<string, Task<IEnumerable<TSource>>> search,
            string query,
            Func<TSource, TTarget> map)
        {
            var sourceItems = await search(query);
            var count = 0;

            foreach (var item in sourceItems)
            {
                target.Add(map(item));
                count++;
            }

            return count;
        }

        public static bool IsApplicationContentType(string? contentType)
        {
            return !string.IsNullOrWhiteSpace(contentType)
                && (contentType.Equals(ContentTypes.Application, StringComparison.OrdinalIgnoreCase)
                    || contentType.StartsWith("App - ", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Executes a batch operation on a list of IDs with logging and time tracking.
        /// </summary>
        public static async Task<int> ExecuteBatchOperationAsync(
            List<string> ids,
            Func<string, Task> operation,
            string contentTypeName,
            string operationName,
            int timeSavedPerItem,
            appFunction functionType)
        {
            int successCount = 0;
            foreach (var id in ids)
            {
                try
                {
                    await operation(id);
                    AppLogger.Info($"{operationName} {contentTypeName} with ID '{id}'.");
                    UpdateTotalTimeSaved(timeSavedPerItem, functionType);
                    successCount++;
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Error processing {contentTypeName} with ID {id}: {ex.Message}");
                }
            }
            return successCount;
        }

        /// <summary>
        /// Executes a batch operation that retrieves the item name before processing.
        /// </summary>
        public static async Task<int> ExecuteBatchOperationWithNameAsync(
            List<string> ids,
            Func<string, Task<string?>> getItemName,
            Func<string, Task> operation,
            string contentTypeName,
            string operationName,
            int timeSavedPerItem,
            appFunction functionType)
        {
            int successCount = 0;
            foreach (var id in ids)
            {
                try
                {
                    string? itemName = await getItemName(id);
                    await operation(id);
                    string displayName = !string.IsNullOrEmpty(itemName) ? $"'{itemName}'" : $"ID '{id}'";
                    AppLogger.Info($"{operationName} {contentTypeName} {displayName}.");
                    UpdateTotalTimeSaved(timeSavedPerItem, functionType);
                    successCount++;
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Error processing {contentTypeName} with ID {id}: {ex.Message}");
                }
            }
            return successCount;
        }
    }
}
