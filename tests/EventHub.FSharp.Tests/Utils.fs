module EventHub.FSharp.Tests.Utils

open EventHub.FSharp
open System
open System.Threading
open Microsoft.Azure.EventHubs

type SendFunction
    = NoOp
    | CountNumberOfSentBatches of int ref
    | RaiseThenCountNumberOfSentBatches of int ref * exn
  
type CreateBatchFunction
    = CreateNewBatch
    | RaiseThenCreateNewBatch of exn
    
type TryAddToBatchFunction
    = CountNumberOfEventsAddedToBatch of int ref
    | AddToBatch
    | FailAddingToBatch
    | FailThenAddToBatch
    
let createEvent() = new EventData([||])

let createDataBatch () = new EventDataBatch(Int64.MaxValue)

let maximumNumberOfEventsInBatch = 10 // Allow 10 messages in a batch

let createSystemUnderTest
    (sendFunction : SendFunction)
    (createBatchFunction : CreateBatchFunction)
    (tryAddToBatchFunction : TryAddToBatchFunction) =
    let sendFunction =
        match sendFunction with
        | NoOp -> fun () -> async { return () }
        | CountNumberOfSentBatches numberOfSentBatches ->
            fun () -> async {
                Interlocked.Increment numberOfSentBatches |> ignore
                return () }
        | RaiseThenCountNumberOfSentBatches (numberOfSentBatches, e) ->
            let mutable isCalled = false
            fun () -> async {
                if isCalled
                then
                    Interlocked.Increment numberOfSentBatches |> ignore
                    return ()
                else
                    isCalled <- true
                    raise e }
        
    let createBatchFunction =
        match createBatchFunction with
        | CreateNewBatch -> (fun () -> createDataBatch())
        | RaiseThenCreateNewBatch e ->
            let mutable isCalled = false
            fun () ->
                if isCalled
                then createDataBatch()
                else
                    isCalled <- true
                    raise e
        
    let tryAddToBatchFunction =
        match tryAddToBatchFunction with
        | CountNumberOfEventsAddedToBatch numberOfEventsAddedToBatch ->
            (fun event (batch : EventDataBatch) ->
                if !numberOfEventsAddedToBatch = maximumNumberOfEventsInBatch then
                    Interlocked.Exchange (numberOfEventsAddedToBatch, 0) |> ignore
                    false
                else
                    batch.TryAdd event |> ignore
                    Interlocked.Increment numberOfEventsAddedToBatch |> ignore
                    true)
        | FailAddingToBatch -> (fun _ _ -> false)
        | AddToBatch -> (fun e b -> b.TryAdd e)
        | FailThenAddToBatch ->
            let mutable isCalled = false
            (fun event (batch : EventDataBatch) ->
                if isCalled
                then batch.TryAdd event
                else
                    isCalled <- true
                    false)
            
    new ThreadSafeEventHubClient
        { new EventHubClientWrapper with
            member x.send _ = sendFunction()
            member x.createBatch () = createBatchFunction()
            member x.tryAddEventToBatch e b = tryAddToBatchFunction e b }