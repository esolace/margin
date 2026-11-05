"""A rebuild must never truncate a plugin file mapped by a running host."""
import mmap
import tempfile
from pathlib import Path
from build_gh2 import atomic_copy

with tempfile.TemporaryDirectory() as directory:
    folder = Path(directory)
    loaded, replacement = folder / 'loaded.rhp', folder / 'replacement.rhp'
    original = b'loaded assembly stays intact'
    loaded.write_bytes(original)
    replacement.write_bytes(b'new assembly')
    with loaded.open('rb') as stream, mmap.mmap(stream.fileno(), 0, access=mmap.ACCESS_READ) as mapped:
        try:
            atomic_copy(replacement, loaded)
        except PermissionError:
            # Windows can refuse replacing a loaded DLL. Refusing safely is valid.
            assert loaded.read_bytes() == original
        else:
            assert loaded.read_bytes() == b'new assembly'
        assert mapped[:] == original
    assert not list(folder.glob('.margin-*'))
print('PASS staging preserves a mapped plugin or refuses replacement safely')
