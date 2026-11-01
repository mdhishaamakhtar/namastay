namespace Namastay.Data.Media

open System
open System.IO
open System.Net
open System.Security.Cryptography
open System.Threading
open System.Threading.Tasks
open Amazon.Runtime
open Amazon.S3
open Amazon.S3.Model
open Amazon.S3.Util

type MediaSettings = {
    Endpoint: string
    Bucket: string
    AccessKey: string
    SecretKey: string
    /// Local RustFS needs the bucket in the path. Railway Buckets take it in the host name.
    PathStyle: bool
}

type MediaStore = {
    Client: AmazonS3Client
    Bucket: string
}

type MediaFile = {
    Content: Stream
    ContentType: string
    Length: int64
}

[<RequireQualifiedAccess>]
module MediaStore =
    let private contentType (key: string) =
        match Path.GetExtension(key).ToLowerInvariant() with
        | ".webp" -> "image/webp"
        | ".avif" -> "image/avif"
        | ".jpg"
        | ".jpeg" -> "image/jpeg"
        | ".png" -> "image/png"
        | ".svg" -> "image/svg+xml"
        | _ -> "application/octet-stream"

    /// Any S3 store works here: RustFS locally, Railway Buckets in production.
    let create (settings: MediaSettings) : MediaStore =
        let config =
            AmazonS3Config(
                ServiceURL = settings.Endpoint,
                ForcePathStyle = settings.PathStyle,
                AuthenticationRegion = "auto",
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            )

        let credentials = BasicAWSCredentials(settings.AccessKey, settings.SecretKey)

        {
            Client = new AmazonS3Client(credentials, config)
            Bucket = settings.Bucket
        }

    let ensureBucket (store: MediaStore) : Task =
        task {
            let! exists = AmazonS3Util.DoesS3BucketExistV2Async(store.Client, store.Bucket)

            if not exists then
                let! _ = store.Client.PutBucketAsync(PutBucketRequest(BucketName = store.Bucket))
                ()
        }

    /// Returns None only for a file that really isn't there. Outages still raise.
    let tryGet (store: MediaStore) (key: string) : Task<MediaFile option> =
        task {
            try
                let! response = store.Client.GetObjectAsync(store.Bucket, key)

                return
                    Some {
                        Content = response.ResponseStream
                        ContentType = contentType key
                        Length = response.ContentLength
                    }
            with :? AmazonS3Exception as error when error.StatusCode = HttpStatusCode.NotFound ->
                return None
        }

    let private existingTags (store: MediaStore) =
        task {
            let tags = Collections.Generic.Dictionary<string, string>()
            let mutable token: string = null
            let mutable more = true

            while more do
                let! page =
                    store.Client.ListObjectsV2Async(
                        ListObjectsV2Request(BucketName = store.Bucket, ContinuationToken = token)
                    )

                for item in
                    page.S3Objects
                    |> Option.ofObj
                    |> Option.defaultValue (Collections.Generic.List()) do
                    tags[item.Key] <- item.ETag.Trim('"')

                more <- page.IsTruncated.GetValueOrDefault false
                token <- page.NextContinuationToken

            return tags
        }

    let private md5 (path: string) =
        Convert.ToHexString(MD5.HashData(File.ReadAllBytes path)).ToLowerInvariant()

    /// Removes what the bucket holds and `folder` doesn't, keeping a copy in `backup` first.
    let prune (store: MediaStore) (folder: string) (backup: string) : Task<int> =
        task {
            let! tags = existingTags store
            let isLocal (key: string) = File.Exists(Path.Combine(folder, key))
            let stale = tags.Keys |> Seq.filter (isLocal >> not) |> Seq.toList

            for key in stale do
                use! response = store.Client.GetObjectAsync(store.Bucket, key)
                let target = Path.Combine(backup, key)
                Directory.CreateDirectory(Path.GetDirectoryName target) |> ignore
                use output = File.Create target
                do! response.ResponseStream.CopyToAsync output
                let! _ = store.Client.DeleteObjectAsync(store.Bucket, key)
                ()

            return stale.Length
        }

    let private uploadsAtOnce = 16

    /// Uploads files that are new or changed, and returns how many it sent.
    let sync (store: MediaStore) (folder: string) : Task<int> =
        task {
            do! ensureBucket store
            let! tags = existingTags store
            let mutable sent = 0
            let files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
            let options = ParallelOptions(MaxDegreeOfParallelism = uploadsAtOnce)

            let upload (path: string) (cancel: CancellationToken) =
                ValueTask(
                    task {
                        let key = Path.GetRelativePath(folder, path).Replace('\\', '/')

                        let unchanged =
                            match tags.TryGetValue key with
                            | true, tag -> tag = md5 path
                            | _ -> false

                        if not unchanged then
                            let request =
                                PutObjectRequest(
                                    BucketName = store.Bucket,
                                    Key = key,
                                    FilePath = path,
                                    ContentType = contentType key
                                )

                            let! _ = store.Client.PutObjectAsync(request, cancel)
                            Interlocked.Increment &sent |> ignore
                    }
                    :> Task
                )

            do! Parallel.ForEachAsync(files, options, Func<_, _, _> upload)
            return sent
        }
