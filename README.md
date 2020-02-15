# EventHub.FSharp
A little library that comes in handy when sending events to an Azure EventHub from multiple threads and don't wan't to handle concurrency and batching.
This library enables you to send events in batches in a thread-safe way.
## API
* __createEventHubClientWrapper__ `: EventHubClient -> EventHubClientWrapper`

    Creates an `EventHubClientWrapper` from an existing `EventHubClient`.
    The instance can then be used to create a `ThreadSafeEventHubClient`.

* __ThreadSafeEventHubClient__ `: EventHubClientWrapper -> ThreadSafeEventHubClient`

    Creates an instance of `ThreadSafeEventHubClient` which can:
    * __send__
    
        Adds the event to a batch. If the batch is full it is sent, and a new batch is created.
    * __flush__
    
        Sends the current batch even though it's not full.

## Example
We all love examples!

### Create a thread safe client
```f#
open EventHub.FSharp
open Microsoft.Azure.EventHubs

let eventHubClient =
    new ThreadSafeEventHubClient
        (createEventHubClientWrapper
             (EventHubClient.CreateFromConnectionString("...")))

```

### Send a million events in parallel
```f#
seq { for i in 1 .. 1000000 do eventHubClient.send (new EventData(BitConverter.GetBytes(i))) }
|> Async.Parallel
|> Async.Ignore
|> Async.RunSynchronously

eventHubClient.flush()
|> Async.Ignore
|> Async.RunSynchronously 
```

### Use result
```f#
let result = eventHubClient.send (new EventData([||])) |> Async.RunSynchronously
match result with
| Ok _ -> Console.WriteLine "All good"
| Error e -> Console.WriteLine (sprintf "Horrible error: %s" e.Message)
```
