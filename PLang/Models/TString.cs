namespace PLang;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

public class TString : IComparable, IComparable<string>, IConvertible,
					   IEquatable<string>, ICloneable, IEnumerable<char>
{
	private readonly string _value;
	private readonly Dictionary<string, string> _translation;
	private readonly dynamic _memoryStack;

	public TString(string value)
	{
		_value = value ?? string.Empty;
		
	}

	// The key method that performs translation and parameter substitution
	public override string ToString()
	{
		// Extract the translation key (everything before the first parameter or the whole string)
		string translationKey = ExtractTranslationKey(_value);

		// Get translated text
		string translatedText = _translation.ContainsKey(translationKey)
			? _translation[translationKey]
			: _value;

		// Replace parameters with values from memoryStack
		return ReplaceParameters(translatedText);
	}

	private string ExtractTranslationKey(string input)
	{
		// Find the first parameter marker
		int paramStart = input.IndexOf('%');
		if (paramStart == -1)
			return input.Trim();

		// Return everything before the first parameter, trimmed
		return input.Substring(0, paramStart).Trim();
	}

	private string ReplaceParameters(string text)
	{
		string result = text;
		int startIndex = 0;

		while (true)
		{
			int paramStart = result.IndexOf('%', startIndex);
			if (paramStart == -1) break;

			int paramEnd = result.IndexOf('%', paramStart + 1);
			if (paramEnd == -1) break;

			string paramName = result.Substring(paramStart, paramEnd - paramStart + 1);
			string paramValue = _memoryStack.Get(paramName)?.ToString() ?? string.Empty;

			result = result.Substring(0, paramStart) + paramValue + result.Substring(paramEnd + 1);
			startIndex = paramStart + paramValue.Length;
		}

		return result;
	}

	public ReadOnlySpan<char> AsSpan() => ToString().AsSpan();
	public ReadOnlySpan<char> AsSpan(int start) => ToString().AsSpan(start);
	public ReadOnlySpan<char> AsSpan(int start, int length) => ToString().AsSpan(start, length);

	// String-like properties
	public int Length => ToString().Length;
	public char this[int index] => ToString()[index];

	// Implicit conversion to string
	public static implicit operator string(TString tstring) => tstring?.ToString();

	// Explicit conversion from string
	public static implicit operator TString(string str) =>
		new TString(str);

	// String comparison methods
	public int CompareTo(object obj)
	{
		if (obj == null) return 1;
		if (obj is string s) return string.Compare(ToString(), s, StringComparison.Ordinal);
		if (obj is TString ts) return string.Compare(ToString(), ts.ToString(), StringComparison.Ordinal);
		throw new ArgumentException("Object must be of type String or TString");
	}

	public int CompareTo(string other) => string.Compare(ToString(), other, StringComparison.Ordinal);

	public bool Equals(string other) => ToString().Equals(other);

	public override bool Equals(object obj)
	{
		if (obj is string s) return ToString().Equals(s);
		if (obj is TString ts) return ToString().Equals(ts.ToString());
		return false;
	}

	public override int GetHashCode() => ToString().GetHashCode();

	// Common String methods
	public bool Contains(string value) => ToString().Contains(value);
	public bool StartsWith(string value) => ToString().StartsWith(value);
	public bool EndsWith(string value) => ToString().EndsWith(value);
	public int IndexOf(string value) => ToString().IndexOf(value);
	public int IndexOf(char value) => ToString().IndexOf(value);
	public int LastIndexOf(string value) => ToString().LastIndexOf(value);
	public string Substring(int startIndex) => ToString().Substring(startIndex);
	public string Substring(int startIndex, int length) => ToString().Substring(startIndex, length);
	public string ToLower() => ToString().ToLower();
	public string ToUpper() => ToString().ToUpper();
	public string Trim() => ToString().Trim();
	public string TrimStart() => ToString().TrimStart();
	public string TrimEnd() => ToString().TrimEnd();
	public string Replace(string oldValue, string newValue) => ToString().Replace(oldValue, newValue);
	public string[] Split(params char[] separator) => ToString().Split(separator);
	public bool IsNullOrEmpty() => string.IsNullOrEmpty(ToString());
	public bool IsNullOrWhiteSpace() => string.IsNullOrWhiteSpace(ToString());

	// ICloneable
	public object Clone() => new TString(_value);

	// IEnumerable<char>
	public IEnumerator<char> GetEnumerator() => ToString().GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	// IConvertible implementation (delegating to string)
	public TypeCode GetTypeCode() => TypeCode.String;
	public bool ToBoolean(IFormatProvider provider) => ((IConvertible)ToString()).ToBoolean(provider);
	public byte ToByte(IFormatProvider provider) => ((IConvertible)ToString()).ToByte(provider);
	public char ToChar(IFormatProvider provider) => ((IConvertible)ToString()).ToChar(provider);
	public DateTime ToDateTime(IFormatProvider provider) => ((IConvertible)ToString()).ToDateTime(provider);
	public decimal ToDecimal(IFormatProvider provider) => ((IConvertible)ToString()).ToDecimal(provider);
	public double ToDouble(IFormatProvider provider) => ((IConvertible)ToString()).ToDouble(provider);
	public short ToInt16(IFormatProvider provider) => ((IConvertible)ToString()).ToInt16(provider);
	public int ToInt32(IFormatProvider provider) => ((IConvertible)ToString()).ToInt32(provider);
	public long ToInt64(IFormatProvider provider) => ((IConvertible)ToString()).ToInt64(provider);
	public sbyte ToSByte(IFormatProvider provider) => ((IConvertible)ToString()).ToSByte(provider);
	public float ToSingle(IFormatProvider provider) => ((IConvertible)ToString()).ToSingle(provider);
	public string ToString(IFormatProvider provider) => ToString();
	public object ToType(Type conversionType, IFormatProvider provider) =>
		((IConvertible)ToString()).ToType(conversionType, provider);
	public ushort ToUInt16(IFormatProvider provider) => ((IConvertible)ToString()).ToUInt16(provider);
	public uint ToUInt32(IFormatProvider provider) => ((IConvertible)ToString()).ToUInt32(provider);
	public ulong ToUInt64(IFormatProvider provider) => ((IConvertible)ToString()).ToUInt64(provider);

	// Operator overloads
	public static bool operator ==(TString a, TString b)
	{
		if (ReferenceEquals(a, b)) return true;
		if (a is null || b is null) return false;
		return a.ToString() == b.ToString();
	}

	public static bool operator !=(TString a, TString b) => !(a == b);

	public static bool operator ==(TString a, string b) => a?.ToString() == b;
	public static bool operator !=(TString a, string b) => !(a == b);

	public static TString operator +(TString a, string b) =>
		new TString(a.ToString() + b);

	public static TString operator +(string a, TString b) =>
	   new TString(a + b.ToString());
}
