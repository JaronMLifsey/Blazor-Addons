using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using static BlazorFileUpload.FrontEndFile;
using static BlazorFileUpload.FrontEndFileStream;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
        public List<FrontEndFile> Files { get; set; } = new();

        [Parameter]
        public EventCallback<List<FrontEndFile>> FilesChanged { get; set; }

        [Parameter]
        public EventCallback<FrontEndFile> FileAdded { get; set; }

        [Parameter]
        public EventCallback<FrontEndFile> FileDeleted { get; set; }


        [Parameter]
        public Func<FrontEndFile, List<string>?>? FileValidation { get; set; }

        [Parameter]
        public Func<IReadOnlyList<FrontEndFile>?, List<string>>? Validation { get; set; }

        /// <summary>
        /// True if, when <see cref="Validate"/> was last called, <see cref="Errors"/> was empty and every file in <see cref="Files"/> was without error.
        /// This is updated when files are added or deleted.
        /// </summary>
        public bool IsValid { get; internal set; } = true;

        /// <summary>
        /// The errors returned by <see cref="Validation"/>.
        /// Errors returned by <see cref="FileValidation"/> are in each individual file in <see cref="Files"/>.
        /// </summary>
        public IReadOnlyList<string> Errors => _Errors;
        public List<string> _Errors { get; set; }


        private ElementReference? Input;
        private ElementReference? DropZone;
        private IJSObjectReference? Module;
        private IJSObjectReference? FileUploadJsObject;

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
            if (!files.Any())
            {
                return;
            }

            List<FrontEndFile> newFiles = files.Select(x => new FrontEndFile(
                    manager: this,
                    fileName: x.GetProperty("FileName").GetString() ?? throw new Exception("Failed to parse JSON object"),
                    fileSizeBytes: x.GetProperty("FileSizeBytes").GetInt64(),
                    id: x.GetProperty("ID").GetInt32()
                )).ToList();

            Files = Files.Concat(newFiles).ToList();
            Validate();

            newFiles.ForEach(x => FileAdded.InvokeAsync(x));
            FilesChanged.InvokeAsync(Files);
        }

        internal FrontEndFileStream CreateStream(FrontEndFile file, DownloadProgressListener? progressListener, double reportFrequency, int maxMessageSize, long maxBuffer)
        {
            if (FileUploadJsObject == null)
            {
                throw new Exception("A stream cannot be created until after the first render.");
            }

            return new FrontEndFileStream(Logger, FileUploadJsObject, file, progressListener: progressListener, reportFrequency: reportFrequency, maxMessageSize: maxMessageSize, maxBuffer: maxBuffer);
        }

        public void Validate()
        {
            bool isValid = true;
            if (FileValidation != null)
            {
                foreach (var file in Files)
                {
                    file.Errors = FileValidation?.Invoke(file) ?? new();
                    isValid = isValid && !file.Errors.Any();
                }
            }

            _Errors = Validation?.Invoke(Files) ?? new();
            isValid = isValid && !_Errors.Any();

            IsValid = isValid;
        }

        public async Task DeleteFile(FrontEndFile file)
        {
            Files.Remove(file);
            Validate();
            await Task.WhenAll(
                FileDeleted.InvokeAsync(file),
                FilesChanged.InvokeAsync(Files)
            );
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
