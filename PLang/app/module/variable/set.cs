using app.Attributes;
using app.variable;

namespace app.module.variable;

/// <summary>
/// Sets a variable in the current context's variable store.
/// When AsDefault is true, only sets if the variable doesn't already exist.
///
/// variable.set is the binding site. With no `as` clause it shallow-clones the source
/// Data under the target name — value (lazy raw included), type, signature and
/// properties shared by reference, no materialize, no deep clone. A `Type` clause
/// converts/mints the declared Data&lt;T&gt;. Variables.Set then replaces the binding
/// (Data values are never mutated in place), carrying event subscribers across the name.
/// </summary>
[Action("set", Cacheable = false)]
public partial class Set : IContext, IBuildValidatable
{
    public static string? ValidateBuild(List<data.@this> parameters)
    {
        var value = parameters.FirstOrDefault(p =>
            string.Equals(p.Name, "Value", StringComparison.OrdinalIgnoreCase));
        // Build-time is a sync surface — read the materialised backing, never
        // the async door. The strict probe and the conversion check below
        // reason over the value's raw face at this proven leaf (ValidateKind
        // and TryConvert are CLR-facing machinery).
        var valueBacking = global::app.type.item.@this.Backing(value?.Peek());

        // Strict kind enforcement at build for literals. Pulls the user-named
        // type entity from the Type parameter (post-Stage-4 — a type, not a
        // string). Mismatches return a build error; %var% values defer to Run.
        var typeParam = parameters.FirstOrDefault(p =>
            string.Equals(p.Name, "Type", StringComparison.OrdinalIgnoreCase));
        if (typeParam?.Peek() is global::app.type.@this t && t.Strict && t.Kind != null
            && valueBacking != null && !value!.HasVariableReference)
        {
            var clr = t.ClrType;
            if (clr != null && typeof(global::app.data.IKindValidatable).IsAssignableFrom(clr))
            {
                var probe = TryInstantiateValidator(clr, valueBacking);
                if (probe is global::app.data.IKindValidatable v)
                {
                    var (ok, actual) = v.ValidateKind(valueBacking, t.Kind);
                    if (!ok)
                        return $"Strict kind mismatch: declared {t.Name}/{t.Kind}"
                            + (actual != null ? $" but content is {actual}." : ".");
                }
            }
        }

        if (value?.Type?.Name != null && valueBacking != null)
        {
            // Skip validation when value contains %variable% references — they resolve at runtime
            if (value.HasVariableReference) return null;

            // Born on the wire: the literal already carries its type, so this is a
            // type-match check, not a conversion. A mismatch is a build error.
            var targetType = value.Type.ClrType;
            if (targetType != null && !targetType.IsInstanceOfType(valueBacking))
                return $"Parameter 'Value' has type={value.Type.Name} but value is not a {value.Type.Name}: {valueBacking}";
        }
        return null;
    }

    public partial data.@this<app.variable.@this> Name { get; init; }
    public partial data.@this Value { get; init; }
    /// <summary>
    /// Optional <c>as</c> clause. Carries the whole <c>type</c> entity (Name,
    /// Kind, Strict) the LLM constructed — replaces the historical bare string.
    /// <c>Run</c> reads <c>Type.Value.Name</c> to resolve the CLR type via the
    /// registry and stamps the entire entity (kind included) onto the minted
    /// variable.
    /// </summary>
    public partial data.@this? Type { get; init; }
    [Default(false)]
    public partial data.@this<global::app.type.@bool.@this> AsDefault { get; init; }

    public async Task<data.@this> Run()
    {
        // Resolve the name door up front; the VALUE door stays closed on this path —
        // a plain `set %x% = %y%` forwards the binding (ShallowClone shares the lazy
        // raw), so opening the door here would parse a lazily-read file on store and
        // defeat verbatim passthrough. Only the branches that genuinely need content
        // (a Properties write, a forced-type conversion) open it below.
        var name = await Name.Value();

        // The Name slot must NAME a thing. A value typed as something other than
        // `variable` (a string-typed literal) declines creation — variable.Create
        // fails the Name binding (CreateVariableDeclined) and answers null. Surface that
        // decline instead of NRE'ing on a null name below.
        if (name == null)
            return Context.Error(Name.Error
                ?? new global::app.error.Error("variable.set: Name did not resolve to a variable.", "CreateVariableDeclined", 400));

        // Variable.Resolve flagged the slot as syntactically malformed
        // (`%x!!cost%`, `%x!a!b%`, etc.) — fail with a typed error rather
        // than silently writing to Properties[""] or replacing the binding
        // with a junk Name.
        if (name.IsMalformed)
            return Context.Error(
                new global::app.error.ServiceError(
                    $"Variable reference '{name.RawValue}' is not a valid name — only a single '!' separates a variable from its Property key, and the suffix may not appear after '.' or '['.",
                    "InvalidVariableReference", 400));

        // %x!cost% target — mutate the named variable's Properties[key]
        // instead of replacing the binding. Same action, two stores:
        // bare-name slots hit Value, !-suffixed slots hit Properties.
        // Goes through Variable.Resolve's parsing — see Variable.Property.
        var property = name.Property;
        if (!string.IsNullOrEmpty(property))
        {
            var target = await Context.Variable.Get(name.Name);
            if (target == null || !target.IsInitialized)
                return Context.Error(
                    new global::app.error.ServiceError($"Variable '{name.Name}' is not set",
                        "VariableNotFound", 400));
            try
            {
                target.Properties[property] = await Value.Value();
            }
            catch (ArgumentException ex)
            {
                return Context.Error(
                    new global::app.error.ServiceError(ex.Message, "InvalidPropertyValue", 400));
            }
            return target;
        }

        if (await AsDefault.ToBooleanAsync())
        {
            var existing = await Context.Variable.Get(name);
            if (existing.IsInitialized)
                return existing;
        }

        // Forced type via [Type]: convert via TryConvert and mint Data<T>. Conversion failure
        // surfaces as Data.Error (Success=false) — Variables.Set is not called in that case so
        // the binding stays whatever it was. For primitives this is straight coercion ("42" → 42).
        // For json (TypeMapping maps "json" → typeof(JsonNode)), TryConvert parses the string
        // into a JsonObject which IS IDictionary — that's what enables `convert %json% from
        // json` (mapped to variable.set Type=json) followed by foreach over the resulting dict.
        //
        // Strict-kind is enforced at THREE genuinely different times in this block — they are
        // NOT redundant, each catches a case the others can't see:
        //   1. ValidateBuild (above) — a literal value, at BUILD time.
        //   2. the IKindValidatable probe below — a %var% value resolved at RUN time.
        //   3. the IStrictKindEnforcer load seam below — byte-backed values, at MATERIALIZATION.
        // An omitted `as` clause is an EMPTY slot, not C# null — the value door
        // answers the `absent` citizen (non-null, ToString() == ""). Gate on the
        // value's own emptiness so an absent type skips the conversion block
        // instead of minting a type with an empty name (UnknownType '').
        var typeValue = Type == null || await Type.IsEmpty() ? null : await Type.Value();
        if (typeValue != null)
        {
            // Kind-derivation and the strict probe read the IN-MEMORY value only: a
            // raw-backed (unparsed) value contributes null — deriving a kind from
            // content would force the parse the verbatim fast-path below exists to
            // avoid. Content is read (door opened) only past that fast-path, where
            // conversion genuinely needs it. A reference (file/url) stays itself —
            // opening the door would read content on store, and the reference IS
            // the declared value (the lazy contract).
            object? sourceValue = Value.RawUntouched ? null
                : Value.Peek() is (global::app.type.file.@this or global::app.type.url.@this) and { } reference ? reference
                : await Value.Value();
            // The kind hooks and the strict probe below reason over the raw CLR
            // face (ctor matching, magic-byte/extension sniffing) — a born-typed
            // text/binary leaf presents its backing here. Minting re-lifts, so
            // the stored value stays born-typed either way.
            if (sourceValue is global::app.type.text.@this st) sourceValue = st.Clr<string>();
            else if (sourceValue is global::app.type.binary.@this sb) sourceValue = sb.Value;
            // The Type value reads through the `type` reader, so it materializes as the type
            // entity itself ({name, kind?, strict?} → type.@this). A bare type-name (raw string)
            // still names a type by name. No dict rebuild — that was the pre-reader path.
            var type = typeValue as global::app.type.@this
                ?? global::app.type.@this.FromName(typeValue.ToString()!);
            // Canonicalise kind through the format registry — `markdown` → `md`,
            // `jpeg` → `jpg`. The factory does this when a context is passed;
            // the .pr round-trip loses the context, so we run it again here.
            if (type.Kind != null)
            {
                var canon = Context.App.Format.CanonicaliseKind(type.Kind);
                if (canon != null) type.Kind = canon;
            }
            // Stamp the entity's Context so ClrType resolves through the
            // registry when the entity wasn't minted with a CLR mate.
            type.Context ??= Context;
            var typeName = type.Name;
            // Resolve the CLR target from the ENTITY, not Get(name). For
            // `number` the name resolves to the number.@this domain class, but
            // a numeric value is a CLR primitive (int/long/...) — the entity's
            // ClrType carries the right mate (typeof(int) for {number, int}).
            var targetType = type.ClrType ?? Context.App.Type.Get(typeName);

            // Stamp kind from the value via the type's Build hook (image parses
            // its path's extension → jpg; number reads the literal's precision →
            // int). `text` has no Build hook (a literal's spelling is not its
            // kind), so a text literal naturally derives nothing here.
            if (type.Kind == null && targetType != null)
            {
                var derivedKind = Context.App.Type.KindHooks.Of(targetType, sourceValue)
                                  ?? (Context.App.Type[typeName] is { ClrType: { } familyClr }
                                      ? Context.App.Type.KindHooks.Of(familyClr, sourceValue)
                                      : null);
                if (derivedKind != null) type.Kind = derivedKind;
            }
            if (targetType == null)
            {
                return Context.Error(
                    new global::app.error.ServiceError($"Unknown type '{typeName}'", "UnknownType", 400));
            }

            // Strict kind enforcement at runtime — for `%var%` paths
            // ValidateBuild deferred to here. When the resolved CLR type
            // implements IKindValidatable and Strict is true, sniff the value.
            // We construct a sample instance using the raw value as the first
            // ctor argument (image's primary ctor takes byte[]); a type without
            // a fitting ctor is treated as "no probe available".
            if (type.Strict && type.Kind != null
                && typeof(global::app.data.IKindValidatable).IsAssignableFrom(targetType))
            {
                var probe = TryInstantiateValidator(targetType, sourceValue);
                if (probe is global::app.data.IKindValidatable v)
                {
                    var (ok, actual) = v.ValidateKind(sourceValue!, type.Kind);
                    if (!ok)
                        return Context.Error(
                            new global::app.error.ServiceError(
                                $"Strict kind mismatch: declared {typeName}/{type.Kind}"
                                + (actual != null ? $" but content is {actual}." : "."),
                                "StrictKindMismatch", 400));
                }
            }

            // The incoming value is ALREADY a raw-backed Data of the declared type
            // — a lazy read assigned via `write to %var%` (file.read/channel.read
            // stamp {table,csv}/{object,json} and the same stamp lands here). Store
            // it as-is so it stays lazy: scalar %var% remains the raw source form
            // and verbatim passthrough holds. Re-materializing (Value.Value below)
            // would parse it on store and defeat the whole lazy path.
            if (Value.RawUntouched && Value.Type is { } vt
                && string.Equals(vt.Name, type.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(vt.Kind ?? "", type.Kind ?? "", StringComparison.OrdinalIgnoreCase))
            {
                Value.Name = name!;
                return await Context.Variable.Set(Value);
            }

            if (Value.RawUntouched) sourceValue = await Value.Value();
            object? converted = sourceValue;

            // The incoming value composes the declared type as a facet under a DIFFERENT
            // name (an image has-a path, so an image bound to a `path` slot satisfies `path`)
            // — keep its own richer type; downgrading would drop the bytes. Same-name is
            // excluded so `as text/md` over `{text}` (and strict) still applies.
            var keepAsIs = Value.Type != null
                && !string.Equals(Value.Type.Name, type.Name, StringComparison.OrdinalIgnoreCase)
                && Value.Type.Is(type);

            // `as <type>` is a converter: the TYPE makes a value of itself (kind-aware).
            // A byte-backed family (image) keeps a literal path-string — the Type entity
            // carries the declared meaning and the value loads later.
            if (!keepAsIs && converted != null && !targetType.IsInstanceOfType(converted))
            {
                var convResult = type.Convert(converted, Context);
                if (convResult.Success)
                    converted = convResult.Peek();
                else if (!typeof(global::app.data.IKindValidatable).IsAssignableFrom(targetType))
                    return convResult;
            }

            // The converted value already carries its type — store it in a Data directly
            // (no reflective Data<T> mint). keepAsIs keeps the value's own richer type.
            var typedData = new data.@this(name, converted, keepAsIs ? null : type, context: Context);

            // Strict kind for a reference fundamental rides WITH the value to its
            // load seam (Ingi: validate at byte-materialization, throw if strict).
            // An already-loaded value (read-lift, raw bytes in hand) validates
            // now; a lazy path-backed value defers — its own load enforces (e.g.
            // image.BytesAsync throws on mismatch). Raw byte[] slots are handled
            // separately above via the IKindValidatable probe.
            if (type.Strict && type.Kind != null
                && (await typedData.Value()) is global::app.data.IStrictKindEnforcer enforcer)
            {
                enforcer.RequireStrictKind(type.Kind);
                if (enforcer.CheckStrictKind() is { ok: false } mismatch)
                    return Context.Error(
                        new global::app.error.ServiceError(
                            $"Strict kind mismatch: declared {typeName}/{type.Kind}"
                            + (mismatch.actualKind != null ? $" but content is {mismatch.actualKind}." : "."),
                            "StrictKindMismatch", 400));
            }

            CopyProperties(Value, typedData);
            return await Context.Variable.Set(typedData);
        }

        // No forced type — bind a shallow clone under the target name. AsCanonical
        // resolves the NAME hop (a full-match `%x%`/`%!data%` → the CURRENT Data
        // instance it points at) WITHOUT computing its value (lazy preserved); storing
        // `Value` directly would copy the reference to the name, so when a reused infra
        // var like `%!data%` rebinds to the next action's result, the target would follow
        // it (the `%msg%` self-reference that blocks the builder). ShallowClone gives the
        // new slot its own Properties copy (the value instance is shared — immutable, safe).
        // AsCanonical resolves the NAME hop (a full-match `%x%`/`%!data%` → the CURRENT
        // Data instance it points at) without computing its value (lazy preserved), so the
        // target binds to that instance, not the rebinding name — the fix for the `%msg%`
        // self-reference.
        var canonical = await Value.AsCanonical(Context);
        // Binding to an UNSET reference (`set %x% = %unset%`, or a navigation that resolves
        // to nothing) is an error — a missing value means something is wrong, surface it with
        // the name rather than silently binding the absent citizen. Infra vars (`%!error%`)
        // are legitimately unset (no error yet) and excepted; a present-null source is
        // initialized and binds fine.
        if (!canonical.IsInitialized && !canonical.Name.StartsWith('!'))
            return Context.Error(new global::app.error.Error(
                $"Variable '{canonical.Name}' not found", "VariableNotFound", 404));
        return await Context.Variable.Set(name.Name, canonical.ShallowClone(name.Name));
    }

    /// <summary>
    /// Properties carry per-Data result metadata (test.report's summaryFail, condition.if's
    /// branchIndex, etc.) that downstream <c>%var.prop%</c> navigation depends on. The
    /// forced-type path (<c>as</c> clause) builds a fresh Data<T> from the converted value;
    /// without this copy, Properties + Signature would be dropped at that mint. The no-type
    /// path shallow-clones and so carries them for free.
    /// </summary>
    private static void CopyProperties(data.@this source, data.@this target)
    {
        if (ReferenceEquals(source, target)) return;
        if (source.Properties.Count == 0) return;
        foreach (var p in source.Properties)
            target.Properties[p.Key] = p.Value;
    }


    /// <summary>
    /// Reflection construction of Data&lt;T&gt; for a runtime type not in the hot if-chain.
    /// </summary>
    private static object? TryInstantiateValidator(System.Type targetType, object? rawValue)
    {
        if (rawValue == null) return null;
        foreach (var ctor in targetType.GetConstructors())
        {
            var ps = ctor.GetParameters();
            if (ps.Length == 0) continue;
            if (!ps[0].ParameterType.IsAssignableFrom(rawValue.GetType())) continue;
            var args = new object?[ps.Length];
            args[0] = rawValue;
            for (int i = 1; i < ps.Length; i++)
                args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue
                    : ps[i].ParameterType == typeof(string) ? string.Empty : null;
            try { return ctor.Invoke(args); }
            catch (System.Exception ex) when (ex is not (System.OutOfMemoryException or System.StackOverflowException))
            { continue; }
        }
        return null;
    }

}
