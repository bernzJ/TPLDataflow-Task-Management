using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TPLDataflow_Task_Management
{
    public class ListViewItem : ObservableObject
    {
        private string id;
        private string url;
        private string status;
        private string title;
        private string description;
        private string scheduledat;

        public string Id { get => id; set => SetProperty(ref id, value); }
        public string Url { get => url; set => SetProperty(ref url, value); }
        public string Status { get => status; set => SetProperty(ref status, value); }
        public string Title { get => title; set => SetProperty(ref title, value); }
        public string Description { get => description; set => SetProperty(ref description, value); }
        public string ScheduledAt { get => scheduledat; set => SetProperty(ref scheduledat, value); }

        public CancellationTokenSource CTS { get; set; }
        public TaskCompletionSource<ListViewItem> TCS { get; set; }
        public Task<ListViewItem> CurrentTask { get { return TCS.Task; } }

        public ListViewItem()
        {
            TCS = new TaskCompletionSource<ListViewItem>();
            CTS = new CancellationTokenSource();
            
        }
        public void TaskComplete(ListViewItem result)
        {
            TCS.SetResult(result);
        }
        public void TaskCanceled()
        {
            TCS.SetCanceled();
        }
        public void TaskFailed(Exception ex)
        {
            TCS.SetException(ex);
        }
    }
}
