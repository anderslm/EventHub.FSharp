module EventHub.FSharp.Tests.Dispose

open System
open Xunit
open FsUnit.Xunit
open Utils

[<Fact>]
let ``When disposed, any remaining batch is sent`` () =
    let mutable numberOfSentBatches = ref 0

    let sut = createSystemUnderTest
                (CountNumberOfSentBatches numberOfSentBatches)
                CreateNewBatch
                AddToBatch
    
    sut.send(createEvent()) |> Async.Ignore |> Async.RunSynchronously
    
    (sut :> IDisposable).Dispose()
    
    Assert.Equal(1, !numberOfSentBatches)
    
[<Fact>]
let ``When disposed and trying to send or flush, then an exception is thrown`` () =
    let sut = createSystemUnderTest
                NoOp
                CreateNewBatch
                AddToBatch
    
    (sut :> IDisposable).Dispose()
    
    (fun () -> sut.send(createEvent()) |> Async.Ignore |> Async.RunSynchronously)
    |> should throw typeof<ObjectDisposedException>
    
    (fun () -> sut.flush() |> Async.Ignore |> Async.RunSynchronously)
    |> should throw typeof<ObjectDisposedException>
    
    