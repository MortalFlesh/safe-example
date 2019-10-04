open System.IO
open System.Threading.Tasks

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open FSharp.Control.Tasks.V2
open Giraffe
open Saturn
open Shared

open Fable.Remoting.Server
open Fable.Remoting.Giraffe

open System.Collections.Concurrent

let tryGetEnv = System.Environment.GetEnvironmentVariable >> function null | "" -> None | x -> Some x

let publicPath = Path.GetFullPath "../Client/public"

let port =
    "SERVER_PORT"
    |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 8085us

let eventInternalStorage = new ConcurrentDictionary<int, Event>()

async {
    let mutable number = 0
    printfn "Generating events ..."

    while true do
        number <- number + 1

        printfn "New event %i" number
        let event = {
            CorrelationId = string number
            Type = sprintf "something_%s_happened" (if number % 2 = 0 then "even" else "odd")
        }

        eventInternalStorage.AddOrUpdate(number, event, fun _ oldEvent -> oldEvent)
        |> ignore

        do! Async.Sleep (2 * 1000)
}
|> Async.Start

let getEvents () =
    async {
        return eventInternalStorage.Values |> List.ofSeq
    }

let counterApi = {
    initialCounter = fun () -> async { return { Value = 42 } }
    loadEvents = getEvents
}

let webApp =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue counterApi
    |> Remoting.buildHttpHandler

let app = application {
    url ("http://0.0.0.0:" + port.ToString() + "/")
    use_router webApp
    memory_cache
    use_static publicPath
    use_gzip
}

run app
