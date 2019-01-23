module FunctionalQuotesApi.Infrastructure

open System.Collections.Concurrent
open System.Threading
open Microsoft.Extensions.Caching.Memory

type IMemoryCache with
    member m.TryGetValue2<'a>(key: obj): 'a option =
        let (isPresent, value) = m.TryGetValue<'a> key
        if isPresent then
            Some value
        else
            None

type Throttler() =
  let locks = ConcurrentDictionary<string, SemaphoreSlim>()
  
  member x.Run(throttlingKey, computation: Async<'a>) = async {
      let semaphore = locks.GetOrAdd(throttlingKey, fun _ -> SemaphoreSlim(1))
      do! (semaphore.WaitAsync()) |> Async.AwaitTask 
      try return! computation
      finally semaphore.Release() |> ignore
  }
