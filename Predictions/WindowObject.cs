using System.Collections.Generic;
using System.Linq;

namespace Predictions
{
    public class WindowObject
    {
        public List<dynamic> Window { get; set; }
        public object Prediction { get; set; }

        public List<float> ToFloats()
        {
            return Window.SelectMany(row => (row as IDictionary<string, object>).Values.Select(v => (float)v)).ToList();
        }
    }
}
