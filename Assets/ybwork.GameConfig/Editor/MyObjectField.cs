using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.UIElements;

public class MyObjectField : Foldout
{
    private readonly ListView _listView;
    private readonly MemberInfo[] _members;
    private readonly Type _type;
    private readonly Dictionary<string, VisualElement> _fields = new();
    private object _data;

    public MyObjectField(ListView listView)
    {
        this._listView = listView;

        _type = listView.itemsSource.GetType().GetGenericArguments()[0];
        _members = _type.GetMembers()
            .Where(member => member.MemberType is MemberTypes.Field or MemberTypes.Property)
            .ToArray();
        foreach (MemberInfo member in _members)
        {
            var memberType = GetMemberType(member);
            if (memberType == typeof(int))
            {
                IntegerField field = new() { label = member.Name };
                Add(field);
                _fields.Add(member.Name, field);
            }
            else if (memberType == typeof(long))
            {
                LongField field = new() { label = member.Name };
                Add(field);
                _fields.Add(member.Name, field);
            }
            else if (memberType == typeof(string))
            {
                TextField field = new() { label = member.Name };
                Add(field);
                _fields.Add(member.Name, field);
            }
            else if (memberType == typeof(float))
            {
                FloatField field = new() { label = member.Name };
                Add(field);
                _fields.Add(member.Name, field);
            }
            else if (memberType == typeof(double))
            {
                DoubleField field = new() { label = member.Name };
                Add(field);
                _fields.Add(member.Name, field);
            }
            else if (memberType == typeof(bool))
            {
                Toggle field = new() { label = member.Name };
                Add(field);
                _fields.Add(member.Name, field);
            }
        }
    }

    public void Bind(int index)
    {
        if (_listView.itemsSource[index] == null)
        {
            _listView.itemsSource[index] = _type.GetConstructor(new Type[] { }).Invoke(new object[] { });
        }
        _data = _listView.itemsSource[index];
        text = "Item " + _listView.itemsSource.IndexOf(_data);

        void BindMember<TMember>(MemberInfo member, BaseField<TMember> input)
        {
            if (member.MemberType == MemberTypes.Property)
            {
                PropertyInfo prop = (PropertyInfo)member;
                input.value = (TMember)prop.GetValue(_data);
                input.RegisterValueChangedCallback(evt => prop.SetValue(_data, evt.newValue));
            }
            else if (member.MemberType == MemberTypes.Field)
            {
                FieldInfo field = (FieldInfo)member;
                input.value = (TMember)field.GetValue(_data);
                input.RegisterValueChangedCallback(evt => field.SetValue(_data, evt.newValue));
            }
        }

        foreach (MemberInfo member in _members)
        {
            var memberType = GetMemberType(member);
            if (memberType == typeof(int))
            {
                IntegerField field = (IntegerField)_fields[member.Name];
                BindMember(member, field);
            }
            else if (memberType == typeof(long))
            {
                LongField field = (LongField)_fields[member.Name];
                BindMember(member, field);
            }
            else if (memberType == typeof(string))
            {
                TextField field = (TextField)_fields[member.Name];
                BindMember(member, field);
            }
            else if (memberType == typeof(float))
            {
                FloatField field = (FloatField)_fields[member.Name];
                BindMember(member, field);
            }
            else if (memberType == typeof(double))
            {
                DoubleField field = (DoubleField)_fields[member.Name];
                BindMember(member, field);
            }
            else if (memberType == typeof(bool))
            {
                Toggle field = (Toggle)_fields[member.Name];
                BindMember(member, field);
            }
        }
    }

    private static Type GetMemberType(MemberInfo member)
    {
        if (member.MemberType == MemberTypes.Property)
        {
            PropertyInfo prop = (PropertyInfo)member;
            return prop.PropertyType;
        }
        else if (member.MemberType == MemberTypes.Field)
        {
            FieldInfo field = (FieldInfo)member;
            return field.FieldType;
        }
        return null;
    }
}
