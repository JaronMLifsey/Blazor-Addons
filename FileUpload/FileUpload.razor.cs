using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Diagnostics;

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
        private IJSObjectReference FileUploadJs = null!;

        private List<FrontEndFile> Files = new();

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                Module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/BlazorFileUpload/FileUpload.js");
                FileUploadJs = await Module.InvokeAsync<IJSObjectReference>("CreateFileUploader", Input, DotNetObjectReference.Create(this));
            }
        }

        [JSInvokable]
        public void OnFilesChanged(dynamic[] files)
        {
            Files = files.Select(x => new FrontEndFile(
                manager: this,
                fileName: x.FileName,
                fileSizeBytes: x.FileSizeBytes,
                iD: x.ID
            )).ToList();

            FilesChanged.InvokeAsync(Files);
        }
        private void UpdateHeading()
        {
            Debug.WriteLine("Test");
        }

        internal async Task<Stream> CreateStream(FrontEndFile file, long maxSize = 1024 * 1024 * 128)
        {
            var dataReference = await FileUploadJs.InvokeAsync<byte[]>("ReadFile");
            return await dataReference.OpenReadStreamAsync(maxAllowedSize: maxSize);
        }

        public void TestMethod()
        {
            Debug.WriteLine("Test");
        }

        public async ValueTask DisposeAsync()
        {
            if (Module != null)
            {
                await Module.DisposeAsync();
            }
            if (FileUploadJs != null)
            {
                await FileUploadJs.DisposeAsync();
            }
        }
    }
}
