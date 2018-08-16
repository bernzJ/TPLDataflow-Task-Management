# TPLDataflow-Task-Management
Create a queue of block encapsuled by throttling and custom consumer. Goal was to enqueue with a max level of parallelism and
timestamp for late-running tasks. Not sure what the EnsureOrdered property is for, the queue should already be FIFO.
This does require `System.Threading.Tasks.Dataflow`, which is can be installed by `NUGET`.

## Limitations:
The first block and last block(consumer) must have same in/out types.

## Basic Usage
First create your consumer function.
```csharp
 public async Task Consumer(string T)
  {

      Console.WriteLine(" {0} wearing shorts.", T);
      await Task.Delay(5000);

  }
```
.. Then populate few blocks, make sure the first and the last block have the same in/out types(native or custom).
```csharp
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
  
  BlockScheduler<string> blockScheduler = new BlockScheduler<string>(new Capsule<string, string>(FirstBlock, FourthBlock), Consumer, DateTime.UtcNow.TimeOfDay, 1, 1);
  for (int i = 0; i < 20; i++)
  {
      blockScheduler.SendAsync(i.ToString());

  }
```
