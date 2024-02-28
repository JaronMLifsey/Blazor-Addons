namespace BlazorFileUpload
{
    public class FrontEndFile
    {
        private readonly FileUpload Manager;
        public readonly string FileName;
        /// <summary>
        /// This is the number of bytes which have been downloaded.
        /// It is automatically updated based on the progress of the stream returned 
        /// from the most recent call to <see cref="CreateStream(IProgress{long}?, double, int, long)"/>. 
        /// </summary>
        public readonly long FileSizeBytes;
        public long BytesDownloaded { get; private set; }
        /// <summary>
        /// Allows for keeping other relevant data with this file for custom rendering / functionality.
        /// </summary>
        public dynamic? ExtraData;

        private FrontEndFileStream? CurrentlyRunningStream = null;
        public bool IsDownloading => CurrentlyRunningStream != null;

        public Action<FrontEndFile, FrontEndFileStream>? OnStreamCreated;

        public Action<FrontEndFile>? OnDownloadProgressMade;

        internal readonly int ID;
        public FrontEndFile(FileUpload manager, string fileName, long fileSizeBytes, int id)
        {
            Manager = manager;
            FileName = fileName;
            FileSizeBytes = fileSizeBytes;
            ID = id;
        }

        public FrontEndFileStream CreateStream(Action<long>? progressListener = null, double reportFrequency = 0.01, int maxMessageSize = 1024 * 31, long maxBuffer = 1024 * 256) {
            var stream = Manager.CreateStream(this, progressListener, reportFrequency, maxMessageSize, maxBuffer);
            OnStreamCreated?.Invoke(this, stream);
            if (CurrentlyRunningStream != null)
            {
                CurrentlyRunningStream.OnDownloadProgress -= UpdateDownloadProgress;
            }
            stream.OnDownloadProgress += UpdateDownloadProgress;
            BytesDownloaded = 0;
            CurrentlyRunningStream = stream;
            return stream;
        }

        public async Task<byte[]> GetAllContents(Action<long>? progressListener = null, double reportFrequency = 0.01, int maxMessageSize = 1024 * 31, long maxBuffer = 1024 * 256)
        {
            using var stream = CreateStream(progressListener, reportFrequency, maxMessageSize, maxBuffer);
            using var memoryStream = new MemoryStream();

            await stream.CopyToAsync(memoryStream);

            return memoryStream.ToArray();
        }

        private void UpdateDownloadProgress(long downloadedBytes)
        {
            BytesDownloaded = downloadedBytes;
            if (BytesDownloaded == FileSizeBytes && CurrentlyRunningStream != null)
            {
                CurrentlyRunningStream.OnDownloadProgress -= UpdateDownloadProgress;
                CurrentlyRunningStream = null;
            }

            OnDownloadProgressMade?.Invoke(this);
        }
    }
}