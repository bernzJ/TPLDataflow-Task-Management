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
            if (T.CTS.IsCancellationRequested)
            {
                T.TaskCanceled();
                return;
            }
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
                if (T.CTS.IsCancellationRequested)
                {
                    return T;
                }
                T.Status = "Running";
                return T;

            }, blockOptions);


            var SecondBlock = new TransformBlock<ListViewItem, ListViewItem>(async T =>
            {
                if (T.CTS.IsCancellationRequested)
                {
                    return T;
                }
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
            var lvi = new ListViewItem() { Url = Url.Text, ScheduledAt = $"{ts.Hours}:{ts.Minutes}:{ts.Seconds}", Status = "Waiting Schedule" };
            // If CTS is implemented within your custom class like in this instance, make sure its the same passed in the tuple.
            // The reason for this token here is that if we want to cancel one specific message (not the whole block) and the timestamp is
            // Quite delayed, this would prevent it to be lock waiting until its time.
            var t = new Tuple<TimeSpan, CancellationTokenSource, ListViewItem>(ts, lvi.CTS, lvi);
            ListViewItems.Add(lvi);
            lvi.Id = lvi.CurrentTask.Id.ToString();
            BlockScheduler.SendAsync(t);

            // Can do tasks continuation.
            lvi.CurrentTask.ContinueWith(T =>
            {
                switch (T.Status)
                {
                    case TaskStatus.Canceled:
                        ListViewItems.Where(l => l.Id == T.Id.ToString()).First().Status = "Canceled";
                        break;
                    case TaskStatus.Faulted:
                        ListViewItems.Where(l => l.Id == T.Id.ToString()).First().Status = T.Exception.InnerException.Message;
                        break;
                    case TaskStatus.RanToCompletion:
                        T.Result.Status = "Completed";
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

        private void CancelAllContext_Click(object sender, RoutedEventArgs e)
        {
            BlockScheduler.CancelAll();
        }
    }
}
