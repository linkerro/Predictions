using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System;

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

        public static IEnumerable<dynamic> OneHotEncode(this IEnumerable<dynamic> records, string columnName, List<dynamic> categories)
        {
            var categoryDictionary = categories.ToDictionary(c => c, c => categories.IndexOf(c));
            IDictionary<string, object> row;
            int i = 0;
            foreach (var r in records)
            {
                row = r as IDictionary<string, object>;
                for (i = 0; i < categories.Count; i++)
                {
                    row[columnName + i] = System.Convert.ToInt32(i == categoryDictionary[row[columnName]]);
                }
                row.Remove(columnName);
            }
            return records;
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
