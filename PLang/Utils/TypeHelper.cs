using System.ComponentModel;
using System.Reflection;
using Newtonsoft.Json.Linq;
using PLang.Errors;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Modules;

namespace PLang.Utils;

public interface ITypeHelper
{
    BaseBuilder GetInstructionBuilderInstance(string module);
    string GetMethodsAsString(Type type, string? methodName = null);
    List<Type> GetRuntimeModules();
    string GetModulesAsString(List<string>? excludedModules = null);
    BaseProgram GetProgramInstance(string module);
    List<Type> GetBuilderModules();
    Type? GetBuilderType(string module);
    Type? GetRuntimeType(string? module);
    string GetMethodNamesAsString(Type type, string? methodName = null);
    List<Type> GetTypesByType(Type type);
}

public class TypeHelper : ITypeHelper
{
    private static List<Type> runtimeModules = new();
    private static List<Type> baseRuntimeModules = new();
    private static List<Type> builderModules = new();
    private static List<Type> baseBuilderModules = new();
    private readonly DependancyHelper dependancyHelper;
    private readonly IPLangFileSystem fileSystem;
    private readonly ISettings settings;

    public TypeHelper(IPLangFileSystem fileSystem, ISettings settings, DependancyHelper dependancyHelper)
    {
        runtimeModules = dependancyHelper.LoadModules(typeof(BaseProgram), fileSystem.GoalsPath);
        builderModules = dependancyHelper.LoadModules(typeof(BaseBuilder), fileSystem.GoalsPath);

        this.fileSystem = fileSystem;
        this.settings = settings;
        this.dependancyHelper = dependancyHelper;
    }

    public List<Type> GetTypesByType(Type type)
    {
        List<Type> types;
        var executingAssembly = Assembly.GetExecutingAssembly();
        if (type.IsInterface || type.IsAbstract)
            types = executingAssembly.GetTypes().Where(t => type.IsAssignableFrom(t)).ToList();
        else
            types = executingAssembly.GetTypes().Where(t => t == type).ToList();

        var modulesDirectory = Path.Combine(fileSystem.GoalsPath, ".modules");
        if (fileSystem.Directory.Exists(modulesDirectory))
        {
            List<string> loaded = new();
            foreach (var dll in fileSystem.Directory.GetFiles(modulesDirectory, "*.dll", SearchOption.AllDirectories))
            {
                var loadedAssembly = Assembly.LoadFile(dll);
                List<Type> typesFromAssembly;

                try
                {
                    if (type.IsInterface || type.IsAbstract)
                        typesFromAssembly = loadedAssembly.GetTypes().Where(t => type.IsAssignableFrom(t)).ToList();
                    else
                        typesFromAssembly = loadedAssembly.GetTypes().Where(t => t == type).ToList();

                    if (typesFromAssembly.Count > 0)
                        // Add the found types to the main list
                        types.AddRange(typesFromAssembly);
                }
                catch (Exception ex)
                {
                    //Console.WriteLine(ex.Message);
                }
            }
        }

        var servicesDirectory = Path.Combine(fileSystem.GoalsPath, ".services");
        if (fileSystem.Directory.Exists(servicesDirectory))
            foreach (var dll in fileSystem.Directory.GetFiles(servicesDirectory, "*.dll", SearchOption.AllDirectories))
            {
                // Load the assembly
                var loadedAssembly = Assembly.LoadFile(dll);
                List<Type> typesFromAssembly;
                if (type.IsInterface || type.IsAbstract)
                    typesFromAssembly = loadedAssembly.GetTypes().Where(t => type.IsAssignableFrom(t)).ToList();
                else
                    typesFromAssembly = loadedAssembly.GetTypes().Where(t => t == type).ToList();

                if (typesFromAssembly.Count > 0)
                    // Add the found types to the main list
                    types.AddRange(typesFromAssembly);
            }

        types = types.GroupBy(p => p.FullName).Select(p => p.FirstOrDefault()).ToList();

        return types;
    }

    public string GetMethodNamesAsString(Type type, string? methodName = null)
    {
        if (type == null) return "";


        var methods = type.GetMethods().Where(p => p.DeclaringType.Name == "Program");
        if (methodName != null) methods = type.GetMethods().Where(p => p.Name == methodName);
        List<string> methodDescs = new();

        foreach (var method in methods)
        {
            var strMethod = "";
            if (method.Module.Name != type.Module.Name) continue;
            if (method.Name == "Run" || method.Name == "Dispose" || method.IsSpecialName) continue;


            strMethod += method.Name;
            var descriptions = method.CustomAttributes.Where(p => p.AttributeType.Name == "DescriptionAttribute");
            foreach (var desc in descriptions)
            {
                if (!strMethod.Contains(" // ")) strMethod += " // ";

                strMethod += desc.ConstructorArguments.FirstOrDefault().Value + ". ";
            }

            methodDescs.Add(strMethod);
        }

        return string.Join("", methodDescs);
    }

    public string GetMethodsAsString(Type type, string? methodName = null)
    {
        if (type == null) return "";


        var methods = type.GetMethods().Where(p => p.DeclaringType.Name == "Program");
        if (methodName != null) methods = type.GetMethods().Where(p => p.Name == methodName);
        List<string> methodDescs = new();

        foreach (var method in methods)
        {
            var strMethod = "";
            if (method.Module.Name != type.Module.Name) continue;
            if (method.Name == "Run" || method.Name == "Dispose" || method.IsSpecialName) continue;

            strMethod += method.Name + "(";
            var parameters = method.GetParameters();
            foreach (var param in parameters)
            {
                if (!strMethod.EndsWith("(")) strMethod += ", ";
                strMethod += param.ParameterType.Name;
                if (param.ParameterType.GenericTypeArguments.Length > 0)
                {
                    strMethod += "<";
                    for (var i = 0; i < param.ParameterType.GenericTypeArguments.Length; i++)
                    {
                        if (i != 0) strMethod += ", ";
                        strMethod += param.ParameterType.GenericTypeArguments[i].Name;
                    }

                    strMethod += ">";
                }

                if (param.IsOptional) strMethod += "?";

                strMethod += " " + param.Name;
                if (!string.IsNullOrEmpty(param.DefaultValue?.ToString()))
                {
                    if (param.DefaultValue.GetType() == typeof(string))
                        strMethod += " = \"" + param.DefaultValue + "\"";
                    else
                        strMethod += " = " + param.DefaultValue;
                }

                var paramDescs = param.CustomAttributes.Where(p => p.AttributeType.Name == "DescriptionAttribute");
                foreach (var desc in paramDescs)
                    strMethod += " /* " + desc.ConstructorArguments.FirstOrDefault().Value + " */ ";
            }

            strMethod += ") ";

            if (method.ReturnType == typeof(Task))
            {
                strMethod += " : void";
            }
            else if (method.ReturnType.GenericTypeArguments.Length > 0)
            {
                var returns = "void";

                foreach (var returnType in method.ReturnType.GenericTypeArguments)
                    if (returnType.Name.StartsWith("ValueTuple"))
                    {
                        foreach (var tupleType in returnType.GenericTypeArguments)
                            if (tupleType.Name == "Object" || !tupleType.IsAssignableFrom(typeof(IError)))
                            {
                                if (tupleType.Name.StartsWith("List"))
                                    returns = $"List<{tupleType.GenericTypeArguments[0].Name}>";
                                else if (tupleType.Name.StartsWith("Dictionary"))
                                    returns =
                                        $"Dicionary<{tupleType.GenericTypeArguments[0].Name}, {tupleType.GenericTypeArguments[1].Name}>";
                                else
                                    returns = tupleType.Name;
                            }
                    }
                    else if (!returnType.IsAssignableFrom(typeof(IError)))
                    {
                        returns = returnType.Name;
                    }

                strMethod += $" : {returns} ";
            }
            else
            {
                Console.WriteLine($"WARNING return type of {method.Name} is not Task");
            }

            var descriptions = method.CustomAttributes.Where(p => p.AttributeType.Name == "DescriptionAttribute");
            foreach (var desc in descriptions)
            {
                if (!strMethod.Contains(" // ")) strMethod += " // ";

                strMethod += desc.ConstructorArguments.FirstOrDefault().Value + ". ";
            }

            strMethod += "\n";
            methodDescs.Add(strMethod);
        }


        return string.Join("", methodDescs);
    }

    public string GetModulesAsString(List<string>? excludedModules = null)
    {
        var strModules = "[";
        foreach (var module in runtimeModules)
        {
            if (excludedModules != null && excludedModules.Contains(module.FullName.Replace(".Program", ""))) continue;

            strModules += $"{{ \"module\": \"{module.FullName.Replace(".Program", "")}\"";


            var descriptions = module.CustomAttributes.Where(p => p.AttributeType.Name == "DescriptionAttribute");
            if (descriptions.Count() > 0)
            {
                strModules += ", \"description\": \"";

                var description = "";
                foreach (var desc in descriptions)
                    strModules += desc.ConstructorArguments.FirstOrDefault().Value + ". ";
                strModules += description + "\"";
            }

            strModules += " }, \n";
        }

        return strModules + "]";
    }

    public BaseBuilder GetInstructionBuilderInstance(string module)
    {
        var classType = GetBuilderType(module) ?? DefaultBuilderType(module);
        ;

        var classInstance = Activator.CreateInstance(classType) as BaseBuilder;
        if (classInstance == null) throw new BuilderException($"Could not create instance of {classType}");
        return classInstance;
    }

    public BaseProgram GetProgramInstance(string module)
    {
        var classType = GetRuntimeType(module);

        if (classType == null) throw new Exception("Could not find module:" + module);

        var classInstance = Activator.CreateInstance(classType, new Dictionary<string, object>()) as BaseProgram;
        if (classInstance == null) throw new Exception($"Could not create instance of {classType}");
        return classInstance;
    }

    public Type? GetBuilderType(string module)
    {
        if (!module.EndsWith(".Builder")) module += ".Builder";
        return builderModules.FirstOrDefault(p => p.FullName == module);
    }

    public Type? GetRuntimeType(string? module)
    {
        if (module == null) return null;
        if (!module.EndsWith(".Program")) module += ".Program";
        return runtimeModules.FirstOrDefault(p => p.FullName == module);
    }

    public List<Type> GetBuilderModules()
    {
        return builderModules;
    }

    public List<Type> GetRuntimeModules()
    {
        return runtimeModules;
    }


    private static Version GetAssemblyVersion(string filePath)
    {
        try
        {
            var assemblyName = AssemblyName.GetAssemblyName(filePath);
            return assemblyName.Version;
        }
        catch
        {
            return new Version(0, 0, 0, 0); // Return a default version if the assembly version cannot be determined
        }
    }

    public Type DefaultBuilderType(string module)
    {
        return builderModules.FirstOrDefault(p => p.FullName == module + ".Builder");
    }

    public static string GetJsonSchemaForRecord(Type type)
    {
        var constructor = type.GetConstructors().First();
        var json = "{";
        foreach (var parameter in constructor.GetParameters())
        {
            var value = "";
            if (parameter.HasDefaultValue)
            {
                if (parameter.DefaultValue == null)
                    value = "null";
                else
                    value = parameter.DefaultValue.ToString()!;
            }
            else
            {
                value = parameter.ParameterType.Name.ToLower();
            }

            json += "\"" + parameter.Name + "\" : " + value + ", ";
        }

        return json += "}";
    }


    public static string GetJsonSchema(Type type)
    {
        var json = type.IsArray || type == typeof(List<>) ? "[\n" : "{\n";

        var primaryConstructor =
            type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
        var constructorParameters = primaryConstructor?.GetParameters()
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        foreach (var prop in type.GetProperties())
        {
            if (prop.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "JsonIgnoreAttribute") !=
                null) continue;

            var propName = "\t\"" + prop.Name + "\"";
            if (prop.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "NullableAttribute") != null)
                propName += "?";
            if (json.Length > 2) json += ",\n";
            if (prop.PropertyType == typeof(List<string>))
            {
                json += $@"{propName}: string[]";
            }
            else if (prop.PropertyType.FullName.StartsWith("System.Collections.Generic.Dictionary"))
            {
                json +=
                    $@"{propName} : {{ {prop.PropertyType.GenericTypeArguments[0].Name} : {prop.PropertyType.GenericTypeArguments[1].Name}, ... }}";
            }
            else if (prop.PropertyType.Name == "Nullable`1")
            {
                json += $@"{propName}?: {prop.PropertyType.GenericTypeArguments[0].Name}";
            }
            else if (prop.PropertyType.IsGenericType)
            {
                var args = prop.PropertyType.GetGenericArguments();
                if (args.Length == 1)
                    json += $@"{propName}: [" + GetJsonSchema(args[0]) + "]";
                else if (args.Length == 2)
                    json += $@"{propName}: {{ ""key"": {args[0].Name}, ""value"": {args[1].Name} }}";
            }
            else if (prop.PropertyType.IsClass && prop.PropertyType.Namespace.StartsWith("PLang"))
            {
                json += $@"{propName}: " + GetJsonSchema(prop.PropertyType);
                /*
                json += $@"""{prop.Name}:"" {{";
                var properties = prop.PropertyType.GetProperties();
                for (int i=0;i<properties.Length;i++)
                {
                    if (i != 0) json += ", ";
                    json += $@"""{properties[i].Name}"": {properties[i].PropertyType.Name}";
                }
                json += "}";*/
            }
            else if (prop.PropertyType.IsEnum)
            {
                json += $@"{propName}: enum";
                //prop.PropertyType.GetFields();
            }
            else if (prop.PropertyType.Namespace == type.Namespace)
            {
                json += $@"{propName}: " + GetJsonSchema(prop.PropertyType);
            }
            else
            {
                json += $@"{propName}: {prop.PropertyType.Name.ToLower()}";
            }

            var attribute = prop.GetCustomAttribute<DefaultValueAttribute>();
            if (attribute != null)
            {
                //schema[prop.Name] = " = " + ((DefaultValueAttribute) attribute).Value;
                json += " = " + (attribute.Value == null ? "null" : attribute.Value);
            }
            else if (constructorParameters != null && constructorParameters.ContainsKey(prop.Name))
            {
                var item = constructorParameters[prop.Name];
                if (item.HasDefaultValue) json += " = " + (item.DefaultValue == null ? "null" : item.DefaultValue);
            }
            else if (prop.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "NullableAttribute") != null)
            {
                //json += " = null";
            }

            var description = prop.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "DescriptionAttribute");
            if (description != null) json += " // " + description.ConstructorArguments[0].Value;
        }

        json += type.IsArray || type == typeof(List<>) ? "\n]" : "\n}";

        return json;
    }


    public static List<string> GetStaticFields(Type type,
        BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Public)
    {
        List<string> keywords = new();

        FieldInfo[] fields = type.GetFields(bindingFlags);

        foreach (var field in fields)
            if (field.FieldType == typeof(string))
                keywords.Add(field.GetValue(null)!.ToString()!);

        return keywords;
    }


    public static object? ConvertToType(object? value, Type targetType)
    {
        if (value == null) return null;

        if (targetType.Name == "String" &&
            (value is JObject || value is JArray || value is JToken || value is JProperty)) return value.ToString();

        if (targetType == null)
            throw new ArgumentNullException(nameof(targetType));

        if (value == null)
            return null;

        if (targetType.IsInstanceOfType(value))
            return value;

        try
        {
            if (targetType.Name.StartsWith("Nullable")) targetType = targetType.GenericTypeArguments[0];

            var parseMethod = targetType.GetMethod("Parse", new[] { typeof(string) });
            if (parseMethod != null) return parseMethod.Invoke(null, new object[] { value.ToString() });
        }
        catch
        {
        }

        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch (Exception ex)
        {
            return value;
        }
    }
}