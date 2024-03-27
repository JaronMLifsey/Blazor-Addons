
class DialogModal {
    private dialog: HTMLDialogElement;

    private closeOnClickOutside: boolean = false;
    private closeOnEscape: boolean = false;
    private dotNetObject: any | null = null;

    private clickOutsideHanlder: any;
    private escapePressedHanlder: any;

    constructor(dialog: HTMLDialogElement, dotNetObject: any) {
        this.dialog = dialog;
        this.dotNetObject = dotNetObject;

        this.clickOutsideHanlder = this.handleClickOutside.bind(this);
        this.escapePressedHanlder = this.handleEscapePressed.bind(this);
    }

    private open(asModal: boolean) {
        document.addEventListener("click", this.clickOutsideHanlder);
        document.addEventListener("keyup", this.escapePressedHanlder);

        if (asModal) {
            this.dialog.showModal();
        }
        else {
            this.dialog.show();
        }
    }

    private close(notify: boolean) {
        document.removeEventListener("click", this.clickOutsideHanlder);
        document.removeEventListener("keyup", this.escapePressedHanlder);

        this.dialog.close();
        if (notify) {
            this.dotNetObject.invokeMethodAsync("Close");
        }
    }

    public handleEscapePressed(event: KeyboardEvent) {
        if (this.closeOnEscape && event.key !== "Escape") {
            return;
        }

        this.close(true);
        event.preventDefault();
    }

    public handleClickOutside(event: PointerEvent) {
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

    public async configurDialog(closeOnEscape: boolean, closeOnClickOutside: boolean, asModal: boolean, open: boolean) {
        this.closeOnEscape = closeOnEscape;
        this.closeOnClickOutside = closeOnClickOutside;


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