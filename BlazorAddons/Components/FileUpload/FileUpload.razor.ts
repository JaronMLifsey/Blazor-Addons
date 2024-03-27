

declare global {
    interface File { fileId: number | undefined; }
}

interface FileInfo {
    file: File;
    chunk: Uint8Array | null;
    chunkOffset: number;
}

class FileStreamer {
    private totalSent: number = 0;
    private totalAcknowledged: number = 0;
    private dotNetStreamReceiver: any;
    private fileInfo: FileInfo;
    private maxMessageSize: number;
    private maxBufferSize: number;

    private streamContinuationResolver: (value: void | PromiseLike<void>) => void | null = null;

    public constructor(dotNetStreamReceiver: any, fileInfo: FileInfo, maxMessageSize: number, maxBufferSize: number) {
        this.dotNetStreamReceiver = dotNetStreamReceiver;
        this.fileInfo = fileInfo;
        this.maxMessageSize = maxMessageSize;
        this.maxBufferSize = maxBufferSize;
    }
    
    public getDataSlice(): Uint8Array {
        if (this.fileInfo.chunk.length - this.fileInfo.chunkOffset > this.maxMessageSize) {
            let slice = this.fileInfo.chunk.slice(this.fileInfo.chunkOffset, this.fileInfo.chunkOffset + this.maxMessageSize);
            this.fileInfo.chunkOffset += this.maxMessageSize;
            return slice;
        }
        else {
            let slice = this.fileInfo.chunk.slice(this.fileInfo.chunkOffset, this.fileInfo.chunk.length);
            this.fileInfo.chunkOffset = 0;
            this.fileInfo.chunk = null;
            return slice;
        }
    }

    public async geadFile(reader: ReadableStreamDefaultReader, maxBytes: number): Promise<Uint8Array> {

        if (this.fileInfo.chunk != null) {
            return this.getDataSlice();
        }

        let result = await reader.read();
        if (result.done) {
            return null;
        }

        var data: Uint8Array = result.value;
        this.fileInfo.chunk = data;
        return this.getDataSlice();
    }

    public async streamFile() {
        let reader = this.fileInfo.file.stream().getReader();

        this.totalSent = 0;
        let remainingToSend = this.fileInfo.file.size;
        let data: Uint8Array;
        while ((data = await this.geadFile(reader, this.maxMessageSize)) != null) {
            if (this.totalSent - this.totalAcknowledged + data.length > this.maxBufferSize) {
                let blocker = new Promise<void>(resolver => this.streamContinuationResolver = resolver);
                await blocker;
            }
            this.dotNetStreamReceiver.invokeMethodAsync('ReceiveData', data, this.totalSent);
            this.totalSent += data.length;
            remainingToSend -= data.length;
        }

        //Let .NET know there's no more data.
        this.dotNetStreamReceiver.invokeMethodAsync('ReceiveData', null, this.totalSent);
    }

    public acknowledge(totalReceived: number) {
        this.totalAcknowledged = totalReceived;

        if (this.totalSent - this.totalAcknowledged + this.maxMessageSize < this.maxBufferSize) {
            this.streamContinuationResolver();
        }
    }
}

class FileUploader {
    private Element: HTMLInputElement | null;
    private DropzoneElement: HTMLElement | null;
    private DotNetObject: any;
    private FileIdCounter = 0;
    private FileMap = new Map<number, FileInfo>();

    public async init(inputElement: HTMLInputElement | null, dropzoneElement: HTMLElement | null, dotNetObject: any) {
        this.Element = inputElement;
        this.DropzoneElement = dropzoneElement;
        this.DotNetObject = dotNetObject;
        this.Element?.addEventListener("change", () => {
            this.handleFiles([...this.Element.files]);
        });

        if (this.DropzoneElement != null) {
            this.DropzoneElement.ondragenter =
            this.DropzoneElement.ondragover =
                (event) => {
                    event.preventDefault();
                    event.stopPropagation();
                    this.DropzoneElement.classList.add("dragged-over");
                };

            this.DropzoneElement.ondragleave = (event) => {
                event.preventDefault();
                event.stopPropagation();
                this.DropzoneElement.classList.remove("dragged-over");
            };

            this.DropzoneElement.ondrop = (event: DragEvent) => {
                event.preventDefault();
                event.stopPropagation();
                this.DropzoneElement.classList.remove("dragged-over");

                if (event.dataTransfer.items) {
                    this.handleFiles([...event.dataTransfer.items].filter(x => x.kind === "file").map(x => x.getAsFile()));
                }
                else {
                    this.handleFiles([...event.dataTransfer.files]);
                }
            };
        }
    }

    public async createStream(dotNetObj: any, fileId: number, maxMessageSize: number, maxBufferSize: number) {
        let fileInfo = this.FileMap.get(fileId);
        if (fileInfo == null) {
            return null;
        }
        return new FileStreamer(dotNetObj, fileInfo, maxMessageSize, maxBufferSize);
    }

    public async handleFiles(fileList: File[]) {
        let fileInfoList: FileInfo[] = [];

        for (let file of fileList) {
            if (file.fileId == null) {
                file.fileId = this.FileIdCounter++;
                let fileInfo: FileInfo = { file: file, chunk: null, chunkOffset: 0 };
                fileInfoList.push(fileInfo);
                this.FileMap.set(file.fileId, fileInfo);
            }
        }
        await this.DotNetObject.invokeMethodAsync(
            'OnFilesChanged',
            fileInfoList
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

async function createFileUploader(inputElement: HTMLInputElement | null, dropzoneElement: HTMLElement | null, dotNetObject: any){
    let uploader = new FileUploader();
    await uploader.init(inputElement, dropzoneElement, dotNetObject);
    return uploader;
}

export { FileUploader, createFileUploader };