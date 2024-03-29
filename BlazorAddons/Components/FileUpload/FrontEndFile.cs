﻿using static BlazorAddons.FrontEndFileStream;

namespace BlazorAddons
{
    public class FrontEndFile
    {
        private readonly FileUpload? Manager;

        /// <summary>
        /// The initial name of the file.
        /// </summary>
        public readonly string FileName;
        public readonly string FileNameExtension;
        public readonly string FileNameNoExtension;

        /// <summary>
        /// Contains the user-renamed file name. If the user never changed it, it will be the same as <see cref="FileName"/>.
        /// </summary>
        public string RenamedFileName
        {
            get => _RenamedFileName;
            set
            {
                if (_RenamedFileName != value)
                {
                    _RenamedFileName = value;
                    RenamedFileNameExtension = Path.GetExtension(value);
                    RenamedFileNameNoExtension = Path.GetFileNameWithoutExtension(value);
                }
            }
        }
        private string _RenamedFileName = default!;

        public string RenamedFileNameExtension { get; private set; } = default!;
        public string RenamedFileNameNoExtension { get; private set; } = default!;


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

        public bool IsUserAdded => Manager != null;

        internal readonly int ID;

        private FrontEndFile(string fileName)
        {
            FileName = fileName;
            FileNameExtension = Path.GetExtension(fileName);
            FileNameNoExtension = Path.GetFileNameWithoutExtension(fileName);
            RenamedFileName = fileName;
        }

        internal FrontEndFile(FileUpload manager, string fileName, long fileSizeBytes, int id) : this(fileName)
        {
            Manager = manager;
            FileSizeBytes = fileSizeBytes;
            ID = id;
        }

        public FrontEndFile(string fileName, long fileSizeBytes) : this(fileName)
        {
            Manager = null;
            FileSizeBytes = fileSizeBytes;
            ID = 0;
        }

        /// <summary>Creates a stream that can be used to read the data from the user-uploaded file. The stream does not support seeking.</summary>
        /// <returns>A stream of the file's contents.</returns>
        /// <remarks>Only call this for files the user has uploaded this session (<see cref="IsUserAdded"/> is true), 
        /// not for files which were passed to <see cref="FileUpload.Files"/>.</remarks>
        /// <param name="progressListener">This will be called when download progress is made on this stream.</param>
        /// <param name="reportFrequency">How often progress is reported as a percentage (0.01 is 1%).</param>
        /// <param name="maxMessageSize">The maximum message size to use when sending file data from JavaScript to C#.</param>
        /// <param name="maxBuffer">The maximum amount of data from JavaScript to buffer in the stream object before the JavaScript 
        /// side will stop sending data and wait for the buffered data to be read from the stream.</param>
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

        /// <returns>All the file's contents.</returns>
        /// <remarks>Only call this for files the user has uploaded this session (<see cref="IsUserAdded"/> is true), 
        /// not for files which were passed to <see cref="FileUpload.Files"/>.</remarks>
        /// <param name="progressListener">This will be called when download progress is made on this stream.</param>
        /// <param name="reportFrequency">How often progress is reported as a percentage (0.01 is 1%).</param>
        /// <param name="maxMessageSize">The maximum message size to use when sending file data from JavaScript to C#.</param>
        /// <param name="maxBuffer">The maximum amount of data from JavaScript to buffer in the stream object before the JavaScript 
        /// side will stop sending data and wait for the buffered data to be read from the stream.</param>
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

        /// <summary>
        /// Formats the passed bytes as a string. E.g., 1000 would be 1KB.
        /// </summary>
        /// <param name="bytes">The number of bytes.</param>
        /// <returns>A formatted string.</returns>
        public static string BytesToString(long bytes)
        {
            bytes = Math.Abs(bytes);
            string[] suffix = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            if (bytes == 0)
            {
                return "0" + suffix[0];
            }

            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes * 10, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(bytes) * num).ToString() + suffix[place];
        }
    }
}