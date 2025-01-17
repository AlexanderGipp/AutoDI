﻿using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Mono.Cecil;

namespace AutoDI.AssemblyGenerator
{
    public static class AssemblyMixins
    {
        public static object GetStaticProperty<TContainingType>(this Assembly assembly, string propertyName, Type containerType = null) where TContainingType : class
        {
            if (assembly is null) throw new ArgumentNullException(nameof(assembly));
            if (propertyName is null) throw new ArgumentNullException(nameof(propertyName));

            string typeName = TypeMixins.GetTypeName(typeof(TContainingType), containerType);
            Type type = assembly.GetType(typeName);
            if (type is null)
                throw new AssemblyGetPropertyException($"Could not find '{typeof(TContainingType).FullName}' in '{assembly.FullName}'");

            PropertyInfo property = type.GetProperty(propertyName);
            if (property is null)
                throw new AssemblyGetPropertyException($"Could not find property '{propertyName}' on type '{type.FullName}'");

            if (property.GetMethod is null)
                throw new AssemblyGetPropertyException($"Property '{type.FullName}.{propertyName}' does not have a getter");

            if (!property.GetMethod.IsStatic)
                throw new AssemblyGetPropertyException($"Property '{type.FullName}.{propertyName}' is not static");

            return property.GetValue(null);
        }

        public static object InvokeStatic<TContainingType>(this Assembly assembly, string methodName, params object[] parameters) where TContainingType : class
        {
            if (assembly is null) throw new ArgumentNullException(nameof(assembly));
            if (methodName is null) throw new ArgumentNullException(nameof(methodName));

            Type type = assembly.GetType(typeof(TContainingType).FullName);
            if (type is null)
                throw new AssemblyInvocationException($"Could not find '{typeof(TContainingType).FullName}' in '{assembly.FullName}'");

            MethodInfo method = type.GetMethod(methodName);
            if (method is null)
                throw new AssemblyInvocationException($"Could not find method '{methodName}' on type '{type.FullName}'");

            if (!method.IsStatic)
                throw new AssemblyInvocationException($"Method '{type.FullName}.{methodName}' is not static");

            return method.Invoke(null, parameters);
        }

        public static void InvokeEntryPoint(this Assembly assembly)
        {
            if (assembly is null) throw new ArgumentNullException(nameof(assembly));
            
            assembly.EntryPoint.Invoke(null, new object[assembly.EntryPoint.GetParameters().Length]);
        }

        public static object InvokeGeneric<TGeneric>(this Assembly assembly, object target, string methodName, params object[] parameters)
        {
            Type genericType = assembly.GetType(typeof(TGeneric).FullName);
            if (genericType is null)
                throw new AssemblyInvocationException($"Could not find generic parameter type '{typeof(TGeneric).FullName}' in '{assembly.FullName}'");

            return InvokeGeneric(assembly, genericType, target, methodName, parameters);
        }

        public static object InvokeGeneric(this Assembly assembly, Type genericType, object target, string methodName, params object[] parameters)
        {
            if (assembly is null) throw new ArgumentNullException(nameof(assembly));
            if (genericType is null) throw new ArgumentNullException(nameof(genericType));
            if (target is null) throw new ArgumentNullException(nameof(target));
            if (methodName is null) throw new ArgumentNullException(nameof(methodName));

            Type targetType = target.GetType();

            IEnumerable<MethodInfo> methods = targetType.GetRuntimeMethods().Where(x => x.Name == methodName)
                .Union(targetType.GetInterfaces()
                    .SelectMany(@interface => @interface.GetRuntimeMethods().Where(x => x.Name == methodName)));

            MethodInfo method = methods.FirstOrDefault(m => !m.IsStatic && m.IsGenericMethodDefinition);
            if (method is null)
                throw new AssemblyInvocationException($"Could not find method '{methodName}' on type '{targetType.FullName}'");
            
            MethodInfo genericMethod = method.MakeGenericMethod(genericType);

            return genericMethod.Invoke(target, parameters);
        }

        public static object CreateInstance<T>(this Assembly assembly, Type containerType = null)
        {
            if (assembly is null) throw new ArgumentNullException(nameof(assembly));

            string typeName = TypeMixins.GetTypeName(typeof(T), containerType);
            Type type = assembly.GetType(typeName);
            if (type is null)
                throw new AssemblyCreateInstanceException($"Could not find '{typeName}' in '{assembly.FullName}'");

            foreach (ConstructorInfo ctor in type.GetConstructors().OrderBy(c => c.GetParameters().Length))
            {
                var parameters = ctor.GetParameters();
                if (parameters.All(pi => pi.HasDefaultValue))
                {
                    return ctor.Invoke(parameters.Select(x => x.DefaultValue).ToArray());
                }
            }
            throw new AssemblyCreateInstanceException($"Could not find valid constructor for '{typeof(T).FullName}'");
        }

        public static object Resolve<T>(this Assembly assembly, Type containerType = null)
        {
            if (assembly is null) throw new ArgumentNullException(nameof(assembly));

            string typeName = TypeMixins.GetTypeName(typeof(T), containerType);
            Type type = assembly.GetType(typeName);
            if (type is null)
                throw new AssemblyCreateInstanceException($"Could not find '{typeName}' in '{assembly.FullName}'");


            IServiceProvider provider = DI.GetGlobalServiceProvider(assembly);

            if (provider is null)
            {
                //TODO: Better exception
                throw new AssemblyInvocationException($"Could not find service provider for '{assembly.FullName}'");
            }

            return provider.GetService(type);
        }

        public static Assembly SingleAssembly(this IDictionary<string, AssemblyInfo> assemblies)
        {
            return assemblies?.Select(x => x.Value.Assembly).Single();
        }

        public static ModuleDefinition SingleModule(this IDictionary<string, AssemblyInfo> assemblies)
        {
            return assemblies?.Select(x => ModuleDefinition.ReadModule(x.Value.FilePath)).Single();
        }
    }
}