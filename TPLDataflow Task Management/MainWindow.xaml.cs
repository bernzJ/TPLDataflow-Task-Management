using System;
using System.Collections.Generic;
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
    //public class TaskResult
    //{
    //    public CancellationTokenSource CTS { get; set; }
    //    public TaskCompletionSource<string> TCS { get; set; }
    //    public string Id { get; set; }
    //    public TaskResult(string Id, CancellationTokenSource CTS)
    //    {
    //        this.Id = Id;
    //        this.CTS = CTS;
    //        TCS = new TaskCompletionSource<string>();
    //    }
    //}
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>   
    public class Rabbit
    {
        public string Lorem { get; set; }
    }
    public partial class MainWindow : Window
    {
        public async Task Consumer(string T)
        {

            Console.WriteLine(" {0} wearing shorts.", T);
            await Task.Delay(5000);

        }
        public MainWindow()
        {
            InitializeComponent();

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

            var FirstBlock = new TransformBlock<string, string>(T =>
            {
                return $" {T} rabbits";
            }, blockOptions);

            var SecondBlock = new TransformBlock<string, Rabbit>(T =>
            {
                return new Rabbit() { Lorem = $"{T} are" };
            }, blockOptions);
            var ThirdBlock = new TransformBlock<Rabbit, string>(T =>
            {
                return $" {T.Lorem} faster than";
            }, blockOptions);

            var FourthBlock = new TransformBlock<string, string>(T =>
            {
                return $" {T} your mother";
            }, blockOptions);
            FirstBlock.LinkTo(SecondBlock, LinkOptions);
            SecondBlock.LinkTo(ThirdBlock, LinkOptions);
            ThirdBlock.LinkTo(FourthBlock, LinkOptions);
            //cts.Cancel();
            BlockScheduler<string> blockScheduler = new BlockScheduler<string>(new Capsule<string, string>(FirstBlock, FourthBlock), Consumer, DateTime.UtcNow.TimeOfDay, 1, 1);
            for (int i = 0; i < 20; i++)
            {
                blockScheduler.SendAsync(i.ToString());

            }
            blockScheduler.CancelAll();

            //Consumer.Complete();
            //Queue.Completion.Wait();
            //Queue.Complete();
            //Consumer.Completion.Wait();
        }
    }
}
