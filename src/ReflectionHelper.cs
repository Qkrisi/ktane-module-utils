using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public static class ReflectionHelper
{
    public static Type FindType(string fullName, string assemblyName = null)
    {
        return AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetSafeTypes()).FirstOrDefault(t => t.FullName != null && t.FullName.Equals(fullName) && (assemblyName==null || t.Assembly.GetName().Name.Equals(assemblyName)));
    }
    
    public static IEnumerable<Type> GetSafeTypes(this Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(x => x != null);
        }
        catch (Exception)
        {
            return new List<Type>();
        }
    }
}