Only when the step names a file or a folder to delete: `delete file %x%`, `remove file 'a.txt'`, `delete the folder 'tmp'`. A bare `remove %x%` removes the variable (variable.remove), even when %x% holds a file — the file stays on disk.
Path — the file or folder to delete · say: `file '<path>'` · builder: as the step writes it
A file that isn't there is the NotFound error, never an option of delete: "ignore if not found", "if it exists" is the clause after it — `file.delete(Path="old.txt"); on.error(Key="NotFound", Ignore=true)`.
Recursive — delete a folder's contents too · say: `recursive`
Returns — the deleted path.
