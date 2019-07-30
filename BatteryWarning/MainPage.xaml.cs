using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Devices.Power;
using Windows.Foundation;
using Windows.System.Power;
using Windows.UI.Notifications;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace BatteryWarning
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        #region Public Fields

        public float LowerLimitPercentage = 30.0f;
        public float UpperLimitPercentage = 80.0f;

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

        public double BatteryPercentage
        {
            get { return _batteryPercentage; }
            set
            {
                _batteryPercentage = value;
                OnPropertyChanged();
            }
        }

        public int ScreenWidth { get; set; } = 600;
        public int ScreenHeight { get; set; } = 600;

        //                                                                     1s 15s 30s 60s 1.5m  5m   15m
        public List<int> TimeIntervalsInSeconds { get; set; } = new List<int> { 1, 15, 30, 60, 90, 300, 900 };

        // filled in the constructor with the intervals labels
        public List<string> TimeIntervalsLabels { get; set; } = new List<string>();

        #endregion Public Properties

        #region Private Vars

        //private int _delayInMilliseconds = (int)TimeSpan.FromSeconds(15).TotalMilliseconds;
        private double _batteryPercentage = -1;

        private double _timeAxis = 0;
        private int _maxAmountInPlot = 100; // 10000

        #endregion Private Vars

        public PlotModel PercentageTimeSerie { get; private set; }

        public MainPage()
        {
            ApplicationView.PreferredLaunchViewSize = new Size(ScreenWidth, ScreenHeight);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;

            DataContext = this;
            this.InitializeComponent();

            // run async battery check
            Task.Run(BatteryCheck);

            // Initialize plot
            PercentageTimeSerie = InitPlot("Battery Charge Status over Minutes", "Minutes", "Percentage", xMin: 0, xMax: 0);

            // fill labels for combobox
            foreach (var t in TimeIntervalsInSeconds)
            {
                TimeIntervalsLabels.Add(GetLabelFromSeconds(t));
            }

            // set first item
            ComboBoxDelay.SelectedIndex = 0;
            ComboBoxDelay.SelectedValue = TimeIntervalsInSeconds[0];
        }

        private PlotModel InitPlot(string title, string xLabel, string yLabel, bool allowScaling = false, double xMin = 0, double xMax = 0, double yMin = 0, double yMax = 100)
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
            var i = ComboBoxDelay.SelectedIndex;
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

        private void UpdateTimeSerie(double percentage, int index = 0)
        {
            var currSerie = PercentageTimeSerie.Series[index];
            var plot = currSerie as LineSeries;
            PreventMemoryLeak(plot);
            plot.Points.Add(new DataPoint(_timeAxis, percentage));
            PercentageTimeSerie.InvalidatePlot(true);
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
                var batteryReport = Battery.AggregateBattery.GetReport();
                double percentage = -1;
                if (batteryReport.RemainingCapacityInMilliwattHours != null)
                {
                    if (batteryReport.FullChargeCapacityInMilliwattHours != null)
                    {
                        percentage = (batteryReport.RemainingCapacityInMilliwattHours.Value /
                                          (double)batteryReport.FullChargeCapacityInMilliwattHours.Value);
                    }
                }

                percentage *= 100;

                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        BatteryPercentage = percentage;
                        UpdateTimeSerie(percentage);
                        UpdateDelayFromComboBox();
                    });

                if (percentage > UpperLimitPercentage && batteryReport.Status == BatteryStatus.Charging)
                {
                    Notification($"Battery Level above {UpperLimitPercentage}%. To disconnect the power supply is suggested.");
                }

                if (percentage < LowerLimitPercentage && batteryReport.Status == BatteryStatus.Discharging)
                {
                    Notification($"Battery Level under {LowerLimitPercentage}%. Please, connect the power supply.");
                }

                var m = SecondsToMilliseconds(SelectedDelay);
                await Task.Delay(m);
            }
        }

        public void Notification(string message)
        {
            var toastContent = $@"<toast launch='action=viewAlarm&amp;alarmId=3' scenario='alarm'>
                  <visual>
                    <binding template='ToastGeneric'>
                      <text>Battery Level Notification!</text>
                      <text>{message}</text>
                    </binding>
                  </visual>
                  <actions>
                    <action
                      activationType='background'
                      arguments='dismiss'
                      content='Dismiss'/>
                  </actions>
                </toast>";

            //ToastVisual visual = new ToastVisual()
            //{
            //    BindingGeneric = new ToastBindingGeneric()
            //    {
            //        Children =
            //        {
            //            new AdaptiveText()
            //            {
            //                Text = title
            //            },

            //            new AdaptiveText()
            //            {
            //                Text = message
            //            },
            //        }
            //    }
            //};

            //// Now we can construct the final toast content
            //ToastContent toastContent = new ToastContent()
            //{
            //    Visual = visual,

            //    // Arguments when the user taps body of toast
            //    Launch = new QueryString()
            //    {
            //        { "action", "viewConversation" },
            //        { "conversationId", Guid.NewGuid().ToString() }
            //    }.ToString()
            //};

            //// And create the toast notification
            //var toast = new ToastNotification(toastContent.GetXml());

            XmlDocument xml = new XmlDocument();
            xml.LoadXml(toastContent);
            var expiration = SecondsToMinutes(GetCurrentDelayInSeconds());
            var toast = new ToastNotification(xml)
            {
                ExpirationTime = DateTime.Now.AddMinutes(expiration)
            };

            ToastNotificationManager.CreateToastNotifier().Show(toast);
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