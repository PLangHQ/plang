namespace PLang.Utils;
public static class TypeExtensions
{
	public static string FullNameNormalized(this Type t) => Normalize(t);

	static string Normalize(Type t)
	{
		if (t.FullName == null) {
			throw new Exception("FullName of type is null. This is not allowed");
		}

		if (t.IsByRef) return $"{Normalize(t.GetElementType()!)}&";
		if (t.IsPointer) return $"{Normalize(t.GetElementType()!)}*";
		if (t.IsArray)
		{
			var rank = t.GetArrayRank();
			var dims = rank == 1 ? "[]" : $"[{new string(',', rank - 1)}]";
			return $"{Normalize(t.GetElementType()!)}{dims}";
		}

		if (t.IsGenericParameter) return t.Name;

		if (t.IsGenericType)
		{
			var def = t.GetGenericTypeDefinition();                 // e.g. System.Collections.Generic.List`1
			var args = string.Join(",", t.GetGenericArguments().Select(Normalize));
			return $"{def.FullName}[{args}]";
		}

		return t.FullName ?? t.Name;
	}

	public static string EnumOptions(this Type t, string separator = "|", bool dedupeByValue = true)
	{
		if (!t.IsEnum) throw new ArgumentException("Type must be enum.", nameof(t));
		var seen = dedupeByValue ? new HashSet<ulong>() : null;
		var names = new List<string>();

		foreach (var v in Enum.GetValues(t))
		{
			var name = Enum.GetName(t, v);
			if (name is null) continue;
			if (seen is not null)
			{
				var u = Convert.ToUInt64(v);
				if (!seen.Add(u)) continue;
			}
			names.Add(name);
		}
		return string.Join(separator, names);
	}
}

