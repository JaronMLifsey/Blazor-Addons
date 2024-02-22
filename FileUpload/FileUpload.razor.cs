using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Diagnostics;
using System.Linq;
using static BlazorFileUpload.FrontEndFileStream;

namespace BlazorFileUpload
{
    public partial class FileUpload : ComponentBase, IAsyncDisposable
    {
        [Inject]
        private IJSRuntime JsRuntime { get; set; } = default!;

        [Parameter]
        public bool Multiple { get; set; } = false;

        [Parameter]
        public EventCallback<IEnumerable<FrontEndFile>> FilesChanged { get; set; }

        [Parameter]
        public RenderFragment<IReadOnlyCollection<FrontEndFile>>? ChildContent { get; set; }

        private ElementReference Input;
        private IJSObjectReference Module = null!;
        private IJSObjectReference FileUploadJsObject = null!;

        private List<FrontEndFile> Files = new();

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                Module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/BlazorFileUpload/FileUpload.js");
                FileUploadJsObject = await Module.InvokeAsync<IJSObjectReference>("CreateFileUploader", Input, DotNetObjectReference.Create(this));
            }
        }

        [JSInvokable]
        public void OnFilesChanged(System.Text.Json.JsonElement[] files)
        {
            Files = files.Select(x => new FrontEndFile(
                manager: this,
                fileName: x.GetProperty("FileName").GetString() ?? throw new Exception("Failed to parse JSON object"),
                fileSizeBytes: x.GetProperty("FileSizeBytes").GetInt64(),
                id: x.GetProperty("ID").GetInt32()
            )).ToList();

            FilesChanged.InvokeAsync(Files);
        }

        internal FrontEndFileStream CreateStream(FrontEndFile file, IProgress<CopyProgress>? progressListener, double reportFrequency = 0.01, int maxMessageSize = 1024 * 31)
        {
            return new FrontEndFileStream(FileUploadJsObject, file, progressListener, reportFrequency);
        }

        public async ValueTask DisposeAsync()
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
    }
}
