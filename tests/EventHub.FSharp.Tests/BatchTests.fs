module EventHub.FSharp.Tests.Batch

open System
open Xunit
open FsCheck
open FsCheck.Xunit
open FsUnit.Xunit
open Utils

[<Property>]
let ``Events are sent in batches`` (numberOfEvents : NonNegativeInt) =
    let numberOfEvents = numberOfEvents.Get
    
    let mutable numberOfEventsAddedToBatch = ref 0
    let mutable numberOfSentBatches = ref 0
    
    let sut = createSystemUnderTest
                (CountNumberOfSentBatches numberOfSentBatches)
                CreateNewBatch
                (CountNumberOfEventsAddedToBatch numberOfEventsAddedToBatch)
    
    for _ in {1..numberOfEvents} do
        sut.send(createEvent()) |> Async.RunSynchronously |> ignore
        
    sut.flush() |> Async.RunSynchronously |> ignore
    
    let expectedNumberOfBatches = Math.Ceiling ((numberOfEvents |> float) / (maximumNumberOfEventsInBatch |> float)) |> int
    
    Assert.Equal(expectedNumberOfBatches, !numberOfSentBatches)
    
[<Fact>]
let ``When an event cannot be added to a batch, and therefore cannot be sent, an error is returned`` () =
    let mutable numberOfSentBatches = ref 0
    
    let sut = createSystemUnderTest
                (CountNumberOfSentBatches numberOfSentBatches)
                CreateNewBatch
                FailAddingToBatch

    let result =
        sut.send(createEvent())
        |> Async.RunSynchronously
        
    match result with
    | Error e ->
        e.Message
        |> should equal "Could not add event to batch - maybe it exceeds the maximum batch size?"
    | _ -> failwith "Expected an error"
    
    sut.send(createEvent()) |> Async.RunSynchronously |> ignore
    sut.flush() |> Async.RunSynchronously |> ignore
    
    Assert.Equal (0, !numberOfSentBatches)
    
[<Fact>]
let ``When an error occurs when adding to batch, then the batch is sent`` () =
    let mutable numberOfSentBatches = ref 0
    
    let sut = createSystemUnderTest
                (CountNumberOfSentBatches numberOfSentBatches)
                CreateNewBatch
                FailThenAddToBatch
    
    sut.send(createEvent()) |> Async.RunSynchronously |> ignore
        
    sut.send(createEvent()) |> Async.RunSynchronously |> ignore
    sut.flush() |> Async.RunSynchronously |> ignore
    
    Assert.Equal (2, !numberOfSentBatches)
    
[<Fact>]
let ``When an unexpected exception is raised when creating a batch, then subsequent send succeeds`` () =
    let mutable numberOfSentBatches = ref 0
    
    let sut = createSystemUnderTest
                (CountNumberOfSentBatches numberOfSentBatches)
                (RaiseThenCreateNewBatch (exn "if a strag (strag: nonhitchhiker) discovers that a hitchhiker has his towel with him, he will automatically assume that he is also in possession of: "))
                (AddToBatch)
        
    sut.send(createEvent()) |> Async.Ignore |> Async.RunSynchronously
            
    sut.send(createEvent()) |> Async.Ignore |> Async.RunSynchronously
    
    sut.flush() |> Async.Ignore |> Async.RunSynchronously
    
    Assert.Equal (1, !numberOfSentBatches)
    
[<Fact>]
let ``When an unexpected exception is raised when sending a batch, then subsequent send succeeds`` () =
    let mutable numberOfSentBatches = ref 0
    
    let sut = createSystemUnderTest
                (RaiseThenCountNumberOfSentBatches (numberOfSentBatches, exn "a toothbrush, washcloth, soap, tin of biscuits, flask, compass, map, ball of string, gnat spray, wet-weather gear, space suit etc., etc.")) 
                CreateNewBatch
                AddToBatch
        
    sut.send(createEvent()) |> Async.Ignore |> Async.RunSynchronously
    sut.flush() |> Async.Ignore |> Async.RunSynchronously
    
    sut.send(createEvent()) |> Async.Ignore |> Async.RunSynchronously
    sut.flush() |> Async.Ignore |> Async.RunSynchronously
    
    Assert.Equal (1, !numberOfSentBatches)
   