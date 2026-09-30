Only when the step names a file or a folder to delete: `delete file %x%`, `remove file 'a.txt'`, `delete the folder 'tmp'`. A bare `remove %x%` removes the variable (variable.remove), even when %x% holds a file — the file stays on disk.
Path — the file or folder to delete, as the step writes it.
