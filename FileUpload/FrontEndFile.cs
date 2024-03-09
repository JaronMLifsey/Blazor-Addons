using static BlazorFileUpload.FrontEndFileStream;

namespace BlazorFileUpload
{
    public class FrontEndFile
    {
        private readonly FileUpload? Manager;
        public readonly string FileName;
        /// <summary>
        /// Contains the name if the user renamed the file, or null if they didn't.
        /// </summary>
        public string? RenamedFileName;
        /// <summary>
        /// This is the number of bytes which have been downloaded.
        /// It is automatically updated based on the progress of the stream returned 
        /// from the most recent call to <see cref="CreateStream(IProgress{long}?, double, int, long)"/>. 
        /// </summary>
        public readonly long FileSizeBytes;

        /// <summary>
        /// The number of bytes which has been downloaded by the most recently created stream.
        /// </summary>
        public long BytesDownloaded { get; private set; }

        /// <summary>
        /// True if the most recently created stream for this file has completed. This is reset if a new stream is created.
        /// </summary>
        public bool DownloadCompleted { get; private set; } = false;

        /// <summary>
        /// True if this file is considered valid.
        /// </summary>
        public bool IsValid => !Errors.Any();

        /// <summary>
        /// The list of errors which were returned from <see cref="FileUpload.FileValidation"/>.
        /// </summary>
        public ICollection<string> Errors { get; internal set; } = new List<string>();

        /// <summary>
        /// Allows for keeping other relevant data with this file for custom rendering / functionality.
        /// </summary>
        public dynamic? ExtraData;

        private FrontEndFileStream? CurrentlyRunningStream = null;
        public bool IsDownloading => CurrentlyRunningStream != null;

        public Action<FrontEndFile, FrontEndFileStream>? OnStreamCreated;

        public DownloadProgressListener? OnDownloadProgressMade;

        public bool CanCreateStream => Manager != null;

        internal readonly int ID;

        internal FrontEndFile(FileUpload manager, string fileName, long fileSizeBytes, int id)
        {
            Manager = manager;
            FileName = fileName;
            FileSizeBytes = fileSizeBytes;
            ID = id;
        }

        public FrontEndFile(string fileName, long fileSizeBytes, int id)
        {
            Manager = null;
            FileName = fileName;
            FileSizeBytes = fileSizeBytes;
            ID = id;
        }

        /// <returns>A stream for the file's contents.</returns>
        /// <remarks>Only call this for files the user has uploaded this session (<see cref="CanCreateStream"/> is true), 
        /// not for files which were passed to <see cref="FileUpload.Files"/>.</remarks>
        /// <exception cref="InvalidOperationException">If this file is not one the user uploaded this session and is instead a file passed to <see cref="FileUpload.Files"/></exception>
        public FrontEndFileStream CreateStream(DownloadProgressListener? progressListener = null, double reportFrequency = 0.01, int maxMessageSize = 1024 * 31, long maxBuffer = 1024 * 256)
        {
            if (Manager == null)
            {
                throw new InvalidOperationException("A stream cannot be created on a file which the user has not uploaded this session.");
            }
            var stream = Manager.CreateStream(this, progressListener, reportFrequency, maxMessageSize, maxBuffer);
            OnStreamCreated?.Invoke(this, stream);
            if (CurrentlyRunningStream != null)
            {
                CurrentlyRunningStream.OnDownloadProgress -= UpdateDownloadProgress;
            }
            stream.OnDownloadProgress += UpdateDownloadProgress;
            BytesDownloaded = 0;
            DownloadCompleted = false;
            CurrentlyRunningStream = stream;
            return stream;
        }

        /// <returns>All the files contents.</returns>
        /// <remarks>Only call this for files the user has uploaded this session (<see cref="CanCreateStream"/> is true), 
        /// not for files which were passed to <see cref="FileUpload.Files"/>.</remarks>
        /// <exception cref="InvalidOperationException">If this file is not one the user uploaded this session and is instead a file passed to <see cref="FileUpload.Files"/></exception>
        public async Task<byte[]> GetAllContents(DownloadProgressListener? progressListener = null, double reportFrequency = 0.01, int maxMessageSize = 1024 * 31, long maxBuffer = 1024 * 256)
        {
            using var stream = CreateStream(progressListener, reportFrequency, maxMessageSize, maxBuffer);
            using var memoryStream = new MemoryStream();

            await stream.CopyToAsync(memoryStream);

            return memoryStream.ToArray();
        }

        private void UpdateDownloadProgress(long downloadedBytes, bool downloadComplete)
        {
            BytesDownloaded = downloadedBytes;
            DownloadCompleted = downloadComplete;

            if (downloadComplete && CurrentlyRunningStream != null)
            {
                CurrentlyRunningStream.OnDownloadProgress -= UpdateDownloadProgress;
                CurrentlyRunningStream = null;
            }

            OnDownloadProgressMade?.Invoke(downloadedBytes, downloadComplete);
        }

        public static String BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            if (byteCount == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }
    }
}