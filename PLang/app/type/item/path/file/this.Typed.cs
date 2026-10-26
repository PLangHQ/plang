using app.Utils;

namespace app.type.item.path.file;

public sealed partial class @this
{
    /// <summary>
    /// The typed form's math — the form a goal writes and a .pr holds: "/" between its parts on every OS (an OS
    /// separator in it reads as "/"). The OS form (<c>Absolute</c>) keeps the OS's own path functions; this form never
    /// uses them, so on Windows it never gains a "\".
    /// </summary>
    private readonly struct Typed(string text)
    {
        private readonly string _text = text.Replace(PathHelper.DirectorySeparatorChar, '/');

        /// <summary>The folder it is in — "data/file.txt" → "data", "/data/file.txt" → "/data", "/x" → "/"; a bare
        /// name's is the goal folder it resolves against, ".".</summary>
        internal string Parent
        {
            get
            {
                var trimmed = _text.Length > 1 ? _text.TrimEnd('/') : _text;
                var at = trimmed.LastIndexOf('/');
                return at < 0 ? "." : at == 0 ? "/" : trimmed[..at];
            }
        }

        /// <summary><paramref name="child"/> (never rooted: its path's Combine reads a rooted one as under it) under it;
        /// under "." (the goal folder) the child is itself.</summary>
        internal string Combine(string child)
        {
            var under = new Typed(child)._text;
            return _text is "" or "." ? under : _text.TrimEnd('/') + "/" + under;
        }

        /// <summary>Its last part's extension changed to <paramref name="extension"/> (with or without its dot).</summary>
        internal string Extension(string extension)
        {
            var dot = _text.LastIndexOf('.');
            var stem = dot > _text.LastIndexOf('/') ? _text[..dot] : _text;
            return stem + (extension.StartsWith('.') ? extension : "." + extension);
        }
    }
}
