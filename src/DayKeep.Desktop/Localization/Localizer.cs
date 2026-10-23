using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Data;
using System.Windows.Markup;

namespace DayKeep.Localization;

public sealed record LanguageChoice(string Code,string NativeName,string CultureName)
{
    public override string ToString()=>NativeName;
}

public static class Localizer
{
    public static IReadOnlyList<LanguageChoice> Languages {get;} = Array.AsReadOnly<LanguageChoice>([
        new("zh-CN","简体中文","zh-CN"),new("zh-TW","繁體中文","zh-TW"),
        new("en","English","en-US"),new("ja","日本語","ja-JP"),new("ko","한국어","ko-KR"),
        new("fr","Français","fr-FR"),new("de","Deutsch","de-DE"),new("es","Español","es-ES"),new("pt","Português","pt-BR")
    ]);
    private static readonly Dictionary<string,Dictionary<string,string>> Catalogs=Load();
    private static readonly Dictionary<string,LocalizedValue> Values=new(StringComparer.Ordinal);
    private static readonly Dictionary<string,string> Reverse=BuildReverse();
    private static readonly List<MessagePattern> Patterns=BuildPatterns();
    private static string _code="zh-CN";
    public static string LanguageCode=>_code;
    public static CultureInfo Culture=>CultureInfo.GetCultureInfo(Languages.Single(l=>l.Code==_code).CultureName);
    public static event EventHandler? Changed;

    public static string NormalizeCode(string? code)
    {
        var exact=Languages.FirstOrDefault(l=>string.Equals(l.Code,code,StringComparison.OrdinalIgnoreCase));
        if(exact is not null) return exact.Code;
        if(code is "zh-Hant" or "zh-HK" or "zh-MO") return "zh-TW";
        var language=code?.Split('-')[0];
        return Languages.FirstOrDefault(l=>string.Equals(l.Code,language,StringComparison.OrdinalIgnoreCase))?.Code ?? "zh-CN";
    }
    public static bool SetLanguage(string? code)
    {
        var normalized=NormalizeCode(code);if(normalized==_code) return false;
        _code=normalized;
        foreach(var value in Values.Values.ToArray()) value.Refresh();
        Changed?.Invoke(null,EventArgs.Empty);return true;
    }
    public static string T(string key,params object?[] args)
    {
        var text=Catalogs[_code].GetValueOrDefault(key) ?? Catalogs["en"].GetValueOrDefault(key) ?? key;
        return args.Length==0 ? text : string.Format(Culture,text,args);
    }
    // Only use this at presentation boundaries for app-owned messages, never task titles or notes.
    public static string Message(string text)
    {
        if(Catalogs["zh-CN"].ContainsKey(text)) return T(text);
        if(Reverse.TryGetValue(text,out var key)) return T(key);
        foreach(var pattern in Patterns)
        {
            var match=pattern.Pattern.Match(text);if(!match.Success) continue;
            var args=Enumerable.Range(0,pattern.Arguments).Select(i=>(object?)match.Groups["v"+i].Value).ToArray();
            return T(pattern.Key,args);
        }
        return text;
    }
    internal static LocalizedValue Value(string key)
    {
        if(!Values.TryGetValue(key,out var value)) Values.Add(key,value=new(key));
        return value;
    }
    public static IReadOnlyCollection<string> Keys=>Catalogs["zh-CN"].Keys;
    public static IReadOnlyDictionary<string,string> Catalog(string code)=>Catalogs[NormalizeCode(code)];
    private static Dictionary<string,Dictionary<string,string>> Load()
    {
        var result=new Dictionary<string,Dictionary<string,string>>(StringComparer.Ordinal);
        foreach(var choice in Languages)
        {
            using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("DayKeep.Localization."+choice.Code+".json") ?? throw new InvalidDataException("Missing language resource: "+choice.Code);
            var catalog=JsonSerializer.Deserialize<Dictionary<string,string>>(stream,new JsonSerializerOptions {AllowDuplicateProperties=false}) ?? throw new InvalidDataException("Invalid language resource.");
            if(catalog.Any(pair=>string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))) throw new InvalidDataException("Empty language entry: "+choice.Code);
            result.Add(choice.Code,catalog);
        }
        var keys=result["zh-CN"].Keys.ToHashSet(StringComparer.Ordinal);
        foreach(var (code,catalog) in result)
            if(!keys.SetEquals(catalog.Keys)) throw new InvalidDataException("Language keys do not match: "+code);
        return result;
    }
    private static Dictionary<string,string> BuildReverse()
    {
        var result=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var catalog in Catalogs.Values) foreach(var (key,value) in catalog) result.TryAdd(value,key);
        return result;
    }
    private sealed record MessagePattern(string Key,Regex Pattern,int Arguments);
    private static List<MessagePattern> BuildPatterns()
    {
        var result=new List<MessagePattern>();
        foreach(var catalog in Catalogs.Values) foreach(var (key,value) in catalog)
        {
            var placeholders=Regex.Matches(value,@"\{(\d+)(?:[^{}]*)\}");if(placeholders.Count==0) continue;
            var pattern=new StringBuilder("^");var offset=0;var max=0;
            foreach(Match match in placeholders)
            {
                var index=int.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture);max=Math.Max(max,index+1);
                pattern.Append(Regex.Escape(value[offset..match.Index])).Append("(?<v").Append(index).Append(">.*?)");offset=match.Index+match.Length;
            }
            pattern.Append(Regex.Escape(value[offset..])).Append('$');
            result.Add(new(key,new(pattern.ToString(),RegexOptions.CultureInvariant|RegexOptions.Singleline|RegexOptions.NonBacktracking,TimeSpan.FromMilliseconds(100)),max));
        }
        return result;
    }
}

internal sealed class LocalizedValue(string key) : INotifyPropertyChanged
{
    public string Text=>Localizer.T(key);
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Refresh()=>PropertyChanged?.Invoke(this,new(nameof(Text)));
}

[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public string Key {get;set;}="";
    public override object ProvideValue(IServiceProvider serviceProvider)=>new Binding(nameof(LocalizedValue.Text))
    {Source=Localizer.Value(Key),Mode=BindingMode.OneWay}.ProvideValue(serviceProvider);
}
