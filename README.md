# Blazor Addons
This repository holds components and utilities for use in Blazor projects.

## Components

### FileUpload
This component seeks to fix a fundamental flaw in the file upload provided by base Blazor - namely the inability to upload files in multiple batches. The built-in file upload component only allows uploading the most recent batch of files to be added by the user.

This component also has basic validation and file management (delete and rename). It was created to be very configurable, even to the point of replacing all the rendering. It also supports reporting download progress.
