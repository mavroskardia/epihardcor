using EpicorConsole.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EpicorLibrary;
using EpicorLibrary.Utils;
using Ui.Properties;

namespace Ui
{
    public partial class MainWindow
    {
        public ObservableCollection<Time> Charges { get; set; }
        public ObservableCollection<NavigatorNode> SearchResults { get; set; }
        public NavigatorNode SelectedNode { get; set; }
        public DateTime WeekShown { get; set; }
        public Timer CodeTimer { get; set; }
        public Timer MessageUpdater { get; set; }

        private bool retrievingCodes;

        public MainWindow()
        {
            InitializeComponent();
            InitializeSettings();
            InitializeCodes();
            InitializeDates(DateTime.Today);
            InitializeCurrentCharges();
        }

        private void InitializeSettings()
        {
            if (!string.IsNullOrWhiteSpace(Settings.Default.ResourceID)) return;

            Settings.Default.ResourceID = new Epicor(null).ResourceId;
            Settings.Default.Save();
        }

        private void InitializeDates(DateTime week)
        {
            WeekShown = week;
            var sunday = new Epicor(Settings.Default.ResourceID).GetStartOfWeek(WeekShown);
            SundayLabel.Text = "Sun " + sunday.ToString("MM/dd");
            MondayLabel.Text = "Mon " + sunday.AddDays(1).ToString("MM/dd");
            TuesdayLabel.Text = "Tue " + sunday.AddDays(2).ToString("MM/dd");
            WednesdayLabel.Text = "Wed " + sunday.AddDays(3).ToString("MM/dd");
            ThursdayLabel.Text = "Thu " + sunday.AddDays(4).ToString("MM/dd");
            FridayLabel.Text = "Fri " + sunday.AddDays(5).ToString("MM/dd");
            SaturdayLabel.Text = "Sat " + sunday.AddDays(6).ToString("MM/dd");
        }

        private IEnumerable<Time> BuildTimes()
        {
            return (from dayOfWeek in Enum.GetNames(typeof (DayOfWeek))
                    let tb = (TextBox) FindName(dayOfWeek)
                    where tb != null && !string.IsNullOrWhiteSpace(tb.Text)
                    select ConvertToTime(dayOfWeek, decimal.Parse(tb.Text))
                    into time
                    where time != null
                    select time).ToList();
        }

        private Time ConvertToTime(string dayOfWeek, decimal hours)
        {
            var epicor = new Epicor(Settings.Default.ResourceID);

            switch (SelectedNode.NodeType)
            {
                case "Task":
                    return epicor.CreateTaskTime(WeekShown, SelectedNode.Data as TaskData, Comments.Text, dayOfWeek, hours);
                case "InternalCode":
                    return epicor.CreateInternalCodeTime(WeekShown, SelectedNode, Comments.Text, dayOfWeek, hours);
            }

            MessageBox.Show("No time type for " + SelectedNode.NodeType, "Failed to convert time", MessageBoxButton.OK,
                MessageBoxImage.Error);

            return null;
        }

        private void InitializeCurrentCharges()
        {
            Charges = new ObservableCollection<Time>();
            CurrentCharges.ItemsSource = Charges;

            var bgWorker = new BackgroundWorker();

            bgWorker.DoWork += (sender, args) =>
            {
                var e = new Epicor(Settings.Default.ResourceID);
                var charges = e.GetCurrentCharges(WeekShown);
                var from = e.GetStartOfWeek(WeekShown);
                var to = from.AddDays(6);
                var totals = Enum.GetNames(typeof (DayOfWeek)).Select(day => new Tuple<string, decimal>(day, charges.Where(c => c.TimeEntryDate.DayOfWeek == ((DayOfWeek) Enum.Parse(typeof (DayOfWeek), day))).Sum(c => c.Hours))).ToList();
                var weekTotal = 0.0m;

                Dispatcher.BeginInvoke(DispatcherPriority.Background, new ThreadStart(() =>
                {
                    foreach (var time in charges)
                        Charges.Add(time);

                    WeeklyChargesLabel.Text = string.Format("Charges for {0} to {1}", from.ToShortDateString(),
                        to.ToShortDateString());
                    WeeklyChargesLabel.FontStyle = FontStyles.Normal;

                    foreach (var hours in totals)
                    {
                        weekTotal += hours.Item2;

                        var elt = ((TextBlock)FindName(hours.Item1 + "Total"));
                        if (elt == null) continue;

                        elt.Text = hours.Item2.ToString("F"); 
                        
                        if (hours.Item1 == DayOfWeek.Sunday.ToString() || hours.Item1 == DayOfWeek.Saturday.ToString())
                            continue;
                        if (hours.Item2 >= 8m) elt.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xb3, 0x39));
                        if (hours.Item2 <= 0m) elt.Foreground = new SolidColorBrush(Color.FromRgb(0xb3, 0x39, 0x00));
                    }

                    WeekTotal.Text = weekTotal.ToString("F");
                    WeekTotal.Foreground = weekTotal >= 40m
                        ? new SolidColorBrush(Color.FromRgb(0x00, 0xb3, 0x39))
                        : new SolidColorBrush(Color.FromRgb(0xb3, 0x39, 0x00));
                }));
            };

            bgWorker.RunWorkerAsync();
        }

        private void InitializeCodes(bool overrideCache=false)
        {
            if (retrievingCodes) return;
            retrievingCodes = true;

            var bgWorker = new BackgroundWorker();

            EpicorTree.Items.Clear();
            LoadingMessageText.Text = "Loading codes from Epicor...";
            LoadingMessage.Visibility = Visibility.Visible;
            TabContainer.Visibility = Visibility.Hidden;

            bgWorker.DoWork += (sender, args) =>
            {
                string message;
                Tree<NavigatorNode> tree;
                var dt = DateTime.Today - Settings.Default.LastUpdateCodesDate;

                if (dt.Days >= 3 || overrideCache)
                {
                    message = "Retrieved from Epicor. Codes will become stale in 3 days.";
                    tree = new Epicor(Settings.Default.ResourceID).GetSiteActivities();
                    CacheTree(tree);
                }
                else
                {
                    message = string.Format("Using cached codes. Codes will become stale in {0} days.", 3 - dt.Days);
                    tree = GetCachedTree();
                }

                Dispatcher.BeginInvoke(DispatcherPriority.Background, new ThreadStart(() =>
                {
                    BuildChargeCodeTree(tree);
                    CacheMessage.Text = message;
                    LoadingMessage.Visibility = Visibility.Hidden;
                    TabContainer.Visibility = Visibility.Visible;
                    retrievingCodes = false;
                }));
            };

            bgWorker.RunWorkerAsync();
        }

        private void CacheTree(Tree<NavigatorNode> tree)
        {
            Settings.Default.CurrentCodes = new TreeSerializer<NavigatorNode>().Serialize(tree);
            Settings.Default.LastUpdateCodesDate = DateTime.Today;
            Settings.Default.Save();
        }

        private Tree<NavigatorNode> GetCachedTree()
        {
            return new TreeSerializer<NavigatorNode>().Deserialize(Settings.Default.CurrentCodes);
        }

        private void BuildChargeCodeTree(Tree<NavigatorNode> tree)
        {
            var root = new TreeViewItem {Header = "All Tasks"};
            BuildTree(root, tree);
            EpicorTree.Items.Add(root);
            EpicorTree.MouseDoubleClick += SelectNodeFromTree;

            SearchResults = new ObservableCollection<NavigatorNode>();
            Results.ItemsSource = SearchResults;

            Results.MouseDoubleClick += (sender, args) =>
            {
                var node = ((ListView) sender).SelectedItem as NavigatorNode;
                SelectNode(node);
            };

            Search.TextChanged += (sender, args) =>
            {
                var matches = new List<NavigatorNode>();

                tree.Traverse(tree, (node, level) =>
                {
                    switch (node.Data.NodeType)
                    {
                        case "InternalCode":
                            if (node.Data.Caption.ToUpper().Contains(Search.Text.ToUpper()))
                                matches.Add(node.Data);
                            break;
                        case "Task":
                        {
                            if (IsTaskMatch(node.Data.Data as TaskData))
                                matches.Add(node.Data);
                        }
                            break;
                    }
                });

                SearchResults.Clear();

                foreach (var match in matches)
                    SearchResults.Add(match);
            };
        }

        private bool IsTaskMatch(TaskData taskData)
        {
            if (taskData == null) return false;

            return taskData.Enabled && (
                    taskData.TaskName.ToUpper().Contains(Search.Text.ToUpper()) ||
                    taskData.ProjectCode.ToUpper().Contains(Search.Text.ToUpper()));
        }

        private void SelectNodeFromTree(object sender, EventArgs args)
        {
            var data = ((TreeView) sender).SelectedItem as TreeViewItem;

            if (data == null || data.Tag == null) return;

            var node = (NavigatorNode) data.Tag;

            if (node.NodeType == "Task" || node.NodeType == "InternalCode")
                SelectNode(node);
        }

        private void SelectNode(NavigatorNode node)
        {
            Save.IsEnabled = true;
            SelectedNode = node;
            SelectedTask.Text = node.ToString();
            ClearEntries();
        }

        private void ClearEntries()
        {
            foreach (var control in new[] { Sunday, Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Comments })
                control.Text = string.Empty;
        }

        private void BuildTree(ItemsControl root, Tree<NavigatorNode> tree)
        {
            foreach (var child in tree.Children)
            {
                var text = child.Data.Caption;
                if (child.Data.NodeType == "Task") text = ((TaskData) child.Data.Data).TaskName;
                var treeView = new TreeViewItem {Header = text, Tag = child.Data};
                BuildTree(treeView, child);
                root.Items.Add(treeView);
            }
        }

        private void SearchKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || Results.Items.Count == 0) return;

            SelectNode(Results.Items[0] as NavigatorNode);

            Monday.Focus();
        }

        private void SaveClick(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null) return;

            Save.IsEnabled = false;
            
            var times = BuildTimes();
            var bgWorker = new BackgroundWorker();

            bgWorker.DoWork += (o, args) =>
            {
                try
                {
                    new Epicor(Settings.Default.ResourceID).SaveTimes(times, TimeStates.New);
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new ThreadStart(() =>
                    {
                        InitializeCurrentCharges();
                        ClearEntries();
                        Messages.Text = "Saved Successfully";
                    }));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                finally
                {
                    Save.IsEnabled = true;
                }
            };

            bgWorker.RunWorkerAsync();
        }

        private void ApproveClick(object sender, RoutedEventArgs e)
        {
            Approve.IsEnabled = false;
            if (CurrentCharges.SelectedItems.Count == 0) return;

            var bgWorker = new BackgroundWorker();
            var epicor = new Epicor(Settings.Default.ResourceID);
            var times = CurrentCharges.SelectedItems.OfType<Time>();

            bgWorker.DoWork += (o, args) =>
            {
                epicor.SaveTimes(times, TimeStates.Modified);

                Dispatcher.BeginInvoke(DispatcherPriority.Background, new ThreadStart(() =>
                {
                    Approve.IsEnabled = true;
                    Messages.Text = "Marked for Approval Successfully";
                    InitializeCurrentCharges();
                }));
            };

            bgWorker.RunWorkerAsync();
        }

        private void CurrentChargesDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var time = ((ListView) sender).SelectedItem as Time;
            
            if (time == null) return;

            MessageBox.Show(time.WorkComment ?? "<no comment>", "Entered Comment");
        }

        private void ResultsKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || Results.SelectedItem == null) return;

            SelectNode(Results.SelectedItem as NavigatorNode);
            Monday.Focus();
        }

        private void RefreshClick(object sender, RoutedEventArgs e)
        {
            var bgWorker = new BackgroundWorker();
            bgWorker.DoWork += (o, args) =>
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new ThreadStart(InitializeCurrentCharges));
            bgWorker.RunWorkerAsync();
        }

        private void RefreshCodesFromEpicor(object sender, RoutedEventArgs e)
        {
            InitializeCodes(true);
        }

        private void ExitMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AboutMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            new About {Owner = this}.Show();
        }

        private void ToggleApprovals(object sender, RoutedEventArgs e)
        {
            Approve.IsEnabled = CurrentCharges.SelectedItems.OfType<Time>().Any(t => t.StatusCode != "E" && t.StatusCode != "A");
            Delete.IsEnabled = CurrentCharges.SelectedItems.OfType<Time>().Any(t => t.StatusCode != "A");
        }

        private void AdvanceWeek(object sender, RoutedEventArgs e)
        {
            WeekShown = WeekShown.AddDays(7);
            InitializeDates(WeekShown);
            InitializeCurrentCharges();
        }

        private void BackWeek(object sender, RoutedEventArgs e)
        {
            WeekShown = WeekShown.AddDays(-7);
            InitializeDates(WeekShown);
            InitializeCurrentCharges();
        }

        private void DeleteClick(object sender, RoutedEventArgs e)
        {
            var messageBoxResult = MessageBox.Show("Are you sure you want to delete the selected entries?", "Delete Confirmation", MessageBoxButton.YesNo);
            if (messageBoxResult != MessageBoxResult.Yes) return;

            Delete.IsEnabled = false;
            if (CurrentCharges.SelectedItems.Count == 0) return;

            var bgWorker = new BackgroundWorker();
            var epicor = new Epicor(Settings.Default.ResourceID);
            var times = CurrentCharges.SelectedItems.OfType<Time>();

            bgWorker.DoWork += (o, args) =>
            {
                epicor.SaveTimes(times, TimeStates.Deleted);

                Dispatcher.BeginInvoke(DispatcherPriority.Background, new ThreadStart(() =>
                {
                    Delete.IsEnabled = true;
                    Messages.Text = "Deleted Successfully";
                    InitializeCurrentCharges();
                }));
            };

            bgWorker.RunWorkerAsync();
        }

        private void ShowSettings(object sender, RoutedEventArgs e)
        {
            new SettingsWindow().Show();
        }

        private void MonthlyTotals_OnClick(object sender, RoutedEventArgs e)
        {
            new TotalsWindow(WeekShown).Show();
        }
    }
}