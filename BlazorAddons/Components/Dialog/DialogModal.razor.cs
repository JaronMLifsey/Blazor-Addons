using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorAddons
{
    public partial class DialogModal : ComponentBase, IAsyncDisposable
    {
        [Inject]
        private IJSRuntime JsRuntime { get; set; } = default!;

        [Parameter]
        public EventCallback<bool> OpenChanged { get; set; }

        [Parameter]
        public RenderFragment? ChildContent { get; set; }

        [Parameter]
        public bool Open { get; set; } = false;
        public bool? _Open;

        /// <summary>
        /// Displays the Dialog as a modal which overlays the rest of the page.
        /// </summary>
        [Parameter]
        public bool AsModal { get; set; } = false;
        public bool? _AsModal;

        [Parameter]
        public bool CloseOnClickOutside { get; set; } = false;
        public bool? _CloseOnClickOutside;

        [Parameter]
        public bool CloseOnEscape { get; set; } = false;
        public bool? _CloseOnClickEscape;

        private ElementReference? DialogElement;
        private IJSObjectReference? Module;
        private IJSObjectReference? DialogJsObj;
        private DotNetObjectReference<DialogModal>? ThisObjectReference;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (Module == null)
            {
                Module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/BlazorAddons/Components/Dialog/DialogModal.razor.js");
                ThisObjectReference = DotNetObjectReference.Create(this);
                DialogJsObj = await Module.InvokeAsync<IJSObjectReference>("createDialogModal", DialogElement, ThisObjectReference);
            }

            if (DialogElement != null 
                && ThisObjectReference != null
                && DialogJsObj != null
                && (
                    CloseOnEscape != _CloseOnClickEscape 
                    || CloseOnClickOutside != _CloseOnClickOutside
                    || AsModal != _AsModal 
                    || Open != _Open
                ))
            {
                _CloseOnClickEscape = CloseOnEscape;
                _CloseOnClickOutside = CloseOnClickOutside;
                _AsModal = AsModal;
                _Open = Open;

                await DialogJsObj.InvokeVoidAsync("configurDialog", CloseOnEscape, CloseOnClickOutside, AsModal, Open);
            }

        }

        [JSInvokable]
        public async Task Close()
        {
            if (Open == true)
            {
                Open = false;
                await OpenChanged.InvokeAsync(Open);
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (Module != null)
                {
                    await Module.DisposeAsync();
                }

                if (DialogJsObj != null)
                {
                    await DialogJsObj.DisposeAsync();
                }

                if (ThisObjectReference != null)
                {
                    ThisObjectReference.Dispose();
                }
            }
            catch (JSDisconnectedException _)
            {
                // Ignore
            }
        }
    }
}
