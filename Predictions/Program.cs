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
        static string buffer = "Start at: " + DateTime.Now.ToString() + Environment.NewLine;

        static void Main(string[] args)
        {
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

            model.Compile(OptOptimizers.Adam, OptLosses.MeanSquaredError, OptMetrics.MSE);
            model.Train(trainFrame, 10, 64, testFrame);

            File.WriteAllText(@"C:\Users\Default.DESKTOP-MDUB405\Downloads\" + DateTime.Now.ToString("yyyy_MM_dd_hh_mm") + ".txt", buffer);

            Console.ReadLine();
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
            var reader = new CsvReader(File.OpenText(@"C:\Users\Default.DESKTOP-MDUB405\Downloads\omega.csv"), new Configuration { Delimiter = ",", HasHeaderRecord = true });
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
                r.Time = TimeSpan.Parse(r.Time as string).Minutes;
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
            var reader = new CsvReader(File.OpenText(@"C:\Users\Default.DESKTOP-MDUB405\Downloads\omega.csv"), new Configuration { Delimiter = ",", HasHeaderRecord = true });
            var records = reader.GetRecords<dynamic>().ToList();
            var firstInfo = records
                .Where(r => r.Mnemonic == "BMW")
                .KeepColumns(new string[] { "MaxPrice", "Date", "Time" })
                .OrderBy(r => DateTime.Parse(r.Date + " " + r.Time))
                .ToList();
            firstInfo.ForEach(r =>
            {
                r.Date = DateTime.Parse(r.Date as string).Day;
                r.Time = TimeSpan.Parse(r.Time as string).TotalMinutes;
            });
            firstInfo = firstInfo.GroupBy(r => r.Date.ToString()
                 + " "
                 + ((int)(r.Time / 60)).ToString())
                .Select(g =>
                {
                    dynamic row = new ExpandoObject();
                    row.Date = g.First().Date;
                    row.Time = g.First().Time;
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
            var reader = new CsvReader(File.OpenText(@"C:\Users\Default.DESKTOP-MDUB405\Downloads\omega.csv"), new Configuration { Delimiter = ",", HasHeaderRecord = true });
            var records = reader.GetRecords<dynamic>().ToList();
            var firstInfo = records
                .KeepColumns(new string[] { "Mnemonic", "MaxPrice", "Date", "Time" })
                .OrderBy(r => DateTime.Parse(r.Date + " " + r.Time))
                .ToList();
            firstInfo.ForEach(r =>
            {
                r.Date = DateTime.Parse(r.Date as string).Day;
                r.Time = TimeSpan.Parse(r.Time as string).TotalMinutes;
            });
            firstInfo = firstInfo.GroupBy(r => r.Mnemonic
                    + r.Date.ToString()
                    + " "
                    + ((int)(r.Time / 60)).ToString())
                .Select(g =>
                {
                    dynamic row = new ExpandoObject();
                    row.Date = g.First().Date;
                    row.Time = g.First().Time;
                    row.Mnemonic = g.First().Mnemonic;
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
            var windows = GetWindows(firstInfo);
            var trainTestData = windows
                .Scramble()
                .SplitWindows();
            return trainTestData;
        }

        private static TrainTestData CreateDataSets4()
        {
            var reader = new CsvReader(File.OpenText(@"C:\Users\Default.DESKTOP-MDUB405\Downloads\omega.csv"), new Configuration { Delimiter = ",", HasHeaderRecord = true });
            var records = reader.GetRecords<dynamic>().ToList();
            var firstInfo = records
                .KeepColumns(new string[] { "Mnemonic", "MaxPrice", "Date", "Time" })
                .OrderBy(r => DateTime.Parse(r.Date + " " + r.Time))
                .ToList();
            firstInfo.ForEach(r =>
            {
                r.Date = DateTime.Parse(r.Date as string).Day;
                r.Time = TimeSpan.Parse(r.Time as string).TotalMinutes;
            });
            firstInfo = firstInfo.GroupBy(r => r.Mnemonic
                    + r.Date.ToString()
                    + " "
                    + ((int)(r.Time / 60)).ToString())
                .Select(g =>
                {
                    dynamic row = new ExpandoObject();
                    row.Date = g.First().Date;
                    row.Time = g.First().Time;
                    row.Mnemonic = g.First().Mnemonic;
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
            var windows = GetWindows(firstInfo);
            var trainTestData = windows
                .Scramble()
                .SplitWindows();
            trainTestData.TrainData = trainTestData.TrainData.Keep(0.2f);
            return trainTestData;
        }

        private static TrainTestData CreateDataSets5()
        {
            var reader = new CsvReader(File.OpenText(@"C:\Users\Default.DESKTOP-MDUB405\Downloads\omega.csv"), new Configuration { Delimiter = ",", HasHeaderRecord = true });
            var records = reader.GetRecords<dynamic>().ToList();
            var firstInfo = records
                .KeepColumns(new string[] { "Mnemonic", "SecurityType", "MaxPrice", "MinPrice", "StartPrice", "EndPrice", "Date", "Time" })
                .OrderBy(r => DateTime.Parse(r.Date + " " + r.Time))
                .ToList();
            firstInfo.ForEach(r =>
            {
                r.Date = DateTime.Parse(r.Date as string).Day;
                r.Time = TimeSpan.Parse(r.Time as string).TotalMinutes;
            });
            firstInfo = firstInfo.GroupBy(r => r.Mnemonic
                    + r.Date.ToString()
                    + " "
                    + ((int)(r.Time / 60)).ToString())
                .Select(g =>
                {
                    dynamic row = new ExpandoObject();
                    row.Date = g.First().Date;
                    row.Time = g.First().Time;
                    row.Mnemonic = g.First().Mnemonic;
                    row.MaxPrice = g.Max(r => r.MaxPrice);
                    row.SecurityType = g.First().SecurityType;
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
            var windows = GetWindows(firstInfo);
            var trainTestData = windows
                .Scramble()
                .SplitWindows();
            //trainTestData.TrainData = trainTestData.TrainData.Keep(0.2f);
            return trainTestData;
        }

        public static List<WindowObject> GetWindows(List<dynamic> records, int windowSize = 5)
        {
            var windows = Enumerable.Range(0, records.Count - windowSize - 2)
                .Select(index =>
                {
                    var window = records
                                .Where((r, rIndex) => rIndex >= index && rIndex < index + windowSize)
                                .ToList();
                    return new WindowObject { Window = window, Prediction = records[index + windowSize + 1].MaxPrice };
                })
                .ToList();
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
