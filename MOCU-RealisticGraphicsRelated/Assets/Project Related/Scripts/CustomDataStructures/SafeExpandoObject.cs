using System.Collections.Generic;
using System.Dynamic;


public class SafeExpandoObject : DynamicObject
{
    private readonly IDictionary<string, object> _properties = new ExpandoObject();


    public override bool TryGetMember(GetMemberBinder binder, out object result)
    {
        if (_properties.TryGetValue(binder.Name, out result))
            return true;

        throw new KeyNotFoundException($"Property '{binder.Name}' does not exist.");
    }

    public override bool TrySetMember(SetMemberBinder binder, object value)
    {
        _properties[binder.Name] = value;
        return true;
    }

    public object Get(string propertyName)
    {
        if (_properties.TryGetValue(propertyName, out var value))
            return value;

        throw new KeyNotFoundException($"Property '{propertyName}' does not exist.");
    }

    public void Set(string propertyName, object value)
    {
        _properties[propertyName] = value;
    }
}