using System;
using System.Globalization;
using System.Windows.Data;

namespace BatteryWarning
{
	public class TextToDoubleConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value.ToString();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			// value is the data from the source object.
			string s = value as string;
			double res = 30;
			Double.TryParse(s, out res);
			return res;
		}
	}
}