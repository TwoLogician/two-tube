// Learn more about F# at http://fsharp.org

open Argu
open System
open System.Linq
open YoutubeExplode
open YoutubeExplode.Videos.Streams
open System.IO

type Argument =
    | [<AltCommandLine("-a")>] Audio
    | [<AltCommandLine("-c")>] Channel of url:string
    | [<AltCommandLine("-o")>][<Mandatory>] Output of path:string
    | [<AltCommandLine("-v")>] Video of url:string

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Audio -> "Download as audio"
            | Channel -> "Download videos in Channel"
            | Output -> "Output path"
            | Video -> "Download the video"

let channelDownload url o a =
    async {
        let id = Channels.ChannelId(url)
        let youtube = YoutubeClient()
        let videos = youtube.Channels.GetUploadsAsync(id).GetAwaiter().GetResult() |> List.ofSeq
        videos |> List.iteri (fun i x ->
            async {
                try
                    let mutable extension = ".mp4"
                    let mutable info = Unchecked.defaultof<IStreamInfo>
                    let! manifest = youtube.Videos.Streams.GetManifestAsync(x.Id) |> Async.AwaitTask
                    if a then
                        info <- manifest.GetAudioOnly().OfType<IStreamInfo>().Where(fun x -> x.Container = Container.Mp4).WithHighestBitrate()
                        extension <- ".mp3"
                    else
                        info <- manifest.GetVideo().OfType<IStreamInfo>().Where(fun x -> x.Container = Container.Mp4).WithHighestBitrate()
                        extension <- ".mp4"
                    if not (isNull info) then
                            youtube.Videos.Streams.DownloadAsync(info, Path.Combine(o, x.Title + extension)) |> Async.AwaitTask |> ignore
                with
                    | ex -> printfn "%A" ex
                printfn "%A/%A" (i + 1) (videos |> List.length)
            } |> Async.RunSynchronously
        )
    }

let videoDownload url o a =
    async {
        let mutable extension = ".mp4"
        let id = Videos.VideoId(url)
        let mutable info = Unchecked.defaultof<IStreamInfo>
        let youtube = YoutubeClient()
        let! manifest = youtube.Videos.Streams.GetManifestAsync(id) |> Async.AwaitTask
        let! video = youtube.Videos.GetAsync(id) |> Async.AwaitTask
        if a then
            info <- manifest.GetAudioOnly().OfType<IStreamInfo>().Where(fun x -> x.Container = Container.Mp4).WithHighestBitrate()
            extension <- ".mp3"
        else
            info <- manifest.GetVideo().OfType<IStreamInfo>().Where(fun x -> x.Container = Container.Mp4).WithHighestBitrate()
            extension <- ".mp4"
        if not (isNull info) then
            youtube.Videos.Streams.DownloadAsync(info, Path.Combine(o, video.Title + extension)) |> Async.AwaitTask |> ignore
    }

[<EntryPoint>]
let main argv =
    let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
    let parser = ArgumentParser.Create<Argument>(programName = "ls", errorHandler = errorHandler)
    let results = parser.ParseCommandLine argv
    let audio = results.TryGetResult Audio
    let channel = results.TryGetResult Channel
    let output = results.TryGetResult Output
    let video = results.TryGetResult Video
    if channel.IsSome then
        channelDownload channel.Value output.Value audio.IsSome |> Async.RunSynchronously
    if video.IsSome then
        videoDownload video.Value output.Value audio.IsSome |> Async.RunSynchronously
    0 // return an integer exit code