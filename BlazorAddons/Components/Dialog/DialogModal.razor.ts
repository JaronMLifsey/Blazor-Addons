
class DialogModal {
    private dialog: HTMLDialogElement;

    private closeOnClickOutside: boolean = false;
    private closeOnEscape: boolean = false;
    private dotNetObject: any | null = null;
    private resizeObserver: ResizeObserver;
    private resizeObservedParent: Element | null = null;

    private clickOutsideHanlder: any;
    private escapePressedHanlder: any;
    private resizeHanlder: any;

    //The origin point within the nearest positioned ancestor of the dialog. (-1, -1) is mapped to the top left and (1, 1) to the bottom right.
    private origin: { x: number, y: number } = { x: 0, y: 0 };
    //The point within the dialog element which will be placed on the origin. (-1, -1) is mapped to the top left and (1, 1) to the bottom right.
    private transform: { x: number, y: number } = { x: 0, y: 0 };
    //An offset which is applied to the final position.
    private offset: { x: number, y: number } = { x: 0, y: 0 };

    private viewportPadding: number = 20;

    private currentPosition: { x: number, y: number } = { x: 0, y: 0 };

    constructor(dialog: HTMLDialogElement, dotNetObject: any) {
        this.dialog = dialog;
        this.dotNetObject = dotNetObject;

        this.clickOutsideHanlder = this.handleClickOutside.bind(this);
        this.escapePressedHanlder = this.handleEscapePressed.bind(this);
        this.resizeHanlder = this.positionDialog.bind(this);

        this.resizeObserver = new ResizeObserver((element) => {
            this.positionDialog();
        });
    }

    private open(asModal: boolean) {
        document.addEventListener("click", this.clickOutsideHanlder);
        document.addEventListener("keyup", this.escapePressedHanlder);

        this.currentPosition = { x: 0, y: 0 };

        if (asModal) {
            this.dialog.style.marginRight = "auto";
            this.dialog.style.marginLeft = "auto";
            this.dialog.style.top = null;
            this.dialog.style.left = "0px";
            this.dialog.style.right = "0px";
        }
        else {
            this.dialog.style.marginRight = null;
            this.dialog.style.marginLeft = null;
            this.dialog.style.top = "0px";
            this.dialog.style.left = "0px";
            this.dialog.style.right = null;
        }

        if (asModal) {
            this.dialog.showModal();
            this.resizeObservedParent = null;
        }
        else {
            this.dialog.show();
            window.addEventListener("resize", this.resizeHanlder);
            this.resizeObservedParent = this.dialog.parentElement;
            this.resizeObserver.observe(this.resizeObservedParent);
            this.positionDialog();
        }
    }

    private close(notify: boolean) {
        document.removeEventListener("click", this.clickOutsideHanlder);
        document.removeEventListener("keyup", this.escapePressedHanlder);
        window.removeEventListener("resize", this.resizeHanlder);

        if (this.resizeObservedParent != null) {
            this.resizeObserver.unobserve(this.resizeObservedParent);
            this.resizeObservedParent = null;
        }

        this.dialog.close();
        if (notify) {
            this.dotNetObject.invokeMethodAsync("Close");
        }
    }

    private handleEscapePressed(event: KeyboardEvent) {
        if (this.closeOnEscape && event.key !== "Escape") {
            return;
        }

        this.close(true);
        event.preventDefault();
    }

    private handleClickOutside(event: PointerEvent) {
        if (!this.closeOnClickOutside) {
            return;
        }

        const dialogDimensions = this.dialog.getBoundingClientRect()
        if (
            event.clientX < dialogDimensions.left ||
            event.clientX > dialogDimensions.right ||
            event.clientY < dialogDimensions.top ||
            event.clientY > dialogDimensions.bottom
        ) {
            this.close(true);
        }
    }

    private positionDialog() {
        //let container = this.dialog.offsetParent as HTMLElement;
        let container = this.dialog.parentElement as HTMLElement;

        if (container == null) {  
            return;
        }

        let containerBounds = container.getBoundingClientRect();
        let dialogBounds = this.dialog.getBoundingClientRect();

        //Get the reference points on the container and dialog element
        let originPoint = { x: containerBounds.left + this.origin.x * containerBounds.width, y: containerBounds.top + this.origin.y * containerBounds.height };
        let transformPoint = { x: dialogBounds.left + this.transform.x * dialogBounds.width, y: dialogBounds.top + this.transform.y * dialogBounds.height };

        //Get the offsets
        let offset = { x: this.offset.x +  originPoint.x - transformPoint.x, y: this.offset.y + originPoint.y - transformPoint.y };

        if (dialogBounds.left + offset.x - this.viewportPadding < 0) {
            offset.x = this.viewportPadding - dialogBounds.left;
        }
        if (dialogBounds.right + offset.x + this.viewportPadding > window.innerWidth) {
            offset.x = window.innerWidth - this.viewportPadding - dialogBounds.right;
        }

        offset.x += this.currentPosition.x;
        offset.y += this.currentPosition.y;

        this.currentPosition = offset;

        this.dialog.style.left = `${offset.x}px`;
        this.dialog.style.top = `${offset.y}px`;
    }

    public async configurDialog(
        closeOnEscape: boolean,
        closeOnClickOutside: boolean,
        asModal: boolean,
        open: boolean,
        originX: number,
        originY: number,
        transformX: number,
        transformY: number,
        offsetX: number,
        offsetY: number,
        viewportPadding: number
    ) {
        this.closeOnEscape = closeOnEscape;
        this.closeOnClickOutside = closeOnClickOutside;
        this.transform = { x: transformX, y: transformY };
        this.origin = { x: originX, y: originY };
        this.offset = { x: offsetX, y: offsetY };
        this.viewportPadding = viewportPadding;
        this.dialog.style.maxWidth = `calc(100vw - ${this.viewportPadding * 2}px)`;

        if (this.dialog.open != open) {
            if (open) {
                this.open(asModal);
            }
            else {
                this.close(false);
            }
        }
    }
}

function createDialogModal(dialog: HTMLDialogElement, dotNetObject: any) {
    return new DialogModal(dialog, dotNetObject);
}


export { DialogModal, createDialogModal };