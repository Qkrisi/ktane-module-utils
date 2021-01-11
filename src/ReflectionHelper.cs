using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public static class ReflectionHelper
{
    /// <summary>
    /// Get the specified type
    /// </summary>
    /// <param name="fullName">Name of the type</param>
    /// <param name="assemblyName">Name of the assembly containing the type (optional)</param>
    /// <returns>The found type</returns>
    public static Type FindType(string fullName, string assemblyName = null)
    {
        return AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetSafeTypes()).FirstOrDefault(t => t.FullName != null && t.FullName.Equals(fullName) && (assemblyName==null || t.Assembly.GetName().Name.Equals(assemblyName)));
    }
    
    /// <summary>
    /// Get all successfully loaded types in the passed Assembly
    /// </summary>
    /// <param name="assembly">Assembly to search types in</param>
    /// <returns>The types in the assembly</returns>
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