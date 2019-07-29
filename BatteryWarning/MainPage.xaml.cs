using System;
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

        #endregion Public Fields

        #region Public Properties

        public int DelayInMinutes
        {
            get => MillisecondsToMinutes(_delayInMilliseconds);
            set
            {
                _delayInMilliseconds = MinutesToMilliseconds(value);
                OnPropertyChanged("DelayInMinutes");
            }
        }

        public double BatteryPercentage
        {
            get { return _batteryPercentage; }
            set
            {
                _batteryPercentage = value;
                OnPropertyChanged("BatteryPercentage");
            }
        }

        #endregion Public Properties

        #region Private Vars

        private int _delayInMilliseconds = (int)TimeSpan.FromSeconds(60).TotalMilliseconds;
        private double _batteryPercentage = -1;

        #endregion Private Vars

        public MainPage()
        {
            ApplicationView.PreferredLaunchViewSize = new Size(350, 250);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;

            DataContext = this;
            this.InitializeComponent();

            Task.Run(BatteryCheck);
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
                    });

                if (percentage > UpperLimitPercentage && batteryReport.Status == BatteryStatus.Charging)
                {
                    Notification($"Battery Level above {UpperLimitPercentage}%. To disconnect the power supply is suggested.");
                }

                if (percentage < LowerLimitPercentage && batteryReport.Status == BatteryStatus.Discharging)
                {
                    Notification($"Battery Level under {LowerLimitPercentage}%. Please, connect the power supply.");
                }

                await Task.Delay(_delayInMilliseconds);
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
            var toast = new ToastNotification(xml)
            {
                ExpirationTime = DateTime.Now.AddMinutes(DelayInMinutes)
            };

            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }

        #region Methods: Utils

        private int MinutesToMilliseconds(int minutes)
        {
            var res = (int)TimeSpan.FromMinutes(minutes).TotalMilliseconds;
            return res;
        }

        private int MillisecondsToMinutes(int milliseconds)
        {
            var res = (int)TimeSpan.FromMilliseconds(milliseconds).TotalMinutes;
            return res;
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