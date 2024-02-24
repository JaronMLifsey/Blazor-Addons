class FileStreamer {
    constructor(dotNetStreamReceiver, fileInfo, maxMessageSize, maxBufferSize) {
        this.TotalSent = 0;
        this.TotalAcknowledged = 0;
        this.StreamContinuationResolver = null;
        this.DotNetStreamReceiver = dotNetStreamReceiver;
        this.FileInfo = fileInfo;
        this.MaxMessageSize = maxMessageSize;
        this.MaxBufferSize = maxBufferSize;
    }
    async ReadFile(reader, maxBytes) {
        function GetDataSlice() {
            if (this.FileInfo.chunk.length - this.FileInfo.chunkOffset > maxBytes) {
                let slice = this.FileInfo.chunk.slice(this.FileInfo.chunkOffset, this.FileInfo.chunkOffset + maxBytes);
                this.FileInfo.chunkOffset += maxBytes;
                return slice;
            }
            else {
                let slice = this.FileInfo.chunk.slice(this.FileInfo.chunkOffset, this.FileInfo.chunk.length);
                this.FileInfo.chunkOffset = 0;
                this.FileInfo.chunk = null;
                return slice;
            }
        }
        if (this.FileInfo.chunk != null) {
            return GetDataSlice();
        }
        let result = await reader.read();
        if (result.done) {
            return null;
        }
        var data = result.value;
        this.FileInfo.chunk = data;
        return GetDataSlice();
    }
    async StreamFile() {
        let reader = this.FileInfo.file.stream().getReader();
        this.TotalSent = 0;
        let remainingToSend = this.FileInfo.file.size;
        let data;
        while ((data = await this.ReadFile(reader, this.MaxMessageSize)) != null) {
            if (this.TotalSent - this.TotalAcknowledged + data.length > this.MaxBufferSize) {
                let blocker = new Promise(resolver => this.StreamContinuationResolver = resolver);
                await blocker;
            }
            this.DotNetStreamReceiver.invokeMethodAsync('ReceiveData', data, this.TotalSent);
            this.TotalSent += data.length;
            remainingToSend -= data.length;
        }
    }
    Acknowledge(totalReceived) {
        this.TotalAcknowledged = totalReceived;
        if (this.TotalSent - this.TotalAcknowledged > this.MaxBufferSize) {
            this.StreamContinuationResolver();
        }
    }
}
class FileUploader {
    constructor() {
        this.FileIdCounter = 0;
        this.FileMap = new Map();
    }
    async init(element, dotNetObject) {
        this.Element = element;
        this.DotNetObject = dotNetObject;
        this.Element.addEventListener("change", this.handleFiles.bind(this), false);
        await this.handleFiles(null);
    }
    async CreateStream(dotNetObj, fileId, maxMessageSize, maxBufferSize) {
        let fileInfo = this.FileMap.get(fileId);
        if (fileInfo == null) {
            return null;
        }
        return new FileStreamer(dotNetObj, fileInfo, maxMessageSize, maxBufferSize);
    }
    async handleFiles(_) {
        const fileList = this.Element.files;
        for (let file of fileList) {
            if (file.fileId == null) {
                file.fileId = this.FileIdCounter++;
                this.FileMap.set(file.fileId, { file: file, chunk: null, chunkOffset: 0 });
            }
        }
        await this.DotNetObject.invokeMethodAsync('OnFilesChanged', Array.from(fileList).map(file => {
            return {
                FileName: file.name,
                FileSizeBytes: file.size,
                ID: file.fileId,
            };
        }));
    }
}
async function CreateFileUploader(element, dotNetObject) {
    let uploader = new FileUploader();
    await uploader.init(element, dotNetObject);
    return uploader;
}
export { FileUploader, CreateFileUploader };
//# sourceMappingURL=FileUpload.js.map