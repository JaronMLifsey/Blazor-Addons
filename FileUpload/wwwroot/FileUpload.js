class FileUploader {
    constructor() {
        this.FileIdCounter = 0;
        this.FileMap = new Map();
        this.Reader = null;
    }
    async init(element, dotNetObject) {
        this.Element = element;
        this.DotNetObject = dotNetObject;
        this.Element.addEventListener("change", this.handleFiles.bind(this), false);
        await this.handleFiles(null);
    }
    async ReadFile(fileId, maxBytes) {
        let fileInfo = this.FileMap.get(fileId);
        if (fileInfo.reader == null) {
            fileInfo.reader = fileInfo.file.stream().getReader();
        }
        function GetDataSlice() {
            if (fileInfo.chunk.length - fileInfo.chunkOffset > maxBytes) {
                let slice = fileInfo.chunk.slice(fileInfo.chunkOffset, maxBytes);
                fileInfo.chunkOffset += maxBytes;
                return slice;
            }
            else {
                let slice = fileInfo.chunk.slice(fileInfo.chunkOffset, fileInfo.chunk.length - fileInfo.chunkOffset);
                fileInfo.chunkOffset = 0;
                fileInfo.chunk = null;
                return slice;
            }
        }
        if (fileInfo.chunk != null) {
            return GetDataSlice();
        }
        let result = await fileInfo.reader.read();
        if (result.done) {
            return null;
        }
        var data = result.value;
        fileInfo.chunk = data;
        return GetDataSlice();
    }
    async handleFiles(_) {
        const fileList = this.Element.files;
        for (let file of fileList) {
            if (file.fileId == null) {
                file.fileId = this.FileIdCounter++;
                this.FileMap.set(file.fileId, { file: file, reader: null, chunk: null, chunkOffset: 0 });
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