using Flurl.Http;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TPLDataflow_Task_Management
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>   

    public partial class MainWindow : Window
    {
        public async Task Consumer(ListViewItem T)
        {
            await Task.Delay(1000);
            T.TaskComplete(T);
        }
        public ObservableCollection<ListViewItem> ListViewItems { get; set; }
        public ObservableCollection<ComboBoxItem> ComboBoxItems { get; set; }
        public BlockScheduler<ListViewItem> BlockScheduler { get; set; }
        public object ListViewItemsLock = new object();
        public int Ids = 0;
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            ListViewItems = new ObservableCollection<ListViewItem>();
            ComboBoxItems = new ObservableCollection<ComboBoxItem>();
            BindingOperations.EnableCollectionSynchronization(ListViewItems, ListViewItemsLock);

            for (int i = 0; i < 50; i++)
            {
                TimeSpan ts = DateTime.UtcNow.AddSeconds(i).TimeOfDay;
                ComboBoxItems.Add(new ComboBoxItem() { Display = $"{ts.Hours}:{ts.Minutes}:{ts.Seconds}", TimeStamp = ts });
            }

            var blockOptions = new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = 1,
                BoundedCapacity = 1,
                EnsureOrdered = true
            };
            var LinkOptions = new DataflowLinkOptions()
            {
                PropagateCompletion = true,
            };

            var FirstBlock = new TransformBlock<ListViewItem, ListViewItem>(T =>
            {
                T.Status = "Running";
                return T;
            }, blockOptions);


            var SecondBlock = new TransformBlock<ListViewItem, ListViewItem>(async T =>
            {
                using (var cli = new FlurlClient())
                {
                    var dom = await cli.Request(T.Url).GetAsync().ReceiveString();
                    var title = dom.Substring(dom.IndexOf("<title>") + 7, dom.IndexOf("</title>") - dom.IndexOf("<title>") - 7);
                    T.Title = title;
                    T.Description = dom.Substring(dom.IndexOf("<p class="), dom.IndexOf("</p>"));
                }
                return T;
            }, blockOptions);
            FirstBlock.LinkTo(SecondBlock, LinkOptions);

            BlockScheduler = new BlockScheduler<ListViewItem>(new Capsule<ListViewItem, ListViewItem>(FirstBlock, SecondBlock), Consumer, 1, 1);

        }

        private void AddTask_Click(object sender, RoutedEventArgs e)
        {
            if (Url.Text == string.Empty) return;
            if (TimeStampComboBox.SelectedItem == null) return;
            var ts = (TimeStampComboBox.SelectedItem as ComboBoxItem).TimeStamp;
            var t = new Tuple<TimeSpan, ListViewItem>(ts, new ListViewItem() { Id = Ids.ToString(), Url = "https://www.reddit.com/r/CryptoCurrency/comments/97zuoa/daily_discussion_megathread_august_17_2018/", ScheduledAt = $"{ts.Hours}:{ts.Minutes}:{ts.Seconds}", Status = "Waiting Schedule" });
            ListViewItems.Add(t.Item2);
            BlockScheduler.SendAsync(t);

            // Can do tasks continuation.
            t.Item2.CurrentTask.ContinueWith(T =>
            {
                var lvi = T.Result;
                switch (T.Status)
                {
                    case TaskStatus.Canceled:
                        ListViewItems.Where(l => l.Id == lvi.Id).First().Status = "Cancelled";
                        break;
                    case TaskStatus.Faulted:
                        ListViewItems.Where(l => l.Id == lvi.Id).First().Status = T.Exception.Message;
                        break;
                    case TaskStatus.RanToCompletion:
                        ListViewItems.Where(l => l.Id == lvi.Id).First().Status = "Completed";
                        break;
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());

            Ids++;

            //Consumer.Completion.Wait();
        }

        private void CancelContext_Click(object sender, RoutedEventArgs e)
        {
            var selected = ListViewTasks.SelectedItem as ListViewItem;
            if (selected == null) return;
            selected.CTS.Cancel();
        }
    }
}
