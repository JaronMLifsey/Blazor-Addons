using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using static BlazorFileUpload.FontEndFileStream;

namespace BlazorFileUpload
{
    public class FrontEndFile
    {
        private readonly FileUpload Manager;
        public readonly string FileName;
        public readonly long FileSizeBytes;
        internal readonly int ID;
        public FrontEndFile(FileUpload manager, string fileName, long fileSizeBytes, int iD)
        {
            Manager = manager;
            FileName = fileName;
            FileSizeBytes = fileSizeBytes;
            ID = iD;
        }

        public async Task<Stream> CreateStream(IProgress<CopyProgress>? progressListener, double reportFrequency = 0.01) => 
            new FontEndFileStream(await Manager.CreateStream(this), FileSizeBytes, progressListener, reportFrequency);

        /// <param name="reportFrequency">How often to report progress in percentage -defaults to 0.01.</param>
        public async Task<byte[]> GetAllContents(IProgress<CopyProgress>? progressReporter = null, double reportFrequency = 0.01)
        {
            var buffer = new byte[1024 * 4];
            using var stream = await CreateStream(progressReporter, reportFrequency);
            using var memoryStream = new MemoryStream();
            int bytesRead = 0;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                memoryStream.Write(buffer, 0, bytesRead);
            }

            return memoryStream.ToArray();
        }
    }
}