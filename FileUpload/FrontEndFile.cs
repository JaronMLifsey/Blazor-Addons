using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using static BlazorFileUpload.FrontEndFileStream;

namespace BlazorFileUpload
{
    public class FrontEndFile
    {
        private readonly FileUpload Manager;
        public readonly string FileName;
        public readonly long FileSizeBytes;
        internal readonly int ID;
        public FrontEndFile(FileUpload manager, string fileName, long fileSizeBytes, int id)
        {
            Manager = manager;
            FileName = fileName;
            FileSizeBytes = fileSizeBytes;
            ID = id;
        }

        public FrontEndFileStream CreateStream(IProgress<long>? progressListener = null, double reportFrequency = 0.01, int maxMessageSize = 1024 * 32, long maxBuffer = 1024 * 256) => 
             Manager.CreateStream(this, progressListener, reportFrequency, maxMessageSize, maxBuffer);

        /// <param name="reportFrequency">How often to report progress in percentage -defaults to 0.01.</param>
        public async Task<byte[]> GetAllContents(IProgress<long>? progressListener = null, double reportFrequency = 0.01, int maxMessageSize = 1024 * 32, long maxBuffer = 1024 * 256)
        {
            var buffer = new byte[1024 * 4];
            using var stream = CreateStream(progressListener, reportFrequency, maxMessageSize, maxBuffer);
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