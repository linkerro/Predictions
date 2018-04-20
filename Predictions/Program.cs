using CNTK;
using CsvHelper;
using CsvHelper.Configuration;
using SiaNet;
using SiaNet.Common;
using SiaNet.Model;
using SiaNet.Model.Layers;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Predictions
{
    class Program
    {
        const int WindowSize = 5;
        private const string OmegaCSV = @".\Resources\omega.csv";
        private const string LogTo = @".\";
        static string buffer = "Start at: " + DateTime.Now.ToString() + Environment.NewLine;

        static void Main(string[] args)
        {
            var devices = DeviceDescriptor.AllDevices().Where(x => (x.Type == DeviceKind.GPU)).ToList();
            if (devices.Count() == 0)
                throw new Exception("No GPU Device found. Please run the CPU examples instead!");

            //Setting global device
            GlobalParameters.Device = devices[0];

            //MergeFiles();
            //MergeFiles2();

            var trainTestData = CreateDataSets();
            CreateAndTrainModel(trainTestData);
        }

        private static void CreateAndTrainModel(TrainTestData trainTestData)
        {
            var trainFrame = new XYFrame();
            trainTestData.TrainData.ForEach(row => trainFrame.Add(row.ToFloats(), (float)row.Prediction));
            var testFrame = new XYFrame();
            trainTestData.TestData.ForEach(row => testFrame.Add(row.ToFloats(), (float)row.Prediction));

            var model = new Sequential();
            model.Add(new LSTM(dim: WindowSize, shape: Shape.Create(trainFrame.XFrame.Shape[1]), returnSequence: true));
            model.Add(new LSTM(dim: WindowSize, shape: Shape.Create(trainFrame.XFrame.Shape[1])));
            model.Add(new Dense(dim: 1));

            model.OnEpochEnd += Model_OnEpochEnd;
            model.OnTrainingEnd += Model_OnTrainingEnd;
            model.OnBatchEnd += Model_OnBatchEnd;

            model.Compile(OptOptimizers.Adam, OptLosses.MeanSquaredError, OptMetrics.MSE);
            model.Train(trainFrame, 10, 256, testFrame);

            File.WriteAllText(LogTo + DateTime.Now.ToString("yyyy_MM_dd_hh_mm") + ".txt", buffer);

            Console.ReadLine();
        }

        private static void Model_OnBatchEnd(int epoch, int batchNumber, uint samplesSeen, double loss, Dictionary<string, double> metrics)
        {
            string epochInfo = $"Epoch: {epoch}, Batch: {batchNumber}, Loss: {loss}, MSE: {metrics.First().Value}";
            buffer += epochInfo + Environment.NewLine;
            Console.WriteLine(epochInfo);
        }

        private static void Model_OnTrainingEnd(Dictionary<string, List<double>> trainingResult)
        {
            var mean = trainingResult[OptMetrics.MSE].Mean();
            var std = trainingResult[OptMetrics.MSE].Std();
            string trainingInfo = $"Training completed. Mean: {mean}, Std: {std}";
            buffer += trainingInfo + Environment.NewLine;
            Console.WriteLine(trainingInfo);
            if (trainingResult.ContainsKey("val_mse"))
            {
                var test_mean = trainingResult["val_mse"].Mean();
                var test_std = trainingResult["val_mse"].Std();
                string testInfo = $"Test info Mean: {test_mean}, Std: {test_std}";
                buffer += testInfo + Environment.NewLine;
                Console.WriteLine(testInfo);
            }
        }

        private static void Model_OnEpochEnd(int epoch, uint samplesSeen, double loss, Dictionary<string, double> metrics)
        {
            string epochInfo = $"Epoch: {epoch}, Loss: {loss}, MSE: {metrics.First().Value}";
            buffer += epochInfo + Environment.NewLine;
            Console.WriteLine(epochInfo);
        }

        private static TrainTestData CreateDataSets()
        {
            var reader = new CsvReader(File.OpenText(OmegaCSV), new Configuration { Delimiter = ",", HasHeaderRecord = true });
            var records = reader.GetRecords<dynamic>().ToList();
            var mnemonicCount = records.GroupBy(r => r.Mnemonic).Select(g => new { g.Key, Count = g.Count() }).OrderByDescending(r => r.Count).ToList();
            var firstInfo = records
                .Where(r => r.Mnemonic == "BMW")
                .KeepColumns(new string[] { "MaxPrice", "Date", "Time" })
                .OrderBy(r => DateTime.Parse(r.Date + " " + r.Time))
                .ToList();
            firstInfo.ForEach(r =>
            {
                r.Date = DateTime.Parse(r.Date as string).Day;
                r.Time = (int)TimeSpan.Parse(r.Time as string).TotalMinutes;
            });
            firstInfo = firstInfo.NormalizeColumn("MaxPrice")
                .NormalizeColumn("Date")
                .NormalizeColumn("Time")
                .ToList();
            var windows = GetWindows(firstInfo);
            var trainTestData = windows
                .Scramble()
                .SplitWindows();
            return trainTestData;
        }

        private static TrainTestData CreateDataSets2()
        {
            var reader = new CsvReader(File.OpenText(OmegaCSV), new Configuration { Delimiter = ",", HasHeaderRecord = true });
            var records = reader.GetRecords<dynamic>().ToList();
            var firstInfo = records
                .Where(r => r.Mnemonic == "BMW")
                .KeepColumns(new string[] { "MaxPrice", "Date", "Time" })
                .OrderBy(r => DateTime.Parse(r.Date + " " + r.Time))
                .ToList();
            firstInfo.ForEach(r =>
            {
                r.Date = DateTime.Parse(r.Date as string).Day;
                r.Time = TimeSpan.Parse(r.Time as string).Hours;
            });
            firstInfo = firstInfo.GroupBy(r => new { r.Date, r.Time })
                .Select(g =>
                {
                    dynamic row = new ExpandoObject();
                    row.Date = g.Key.Date;
                    row.Time = g.Key.Time;
                    row.MaxPrice = g.Max(r => r.MaxPrice);
                    return row;
                })
                .ToList();
            firstInfo = firstInfo.NormalizeColumn("MaxPrice")
                .NormalizeColumn("Date")
                .NormalizeColumn("Time")
                .ToList();
            var windows = GetWindows(firstInfo);
            var trainTestData = windows
                .Scramble()
                .SplitWindows();
            return trainTestData;
        }

        private static TrainTestData CreateDataSets3()
        {
            var reader = new CsvReader(File.OpenText(OmegaCSV), new Configuration { Delimiter = ",", HasHeaderRecord = true });
            var records = reader.GetRecords<dynamic>().ToList();
            var firstInfo = records
                .KeepColumns(new string[] { "Mnemonic", "MaxPrice", "Date", "Time" })
                .OrderBy(r => DateTime.Parse(r.Date + " " + r.Time))
                .ToList();
            firstInfo.ForEach(r =>
            {
                r.Date = DateTime.Parse(r.Date as string).Day;
                r.Time = TimeSpan.Parse(r.Time as string).Hours;
            });
            firstInfo = firstInfo.GroupBy(r => new { r.Mnemonic, r.Date, r.Time }).Select(g =>
                {
                    dynamic row = new ExpandoObject();
                    row.Date = g.Key.Date;
                    row.Time = g.Key.Time;
                    row.Mnemonic = g.Key.Mnemonic;
                    row.MaxPrice = g.Max(r => r.MaxPrice);
                    return row;
                })
                .ToList();
            var categories = firstInfo.GetCategoryInformation("Mnemonic");
            firstInfo = firstInfo.NormalizeColumn("MaxPrice")
                .NormalizeColumn("Date")
                .NormalizeColumn("Time")
                .Categorize("Mnemonic", categories)
                .NormalizeColumn("Mnemonic")
                .ToList();
            var windows = GetWindows(firstInfo.GroupBy(r => r.Mnemonic));
            var trainTestData = windows
                .Scramble()
                .SplitWindows();
            return trainTestData;
        }

        private static TrainTestData CreateDataSets4()
        {
            var reader = new CsvReader(File.OpenText(OmegaCSV), new Configuration { Delimiter = ",", HasHeaderRecord = true });
            var records = reader.GetRecords<dynamic>().ToList();
            var firstInfo = records
                .KeepColumns(new string[] { "Mnemonic", "MaxPrice", "Date", "Time" })
                .OrderBy(r => DateTime.Parse(r.Date + " " + r.Time))
                .ToList();
            firstInfo.ForEach(r =>
            {
                r.Date = DateTime.Parse(r.Date as string).Day;
                r.Time = TimeSpan.Parse(r.Time as string).Hours;
            });
            firstInfo = firstInfo.GroupBy(r => new { r.Mnemonic, r.Date, r.Time }).Select(g =>
                {
                    dynamic row = new ExpandoObject();
                    row.Date = g.Key.Date;
                    row.Time = g.Key.Time;
                    row.Mnemonic = g.Key.Mnemonic;
                    row.MaxPrice = g.Max(r => r.MaxPrice);
                    return row;
                })
                .ToList();
            var categories = firstInfo.GetCategoryInformation("Mnemonic");
            firstInfo = firstInfo.NormalizeColumn("MaxPrice")
                .NormalizeColumn("Date")
                .NormalizeColumn("Time")
                .Categorize("Mnemonic", categories)
                .NormalizeColumn("Mnemonic")
                .ToList();
            var windows = GetWindows(firstInfo.GroupBy(r => r.Mnemonic));
            var trainTestData = windows
                .Scramble()
                .SplitWindows();
            return trainTestData;
        }

        private static TrainTestData CreateDataSets5()
        {
            var reader = new CsvReader(File.OpenText(OmegaCSV), new Configuration { Delimiter = ",", HasHeaderRecord = true });
            var records = reader.GetRecords<dynamic>().ToList();
            var firstInfo = records
                .KeepColumns(new string[] { "Mnemonic", "SecurityType", "MaxPrice", "MinPrice", "StartPrice", "EndPrice", "Date", "Time" })
                .OrderBy(r => DateTime.Parse(r.Date + " " + r.Time))
                .ToList();
            firstInfo.ForEach(r =>
            {
                r.Date = DateTime.Parse(r.Date as string).Day;
                r.Time = TimeSpan.Parse(r.Time as string).Hours;
            });
            firstInfo = firstInfo.GroupBy(r => new { r.Mnemonic, r.Date, r.Time, r.SecurityType })
                .Select(g =>
                {
                    dynamic row = new ExpandoObject();
                    row.Date = g.Key.Date;
                    row.Time = g.Key.Time;
                    row.Mnemonic = g.Key.Mnemonic;
                    row.MaxPrice = g.Max(r => r.MaxPrice);
                    row.SecurityType = g.Key.SecurityType;
                    row.MinPrice = g.Min(r => r.MinPrice);
                    row.StartPrice = g.First().StartPrice;
                    row.EndPrice = g.Last().EndPrice;
                    return row;
                })
                .ToList();
            var mnemonicCategories = firstInfo.GetCategoryInformation("Mnemonic");
            var securityTypeCategories = firstInfo.GetCategoryInformation("SecurityType");
            firstInfo = firstInfo.NormalizeColumn("MaxPrice")
                .NormalizeColumn("MinPrice")
                .NormalizeColumn("StartPrice")
                .NormalizeColumn("EndPrice")
                .NormalizeColumn("Date")
                .NormalizeColumn("Time")
                .Categorize("Mnemonic", mnemonicCategories)
                .NormalizeColumn("Mnemonic")
                .Categorize("SecurityType", securityTypeCategories)
                .NormalizeColumn("SecurityType")
                .ToList();
            var windows = GetWindows(firstInfo.GroupBy(r => r.Mnemonic));
            var trainTestData = windows
                .Scramble()
                .SplitWindows();
            return trainTestData;
        }

        public static List<WindowObject> GetWindows(List<dynamic> records, int windowSize = 5)
        {
            var count = records.Count - windowSize - 2;
            if (count < 0)
            {
                //Console.WriteLine($"Not enough records ({records.Count}) to make a proper list of windows ({windowSize})");
                return new List<WindowObject>();
            }
            var windows = Enumerable.Range(0, count)
                .Select(index =>
                {
                    var window = new List<dynamic>();
                    for (int i = index; i < index + windowSize; i++)
                    {
                        window.Add(records[i]);
                    };
                    return new WindowObject { Window = window, Prediction = records[index + windowSize + 1].MaxPrice };
                })
                .ToList();
            return windows;
        }

        public static List<WindowObject> GetWindows<TKey>(IEnumerable<IGrouping<TKey, dynamic>> records, int windowSize = 5)
        {
            var windows = new List<WindowObject>();
            foreach (var item in records)
            {
                windows.AddRange(GetWindows(item.ToList(), windowSize));
            }

            return windows;
        }
    }

    public static class WindowListExtensions
    {
        public static TrainTestData SplitWindows(this List<WindowObject> windows, float testRatio = 0.8f)
        {
            var testWindowCount = (int)(windows.Count * testRatio);
            var trainData = windows.Take(testWindowCount).ToList();
            var testData = windows.Skip(testWindowCount).ToList();
            return new TrainTestData { TestData = testData, TrainData = trainData };
        }

        public static List<WindowObject> Scramble(this List<WindowObject> windows)
        {
            return windows.OrderBy(w => Guid.NewGuid()).ToList();
        }

        public static List<WindowObject> Keep(this List<WindowObject> windows, float keepRatio = 0.2f)
        {
            return windows.Take((int)(windows.Count * keepRatio)).ToList();
        }

    }

}
