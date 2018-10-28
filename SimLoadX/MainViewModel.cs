using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WpfDirect2D.Shapes;

namespace SimLoadX
{
    public class MainViewModel : BindableBase
    {
        private static Random _random = new Random(1);

        [ThreadStatic]
        private Coordinate[] _points;

        List<Task<float>> _tasks;
        TaskFactory _factory;
        private Subject<Tuple<double, double>> _resultStream;
        private string _performanceValue;
        private string _numberOfTasks;
        private string _numberOfCores;
        private int _dataPacketSizeExponent;
        private string _pseudoResult;
        CancellationTokenSource _cts;
        private bool _useLocalData;
        private bool _useCopyData;
        private IDisposable _disposableSequence;
        private LimitedConcurrencyLevelTaskScheduler _lcts;
        private string _dataPacketSize;
        private bool _isConfigurable;
        private ISubject<Size> _sizeStream;
        private double _chartWidth;
        private double _chartHeight;

        public string PerformanceValue
        {
            get { return _performanceValue; }
            set { _performanceValue = value; RaisePropertyChanged(); }
        }

        public string NumberOfTasks
        {
            get { return _numberOfTasks; }
            set { _numberOfTasks = value; RaisePropertyChanged(); }
        }

        public string NumberOfCores
        {
            get { return _numberOfCores; }
            set { _numberOfCores = value; RaisePropertyChanged(); }
        }

        public string PseudoResult
        {
            get { return _pseudoResult; }
            set { _pseudoResult = value; RaisePropertyChanged(); }
        }

        public int DataPacketSizeExponent
        {
            get { return _dataPacketSizeExponent; }
            set
            {
                _dataPacketSizeExponent = value;
                //((2 ^ n) * 2 * 4) / 1000
                DataPacketSize = Math.Round((Math.Pow(2, value) * 2 * 4 / 1000d), 2).ToString() + " Kbyte";
                RaisePropertyChanged();
            }
        }

        public bool UseLocalData
        {
            get { return _useLocalData; }
            set { _useLocalData = value; RaisePropertyChanged(); }
        }

        public bool UseCopyData
        {
            get { return _useCopyData; }
            set { _useCopyData = value; RaisePropertyChanged(); }
        }

        public bool IsConfigurable
        {
            get { return _isConfigurable; }
            set { _isConfigurable = value; RaisePropertyChanged(); }
        }

        public string DataPacketSize
        {
            get { return _dataPacketSize; }
            set { _dataPacketSize = value; RaisePropertyChanged(); }
        }

        public List<IShape> Geometries { get; private set; }

        public ICommand StartBenchCommand { get; }

        public ICommand StopStopCommand { get; }

        public ICommand DataPacketSizeChangedCommand { get; }

        public MainViewModel(ISubject<Size> sizeStream)
        {
            _sizeStream = sizeStream;
            _sizeStream.Subscribe(SetLiveChartSize);

            // Set defaults
            PerformanceValue = double.NaN.ToString();
            NumberOfTasks = "8";
            NumberOfCores = "8";
            PseudoResult = double.NaN.ToString();
            DataPacketSizeExponent = 12;
            IsConfigurable = true;

            SetChartProperties();

            // Set commands
            StartBenchCommand = new DelegateCommand(OnStartBenchmark);
            StopStopCommand = new DelegateCommand(OnStopBenchmark);
            DataPacketSizeChangedCommand = new DelegateCommand<object>(x => OnDataPacketSizeChanged(x));
        }

        private void SetLiveChartSize(Size size)
        {
            _chartWidth = size.Width;
            _chartHeight = size.Height;
        }

        private void SetChartProperties()
        {
            //var mapper = Mappers.Xy<MeasureModel>()
            //    .X(model => model.DateTime.Ticks)   //use DateTime.Ticks as X
            //    .Y(model => model.Value);           //use the value property as Y

            ////lets save the mapper globally.
            //Charting.For<MeasureModel>(mapper);

            ////the values property will store our values array
            //ChartValues = new GearedValues<MeasureModel>();
            //ChartValues.WithQuality(Quality.Low);

            ////lets set how to display the X Labels
            //DateTimeFormatter = value => new DateTime((long)value).ToString("mm:ss");

            ////AxisStep forces the distance between each separator in the X axis
            //AxisStep = TimeSpan.FromSeconds(1).Ticks;
            ////AxisUnit forces lets the axis know that we are plotting seconds
            ////this is not always necessary, but it can prevent wrong labeling
            //AxisUnit = TimeSpan.TicksPerSecond;

            SetAxisLimits(DateTime.UtcNow);
        }

        private void SetAxisLimits(DateTime now)
        {
            //AxisMax = now.Ticks + TimeSpan.FromSeconds(1).Ticks; // lets force the axis to be 1 second ahead
            //AxisMin = now.Ticks - TimeSpan.FromSeconds(8).Ticks; // and 8 seconds behind
        }

        private Coordinate[] SetPoints()
        {
            int numberOfPoints = (int)Math.Pow(2, DataPacketSizeExponent);
            var path = new Coordinate[numberOfPoints];

            for (int i = 0; i < numberOfPoints; i++)
            {
                double x = _random.NextDouble() * 1E03;
                double y = _random.NextDouble() * 1E03;

                path[i] = new Coordinate((float)x, (float)y);
            }

            return path;
        }

        private void OnStartBenchmark()
        {
            IsConfigurable = false;
            _lcts = new LimitedConcurrencyLevelTaskScheduler(Convert.ToInt32(NumberOfCores));
            _factory = new TaskFactory(_lcts);
            _cts = new CancellationTokenSource();

            _disposableSequence?.Dispose();
            _resultStream = new Subject<Tuple<double, double>>();
            //_disposableSequence = Observable.Generate(0, // initialState
            //                            x => true, //condition
            //                            x => x, //iterate
            //                            x => x, //resultSelector
            //                            x => TimeSpan.FromMilliseconds(300)).ObserveOn(new EventLoopScheduler()).Subscribe(UpdateChart);//_resultStream.SampleByInterval(TimeSpan.FromMilliseconds(500)).SubscribeOn(new EventLoopScheduler()).Subscribe(UpdateChart);

            Task.Factory.StartNew(() =>
            {
                const double xAxisOffset = 40;
                const double yAxisOffset = 45;

                const int numLines = 100;
                var valueList = new List<int>(numLines);

                while (!_cts.IsCancellationRequested)
                {
                    Thread.Sleep(16);
                    UpdateChart(1);
                    
                    var line = new LineShape
                    {
                        FillColor = Colors.Blue,
                        StrokeColor = Colors.Blue,
                        SelectedColor = Colors.PaleVioletRed,
                        StrokeWidth = 1f,
                        IsLineClosed = false
                    };

                    var value = _random.Next(7, (int)(_chartHeight - xAxisOffset));

                    // Manage data to visualize
                    if (valueList.Count == numLines)
                    {
                        valueList.RemoveAt(0);
                        valueList.Add(value);
                    }
                    else
                    {
                        valueList.Add(value);
                    }

                    line.LineNodes.AddRange(valueList.Select((val, i) => new Point(yAxisOffset + (double)i * (_chartWidth - yAxisOffset) / (double)numLines, val)));

                    var geometryList = new List<IShape>
                    {
                        line
                    };

                    Geometries = new List<IShape>(geometryList);
                    RaisePropertyChanged(nameof(Geometries));
                }
            });


            //try
            //{
            //    Task.Factory.StartNew(() =>
            //   {
            //       while (!_cts.IsCancellationRequested)
            //       {
            //           var res = HandleCurrentPaket();
            //           _resultStream.OnNext(res);
            //       }
            //   });
            //}
            //catch (Exception ex)
            //{
            //    PseudoResult = "Benchmark aborted";
            //}
        }

        private void UpdateChart(int x)
        {
            //var now = DateTime.UtcNow;

            //ChartValues.Add(new MeasureModel
            //{
            //    DateTime = now,
            //    Value = _random.NextDouble()//Math.Round(1E06 / x.Item1, 2)
            //});

            //SetAxisLimits(now);

            ////lets only use the last 150 values
            //if (ChartValues.Count >= 100)
            //    ChartValues.RemoveAt(0);
        }

        private void UpdatePerformanceValue(Tuple<double, double> x)
        {
            if (x == null)
                return;

            PerformanceValue = Math.Round(1E06 / x.Item1, 2).ToString();
            PseudoResult = Math.Round(x.Item2, 2).ToString();
        }

        private Tuple<double, double> HandleCurrentPaket()
        {
            _tasks = new List<Task<float>>();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            for (int tCtr = 0; tCtr < Convert.ToInt32(NumberOfTasks); tCtr++)
            {
                if (!UseLocalData)
                {
                    _points = SetPoints();
                }

                Task<float> task = _factory.StartNew(() =>
                {
                    if (!UseLocalData)
                    {
                        if (UseCopyData)
                        {
                            var copyPoints = new Coordinate[_points.Length];
                            Array.Copy(_points, copyPoints, _points.Length);

                            return GetDistance(copyPoints);
                        }
                        else
                        {
                            return GetDistance(_points);
                        }
                    }
                    else
                    {
                        var threadSafeRandom = new ThreadSafeRandom();
                        int numberOfPoints = (int)Math.Pow(2, DataPacketSizeExponent);
                        var path = new Coordinate[numberOfPoints];

                        for (int i = 0; i < numberOfPoints; i++)
                        {
                            double x = threadSafeRandom.NextDouble() * 1E03;
                            double y = threadSafeRandom.NextDouble() * 1E03;

                            path[i] = new Coordinate((float)x, (float)y);
                        }

                        return GetDistance(path);
                    }


                }, _cts.Token);
                _tasks.Add(task);
            }

            Task.WaitAll(_tasks.ToArray());

            var minLength = _tasks.Select(t => t.Result).Min();

            stopwatch.Stop();

            return new Tuple<double, double>(stopwatch.ElapsedTicks, minLength);
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetDistance(Coordinate[] points)
        {
            var random = new ThreadSafeRandom();

            float[] distances = new float[points.Length];

            int[] sequence = Enumerable.Range(0, points.Length).ToArray();

            for (int i = 0; i < points.Length; i++)
            {
                // Use Next on random instance with an argument.
                // ... The argument is an exclusive bound.
                //     So we will not go past the end of the array.
                int r = i + random.Next(points.Length - i);
                int t = sequence[r];
                sequence[r] = sequence[i];
                sequence[i] = t;
            }

            for (int i = 0; i < points.Length - 1; i++)
            {
                var squaredDistance = (points[sequence[i]].X - points[sequence[i + 1]].X) *
                                      (points[sequence[i]].X - points[sequence[i + 1]].X) +
                                      (points[sequence[i]].Y - points[sequence[i + 1]].Y) *
                                      (points[sequence[i]].Y - points[sequence[i + 1]].Y);

                distances[i] = (float)Math.Sqrt(squaredDistance);
            }

            distances[sequence[points.Length - 1]] = (float)Math.Sqrt((points[sequence[0]].X - points[sequence[points.Length - 1]].X) *
                                                            (points[sequence[0]].X - points[sequence[points.Length - 1]].X) +
                                                            (points[sequence[0]].Y - points[sequence[points.Length - 1]].Y) *
                                                            (points[sequence[0]].Y - points[sequence[points.Length - 1]].Y));

            return distances.Sum();
        }

        private void OnStopBenchmark()
        {
            _cts?.Cancel();
            _disposableSequence?.Dispose();
            PseudoResult = "Benchmark aborted";
            IsConfigurable = true;
        }

        private void OnDataPacketSizeChanged(object value)
        {
            DataPacketSizeExponent = Convert.ToInt32(value);
        }
    }
}
