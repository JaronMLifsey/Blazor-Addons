using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Threading.Tasks.Dataflow;

namespace BlazorAddons
{
    public class FrontEndFileStream : Stream
    {
        private readonly ILogger? Logger;
        private readonly FrontEndFile File;
        private double LastReportedProgress = 0.0;
        private long _Position;
        private long TotalReceived = 0;
        private long CurrentlyBuffered = 0;
        private BufferBlock<byte[]?> BufferQueue = new();
        private Memory<byte>? CurrentBuffer;
        private bool ReceivingComplete = false;

        public readonly int MaxMessageSize;
        public readonly long MaxBuffer;

        /// <summary>
        /// How often to report progress in percent downloaded (1.0 is 100%).
        /// </summary>
        public double ReportFrequency;

        public bool ReadingComplete { get; private set; } = false;

        public delegate void DownloadProgressListener(long bytesDownloaded, bool downloadComplete);

        /// <summary>
        /// Reports the progress of a download in bytes and whether the file was fully downloaded (true).
        /// </summary>
        public DownloadProgressListener? OnDownloadProgress;

        public bool ErrorFileNotAvailable { get; private set; } = false;
        public bool ErrorDisconnected { get; private set; } = false;

        public long BytesDownloaded => _Position;
        public double PercentDownloaded => (double)_Position / File.FileSizeBytes;

        public DotNetObjectReference<FrontEndFileStream>? ThisObjectReference { private set; get; }

        private readonly IJSObjectReference FileUploadJsObject;
        private IJSObjectReference? FileStreamerJsObject = null;

        public FrontEndFileStream(ILogger? logger, IJSObjectReference jsObject, FrontEndFile file, DownloadProgressListener? progressListener = null, double reportFrequency = 0.01, int maxMessageSize = 1024 * 32, long maxBuffer = 1024 * 256)
        {
            Logger = logger;
            FileUploadJsObject = jsObject;
            File = file;
            if (progressListener != null)
            {
                OnDownloadProgress += progressListener;
            }
            ReportFrequency = reportFrequency;
            MaxMessageSize = maxMessageSize;
            MaxBuffer = maxBuffer;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => File.FileSizeBytes;
        public override long Position
        {
            get => _Position;
            set => throw new NotImplementedException("Seeking is not possible.");
        }

        public override void Flush() { }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (ReadingComplete)
            {
                return 0;
            }

            if (ThisObjectReference == null)
            {
                ThisObjectReference = DotNetObjectReference.Create(this);

                try
                {
                    FileStreamerJsObject = await FileUploadJsObject.InvokeAsync<IJSObjectReference>("CreateStream", ThisObjectReference, File.ID, MaxMessageSize, MaxBuffer);
                }
                catch (Exception ex)
                {
                    Logger?.LogWarning("The following exception occurred when creating a JavaScript stream: " + ex.ToString());
                    ReadingComplete = true;
                    ErrorDisconnected = true;
                    return 0;
                }

                if (FileStreamerJsObject == null)//File not found.
                {
                    ReadingComplete = true;
                    ErrorFileNotAvailable = true;
                    return 0;
                }

                _ = FileStreamerJsObject.InvokeVoidAsync("StreamFile");
            }

            int written = 0;
            while (count > 0 && !ReadingComplete)
            {
                if (CurrentBuffer == null)
                {
                    var data = await BufferQueue.ReceiveAsync();
                    if (data != null)
                    {
                        CurrentlyBuffered -= data.Length;
                        _ = FileStreamerJsObject!.InvokeVoidAsync("Acknowledge", TotalReceived - CurrentlyBuffered);
                        CurrentBuffer = data;
                    }
                }

                if (CurrentBuffer == null)
                {
                    ReadingComplete = true;
                    break;
                }

                var bytesToCopy = Math.Min(CurrentBuffer.Value.Length, count);

                if (written + bytesToCopy > File.FileSizeBytes)
                {
                    //We're getting more data than expected. Don't proceed lest a malicious actor send endless data.
                    var message = $"Received more data than the maximum expected of {File.FileSizeBytes} bytes for file {File.FileName}.";
                    Logger?.LogError(message);
                    throw new Exception(message);
                }

                CurrentBuffer.Value.Slice(0, bytesToCopy).CopyTo(new Memory<byte>(buffer, offset, bytesToCopy));

                written += bytesToCopy;
                offset += bytesToCopy;
                count -= bytesToCopy;

                if (bytesToCopy == CurrentBuffer.Value.Length)
                {
                    CurrentBuffer = null;
                }
                else//This implies that bytesToCopy == count and so written will now == count
                {
                    CurrentBuffer = CurrentBuffer.Value.Slice(bytesToCopy);
                }

                _Position += bytesToCopy;
                var completePercentage = (double)_Position / File.FileSizeBytes;

                if (OnDownloadProgress != null && Math.Abs(completePercentage - LastReportedProgress) >= ReportFrequency)
                {
                    OnDownloadProgress(_Position, false);
                    LastReportedProgress = completePercentage;
                }
            }

            if (ReadingComplete)//Report final result.
            {
                OnDownloadProgress?.Invoke(_Position, true);
            }

            return written;
        }

        [JSInvokable]
        public void ReceiveData(byte[]? data, long totalSent)
        {
            if (totalSent != TotalReceived)
            {
                //JSInterop is single threaded so this should never happen.;
                var message = "Fundamental assumption proved false - Data received out of order.";
                Logger?.LogError(message);
                throw new Exception(message);
            }
            if (FileStreamerJsObject == null)
            {
                var message = "Data received before streaming was requested. This should not be possible.";
                Logger?.LogError(message);
                throw new Exception(message);
            }

            if (data == null)
            {
                if (!ReceivingComplete)
                {
                    BufferQueue.Post(null);
                    ReceivingComplete = true;
                }
                return;
            }

            if (CurrentlyBuffered + data.Length > MaxBuffer)
            {
                //Should be impossible if the program is working correctly, unless there's a malicious attack in which case kill the client connection.
                var message = "Too much data was received from JavaScript.";
                Logger?.LogError(message);
                throw new Exception(message);
            }

            CurrentlyBuffered += data.Length;
            TotalReceived += data.Length;
            BufferQueue.Post(data);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException("ReadAsync must be used instead.");

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _Position = offset; break;
                case SeekOrigin.Current:
                    _Position += offset; break;
                case SeekOrigin.End:
                    _Position = Length - offset; break;
            }
            return _Position;
        }
        public override void SetLength(long value) => throw new NotImplementedException("Only reading is possible.");
        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException("Only reading is possible.");

        public override async ValueTask DisposeAsync()
        {
            ThisObjectReference?.Dispose();
            if (FileStreamerJsObject != null)
            {
                await FileStreamerJsObject.DisposeAsync();
            }
            await base.DisposeAsync();
        }
    }
}
