using CommunityToolkit.WinUI.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

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
    }
}
