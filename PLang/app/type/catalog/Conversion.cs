using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using app.data;

namespace app.type.catalog;

/// <summary>
/// Conversion partial of <see cref="@this"/>. The public surface is the infra dispatch
/// door <see cref="Convert(object?, System.Type, actor.context.@this?, string?)"/>; the
/// shared dispatcher (<c>TryConvert</c>) + its helpers stay <c>internal static</c> — the
/// type-agnostic plumbing + residual primitive leaf both doors share. Per-type construction
/// knowledge lives on the owning types (their <c>Convert</c> hooks), reached here via the
/// hook dispatch; this file holds no per-type arms. Pure-logic helpers stay
/// <c>private static</c> (Rule C stateless behaviour).
/// </summary>
public sealed partial class @this
{
    /// <summary>
    /// Local case-insensitive read options for the conversion path. Stage 27 dispersed
    /// the former <c>Utils.Json.CaseInsensitiveRead</c> static — http/code/Default holds
    /// its own copy too. Per-consumer ownership keeps Rule C closed without inventing a
    /// shared "Json" god-bag.
    ///
    /// Exposed via <see cref="CaseInsensitiveRead"/> (<c>internal</c>) so the test
    /// facade <c>App.Utils.Json.CaseInsensitiveRead</c> routes here instead of forking
    /// a fourth copy. Adding a converter here updates both this conversion path and the
    /// test surface in one place; <c>http/code/Default</c>'s separate copy stays
    /// independent (different consumer, different concern).
    /// </summary>
    // The shared read options live on the serializer (json.Options.Read); the
    // context-less form is cached here as the read side's hot default.
    internal static readonly JsonSerializerOptions _caseInsensitiveRead =
        global::app.channel.serializer.json.Options.Read();

    /// <summary>
    /// Context-bound read options — <see cref="_caseInsensitiveRead"/> with the
    /// Context-wired path adapter, so every <see cref="app.type.item.path.@this"/>
    /// field in the deserialized graph lands fully Context-wired. Same factory the
    /// dict's own record reconstruction uses — one converter set, no drift.
    /// </summary>
    private static JsonSerializerOptions ContextualReadOptions(actor.context.@this context)
        => global::app.channel.serializer.json.Options.Read(context);

    /// <summary>
    /// Read options for a <c>goal</c> — same as <see cref="ContextualReadOptions"/>
    /// but with the <see cref="global::app.data.Wire"/> in template mode. A goal is
    /// developer-authored code (it is what <c>application/plang-goal</c> names), so
    /// its step params' <c>%ref%</c> holes born as live templates as they are read.
    /// The mode rides the goal type — not a read-path flag, not the file extension —
    /// and runtime data, never being a goal, is never read through here, so a forged
    /// <c>%secret%</c> in a message stays literal.
    /// </summary>
    [System.Obsolete("The goal .pr read moves to the format-agnostic reflection reader — do not add new callers.")]
    internal static JsonSerializerOptions GoalReadOptions(actor.context.@this context)
        // A goal's nested Data step-params are reconstruction — skip verify (covered by the
        // goal's own signature when it has one); they have no actor of their own.
        => global::app.data.Wire.ReadOptions(
            new global::app.type.reader.ReadContext(context, "plang", global::app.View.Store, Verify: false));

    /// <summary>Internal accessor for the test facade — see <see cref="_caseInsensitiveRead"/>.</summary>
    internal static JsonSerializerOptions CaseInsensitiveRead => _caseInsensitiveRead;

    /// <summary>
    /// Infra dispatch door — used by callers that hold only a CLR target (Data.As&lt;T&gt;,
    /// wire reconstruct, the variable navigator, Sqlite, builder validators). Resolves the
    /// owning family from <paramref name="clrTarget"/>, asks its <c>Convert</c> hook, and
    /// falls through to the residual primitive leaf + plumbing in <see cref="TryConvert"/>.
    /// The primary door (<c>type.@this.Convert</c>) is for callers holding a PLang type entity.
    /// <paramref name="slot"/> names the binding target so an action-parameter bind failure
    /// reads naturally.
    /// </summary>
    public global::app.data.@this Convert(object? value, System.Type clrTarget,
        actor.context.@this context, string? slot = null)
    {
        var (result, error) = TryConvert(value, clrTarget, context, slot);
        return error != null ? context.Error(error) : context.Ok(result);
    }



    // (catalog.Populate — the lift-then-lower config loader — removed. The CLI convert-walk is
    //  app.setting.@this.Set(node, dict): public-setter gate + per-leaf TryConvert + composite descend.)

    /// <summary>
    /// Element-wise list conversion — the SINGLE home for it (JsonElement[], JsonArray,
    /// non-generic IList all route here). Converts each element to <paramref name="elementType"/>
    /// and AGGREGATES per-element failures into a <c>ListConversionFailed</c> carrying an
    /// <c>ErrorChain</c>. Never silently drops a failed element nor pollutes the typed list
    /// with an unconverted one — that swallow class is what made list&lt;T&gt; lie about its
    /// contents (codeanalyzer F1).
    /// </summary>

    [System.Obsolete("Superseded by Type.Create (list<T> builds its own elements) — do not add new callers.")]
    private static (object? Value, error.Error? Error) ConvertElementsInto(
        System.Type targetListType, System.Type elementType,
        System.Collections.IEnumerable elements, int count,
        System.Type sourceType, actor.context.@this context)
    {
        var targetList = (System.Collections.IList)System.Activator.CreateInstance(targetListType)!;
        var errors = new List<error.Error>();
        int i = 0;
        foreach (var elem in elements)
        {
            var (converted, itemError) = TryConvert(elem, elementType, context);
            if (itemError != null)
                errors.Add(new error.Error($"[{i}]: {itemError.Message}", "ElementConversionFailed", 400)
                    { FixSuggestion = itemError.FixSuggestion });
            else if (converted != null)
                targetList.Add(converted);
            i++;
        }
        if (errors.Count > 0)
        {
            var err = new error.Error(
                $"Failed converting {errors.Count}/{count} elements from {sourceType.Name} to {targetListType.Name}",
                "ListConversionFailed", 400) { FixSuggestion = $"Element type: {elementType.Name}" };
            foreach (var e in errors) err.ErrorChain.Add(e);
            return (targetList.Count > 0 ? targetList : null, err);
        }
        return (targetList, null);
    }

    /// <summary>
    /// Attempts to convert a value to the specified type.
    /// Returns the converted value and null error on success,
    /// or null value and an Error describing what went wrong.
    /// </summary>
    [System.Obsolete("Construction stages superseded by Type.Create; primitive lowering lives in item.Clr — do not add new callers.")]
    internal static (object? Value, error.Error? Error) TryConvert(object? value, System.Type targetType,
        actor.context.@this context, string? targetName = null)
    {
        if (value == null)
            return (targetType.IsValueType ? System.Activator.CreateInstance(targetType) : null, null);

        var sourceType = value.GetType();
        if (targetType.IsAssignableFrom(sourceType))
            return (value, null);

        // Born-typed: there is NO central wrapper→raw converter. A handler
        // reads the typed value and calls the TYPE'S OWN member (number.ToInt64,
        // text.Value, path's gated Absolute) at the .NET boundary that needs
        // raw — the type converts itself, checked, erroring honestly on loss.
        // (IConvertible implementors — number, text — still self-convert
        // through the Convert.ChangeType arm below, the standard protocol.)

        // data.@this is the universal value wrapper — any value can become Data.
        // A wire-shaped object ({value, type, ...}) IS a serialized Data, so
        // reconstruct it as a whole (value + type) rather than nesting the dict as a
        // Data value — nesting mislabels the type as `object` and loses the inner
        // value's real type, so sign and verify would hash different canonical shapes.
        if (targetType == typeof(data.@this) && value is not data.@this)
        {
            // A dict stays a dict inside the Data — don't unwrap to raw here, that would
            // lose the native value type. (A serialized Data is read back AS a Data through
            // the Wire's data reader, so it never reaches here as a @schema-marked dict.)
            return (new data.@this("", value, context: context), null);
        }

        // Every value owns its raw CLR projection (item.Clr): a scalar wrapper
        // yields its backing scalar (→ string/int/DateTime/…), dict/list decompose
        // to a raw Dictionary/List, null → C# null, and a domain value that IS its
        // own raw form (path/image/code) returns self (so it falls through to the
        // OwnerOf reconstruction below). Same-type / item / object slots were already
        // returned by IsAssignableFrom above. One unwrap at the leaf, no type-switch —
        // the value decides. The reconstruction arms below then apply unchanged.
        if (value is app.type.item.@this itemValue)
        {
            // LOWER: when the target is the value's OWN family (string for text, the
            // numeric tower for number, IList for list, …), the value lowers ITSELF —
            // terminal, the value owns its CLR projection. A DIFFERENT family (text→int,
            // text→datetime) is a CONVERT and falls through to the family hooks below.
            var (ownFamily, _) = global::app.type.convert.@this.OwnerOf(targetType);
            if (ownFamily != null && ownFamily.IsInstanceOfType(itemValue))
            {
                try { return (itemValue.Clr(targetType), null); }
                catch (System.Exception ex) when (ex is System.InvalidCastException or System.FormatException or System.OverflowException)
                { return (null, WithSlot(new error.Error(ex.Message, "TypeConversionFailed", 400), targetName)); }
            }
            var raw = itemValue.Clr<object>();
            if (!ReferenceEquals(raw, value))
                return TryConvert(raw, targetType, context, targetName);
        }

        // choice<T> target — the LLM emits the chosen option's NAME (a string); build
        // the typed choice from it. Also accepts the inner value (enum/named-set class)
        // by re-resolving its ToString name. A choice<T> source was caught above.
        // (choice<T> is built by its own discovered Convert hook via the OwnerOf/OfStatic
        // build arm below — no special arm here.)

        // (No list<T> arm — list<T> is built by list<T>.Create, a re-tag, via the live
        // As<list<T>>/Value<list<T>> path. Nothing in runtime feeds a list<T> target here.)

        // Handle nullable target types
        var underlying = System.Nullable.GetUnderlyingType(targetType);
        if (underlying != null)
            return TryConvert(value, underlying, context, targetName);

        // OBP: ask the owning type to build the value — the type knows its own
        // construction (number/text/datetime/duration/path/image/goal.call). When a
        // family owns the target it is authoritative: success returns the value, an
        // error surfaces (enriched with the slot name). A null result means the family
        // declined this value shape (e.g. image for raw bytes) — fall through to the
        // residual leaf + plumbing below.
        {
            var (family, kind) = global::app.type.convert.@this.OwnerOf(targetType);
            // An error value isn't a convertible payload — let it fall through to the residual
            // leaf, which keeps the original error primary and demotes the conversion failure
            // onto its chain (ErrorAsStringSlot). The family hook would otherwise surface its own
            // "cannot convert" error as primary, burying the real cause.
            if (family != null && value is not global::app.error.Error)
            {
                // With an App in scope use the instance dispatch; context-free (the Text
                // serializer's string deserialize) the scalar families' static Convert hook
                // still parses into the born-native wrapper.
                var owned = context != null
                    ? context.App.Type.Conversions.Of(family, value, kind, context)
                    : global::app.type.convert.@this.OfStatic(family, value, kind, null);
                if (owned != null)
                {
                    if (owned.Success)
                    {
                        var built = owned.Peek();
                        // The hook builds the PLang value; the door's postcondition is
                        // assignability to the asked-for target. A CLR target (TimeSpan,
                        // double, string) lowers through the wrapper's own Clr exit —
                        // the value converts itself, erroring honestly on loss.
                        if (built is global::app.type.item.@this wrapper && !targetType.IsInstanceOfType(built))
                        {
                            try { return (wrapper.Clr(targetType), null); }
                            catch (System.Exception ex) when (ex is System.InvalidCastException or System.NotSupportedException or System.FormatException or System.OverflowException)
                            {
                                return (null, WithSlot(new error.Error(ex.Message, "TypeConversionFailed", 400) { Exception = ex }, targetName));
                            }
                        }
                        return (built, null);
                    }
                    var hookErr = owned.Error as error.Error
                        ?? new error.Error(owned.Error!.Message, "TypeConversionFailed", 400);
                    return (null, WithSlot(hookErr, targetName));
                }
            }
        }

        // String → JsonNode: use ToJson() extension with fix-and-retry
        if (targetType == typeof(JsonNode) && value is string jsonNodeStr)
        {
            var (node, jsonError) = jsonNodeStr.ToJson();
            if (jsonError is error.Error err) return (null, err);
            return (node, null);
        }

        // A target type that owns its wire reconstruction — a static
        // `object? FromWire(string, string?)` — rebuilds from the raw string
        // itself (the snapshot / crypto.hash seam). Checked before the generic
        // JSON deserialize below, which would produce a broken object for types
        // whose state isn't plain public properties (snapshot's private section
        // tree). Kind is null here — FromWire types needing a kind carry it on
        // the type entity, not on this conversion path.
        if (value is string wireStr)
        {
            var reader = global::app.type.@this.WireReader(targetType);
            if (reader != null)
            {
                var built = reader.Invoke(null, new object?[] { wireStr, null });
                if (built != null) return (built, null);
            }
        }

        // String → complex type: try JSON deserialization before list handling
        // (e.g., file.read of .pr returns JSON string → Goal). Only when the string
        // actually looks like JSON ({ or [) — a bare word ("hello") is not a malformed
        // JSON document, it's a single element to wrap / an enum / a ctor arg, so it
        // falls through to those arms below instead of throwing 'h is an invalid start'.
        if (value is string jsonStr && !targetType.IsPrimitive && targetType != typeof(string)
            && jsonStr.TrimStart() is { Length: > 0 } trimmed && (trimmed[0] == '{' || trimmed[0] == '['))
        {
            // Context-bound options when the caller passed one — deserialised
            // Paths get path.Resolve(raw, context) treatment so they land Context-
            // wired. Falls back to the static stub-Path options otherwise.
            // A goal reads with templates on (it is authored code); everything else
            // reads literal.
            var readOpts = context == null ? _caseInsensitiveRead
                : targetType == typeof(global::app.goal.@this) ? GoalReadOptions(context)
                : ContextualReadOptions(context);
            try
            {
                var jsonResult = JsonSerializer.Deserialize(jsonStr, targetType, readOpts);
                if (jsonResult != null) return (jsonResult, null);
            }
            catch (System.Exception ex) when (ex is JsonException || ex is NotSupportedException || ex is ArgumentException)
            {
                // Single-object target but array source — retry as List<T>, take first.
                if (jsonStr.TrimStart().StartsWith('['))
                {
                    try
                    {
                        var listType = typeof(List<>).MakeGenericType(targetType);
                        if (JsonSerializer.Deserialize(jsonStr, listType, readOpts)
                                is System.Collections.IList { Count: > 0 } listResult)
                            return (listResult[0], null);
                    }
                    catch (System.Exception inner) when (inner is JsonException || inner is NotSupportedException || inner is ArgumentException) { }
                }
                // Otherwise terminal for the string→record path (the arms below are
                // list/ctor/enum, which a {…}→record never matches). Surface the real
                // cause as an Error — visible, and cheaper than letting it propagate. The
                // inner Wire throw names the bad slot; keep the exception for the stack.
                // Otherwise terminal for the string→record path — let the original
                // exception propagate to the .pr materialization boundary (source.Value),
                // which catches it as MaterializeFailed (naming the slot + reason). No
                // swallow, no Error that a later .Peek() would drop.
                throw;
            }
        }

        // (Native plang list<T> is converted by its own `Convert` hook, discovered via
        // convert.OwnerOf above — the list owns its construction; no special case here.)

        // List-like target: CLR List<T> or types inheriting List<T> (plang list<T> is handled
        // by its own Convert hook via OwnerOf above). Why not just let the STJ fallback below
        // own this? STJ deserializes a JSON *array* → List<T>, but it CANNOT:
        //   1. wrap a single scalar into a one-element list — `5 → List<int> = [5]`,
        //      `"foo" → List<string> = ["foo"]` (tests TryConvertTo_IntToListOfInt /
        //      _StringToListOfString) — a bare scalar isn't a JSON array, so STJ throws;
        //   2. handle a CLR IList / attribute-sourced sequence (test AutoTags, Discover_*,
        //      the file-list read) without a serialize-roundtrip that mishandles the shape.
        // Deleting this block and routing CLR List<T> through STJ broke 9 tests (verified
        // 2026-07-07). It also routes each element through the catalog (custom Convert hooks)
        // and aggregates per-element errors — not a reinvention of STJ, a superset it needs.
        var listElementType = GetListElementType(targetType);
        if (listElementType != null)
        {
            // JsonElement-array source — enumerate elements and convert each. Without
            // this, a JSON-roundtripped List (Variables.Set's deep-clone path) would
            // be treated as a single value and only the array's "wrapper" would land
            // in a single-element list.
            // All three element-bearing sources go through ONE aggregating helper — a
            // failed element surfaces a ListConversionFailed (per-element ErrorChain),
            // never a silent drop (the JSON-array arms used to drop) or a pollution.
            if (value is JsonElement jeArr && jeArr.ValueKind == JsonValueKind.Array)
                return ConvertElementsInto(targetType, listElementType, jeArr.EnumerateArray(), jeArr.GetArrayLength(), sourceType, context);

            // JsonArray implements IList<JsonNode?> but NOT the non-generic IList, so it
            // skips the generic-list arm below — handle it here.
            if (value is JsonArray jArr)
                return ConvertElementsInto(targetType, listElementType, jArr, jArr.Count, sourceType, context);

            if (value is System.Collections.IList sourceList)
                return ConvertElementsInto(targetType, listElementType, sourceList, sourceList.Count, sourceType, context);

            // The value IS a collection, but none of the arms above recognized it
            // (e.g. a generic-only IList<T>/IEnumerable<T> that isn't the non-generic
            // IList — a domain collection like step.actions.@this). Falling through to
            // the single-element wrap below would silently bury the whole collection in
            // one slot and hide the gap. Throw loudly so it surfaces and the list
            // converter grows a real arm for this shape instead of masking it.
            if (value is System.Collections.IEnumerable && value is not string)
                throw new System.InvalidOperationException(
                    $"List-conversion gap: cannot convert {sourceType.FullName} into {targetType.FullName}. " +
                    "It is a collection the list converter doesn't recognize (likely a generic-only IList<T>); " +
                    "add an arm for it rather than coercing the whole collection into a single element.");

            if (listElementType.IsAssignableFrom(sourceType))
            {
                var list = (System.Collections.IList)System.Activator.CreateInstance(targetType)!;
                list.Add(value);
                return (list, null);
            }
            var (converted, convError) = TryConvert(value, listElementType, context);
            if (converted != null && listElementType.IsAssignableFrom(converted.GetType()))
            {
                var list = (System.Collections.IList)System.Activator.CreateInstance(targetType)!;
                list.Add(converted);
                return (list, null);
            }
            if (convError != null)
                return (null, convError);
        }

        // (string → path and string → path-backed reference fundamental (image) are
        // owned by path.Convert / image.Convert, dispatched via the hook above.)

        // Types with a constructor that accepts a single string (may have optional params).
        if (value is string ctorStr)
        {
            var ctor = targetType.GetConstructors()
                .FirstOrDefault(c =>
                {
                    var ps = c.GetParameters();
                    return ps.Length >= 1
                        && ps[0].ParameterType == typeof(string)
                        && ps.Skip(1).All(p => p.IsOptional);
                });
            if (ctor != null)
            {
                try
                {
                    var ps = ctor.GetParameters();
                    var args = new object?[ps.Length];
                    args[0] = ctorStr;
                    for (int ci = 1; ci < ps.Length; ci++)
                    {
                        if (context != null && ps[ci].ParameterType == typeof(actor.context.@this))
                            args[ci] = context;
                        else
                            args[ci] = ps[ci].DefaultValue;
                    }
                    return (ctor.Invoke(args), null);
                }
                catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
                {
                    return (null, new error.Error(ex.InnerException?.Message ?? ex.Message, "ConstructorFailed", 400));
                }
            }
        }

        // (string → TimeSpan is owned by duration.Convert, dispatched via the hook above.)

        // Enum types
        if (targetType.IsEnum)
        {
            if (value is string s)
            {
                if (System.Enum.TryParse(targetType, s, ignoreCase: true, out var parsed))
                    return (parsed, null);
                return (null, new error.Error(
                    $"Cannot parse '{s}' as {targetType.Name}",
                    "EnumParseFailed", 400)
                    { FixSuggestion = $"Valid values: {string.Join(", ", System.Enum.GetNames(targetType))}" });
            }
            if (value.GetType().IsEnum)
                return (value, null);
            try { return (System.Enum.ToObject(targetType, value), null); }
            catch (System.ArgumentException) { return (null, new error.Error(
                $"Cannot convert {sourceType.Name} to enum {targetType.Name}",
                "EnumConversionFailed", 400)); }
        }

        // (string/JsonElement/dict → goal.call is owned by GoalCall.Convert, dispatched via the hook above.)

        // Primitives via Convert.ChangeType. InvariantCulture so JSON-shaped
        // numbers ("3.14", "1000") parse identically regardless of the user's locale.
        if (IsPrimitive(targetType))
        {
            try
            {
                return (System.Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.InvariantCulture), null);
            }
            catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
            {
                // Lead with target type + parameter name + actual content; never
                // surface the raw C# exception text (e.g. "Object must implement
                // IConvertible") as the headline — it's meaningless to a PLang dev.
                var convErr = new error.Error(
                    BindFailureMessage(value, sourceType, targetType, targetName),
                    "PrimitiveConversionFailed", 400)
                { Exception = ex };
                if (value is error.Error sourceErr)
                {
                    sourceErr.ErrorChain.Add(convErr);
                    return (null, sourceErr);
                }
                return (null, convErr);
            }
        }

        // Complex types: dict/JsonElement/JsonNode/list → serialize to JSON → deserialize to target type.
        if (value is IDictionary<string, object?> or JsonElement or JsonNode or System.Collections.IList)
        {
            string json = "";
            try
            {
                // Serialize with the same converter set as the read side so a
                // path.@this nested in the dict goes through PathJsonConverter
                // (string form) instead of being reflected into a full object
                // graph that the read side then can't deserialize.
                var writeOpts = context != null ? ContextualReadOptions(context) : _caseInsensitiveRead;
                json = JsonSerializer.Serialize(value, writeOpts);
                var result = JsonSerializer.Deserialize(json, targetType, writeOpts);
                return (result, null);
            }
            catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
            {
                string jsonPreview;
                var posMatch = System.Text.RegularExpressions.Regex.Match(ex.Message, @"BytePositionInLine: (\d+)");
                if (posMatch.Success && int.TryParse(posMatch.Groups[1].Value, out var bytePos) && bytePos < json.Length)
                {
                    var start = System.Math.Max(0, bytePos - 100);
                    var end = System.Math.Min(json.Length, bytePos + 100);
                    jsonPreview = $"...{json[start..end]}...";
                }
                else
                {
                    var maxLen = context?.App?.Debug?.MaxLength ?? 500;
                    jsonPreview = json.Length > maxLen ? json[..maxLen] + $"... ({json.Length} chars)" : json;
                }
                return (null, new error.Error(
                    $"Failed to deserialize {sourceType.Name} to {targetType.Name}: {ex.Message}",
                    "DeserializationFailed", 400)
                    { FixSuggestion = $"JSON around error: {jsonPreview}" });
            }
        }

        // Last resort: type mismatch
        if (!targetType.IsAssignableFrom(sourceType))
        {
            var convErr = new error.Error(
                FormatTypeMismatch(value, sourceType, targetType, targetName),
                "TypeMismatch", 400)
                { FixSuggestion = TypeMismatchHint(value, sourceType, targetType) };
            // An error value isn't convertible — keep the original error primary and demote the
            // conversion failure onto its chain (mirrors the primitive path above), so a failed
            // `%!error% as text` doesn't bury the real cause behind conversion scaffolding.
            if (value is error.Error sourceErr)
            {
                sourceErr.ErrorChain.Add(convErr);
                return (null, sourceErr);
            }
            return (null, convErr);
        }

        return (value, null);
    }

    /// <summary>
    /// A parameter-binding failure in plain language: what we tried to convert
    /// <em>to</em> (target PLang type), <em>where</em> (the parameter name, when
    /// the binding layer threaded it in), and <em>from</em> (the actual value's
    /// type + a content preview). Leads with these three facts and never the raw
    /// C# exception text.
    /// </summary>
    private static string BindFailureMessage(object? value, System.Type sourceType, System.Type targetType, string? targetName)
    {
        var expected = PlangTypeLabel(targetType);
        var slot = string.IsNullOrEmpty(targetName) ? "" : $" parameter '{targetName}'";
        var actual = value is error.Error err
            ? $"an Error object ({err.Key}: {Truncate(err.Message, 120)})"
            : $"{PlangTypeLabel(sourceType)} {FormatValuePreview(value)}";
        return $"Could not bind{slot}: expected {expected} but the value is {actual}.";
    }

    /// <summary>PLang type name + CLR type for a target/source, e.g. "text (string)".</summary>
    private static string PlangTypeLabel(System.Type type)
    {
        var u = System.Nullable.GetUnderlyingType(type) ?? type;
        return app.type.primitive.@this.Canonical.TryGetValue(u, out var plang)
            ? $"{plang} ({u.Name})"
            : u.Name;
    }

    /// <summary>
    /// Prepends the binding slot (parameter/variable name) to an owning-type hook's
    /// error so an action-parameter bind failure still names where it failed — the hooks
    /// don't carry the slot, the door does. No-op when the slot is unknown or already named.
    /// </summary>
    private static error.Error WithSlot(error.Error e, string? targetName)
    {
        if (string.IsNullOrEmpty(targetName) || e.Message.Contains($"'{targetName}'"))
            return e;
        return new error.Error($"parameter '{targetName}': {e.Message}", e.Key, e.StatusCode)
        {
            FixSuggestion = e.FixSuggestion,
            Exception = e.Exception
        };
    }

    private static string Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? "" : (s!.Length <= max ? s : s[..max] + "…");

    private static string FormatTypeMismatch(object? value, System.Type sourceType, System.Type targetType, string? targetName = null)
    {
        // FullName (not Name) so an OBP `@this` target disambiguates; value
        // preview surfaces an unresolved %var% in the headline. Lead with the
        // parameter name when the binding layer threaded it in.
        var slot = string.IsNullOrEmpty(targetName) ? "" : $"parameter '{targetName}': ";
        return $"{slot}Cannot convert {sourceType.FullName} to {targetType.FullName}. Source value: {FormatValuePreview(value)}";
    }

    private static string TypeMismatchHint(object? value, System.Type sourceType, System.Type targetType)
    {
        if (value is string s && s.Contains('%'))
            return $"Source value contains '%' — likely an unresolved %var% reference. Check that the variable is set and reachable in the current context, or that the dot-path navigation matches the value's actual shape.";
        return $"Source: {sourceType.FullName}, Target: {targetType.FullName}";
    }

    private static string FormatValuePreview(object? value)
    {
        if (value == null) return "(null)";
        if (value is string s)
        {
            var len = s.Length;
            if (len <= 100) return $"\"{s}\" (string, {len} chars)";
            return $"\"{s[..100]}…\" (string, {len} chars)";
        }
        if (value is System.Collections.ICollection col)
            return $"<{value.GetType().Name} @ {col.Count} items>";
        // A value that is neither string nor collection: show its TYPE, never
        // ToString() it. A domain object's ToString() leaks the CLR type name as if
        // it were content and masks the real defect (an object reaching a text slot);
        // the type name alone is the honest, useful signal for a bind-failure message.
        return $"<{value.GetType().FullName}>";
    }

    private static System.Type? GetListElementType(System.Type targetType)
    {
        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            return targetType.GetGenericArguments()[0];

        var baseType = targetType.BaseType;
        while (baseType != null)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(List<>))
                return baseType.GetGenericArguments()[0];
            baseType = baseType.BaseType;
        }

        return null;
    }
}
