using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using ToastNotifications;
using ToastNotifications.Lifetime;
using ToastNotifications.Lifetime.Clear;
using ToastNotifications.Position;
using ToastNotifications.Messages;

namespace BatteryWarning.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Public Fields

        // Ugly ugly solution to ComboBox retrive problem: await of thread needs combobox value but it's on dispatcher (dunno how to solve)
        public int SelectedDelay = 1;

        #endregion Public Fields

        #region Public Properties

        //public int DelayInMinutes
        //{
        //    get => MillisecondsToMinutes(_delayInMilliseconds);
        //    set
        //    {
        //        _delayInMilliseconds = MinutesToMilliseconds(value);
        //        OnPropertyChanged();
        //    }
        //}
        private float _minPercentage = 30.0f;

        private float _maxPercentage = 80.0f;

        public float MinPercentage
        {
            //get => MinPercentages[MinPercentageComboBox.SelectedIndex];
            get
            {
                if (MinPercentageComboBox.SelectedIndex < 0)
                {
                    return MinPercentages[0];
                }
                else
                {
                    return MinPercentages[MinPercentageComboBox.SelectedIndex];
                }
            }
            set => MinPercentage = value;
        }

        public float MaxPercentage
        {
            // get => MaxPercentages[MaxPercentageComboBox.SelectedIndex];
            get
            {
                if (MaxPercentageComboBox.SelectedIndex < 0)
                {
                    return MaxPercentages[0];
                }
                else
                {
                    return MaxPercentages[MaxPercentageComboBox.SelectedIndex];
                }
            }
            set => MaxPercentage = value;
        }

        public double BatteryPercentage
        {
            get { return _batteryPercentage; }
            set
            {
                _batteryPercentage = value;
                OnPropertyChanged();
            }
        }

        public int ScreenWidth { get; set; } = 500;
        public int ScreenHeight { get; set; } = 850;

        //                                                                     1s 15s 30s 60s 1.5m  5m   15m
        public List<int> TimeIntervalsInSeconds { get; set; } = new List<int> { 1, 15, 30, 60, 90, 300, 900 };

        // filled in the constructor with the intervals labels
        public List<string> TimeIntervalsLabels { get; set; } = new List<string>();

        public List<float> MaxPercentages { get; set; } = new List<float> { 100, 90, 80, 70, 60 };
        public List<float> MinPercentages { get; set; } = new List<float> { 10, 20, 30, 40, 50 };

        // public IList<DataPoint> PercentageDataPoints { get; private set; } = new List<DataPoint>();

        public string PlotTitle { get; set; } = "Battery Status";

        #endregion Public Properties

        #region Private Vars

        //private int _delayInMilliseconds = (int)TimeSpan.FromSeconds(15).TotalMilliseconds;
        private double _batteryPercentage = -1;

        private double _timeAxis = 0;
        private int _maxAmountInPlot = 100; // 10000

        private Notifier _notifier;

        #endregion Private Vars

        public delegate void OnBatteryChangedHandler(double percentage);

        public static event OnBatteryChangedHandler OnBatteryChanged;

        public PlotModel BatteryPercentageModel { get; private set; }

        public MainWindow()
        {
            ResetApplicationView();

            this.InitializeComponent();

            // Setup the method for update
            OnBatteryChanged += UpdateInterface;

            // run async battery check
            Task.Run(BatteryCheck);

            // Initialize plot
            BatteryPercentageModel = InitPlot("Battery Status", "Minutes", "Percentage");
            //TestFill();

            // Set context AFTER creation of model
            DataContext = this;

            // fill labels for combobox
            TimeIntervalsInSeconds.Sort();
            foreach (var t in TimeIntervalsInSeconds)
            {
                TimeIntervalsLabels.Add(GetLabelFromSeconds(t));
            }

            // set first item
            SetFirstItems();

            InstantiateNotifier();
        }

        private void InstantiateNotifier()
        {
            _notifier = new Notifier(cfg =>
            {
                cfg.PositionProvider = new PrimaryScreenPositionProvider(
                    corner: Corner.BottomRight,
                    offsetX: 10,
                    offsetY: 10
                );
                //cfg.PositionProvider = new WindowPositionProvider(
                //    parentWindow: Application.Current.MainWindow,
                //    corner: Corner.TopRight,
                //    offsetX: 10,
                //    offsetY: 10);

                cfg.LifetimeSupervisor = new TimeAndCountBasedLifetimeSupervisor(
                    notificationLifetime: TimeSpan.FromSeconds(3),
                    maximumNotificationCount: MaximumNotificationCount.FromCount(5));

                cfg.Dispatcher = Application.Current.Dispatcher;
                cfg.DisplayOptions.TopMost = true;
            });
        }

        private void SetFirstItems(int delayIndex = 0, int minPercentageIndex = 2, int maxPercentageIndex = 1)
        {
            DelayComboBox.SelectedIndex = delayIndex;
            DelayComboBox.SelectedValue = TimeIntervalsInSeconds[delayIndex];

            MaxPercentageComboBox.SelectedIndex = maxPercentageIndex;
            MaxPercentageComboBox.SelectedValue = MaxPercentages[maxPercentageIndex];

            MinPercentageComboBox.SelectedIndex = minPercentageIndex;
            MinPercentageComboBox.SelectedValue = MinPercentages[minPercentageIndex];
        }

        private void TestFill()
        {
            var plot = BatteryPercentageModel;
            plot.Series.Add(CreateNormalDistributionSeries(-5, 5, 0, 0.2));
            plot.Series.Add(CreateNormalDistributionSeries(-5, 5, 0, 1));
            plot.Series.Add(CreateNormalDistributionSeries(-5, 5, 0, 5));
            plot.Series.Add(CreateNormalDistributionSeries(-5, 5, -2, 0.5));
        }

        private static LineSeries CreateNormalDistributionSeries(double x0, double x1, double mean, double variance, int n = 1000)
        {
            var ls = new LineSeries
            {
                Title = string.Format("μ={0}, σ²={1}", mean, variance)
            };

            for (int i = 0; i < n; i++)
            {
                double x = x0 + ((x1 - x0) * i / (n - 1));
                double f = 1.0 / Math.Sqrt(2 * Math.PI * variance) * Math.Exp(-(x - mean) * (x - mean) / 2 / variance);
                ls.Points.Add(new DataPoint(x, f));
            }

            return ls;
        }

        public void ResetApplicationView()
        {
            //var size = new Size(ScreenWidth, ScreenHeight);
            //var view = ApplicationView.GetForCurrentView();
            //view.ExitFullScreenMode();
            //ApplicationView.PreferredLaunchViewSize = size;
            //ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
            return;
        }

        private void UpdateInterface(double percentage)
        {
            //ApplicationView.GetForCurrentView().TryResizeView(new Size(ScreenWidth, ScreenHeight));
            BatteryPercentage = percentage;
            UpdateTimeSerie();
            UpdateDelayFromComboBox();
        }

        private static PlotModel InitPlot(string title,
            string xLabel, string yLabel,
            bool allowScaling = false,
            double xMin = 0, double xMax = 0,
            double yMin = 0, double yMax = 100)
        {
            var plot = new PlotModel { Title = title };
            LineSeries ls = new LineSeries();

            var yAxis = new LinearAxis()
            {
                Position = AxisPosition.Left,
                Minimum = yMin,
                Maximum = yMax,
                Key = "Vertical",
                Title = yLabel ?? "",
                IsZoomEnabled = false
            };

            var xAxis = new LinearAxis()
            {
                Position = AxisPosition.Bottom,
                Key = "Horizontal",
                Title = xLabel ?? "",
                IsZoomEnabled = allowScaling
            };

            if (xMax > 0 && xMin > 0)
            {
                xAxis.Minimum = xMin;
                xAxis.Maximum = xMax;
            }

            ls.XAxisKey = "Horizontal";
            ls.YAxisKey = "Vertical";

            plot.Axes.Add(xAxis);
            plot.Axes.Add(yAxis);
            plot.Series.Add(ls);
            return plot;
        }

        private void UpdateDelayFromComboBox()
        {
            SelectedDelay = GetCurrentDelayInSeconds();
        }

        private int GetCurrentDelayInSeconds()
        {
            var i = DelayComboBox.SelectedIndex;
            if (i >= 0)
            {
                return TimeIntervalsInSeconds[i];
            }

            return TimeIntervalsInSeconds[0];
        }

        //private int GetCurrentDelayInMilliseconds()
        //{
        //    var s = GetCurrentDelayInSeconds();
        //    var m = SecondsToMilliseconds(s);
        //    return m;
        //}

        private void UpdateTimeSerie(int index = 0)
        {
            var currSerie = BatteryPercentageModel.Series[index];
            var plot = currSerie as LineSeries;
            PreventMemoryLeak(plot);
            plot.Points.Add(new DataPoint(_timeAxis, BatteryPercentage));
            BatteryPercentageModel.InvalidatePlot(true);
            _timeAxis += TimeSpan.FromSeconds(GetCurrentDelayInSeconds()).TotalMinutes;
        }

        private void PreventMemoryLeak(LineSeries plot)
        {
            // non funziona bene
            if (plot.Points.Count > _maxAmountInPlot)
            {
                var origPoint = plot.Points[0];
                var firstPoint = new DataPoint(origPoint.X, origPoint.Y);
                plot.Points.Clear();
                plot.Points.Add(firstPoint);
            }
        }

        private async Task BatteryCheck()
        {
            while (true)
            {
                double percentage = SystemPower.BatteryCharge();

                percentage *= 100;
                percentage = percentage < 0 ? 0 : percentage;   // if missing battery.. TODO

                var upperLimitPercentage = 80.0f;
                var lowerLimitPercentage = 30.0f;

                //TODO: correggere
                var flag = SystemPower.GetCurrentStatus();
                if (flag == SystemPower.BatteryFlag.NoSystemBattery || flag == SystemPower.BatteryFlag.Unknown)
                {
                    percentage = -1;
                }

                await Dispatcher.BeginInvoke((Action)(() =>
                {
                    UpdateInterface(percentage);
                    //upperLimitPercentage = MaxPercentage;
                    //lowerLimitPercentage = MinPercentage;
                }));

                //await Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                //    () =>
                //{
                //    UpdateInterface(percentage);
                //    upperLimitPercentage = MaxPercentage;
                //    lowerLimitPercentage = MinPercentage;
                //});
                var isCharging = flag == SystemPower.BatteryFlag.Charging;

                if (percentage > upperLimitPercentage && isCharging)
                {
                    await Dispatcher.BeginInvoke((Action)(() =>
                   {
                       NotifyUser(
                           $"Battery Level above {upperLimitPercentage}%. To disconnect the power supply is suggested.");
                   }));
                }

                if (percentage < lowerLimitPercentage && !isCharging)
                {
                    await Dispatcher.BeginInvoke((Action)(() =>
                    {
                        NotifyUser($"Battery Level under {lowerLimitPercentage}%. Please, connect the power supply.");
                    }));
                }

                //MainPage.OnBatteryChanged.Invoke(percentage);
                var m = SecondsToMilliseconds(SelectedDelay);
                await Task.Delay(m);
            }
        }

        public void NotifyUser(string message)
        {
            var expiration = SecondsToMinutes(GetCurrentDelayInSeconds());
            NotifyUser(message, expiration);
        }

        public void NotifyUser(string message, int durationSeconds)
        {
            //TODO: get notification working
            _notifier.ClearMessages(new ClearFirst());
            _notifier.ShowWarning(message);
        }

        #region Methods: Utils

        private int MinutesToMilliseconds(int minutes)
        {
            return (int)TimeSpan.FromMinutes(minutes).TotalMilliseconds;
        }

        private int MillisecondsToMinutes(int milliseconds)
        {
            return (int)TimeSpan.FromMilliseconds(milliseconds).TotalMinutes;
        }

        private int SecondsToMinutes(int seconds)
        {
            return (int)TimeSpan.FromSeconds(seconds).TotalMinutes;
        }

        private int SecondsToMilliseconds(int seconds)
        {
            return (int)TimeSpan.FromSeconds(seconds).TotalMilliseconds;
        }

        private string GetLabelFromSeconds(int seconds)
        {
            var t = TimeSpan.FromSeconds(seconds);
            var m = (int)t.TotalMinutes;
            if (m == 0)
            {
                return $"{seconds} seconds";
            }

            if (m > 0 && m < 60)
            {
                return $"{m} minutes";
            }

            if (m >= 60 && t.Hours < 24)
            {
                return $"{t.Hours} hours";
            }
            else
            {
                return $"{t.Days} days, {t.Hours} hours";
            }
        }

        #endregion Methods: Utils

        #region Interfaces Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion Interfaces Implementation
    }
}