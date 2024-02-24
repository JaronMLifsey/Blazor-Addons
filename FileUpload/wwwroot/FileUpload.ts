

declare global {
    interface File { fileId: number | undefined; }
}

interface FileInfo {
    file: File;
    chunk: Uint8Array | null;
    chunkOffset: number;
}

class FileStreamer {
    private TotalSent: number = 0;
    private TotalAcknowledged: number = 0;
    private DotNetStreamReceiver: any;
    private FileInfo: FileInfo;
    private MaxMessageSize: number;
    private MaxBufferSize: number;

    private StreamContinuationResolver: (value: void | PromiseLike<void>) => void | null = null;

    public constructor(dotNetStreamReceiver: any, fileInfo: FileInfo, maxMessageSize: number, maxBufferSize: number) {
        this.DotNetStreamReceiver = dotNetStreamReceiver;
        this.FileInfo = fileInfo;
        this.MaxMessageSize = maxMessageSize;
        this.MaxBufferSize = maxBufferSize;
    }
    
    public GetDataSlice(): Uint8Array {
        if (this.FileInfo.chunk.length - this.FileInfo.chunkOffset > this.MaxMessageSize) {
            let slice = this.FileInfo.chunk.slice(this.FileInfo.chunkOffset, this.FileInfo.chunkOffset + this.MaxMessageSize);
            this.FileInfo.chunkOffset += this.MaxMessageSize;
            return slice;
        }
        else {
            let slice = this.FileInfo.chunk.slice(this.FileInfo.chunkOffset, this.FileInfo.chunk.length);
            this.FileInfo.chunkOffset = 0;
            this.FileInfo.chunk = null;
            return slice;
        }
    }

    public async ReadFile(reader: ReadableStreamDefaultReader, maxBytes: number): Promise<Uint8Array> {

        if (this.FileInfo.chunk != null) {
            return this.GetDataSlice();
        }

        let result = await reader.read();
        if (result.done) {
            return null;
        }

        var data: Uint8Array = result.value;
        this.FileInfo.chunk = data;
        return this.GetDataSlice();
    }

    public async StreamFile() {
        let reader = this.FileInfo.file.stream().getReader();

        this.TotalSent = 0;
        let remainingToSend = this.FileInfo.file.size;
        let data: Uint8Array;
        while ((data = await this.ReadFile(reader, this.MaxMessageSize)) != null) {
            if (this.TotalSent - this.TotalAcknowledged + data.length > this.MaxBufferSize) {
                let blocker = new Promise<void>(resolver => this.StreamContinuationResolver = resolver);
                await blocker;
            }
            this.DotNetStreamReceiver.invokeMethodAsync('ReceiveData', data, this.TotalSent);
            this.TotalSent += data.length;
            remainingToSend -= data.length;
        }

        //Let .NET know there's no more data.
        this.DotNetStreamReceiver.invokeMethodAsync('ReceiveData', null, this.TotalSent);
    }

    public Acknowledge(totalReceived: number) {
        this.TotalAcknowledged = totalReceived;

        if (this.TotalSent - this.TotalAcknowledged + this.MaxMessageSize < this.MaxBufferSize) {
            this.StreamContinuationResolver();
        }
    }
}

class FileUploader {
    private Element: HTMLInputElement;
    private DotNetObject: any;
    private FileIdCounter = 0;
    private FileMap = new Map<number, FileInfo>();

    public async init(element: HTMLInputElement, dotNetObject: any) {
        this.Element = element;
        this.DotNetObject = dotNetObject;
        this.Element.addEventListener("change", this.handleFiles.bind(this), false);
        await this.handleFiles(null);
    }

    public async CreateStream(dotNetObj: any, fileId: number, maxMessageSize: number, maxBufferSize: number) {
        let fileInfo = this.FileMap.get(fileId);
        if (fileInfo == null) {
            return null;
        }
        return new FileStreamer(dotNetObj, fileInfo, maxMessageSize, maxBufferSize);
    }

    public async handleFiles(_: Event | null) {
        const fileList = this.Element.files;
        for (let file of fileList) {
            if (file.fileId == null) {
                file.fileId = this.FileIdCounter++;
                this.FileMap.set(file.fileId, { file: file, chunk: null, chunkOffset: 0});
            }
        }
        await this.DotNetObject.invokeMethodAsync(
            'OnFilesChanged',
            Array.from(this.FileMap.values())
                .map(fileInfo => fileInfo.file)
                .map(file => {
                    return {
                        FileName: file.name,
                        FileSizeBytes: file.size,
                        ID: file.fileId,
                    };
                })
        );
    }
}

async function CreateFileUploader(element: HTMLInputElement, dotNetObject: any){
    let uploader = new FileUploader();
    await uploader.init(element, dotNetObject);
    return uploader;
}

export { FileUploader, CreateFileUploader };