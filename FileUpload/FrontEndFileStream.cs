using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Threading.Tasks;

namespace BlazorFileUpload
{
    public class FrontEndFileStream : Stream
    {
        public record CopyProgress (long Coppied, long Total);
        private readonly FrontEndFile File;
        private readonly IProgress<CopyProgress>? ProgressListener;
        private readonly double ReportFrequency;
        private readonly int MaxMessageSize;
        private double LastReportedProgress = 0.0;
        long _Position;

        private readonly IJSObjectReference JsObject;

        public FrontEndFileStream(IJSObjectReference jsObject, FrontEndFile file, IProgress<CopyProgress>? progressListener, double reportFrequency = 0.01, int maxMessageSize = 1024 * 31)
        {
            JsObject = jsObject;
            File = file;
            ProgressListener = progressListener;
            ReportFrequency = reportFrequency;
            MaxMessageSize = maxMessageSize;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => File.FileSizeBytes;
        public override long Position { 
            get => _Position;
            set => throw new NotImplementedException("Seeking is not possible."); 
        }

        public override void Flush() { }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            //TODO split writes to buffer into chunks if count is > MaxMessageSize
            var bytes = await JsObject.InvokeAsync<byte[]?>("ReadFile", cancellationToken, File.ID, count);
            if (bytes == null)
            {
                return 0;
            }

            bytes.CopyTo(buffer, offset);
            _Position += bytes.Length;
            var completePercentage = (double)_Position / File.FileSizeBytes;

            if (ProgressListener != null && (_Position >= File.FileSizeBytes || Math.Abs(completePercentage - LastReportedProgress) >= ReportFrequency))
            {
                ProgressListener.Report(new CopyProgress(_Position, File.FileSizeBytes));
                LastReportedProgress = completePercentage;
            }
            return bytes.Length;
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
    }
}
