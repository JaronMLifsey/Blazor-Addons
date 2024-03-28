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
        public string? Class { get; set; }

        [Parameter]
        public RenderFragment? ChildContent { get; set; }

        [Parameter]
        public bool Open { get; set; } = false;
        public bool? _Open;

        /// <summary>
        /// The origin point of the containing stacking context which <see cref="Transform"/> will be based from. 
        /// (0.5, 0.5) is the center, (0, 0) is the top left and (1, 1) is the bottom right.
        /// Defaults to (0.5, 1) which is the bottom center of the containing context.
        /// </summary>
        /// <remarks>Does nothing if <see cref="AsModal"/> is true."/></remarks>
        [Parameter]
        public (double X, double Y) Origin { get; set; } = (0.5, 1);
        public (double X, double Y)? _Origin;

        /// <summary>
        /// The point on the dialog which will be placed on <see cref="Origin"/>.
        /// (0, 0) is the top left, (0.5, 0.5) is the center, and (1, 1) is the bottom right.
        /// Defaults to (0.5, 0) which is the top center of the dialog.
        /// </summary>
        /// <remarks>Does nothing if <see cref="AsModal"/> is true."/></remarks>
        [Parameter]
        public (double X, double Y) Transform { get; set; } = (0.5, 0);
        public (double X, double Y)? _Transform;

        /// <summary>
        /// The pixel offset of the dialog from where <see cref="Transform"/> and <see cref="Origin"/> would put the dialog.
        /// </summary>
        /// <remarks>Does nothing if <see cref="AsModal"/> is true."/></remarks>
        [Parameter]
        public (double X, double Y) OffsetPixels { get; set; } = (0, 0);
        public (double X, double Y)? _OffsetPixels;

        /// <summary>
        /// The minimum number of pixels between the edge of the screen and the dialog.
        /// </summary>
        /// <remarks>Does nothing if <see cref="AsModal"/> is true."/></remarks>
        [Parameter]
        public double ViewPortPadding { get; set; } = 20;
        public double? _ViewPortPadding;

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
                    || Origin != _Origin
                    || Transform != _Transform
                    || OffsetPixels != _OffsetPixels
                    || ViewPortPadding != _ViewPortPadding
                ))
            {
                _CloseOnClickEscape = CloseOnEscape;
                _CloseOnClickOutside = CloseOnClickOutside;
                _AsModal = AsModal;
                _Open = Open;
                _Origin = Origin;
                _Transform = Transform;
                _OffsetPixels = OffsetPixels;
                _ViewPortPadding = ViewPortPadding;

                await DialogJsObj.InvokeVoidAsync("configurDialog", CloseOnEscape, CloseOnClickOutside, AsModal, Open, Origin.X, Origin.Y, Transform.X, Transform.Y, OffsetPixels.X, OffsetPixels.Y, ViewPortPadding);
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
