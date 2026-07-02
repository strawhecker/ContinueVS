using ContinueTranslator.Core.IR;

namespace ContinueTranslator.Core.Mapping;

/// <summary>
/// Orchestrates <see cref="NodeApiMap"/>, <see cref="NpmPackageMap"/>,
/// <see cref="TypeMap"/>, and <see cref="CallSiteMap"/> over a <see cref="TsFile"/> array,
/// producing a new array with all resolvable type references replaced with their C# equivalents.
/// Unresolvable type references are tagged with an <c>@ct:todo=unresolved</c> cookie.
/// </summary>
internal sealed partial class MappingEngine
{
    private const string MapCookiePrefix = "@ct:map=";
    private const string TodoUnresolved = "@ct:todo=unresolved";

    private readonly NodeApiMap _nodeApi;
    private readonly NpmPackageMap _npmPackage;
    private readonly TypeMap _typeMap;
    private readonly CallSiteMap _callSiteMap;
    private HashSet<string> _localTypes = new(StringComparer.Ordinal);

    public MappingEngine(NodeApiMap nodeApi, NpmPackageMap npmPackage, TypeMap typeMap, CallSiteMap callSiteMap)
    {
        ArgumentNullException.ThrowIfNull(nodeApi);
        ArgumentNullException.ThrowIfNull(npmPackage);
        ArgumentNullException.ThrowIfNull(typeMap);
        ArgumentNullException.ThrowIfNull(callSiteMap);

        _nodeApi = nodeApi;
        _npmPackage = npmPackage;
        _typeMap = typeMap;
        _callSiteMap = callSiteMap;
    }

    /// <summary>
    /// Applies all mapping tables to every type reference in <paramref name="files"/> and
    /// returns a new array of <see cref="TsFile"/> records with resolved types.
    /// Input records are never mutated.
    /// </summary>
    public TsFile[] Apply(TsFile[] files)
    {
        ArgumentNullException.ThrowIfNull(files);

        // First pass: collect all locally-defined types (classes, interfaces, enums)
        _localTypes = CollectLocalTypes(files);

        return [.. files.Select(ApplyFile)];
    }

    /// <summary>
    /// Collects all class, interface, enum, and type alias names defined across all files.
    /// These are considered "local types" and should not trigger TODO comments when used.
    /// </summary>
    private static HashSet<string> CollectLocalTypes(TsFile[] files)
    {
        var local = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            foreach (var cls in file.Classes)
                local.Add(cls.Name);

            foreach (var iface in file.Interfaces)
                local.Add(iface.Name);

            foreach (var enumm in file.Enums)
                local.Add(enumm.Name);

            foreach (var alias in file.TypeAliases)
                local.Add(alias.Name);
        }

        return local;
    }

    private TsFile ApplyFile(TsFile file) => file with
    {
        Imports = [.. file.Imports.Select(ApplyImport)],
        Classes = [.. file.Classes.Select(ApplyClass)],
        Interfaces = [.. file.Interfaces.Select(ApplyInterface)],
        Functions = [.. file.Functions.Select(ApplyFunction)],
    };

    private TsImport ApplyImport(TsImport import)
    {
        // Check @ct:map= cookie on the import node.
        if (TryGetMapCookie(import.Cookies, out string mapped))
            return import with { ModuleSpecifier = mapped };

        // Try npm-package map on the module specifier.
        if (!string.IsNullOrWhiteSpace(import.ModuleSpecifier) &&
            _npmPackage.TryResolve(import.ModuleSpecifier, out string dotNetType))
            return import with { ModuleSpecifier = dotNetType };

        // Try stripping leading '@' scope prefix (e.g. "@scope/pkg" → "pkg").
        string bare = import.ModuleSpecifier.TrimStart('@').Split('/')[^1];
        if (!string.IsNullOrWhiteSpace(bare) &&
            _npmPackage.TryResolve(bare, out string dotNetType2))
            return import with { ModuleSpecifier = dotNetType2 };

        return import;
    }

    private TsClass ApplyClass(TsClass cls) => cls with
    {
        Properties = [.. cls.Properties.Select(ApplyProperty)],
        Methods = [.. cls.Methods.Select(ApplyMethod)],
    };

    private TsInterface ApplyInterface(TsInterface iface) => iface with
    {
        Properties = [.. iface.Properties.Select(ApplyProperty)],
        Methods = [.. iface.Methods.Select(ApplyMethod)],
    };

    private TsProperty ApplyProperty(TsProperty prop)
    {
        if (TryGetMapCookie(prop.Cookies, out string overrideType))
        {
            var overrideRef = new TsTypeRef(overrideType, overrideType, [], false);
            return prop with { Type = overrideRef };
        }

        var (resolved, wasResolved) = ResolveTypeRef(prop.Type);
        if (!wasResolved)
            return prop with { Type = resolved, Cookies = AppendTodoUnresolved(prop.Cookies) };

        return prop with { Type = resolved };
    }

    private TsMethod ApplyMethod(TsMethod method)
    {
        TsTypeRef returnType;
        string[] cookies = method.Cookies;

        if (TryGetMapCookie(method.Cookies, out string overrideType))
        {
            returnType = new TsTypeRef(overrideType, overrideType, [], false);
        }
        else
        {
            var (resolved, wasResolved) = ResolveTypeRef(method.ReturnType);
            returnType = resolved;
            if (!wasResolved)
                cookies = AppendTodoUnresolved(cookies);
        }

        return method with
        {
            ReturnType = returnType,
            Parameters = [.. method.Parameters.Select(ApplyParameter)],
            Cookies = cookies,
        };
    }

    private TsParameter ApplyParameter(TsParameter param)
    {
        if (TryGetMapCookie(param.Cookies, out string overrideType))
        {
            var overrideRef = new TsTypeRef(overrideType, overrideType, [], false);
            return param with { Type = overrideRef };
        }

        var (resolved, wasResolved) = ResolveTypeRef(param.Type);
        if (!wasResolved)
            return param with { Type = resolved, Cookies = AppendTodoUnresolved(param.Cookies) };

        return param with { Type = resolved };
    }

    private TsFunction ApplyFunction(TsFunction func)
    {
        TsTypeRef returnType;
        string[] cookies = func.Cookies;

        if (TryGetMapCookie(func.Cookies, out string overrideType))
        {
            returnType = new TsTypeRef(overrideType, overrideType, [], false);
        }
        else
        {
            var (resolved, wasResolved) = ResolveTypeRef(func.ReturnType);
            returnType = resolved;
            if (!wasResolved)
                cookies = AppendTodoUnresolved(cookies);
        }

        return func with
        {
            ReturnType = returnType,
            Parameters = [.. func.Parameters.Select(ApplyParameter)],
            Cookies = cookies,
        };
    }

    /// <summary>
    /// Recursively resolves a <see cref="TsTypeRef"/>, mapping type args first, then the
    /// base name.  Returns the (possibly updated) ref and whether every node resolved.
    /// </summary>
    private (TsTypeRef Ref, bool WasResolved) ResolveTypeRef(TsTypeRef typeRef)
    {
        // Handle function signature types (e.g., "(args: X) => boolean").
        // Function types cannot be precisely mapped without full signature parsing,
        // so we mark them as resolved by converting to "Delegate" type.
        if (typeRef.Text.Contains("=>", StringComparison.Ordinal))
        {
            // Replace the entire function signature with "Delegate" as a resolvable stand-in.
            return (typeRef with { Name = "Delegate", Text = "Delegate" }, true);
        }

        // Handle TypeScript union-with-null (e.g. "string | null") → nullable C# type (e.g. "string?").
        if (typeRef.Text.EndsWith(" | null", StringComparison.Ordinal))
        {
            const string Suffix = " | null";
            int suffixLen = Suffix.Length;

            string strippedText = typeRef.Text.Substring(0, typeRef.Text.Length - suffixLen).Trim();
            string strippedName = typeRef.Name.EndsWith(Suffix, StringComparison.Ordinal)
                ? typeRef.Name.Substring(0, typeRef.Name.Length - suffixLen).Trim()
                : typeRef.Name;

            // Detect array syntax that was hidden behind the union suffix (e.g. "Foo[] | null").
            bool strippedIsArray = typeRef.IsArray;
            if (!strippedIsArray && strippedText.EndsWith("[]", StringComparison.Ordinal))
            {
                strippedText = strippedText[..^2];
                if (strippedName.EndsWith("[]", StringComparison.Ordinal))
                    strippedName = strippedName[..^2];
                strippedIsArray = true;
            }

            var strippedRef = new TsTypeRef(
                Text: strippedText,
                Name: strippedName,
                TypeArgs: typeRef.TypeArgs,
                IsArray: strippedIsArray);
            var (inner, wasResolved) = ResolveTypeRef(strippedRef);
            return (inner with { Text = inner.Text + "?" }, wasResolved);
        }

        // Handle TypeScript union-with-undefined (e.g. "string | undefined") → nullable C# type (e.g. "string?").
        if (typeRef.Text.EndsWith(" | undefined", StringComparison.Ordinal))
        {
            const string Suffix = " | undefined";
            int suffixLen = Suffix.Length;

            string strippedText = typeRef.Text.Substring(0, typeRef.Text.Length - suffixLen).Trim();
            string strippedName = typeRef.Name.EndsWith(Suffix, StringComparison.Ordinal)
                ? typeRef.Name.Substring(0, typeRef.Name.Length - suffixLen).Trim()
                : typeRef.Name;

            // Detect array syntax that was hidden behind the union suffix (e.g. "Foo[] | undefined").
            bool strippedIsArray = typeRef.IsArray;
            if (!strippedIsArray && strippedText.EndsWith("[]", StringComparison.Ordinal))
            {
                strippedText = strippedText[..^2];
                if (strippedName.EndsWith("[]", StringComparison.Ordinal))
                    strippedName = strippedName[..^2];
                strippedIsArray = true;
            }

            var strippedRef = new TsTypeRef(
                Text: strippedText,
                Name: strippedName,
                TypeArgs: typeRef.TypeArgs,
                IsArray: strippedIsArray);
            var (inner, wasResolved) = ResolveTypeRef(strippedRef);
            return (inner with { Text = inner.Text + "?" }, wasResolved);
        }

        // Recursively resolve type arguments first.
        bool allArgsResolved = true;
        TsTypeRef[] resolvedArgs = typeRef.TypeArgs.Length == 0
            ? typeRef.TypeArgs
            : [.. typeRef.TypeArgs.Select(arg =>
            {
                var (r, ok) = ResolveTypeRef(arg);
                if (!ok) allArgsResolved = false;
                return r;
            })];

        // Attempt to resolve the base name.
        string resolvedName = ResolveName(typeRef.Name, out bool nameResolved);

        // Rebuild the Text representation with resolved parts.
        string resolvedText = resolvedArgs.Length > 0
            ? $"{resolvedName}<{string.Join(", ", resolvedArgs.Select(a => a.Text))}>"
            : typeRef.IsArray
                ? $"{resolvedName}[]"
                : resolvedName;

        var result = typeRef with
        {
            Name = resolvedName,
            Text = resolvedText,
            TypeArgs = resolvedArgs,
        };

        return (result, nameResolved && allArgsResolved);
    }

    /// <summary>
    /// Resolves a single type base name against TypeMap, NodeApiMap, NpmPackageMap, local types, and CallSiteMap.
    /// Local types defined in the source files are considered "resolved" to prevent false TODO markers.
    /// </summary>
    private string ResolveName(string name, out bool resolved)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            resolved = false;
            return name;
        }

        // TypeMap covers TS primitives and well-known generics.
        // Use Contains() to distinguish "type found but identity-mapped" from "type not found".
        if (_typeMap.Contains(name))
        {
            resolved = true;
            return _typeMap.Resolve(name);
        }

        // NodeApiMap (e.g. "fs.readFile").
        if (_nodeApi.TryResolve(name, out string nodeResult))
        {
            resolved = true;
            return nodeResult;
        }

        // NpmPackageMap (e.g. "axios").
        if (_npmPackage.TryResolve(name, out string npmResult))
        {
            resolved = true;
            return npmResult;
        }

        // CallSiteMap (e.g. "fs.readFileSync").
        if (_callSiteMap.TryResolve(name, out string callSiteResult))
        {
            resolved = true;
            return callSiteResult;
        }

        // Local types (classes, interfaces, enums, aliases defined in source files).
        // These are considered "resolved" to avoid marking them as TODO.
        if (_localTypes.Contains(name))
        {
            resolved = true;
            return name;
        }

        // Unresolved — return the original name, caller decides whether to tag.
        resolved = false;
        return name;
    }

    // ── Cookie helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> and the override value when any cookie starts with
    /// <c>@ct:map=</c>.
    /// </summary>
    private static bool TryGetMapCookie(string[] cookies, out string value)
    {
        foreach (string cookie in cookies)
        {
            if (cookie.StartsWith(MapCookiePrefix, StringComparison.Ordinal))
            {
                value = cookie[MapCookiePrefix.Length..];
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static string[] AppendTodoUnresolved(string[] cookies)
    {
        // Only append once.
        if (Array.IndexOf(cookies, TodoUnresolved) >= 0)
            return cookies;

        return [.. cookies, TodoUnresolved];
    }
}

