using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace BlazorFileUpload
{
    public partial class FileUpload : ComponentBase, IAsyncDisposable
    {
        [Inject]
        private IJSRuntime JsRuntime { get; set; } = default!;
        [Inject]
        private ILogger<FileUpload>? Logger { get; set; } = default!;

        [Parameter]
        public bool Multiple { get; set; } = false;

        [Parameter]
        public EventCallback<IEnumerable<FrontEndFile>> FilesChanged { get; set; }

        [Parameter]
        public EventCallback<FrontEndFile> FileAdded { get; set; }

        [Parameter]
        public EventCallback<FrontEndFile> FileDeleted { get; set; }

        private ElementReference? Input;
        private ElementReference? DropZone;
        private IJSObjectReference? Module;
        private IJSObjectReference? FileUploadJsObject;

        private List<FrontEndFile> Files = new();

        public FileUpload()
        {
            RenderHelper = new(this);
            ChildContent = DefaultRenderChildContent;
            InnerRender = DefaultRenderInner;
            FilesRender = DefaultRenderFiles;
            FileRender = DefaultRenderFile;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                Module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/BlazorFileUpload/FileUpload.js");
                FileUploadJsObject = await Module.InvokeAsync<IJSObjectReference>("CreateFileUploader", Input, DropZone, DotNetObjectReference.Create(this));
            }
        }

        [JSInvokable]
        public void OnFilesChanged(System.Text.Json.JsonElement[] files)
        {
            var newFiles = files.Select(x => new FrontEndFile(
                    manager: this,
                    fileName: x.GetProperty("FileName").GetString() ?? throw new Exception("Failed to parse JSON object"),
                    fileSizeBytes: x.GetProperty("FileSizeBytes").GetInt64(),
                    id: x.GetProperty("ID").GetInt32()
                )).ToList();
            Files.AddRange(newFiles);

            FilesChanged.InvokeAsync(Files);
            newFiles.Select(x => FileAdded.InvokeAsync(x));
        }

        internal FrontEndFileStream CreateStream(FrontEndFile file, Action<long>? progressListener, double reportFrequency, int maxMessageSize, long maxBuffer)
        {
            if (FileUploadJsObject == null)
            {
                throw new Exception("A stream cannot be created until after the first render.");
            }

            return new FrontEndFileStream(Logger, FileUploadJsObject, file, progressListener: progressListener, reportFrequency: reportFrequency, maxMessageSize: maxMessageSize, maxBuffer: maxBuffer);
        }

        public async Task DeleteFile(FrontEndFile file)
        {
            Files.Remove(file);
            await FileDeleted.InvokeAsync();
            StateHasChanged();
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (Module != null)
                {
                    await Module.DisposeAsync();
                }
                if (FileUploadJsObject != null)
                {
                    await FileUploadJsObject.DisposeAsync();
                }
            }
            catch (JSDisconnectedException ex)
            {
                // Ignore
            }
        }
    }
}
