module EventHub.FSharp

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Azure.EventHubs

type private EventHubMessage =
    | PostEvent of EventData * AsyncReplyChannel<Async<Result<unit, exn>>>
    | Flush of AsyncReplyChannel<Async<Result<unit, exn>>>

type EventHubClientWrapper =
    abstract createBatch        : unit           -> EventDataBatch
    abstract send               : EventDataBatch -> Async<unit>
    abstract tryAddEventToBatch : EventData      -> EventDataBatch -> bool

let createEventHubClientWrapper (eventHubClient : EventHubClient) =
    { new EventHubClientWrapper with
        member x.createBatch () = eventHubClient.CreateBatch()
        member x.send (b : EventDataBatch) = eventHubClient.SendAsync(b) |> Async.AwaitTask
        member x.tryAddEventToBatch (e : EventData) (b : EventDataBatch) = b.TryAdd e }
    
/// Send events asynchronously to an EventHub with the specified connection string and name.
/// The events gets buffered internally and sent when the buffer is full.
type ThreadSafeEventHubClient (eventHubClient : EventHubClientWrapper) =
    let tryAddToNewBatch event : EventDataBatch option =
        let batch = eventHubClient.createBatch()

        let isAddedToNewBatch = eventHubClient.tryAddEventToBatch event batch

        if isAddedToNewBatch
        then Some batch
        else None

    let addToBatch batch event =
        let batch =
            batch
            |> Option.defaultWith (eventHubClient.createBatch)

        let isAddedToExistingBatch = eventHubClient.tryAddEventToBatch event batch

        if isAddedToExistingBatch then
            async { return () }, Some batch
        else
            let async = eventHubClient.send batch

            let newBatch = tryAddToNewBatch event

            if newBatch.IsSome then
                async, newBatch
            else
                raise (exn "Could not add event to batch - maybe it exceeds the maximum batch size?"),
                None

    let cts = new CancellationTokenSource()
    
    let eventAgent =
        MailboxProcessor<EventHubMessage>.Start
            ((fun inbox ->
                let dataBatch : EventDataBatch option = None
                
                let rec receiveLoop (batch : EventDataBatch option) = async {
                    let! message = inbox.Receive()
                    
                    let flush (channel : AsyncReplyChannel<Async<Result<unit, exn>>>) =
                        batch
                        |> Option.map (fun b -> async { do! eventHubClient.send b })
                        |> Option.defaultValue (async { return () })
                        |> (fun sendBatchAsync -> async {
                            try
                                do! sendBatchAsync
                                return Ok ()
                            with
                            | e -> return Error e
                        })
                        |> channel.Reply
                    
                    match message with
                    | PostEvent (event, channel) ->
                        let (addToBatchAsyncOrError, batch) =
                            try
                                let (addToBatchAsync, batch) = addToBatch batch event

                                (Choice1Of2 addToBatchAsync), batch
                            with
                            | e -> (Choice2Of2 e), batch

                        match addToBatchAsyncOrError with
                        | Choice1Of2 addToBatchAsync -> async {
                                try
                                    do! addToBatchAsync
                                    return Ok ()
                                with
                                | e -> return (Error e)
                            }
                        | Choice2Of2 error -> async { return Error error }
                        |> channel.Reply

                        return! receiveLoop batch
                    | Flush channel ->
                        flush channel

                        return! receiveLoop None
                }

                receiveLoop dataBatch)
            , cts.Token)

    member x.send (event : EventData) =
        eventAgent.PostAndReply (fun channel -> PostEvent (event, channel))

    member x.flush () = eventAgent.PostAndReply Flush
    
    interface IDisposable with
        member x.Dispose() =
            eventAgent.PostAndReply Flush |> Async.RunSynchronously |> ignore
            (eventAgent :> IDisposable).Dispose()
            cts.Cancel()
            cts.Dispose()
