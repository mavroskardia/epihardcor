using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using EpicorLibrary;
using Ui.Properties;

namespace Ui
{
    /// <summary>
    /// Interaction logic for TotalsWindow.xaml
    /// </summary>
    public partial class TotalsWindow : Window
    {
        public TotalsWindow(DateTime week)
        {
            Week = week;
            
            InitializeComponent();
            
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new ThreadStart(() =>
            {
                GatherTotals();
                TotalHoursLabel.Inlines.Clear();
                TotalHoursLabel.Inlines.Add("Hours charged in ");
                TotalHoursLabel.Inlines.Add(Week.ToString("MMMM"));
                TotalHoursLabel.Inlines.Add(": ");
                TotalHoursLabel.Inlines.Add(new Bold(new Run(MonthlyTotal.ToString(CultureInfo.CurrentCulture))) { FontSize = 16 });
                TotalHoursLabel.Inlines.Add(" / ");
                TotalHoursLabel.Inlines.Add(new Bold(new Run(TotalHoursInMonth.ToString(CultureInfo.CurrentCulture))) { FontSize = 14 });
            }));
        }

        public DateTime Week { get; set; }
        public decimal MonthlyTotal { get; set; }
        public decimal TotalHoursInMonth { get; set; }

        private void GatherTotals()
        {
            var startDate = new DateTime(Week.Year, Week.Month, 1);
            var endDate = new DateTime(Week.Year, Week.Month, DateTime.DaysInMonth(Week.Year, Week.Month));
            var epicor = new Epicor(Settings.Default.ResourceID);
            var timeList = epicor.GetChargesBetween(startDate, endDate);
            MonthlyTotal = timeList.Sum(t => t.Hours);
            TotalHoursInMonth = GetBusinessDays(startDate, endDate) * 8;
        }

        private void OK_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public static decimal GetBusinessDays(DateTime startDate, DateTime endDate)
        {
            var calcBusinessDays =(decimal)
                (1 + ((endDate - startDate).TotalDays * 5 -
                (startDate.DayOfWeek - endDate.DayOfWeek) * 2) / 7);

            if ((int)endDate.DayOfWeek == 6) calcBusinessDays--;
            if ((int)startDate.DayOfWeek == 0) calcBusinessDays--;

            return calcBusinessDays;
        }

    }
}
