using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Predictions
{
    public class TrainTestData
    {
        public List<WindowObject> TrainData { get; set; }
        public List<WindowObject> TestData { get; set; }
    }
}
