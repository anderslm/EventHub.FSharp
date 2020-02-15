module EventHub.FSharp.Tests.Flush

open Utils
open Xunit
 
[<Fact>]
let ``A batch is sent when flushing`` () =
    let mutable numberOfSentBatches = ref 0

    let sut = createSystemUnderTest
                (CountNumberOfSentBatches numberOfSentBatches)
                CreateNewBatch
                AddToBatch
    
    sut.send(createEvent()) |> Async.Ignore |> Async.RunSynchronously
    sut.flush() |> Async.Ignore |> Async.RunSynchronously
    
    Assert.Equal(1, !numberOfSentBatches)
    
[<Fact>]
let ``Multiple batches are sent when flushing multiple times`` () =
    let mutable numberOfSentBatches = ref 0

    let sut = createSystemUnderTest
                (CountNumberOfSentBatches numberOfSentBatches)
                CreateNewBatch
                AddToBatch
    
    sut.send(createEvent()) |> Async.Ignore |> Async.RunSynchronously
    sut.flush() |> Async.Ignore |> Async.RunSynchronously
    
    sut.send(createEvent()) |> Async.Ignore |> Async.RunSynchronously
    sut.flush() |> Async.Ignore |> Async.RunSynchronously
    
    Assert.Equal(2, !numberOfSentBatches)
    
[<Fact>]
let ``No batch is sent when flushing no events`` () =
    let mutable numberOfSentBatches = ref 0
    
    let sut = createSystemUnderTest
                (CountNumberOfSentBatches numberOfSentBatches)
                CreateNewBatch
                AddToBatch
 
    sut.flush() |> Async.Ignore |> Async.RunSynchronously
    
    Assert.Equal(0, !numberOfSentBatches)

