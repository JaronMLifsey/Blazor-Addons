using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using static BlazorAddons.FrontEndFileStream;

namespace BlazorAddons
{
    public partial class FileUpload : ComponentBase, IAsyncDisposable
    {
        [Inject]
        private IJSRuntime JsRuntime { get; set; } = default!;

        [Inject]
        private ILogger<FileUpload>? Logger { get; set; } = default!;

        [Parameter]
        public List<FrontEndFile> Files { get; set; } = new();

        [Parameter]
        public EventCallback<List<FrontEndFile>> FilesChanged { get; set; }

        [Parameter]
        public EventCallback<FrontEndFile> FileAdded { get; set; }

        [Parameter]
        public EventCallback<FrontEndFile> FileDeleted { get; set; }

        [Parameter]
        public EventCallback<FrontEndFile> FileRenamed { get; set; }


        [Parameter]
        public Func<FrontEndFile, List<string>?>? FileValidation { get; set; }

        [Parameter]
        public Func<IReadOnlyList<FrontEndFile>?, List<string>>? Validation { get; set; }

        /// <summary>
        /// The minimum number of files which must be added or <see cref="MinimumFileCountError"/> will be displayed.
        /// </summary>
        [Parameter]
        public int MinimumFileCount { get; set; } = 0;

        /// <summary>
        /// The error message that will be displayed if <see cref="MinimumFileCount"/> is exceeded.
        /// If present, {0} will be replaced with <see cref="MinimumFileCount"/>.
        /// </summary>
        [Parameter]
        public string MinimumFileCountError { get; set; } = "At least {0} file(s) must be uploaded.";

        /// <summary>
        /// The maximum number of files which can be added before <see cref="MaximumFileCountError"/> will be displayed.
        /// If more than 1, the file selector which opens will allow adding multiple files.
        /// </summary>
        [Parameter]
        public int MaximumFileCount { get; set; } = 10;

        /// <summary>
        /// The error message that will be displayed if <see cref="MaximumFileCount"/> is exceeded.
        /// If present, {0} will be replaced with <see cref="MaximumFileCount"/>.
        /// </summary>
        [Parameter]
        public string MaximumFileCountError { get; set; } = "More files were added than the maximum of {0}.";

        /// <summary>
        /// The maximum number of files which can be added before <see cref="MaximumFileCountError"/> will be displayed.
        /// If more than 1, the file selector which opens will allow adding multiple files.
        /// </summary>
        [Parameter]
        public int MaximumFileSize { get; set; } = 10;

        /// <summary>
        /// The error message that will be displayed if <see cref="MaximumFileSize"/> is exceeded.
        /// If present, {0} will be replaced with the file name and {1} with <see cref="MaximumFileSize"/> formatted as a file size.
        /// </summary>
        [Parameter]
        public string MaximumFileSizeError { get; set; } = "{0}: Files cannot be greater than {1}.";

        /// <summary>
        /// This will be passed to the "accept" attribute of the file input. No validation is performed based on this.
        /// </summary>
        [Parameter]
        public string? AcceptedFiles { get; set; }

        /// <summary>
        /// True if, when <see cref="Validate"/> was last called, <see cref="Errors"/> was empty and every file in <see cref="Files"/> was without error.
        /// This is updated when files are added or deleted.
        /// </summary>
        public bool IsValid { get; internal set; } = true;

        /// <summary>
        /// The errors returned by <see cref="Validation"/>.
        /// Errors returned by <see cref="FileValidation"/> are in each individual file in 
        /// <see cref="Files"/> an can be enumerated through <see cref="FileErrors"/>.
        /// </summary>
        public IEnumerable<string> Errors => _Errors;
        public List<string> _Errors { get; set; } = new();

        /// <summary>
        /// An enumeration of the errors of all <see cref="Files"/>.
        /// </summary>
        public IEnumerable<string> FileErrors => Files.SelectMany(x => x.Errors);

        /// <summary>
        /// A concatenation of <see cref="Errors"/> with <see cref="FileErrors"/>.
        /// </summary>
        public IEnumerable<string> AllErrors => Errors.Concat(FileErrors);


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

        protected override void OnParametersSet()
        {
            if (Files.Any())
            {
                Validate();
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                Module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/BlazorAddons/FileUpload.js");
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
            bool allFilesValid = true;
            if (FileValidation != null)
            {
                foreach (var file in Files)
                {
                    file.Errors = FileValidation?.Invoke(file) ?? new();

                    if (file.FileSizeBytes > MaximumFileSize)
                    {
                        file.Errors.Add(string.Format(MaximumFileSizeError, file.RenamedFileName, FrontEndFile.BytesToString(MaximumFileSize)));
                    }

                    allFilesValid = allFilesValid && !file.Errors.Any();
                }
            }

            _Errors = new();

            if (Files.Count > MaximumFileCount)
            {
                _Errors.Add(string.Format(MaximumFileCountError, MaximumFileCount));
            }

            if (Files.Count < MinimumFileCount)
            {
                _Errors.Add(string.Format(MinimumFileCountError, MinimumFileCount));
            }

            var userErrors = Validation?.Invoke(Files);
            if (userErrors != null)
            {
                _Errors.AddRange(userErrors);
            }

            IsValid = allFilesValid && !_Errors.Any();
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
