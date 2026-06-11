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

            // ClrType is non-public on `type.@this` — `internal` to the entity
            // so the registry/primitive-fallback chain stays one place. Same
            // assembly, so the read is direct; no external GetPrimitiveOrMime
            // fallback to maintain at the call site.
            var targetType = value.Type.ClrType;
            if (targetType != null && !targetType.IsInstanceOfType(valueBacking))
            {
                var (_, error) = global::app.type.catalog.@this.TryConvert(valueBacking, targetType);
                if (error != null)
                    return $"Parameter 'Value' has type={value.Type.Name} but value cannot be converted: {error.Message}";
            }
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

        // Variable.Resolve flagged the slot as syntactically malformed
        // (`%x!!cost%`, `%x!a!b%`, etc.) — fail with a typed error rather
        // than silently writing to Properties[""] or replacing the binding
        // with a junk Name.
        if (name!.IsMalformed)
            return global::app.data.@this.FromError(
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
                return global::app.data.@this.FromError(
                    new global::app.error.ServiceError($"Variable '{name.Name}' is not set",
                        "VariableNotFound", 400));
            try
            {
                target.Properties[property] = await Value.Value();
            }
            catch (ArgumentException ex)
            {
                return global::app.data.@this.FromError(
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
            // Type entity rides in a bare Data — `type` is not `: item`, so it can't be a
            // Data<T> that auto-converts. Reconstruct it from whatever the .pr served:
            //   - already a type.@this → use it;
            //   - the `{name, kind?, strict?}` wire dict → TypeFromWire rebuilds name+kind+strict;
            //   - a bare type-name (raw string or text wrapper) → FromName of its text.
            // Only the dict goes through TypeFromWire — its string arm binds context onto the
            // entity, which would resolve a kindless name's ClrType to the wrapper (number) rather
            // than the raw CLR mate (Int32) the no-context FromName yields.
            var typeEntity = typeValue as global::app.type.@this
                ?? (typeValue is global::app.type.dict.@this or System.Collections.Generic.IDictionary<string, object?>
                        ? global::app.data.@this.TypeFromWire(typeValue, Context)
                        : null)
                ?? global::app.type.@this.FromName(typeValue.ToString()!);
            // Canonicalise kind through the format registry — `markdown` → `md`,
            // `jpeg` → `jpg`. The factory does this when a context is passed;
            // the .pr round-trip loses the context, so we run it again here.
            if (typeEntity.Kind != null)
            {
                var canon = Context.App.Format.CanonicaliseKind(typeEntity.Kind);
                if (canon != null) typeEntity.Kind = canon;
            }
            // Stamp the entity's Context so ClrType resolves through the
            // registry when the entity wasn't minted with a CLR mate.
            typeEntity.Context ??= Context;
            var typeName = typeEntity.Name;
            // Resolve the CLR target from the ENTITY, not Get(name). For
            // `number` the name resolves to the number.@this domain class, but
            // a numeric value is a CLR primitive (int/long/...) — the entity's
            // ClrType carries the right mate (typeof(int) for {number, int}).
            var targetType = typeEntity.ClrType ?? Context.App.Type.Get(typeName);

            // Stamp kind from the value via the type's Build hook (image parses
            // its path's extension → jpg; number reads the literal's precision →
            // int). `text` has no Build hook (a literal's spelling is not its
            // kind), so a text literal naturally derives nothing here.
            if (typeEntity.Kind == null && targetType != null)
            {
                var derivedKind = Context.App.Type.KindHooks.Of(targetType, sourceValue)
                                  ?? (Context.App.Type[typeName] is { ClrType: { } familyClr }
                                      ? Context.App.Type.KindHooks.Of(familyClr, sourceValue)
                                      : null);
                if (derivedKind != null) typeEntity.Kind = derivedKind;
            }
            if (targetType == null)
            {
                return global::app.data.@this.FromError(
                    new global::app.error.ServiceError($"Unknown type '{typeName}'", "UnknownType", 400));
            }

            // Strict kind enforcement at runtime — for `%var%` paths
            // ValidateBuild deferred to here. When the resolved CLR type
            // implements IKindValidatable and Strict is true, sniff the value.
            // We construct a sample instance using the raw value as the first
            // ctor argument (image's primary ctor takes byte[]); a type without
            // a fitting ctor is treated as "no probe available".
            if (typeEntity.Strict && typeEntity.Kind != null
                && typeof(global::app.data.IKindValidatable).IsAssignableFrom(targetType))
            {
                var probe = TryInstantiateValidator(targetType, sourceValue);
                if (probe is global::app.data.IKindValidatable v)
                {
                    var (ok, actual) = v.ValidateKind(sourceValue!, typeEntity.Kind);
                    if (!ok)
                        return global::app.data.@this.FromError(
                            new global::app.error.ServiceError(
                                $"Strict kind mismatch: declared {typeName}/{typeEntity.Kind}"
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
                && string.Equals(vt.Name, typeEntity.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(vt.Kind ?? "", typeEntity.Kind ?? "", StringComparison.OrdinalIgnoreCase))
            {
                Value.Name = name!;
                return await Context.Variable.Set(Value);
            }

            if (Value.RawUntouched) sourceValue = await Value.Value();
            object? converted = sourceValue;
            System.Type? mintType = targetType;

            // The incoming value composes the declared type as a facet under a
            // DIFFERENT name — an image has-a path, so an image bound to a `path`
            // slot already satisfies `path`. Keep it as-is with its own (richer)
            // type; the `path` hint was one the image already meets, downgrading
            // would drop the bytes. Same-name is excluded: there the declared
            // type may refine (`as text/md` over `{text}`) or carry strict, which
            // must still apply (strict, below, then runs against the declared
            // kind, so `image/gif strict` on a PNG still fails).
            var keepAsIs = Value.Type != null
                && !string.Equals(Value.Type.Name, typeEntity.Name, StringComparison.OrdinalIgnoreCase)
                && Value.Type.Is(typeEntity);
            if (keepAsIs)
            {
                mintType = converted?.GetType() ?? targetType;
            }
            // CLR target type that can construct from the raw value (string→int,
            // string→DateTime, dict→record) — convert in place. Conversion
            // failure is a real error UNLESS the target is byte-backed
            // (IKindValidatable family like image), in which case a literal
            // path-string is a legitimate value the Type entity annotates;
            // mint as Data<string> and let downstream consumers resolve.
            else if (converted != null && !targetType.IsInstanceOfType(converted))
            {
                // Ask the TYPE to make a value of itself (kind-aware) — the type owns
                // its own construction; we don't reach for Convert.ChangeType here.
                var convResult = typeEntity.Convert(converted, Context);
                if (convResult.Success)
                {
                    converted = convResult.Peek();
                    mintType = converted?.GetType() ?? targetType;
                }
                else if (typeof(global::app.data.IKindValidatable).IsAssignableFrom(targetType))
                    // Byte-backed family — keep the raw value, the Type entity
                    // (with its kind/strict) carries the user-declared meaning.
                    mintType = converted.GetType();
                else
                    return convResult;
            }
            // The instance carries its own type: kept-as-is keeps the value's
            // richer truth (image wins over a `path` hint); otherwise the
            // declared entity rides into the lift so kind survives the mint.
            var typedData = ConstructDataOfT(name, mintType, converted, keepAsIs ? null : typeEntity, Context);

            // Strict kind for a reference fundamental rides WITH the value to its
            // load seam (Ingi: validate at byte-materialization, throw if strict).
            // An already-loaded value (read-lift, raw bytes in hand) validates
            // now; a lazy path-backed value defers — its own load enforces (e.g.
            // image.BytesAsync throws on mismatch). Raw byte[] slots are handled
            // separately above via the IKindValidatable probe.
            if (typeEntity.Strict && typeEntity.Kind != null
                && (await typedData.Value()) is global::app.data.IStrictKindEnforcer enforcer)
            {
                enforcer.RequireStrictKind(typeEntity.Kind);
                if (enforcer.CheckStrictKind() is { ok: false } mismatch)
                    return global::app.data.@this.FromError(
                        new global::app.error.ServiceError(
                            $"Strict kind mismatch: declared {typeName}/{typeEntity.Kind}"
                            + (mismatch.actualKind != null ? $" but content is {mismatch.actualKind}." : "."),
                            "StrictKindMismatch", 400));
            }

            CopyProperties(Value, typedData);
            return await Context.Variable.Set(typedData);
        }

        // No forced type — bind the value under the target name.
        return await Context.Variable.Set(name.Name, Value);
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
        // Signature is result metadata that must survive the binding-mint: the
        // re-mint builds a fresh Data from the raw value, so `sign … write to %x%`
        // would otherwise drop the signature before `verify %x%` ever sees it.
        if (source.Signature != null) target.Signature = source.Signature;
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

    private static data.@this ConstructDataOfT(string name, System.Type t, object? value, global::app.type.@this? declared, actor.context.@this context)
    {
        // Data<T> requires T : item. A raw CLR mint type (string/int/byte[]/…) maps to the
        // item wrapper that owns it (text/number/binary/…) via the conversion registry — the
        // Data<wrapper> holds the raw value and projects it lazily on read. A type with no
        // item wrapper falls back to a bare Data. The declared entity rides into the ctor
        // so the entry lift stamps kind onto the instance (the instance owns kind).
        if (!typeof(global::app.type.item.@this).IsAssignableFrom(t))
        {
            var (family, _) = global::app.type.convert.@this.OwnerOf(t);
            if (family != null && typeof(global::app.type.item.@this).IsAssignableFrom(family))
            {
                // Wrap the raw value into its item form so the Data<wrapper> ctor (which takes
                // a T? = the wrapper) binds — a raw string/int/byte[] won't match Data<text/number/binary>.
                value = global::app.type.catalog.@this.ConvertTo(value, family, context) ?? value;
                t = family;
            }
            else
                return new data.@this(name, value, declared) { Context = context };
        }
        var generic = typeof(data.@this<>).MakeGenericType(t);
        var instance = (data.@this)Activator.CreateInstance(generic, name, value, declared, null)!;
        instance.Context = context;
        return instance;
    }

}
