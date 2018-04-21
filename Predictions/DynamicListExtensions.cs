using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Predictions
{
    public static class DynamicEnumerableExtensions
    {
        public static IEnumerable<dynamic> KeepColumns(this IEnumerable<dynamic> records, IEnumerable<string> columnsToKeep)
        {
            return records.Select(r =>
            {
                var test = new ExpandoObject();
                foreach (var propertyName in columnsToKeep)
                {
                    (test as IDictionary<string, object>)[propertyName] = (r as IDictionary<string, object>)[propertyName];
                }
                return (dynamic)test;
            })
            .ToList();
        }

        public static List<dynamic> GetCategoryInformation(this IEnumerable<dynamic> records, string columnName)
        {
            return records.Select(r => (r as IDictionary<string, object>)[columnName]).Distinct().ToList();
        }

        public static IEnumerable<dynamic> Categorize(this IEnumerable<dynamic> records, string columnName, List<dynamic> categories)
        {
            records.ToList().ForEach(r => { (r as IDictionary<string, object>)[columnName] = categories.IndexOf((r as IDictionary<string, object>)[columnName]); });
            return records;
        }

        // WARNING: this will not work in the current logic because it returns a dictionary and not a ExpandoObject
        public static IEnumerable<dynamic> OneHotEncode(this IEnumerable<dynamic> records, string columnName, List<dynamic> categories)
        {
            // TODO: get directly categories.Count and use Parallel.For(...)
            var categoriesCount = categories.Count;
            //var categoryDictionary = categories.ToDictionary(c => c, c => categories.IndexOf(c));
            ConcurrentDictionary<int, Dictionary<string, object>> oneHotEncodingValues = new ConcurrentDictionary<int, Dictionary<string, object>>();
            Parallel.ForEach(categories, (category, state, index) =>
            {
                var value = new int[categoriesCount];
                value[index] = 1;
                var dictionary = value.Select((item, indx) => new { item, index = columnName + indx }).ToDictionary(key => key.index, v => (object)v.item);
                oneHotEncodingValues.TryAdd((int)index, dictionary);
            });

            // Siple version
            List<dynamic> result = new List<dynamic>();
            foreach (var r in records)
            {
                var row = r as IDictionary<string, object>;
                // this will blow up on duplicate keys
                var hailMarry = new[] { row, oneHotEncodingValues[(int)row[columnName]] }.SelectMany(dict => dict).ToDictionary(key => key.Key, val => val.Value);
                // TODO: convert from Dictionary back to ExpandoObject ... preferably without loops
                result.Add(hailMarry as dynamic);
            }

            //// This works most of the time
            //var result = new ConcurrentBag<dynamic>();
            //Parallel.ForEach(records, (r) =>
            //{
            //    var row = r as IDictionary<string, object>;
            //    // this will blow up on duplicate keys
            //    var hailMarry = new[] { row, oneHotEncodingValues[(int)row[columnName]] }.SelectMany(dict => dict).ToDictionary(key => key.Key, val => val.Value);
            //// TODO: convert from Dictionary back to ExpandoObject ... preferably without loops
            //    result.Add(hailMarry as dynamic);
            //});

            return result;
        }

        public static IEnumerable<dynamic> NormalizeColumn(this IEnumerable<dynamic> records, string columnName)
        {
            var propertyMap = records.Select(r => float.Parse((r as IDictionary<string, object>)[columnName].ToString())).ToList();
            var max = propertyMap.Max();
            var min = propertyMap.Min();
            var range = max - min;
            records.ToList().ForEach((row) => (row as IDictionary<string, object>)[columnName] = ((float.Parse((row as IDictionary<string, object>)[columnName].ToString()) - min) / range) * 2 - 1);
            return records;
        }
    }
}