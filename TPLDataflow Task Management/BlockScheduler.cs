using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace TPLDataflow_Task_Management
{
    public class Capsule<T, T1>
    {
        public IPropagatorBlock<T, T> Beginning { get; private set; }
        public IPropagatorBlock<T1, T1> Ending { get; private set; }
        public Capsule(IPropagatorBlock<T, T> Beginning, IPropagatorBlock<T1, T1> Ending)
        {
            this.Beginning = Beginning;
            this.Ending = Ending;
        }
    }
    public class BlockScheduler<T>
    {
        private BufferBlock<Tuple<TimeSpan, CancellationTokenSource, T>> Queue { get; set; }
        private List<Task> Consumers { get; set; }
        private CancellationTokenSource BlockCTS { get; set; }

        private IPropagatorBlock<Tuple<TimeSpan, CancellationTokenSource, T1>, T1> CreateThrottleBlock<T1>(CancellationTokenSource CTS, int MaxPerInterval, int BlockMaxDegreeOfParallelism)
        {
            SemaphoreSlim sem = new SemaphoreSlim(MaxPerInterval, MaxPerInterval);
            return new TransformBlock<Tuple<TimeSpan, CancellationTokenSource, T1>, T1>(async (x) =>
            {
                await sem.WaitAsync();
                while (x.Item1 >= DateTime.UtcNow.TimeOfDay && !x.Item2.IsCancellationRequested && !CTS.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000);
                }
                sem.Release();
                return x.Item3;
            },
                 new ExecutionDataflowBlockOptions
                 {
                     BoundedCapacity = MaxPerInterval,
                     MaxDegreeOfParallelism = BlockMaxDegreeOfParallelism,
                     EnsureOrdered = true,
                     CancellationToken = CTS.Token
                 });
        }
        /// <summary>
        /// Create a queue of block encapsuled by throttling and custom consumer.
        /// Limitations: The first block and last block(consumer) must have same in/out types.
        /// </summary>
        /// <param name="Capsule"></param>
        /// <param name="ConsumerAction"></param>
        /// <param name="ScheduledAt"></param>
        /// <param name="MaxDegreeOfParallelism"></param>
        /// <param name="BoundedCapacity"></param>
        public BlockScheduler(Capsule<T, T> Capsule, Func<T, Task> ConsumerAction, int MaxDegreeOfParallelism, int BoundedCapacity)
        {
            BlockCTS = new CancellationTokenSource();

            var ConsumerOptions = new ExecutionDataflowBlockOptions { BoundedCapacity = BoundedCapacity, MaxDegreeOfParallelism = MaxDegreeOfParallelism, EnsureOrdered = true, CancellationToken = BlockCTS.Token };
            var LinkOptions = new DataflowLinkOptions { PropagateCompletion = true, };
            Consumers = new List<Task>();
            Queue = new BufferBlock<Tuple<TimeSpan, CancellationTokenSource, T>>();

            var Consumer = new ActionBlock<T>(async TT => await ConsumerAction(TT), ConsumerOptions);

            var ThrottleBlock = CreateThrottleBlock<T>(BlockCTS, BoundedCapacity, MaxDegreeOfParallelism);
            Queue.LinkTo(ThrottleBlock, LinkOptions);


            ThrottleBlock.LinkTo(Capsule.Beginning, LinkOptions);
            Capsule.Ending.LinkTo(Consumer, LinkOptions);

            for (int i = 0; i < MaxDegreeOfParallelism; i++)
            {
                Consumers.Add(Consumer.Completion);
            }
        }
        public async Task SendAsync(Tuple<TimeSpan, CancellationTokenSource, T> Item)
        {
            await Queue.SendAsync(Item);
        }

        public async Task SendAsync(IEnumerable<Tuple<TimeSpan, CancellationTokenSource, T>> Items)
        {
            foreach (var item in Items)
            {
                await Queue.SendAsync(item);
            }
        }

        public async Task CompleteAsync()
        {
            Queue.Complete();
            await Task.WhenAll(Consumers);
        }
        public async Task CancelAll()
        {
            BlockCTS.Token.ThrowIfCancellationRequested();
            //await Task.Delay(6000);
            BlockCTS.Cancel();
        }
    }
}
