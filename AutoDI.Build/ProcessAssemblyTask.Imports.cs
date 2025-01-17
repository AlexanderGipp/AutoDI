﻿using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace AutoDI.Build
{
    public partial class ProcessAssemblyTask
    {
        private Imports Import { get; set; }

        private void LoadRequiredData()
        {
            if (Import is null)
            {
                Import = new Imports(TypeResolver.ResolveType, ModuleDefinition, this);
            }
        }

        internal class Imports
        {
            public Imports(Func<string, TypeDefinition> findType, ModuleDefinition moduleDefinition,
                ProcessAssemblyTask processAssemblyTask)
            {
                System = new SystemImport(findType, moduleDefinition);
                DependencyInjection = new DependencyInjectionImport(findType, moduleDefinition);
                AutoDI = new AutoDIImport(findType, moduleDefinition, processAssemblyTask, this);
            }

            public SystemImport System { get; }

            public AutoDIImport AutoDI { get; }

            public DependencyInjectionImport DependencyInjection { get; }

            public class SystemImport
            {
                public SystemImport(Func<string, TypeDefinition> findType, ModuleDefinition moduleDefinition)
                {
                    Action = new ActionImport(findType, moduleDefinition);
                    Type = new TypeImport(findType, moduleDefinition);
                    Collections = new CollectionsImport(findType, moduleDefinition);
                    IDisposable = new DisposableImport(findType, moduleDefinition);

                    IServiceProvider = moduleDefinition.ImportReference(findType("System.IServiceProvider"));
                    Exception = moduleDefinition.ImportReference(findType("System.Exception"));
                    Object = moduleDefinition.ImportReference(findType("System.Object"));
                    Void = moduleDefinition.ImportReference(findType("System.Void"));

                    var aggregateExceptionType = findType("System.AggregateException");
                    var enumerableException = Collections.Enumerable.MakeGenericInstanceType(Exception);

                    AggregateExceptionCtor = moduleDefinition.ImportReference(aggregateExceptionType
                        .GetConstructors().Single(c =>
                            c.Parameters.Count == 2 &&
                            c.Parameters[0].ParameterType.IsType<string>() &&
                            c.Parameters[1].ParameterType.IsType(enumerableException)));

                    Func2Ctor =
                        moduleDefinition.ImportReference(findType("System.Func`2")).Resolve().GetConstructors().Single();
                }

                public TypeReference Exception { get; }

                public TypeReference Object { get; }

                public TypeReference Void { get; }

                public TypeReference IServiceProvider { get; }

                public ActionImport Action { get; }

                public DisposableImport IDisposable { get; }

                public TypeImport Type { get; }

                public CollectionsImport Collections { get; }

                public MethodReference AggregateExceptionCtor { get; }

                public MethodReference Func2Ctor { get; }

                public class ActionImport
                {
                    public TypeReference Type { get; }

                    public MethodReference Ctor { get; }

                    public MethodReference Invoke { get; }

                    public ActionImport(Func<string, TypeDefinition> findType, ModuleDefinition moduleDefinition)
                    {
                        Type = moduleDefinition.ImportReference(findType("System.Action`1"));

                        var resolved = Type.Resolve();

                        Invoke = moduleDefinition.ImportReference(resolved.GetMethods().Single(x => x.Name == "Invoke"));

                        Ctor = moduleDefinition.ImportReference(resolved.GetConstructors().Single());
                    }
                }

                public class TypeImport
                {
                    public TypeReference Type { get; }

                    public MethodReference GetTypeFromHandle { get; }


                    public TypeImport(Func<string, TypeDefinition> findType, ModuleDefinition moduleDefinition)
                    {
                        var type = findType("System.Type");
                        Type = moduleDefinition.ImportReference(type);

                        GetTypeFromHandle =
                            moduleDefinition.ImportReference(type.GetMethods().Single(m => m.Name == "GetTypeFromHandle"));
                    }
                }

                public class CollectionsImport
                {
                    public CollectionsImport(Func<string, TypeDefinition> findType, ModuleDefinition moduleDefinition)
                    {
                        List = new ListImport(findType, moduleDefinition);
                        Enumerable = moduleDefinition.ImportReference(findType("System.Collections.Generic.IEnumerable`1"));

                    }

                    public ListImport List { get; }

                    public TypeReference Enumerable { get; }


                    public class ListImport
                    {
                        public ListImport(Func<string, TypeDefinition> findType, ModuleDefinition moduleDefinition)
                        {
                            TypeDefinition type = findType("System.Collections.Generic.List`1");

                            Type = moduleDefinition.ImportReference(type);
                            Ctor = moduleDefinition.ImportReference(type.GetConstructors()
                                .Single(c => c.IsPublic && c.Parameters.Count == 0));
                            Add = moduleDefinition.ImportReference(type.GetMethods()
                                .Single(m => m.Name == "Add" && m.IsPublic && m.Parameters.Count == 1));
                            Count = moduleDefinition.ImportReference(type.GetMethods()
                                .Single(m => m.IsPublic && m.Name == "get_Count"));
                        }

                        public TypeReference Type { get; }

                        public MethodReference Ctor { get; }

                        public MethodReference Add { get; }

                        public MethodReference Count { get; }
                    }

                }

                public class DisposableImport
                {
                    public TypeReference Type { get; }

                    public MethodReference Dispose { get; }

                    public DisposableImport(Func<string, TypeDefinition> findType, ModuleDefinition moduleDefinition)
                    {
                        Type = moduleDefinition.ImportReference(findType("System.IDisposable"));

                        var resolved = Type.Resolve();

                        Dispose = moduleDefinition.ImportReference(resolved.GetMethods().Single(x => x.Name == "Dispose"));
                    }
                }
            }

            public class AutoDIImport
            {
                public AutoDIExceptionsImport Exceptions { get; }

                public IApplicationBuilderImport IApplicationBuilder { get; }

                public ApplicationBuilderImport ApplicationBuilder { get; }

                public GlobalDIImport GlobalDI { get; }

                public ServiceCollectionMixinsImport ServiceCollectionMixins { get; }

                public TypeReference DependencyAttributeType { get; }

                public AutoDIImport(Func<string, TypeDefinition> findType, ModuleDefinition moduleDefinition,
                    ProcessAssemblyTask processAssemblyTask, Imports imports)
                {
                    Exceptions = new AutoDIExceptionsImport(findType, moduleDefinition);
                    IApplicationBuilder = new IApplicationBuilderImport(findType, moduleDefinition, processAssemblyTask, imports);
                    ApplicationBuilder = new ApplicationBuilderImport(findType, moduleDefinition);
                    GlobalDI = new GlobalDIImport(findType, moduleDefinition);
                    ServiceCollectionMixins = new ServiceCollectionMixinsImport(findType, moduleDefinition, imports);

                    DependencyAttributeType = moduleDefinition.ImportReference(findType("AutoDI.DependencyAttribute"));
                }

                public class AutoDIExceptionsImport
                {
                    public AutoDIExceptionsImport(Func<string, TypeDefinition> findType, ModuleDefinition moduleDefinition)
                    {
                        TypeDefinition alreadyInitialized = findType("AutoDI.AlreadyInitializedException");
                        AlreadyInitializedExceptionCtor = moduleDefinition.ImportReference(alreadyInitialized.GetConstructors().Single(x => !x.HasParameters));

                        var autoDIExceptionType = findType("AutoDI.AutoDIException");
                        AutoDIExceptionCtor = moduleDefinition.ImportReference(autoDIExceptionType.GetConstructors().Single(c =>
                            c.Parameters.Count == 2 && c.Parameters[0].ParameterType.IsType<string>() &&
                            c.Parameters[1].ParameterType.IsType<Exception>()));
                    }

                    public MethodReference AlreadyInitializedExceptionCtor { get; }

                    public MethodReference AutoDIExceptionCtor { get; }
                }

                public class IApplicationBuilderImport
                {
                    public const string TypeName = "AutoDI.IApplicationBuilder";

                    public IApplicationBuilderImport(Func<string, TypeDefinition> findType,
                        ModuleDefinition moduleDefinition, ProcessAssemblyTask processAssemblyTask,
                        Imports imports)
                    {
                        Type = moduleDefinition.ImportReference(findType(TypeName));
                        TypeDefinition resolved = Type.Resolve();

                        var configureServices = resolved
                            .GetMethods()
                            .Single(x => x.Name == "ConfigureServices");
                        configureServices.Parameters[0].ParameterType =
                            imports.System.Action.Type.MakeGenericInstanceType(imports.DependencyInjection
                                .IServiceCollection);
                        ConfigureServices = moduleDefinition.ImportReference(configureServices);
                    
                        Build = moduleDefinition.ImportReference(resolved.GetMethods().Single(x => x.Name == "Build"));
                    }

                    public TypeReference Type { get; }

                    public MethodReference ConfigureServices { get; }

                    public MethodReference Build { get; }
                }

                public class ApplicationBuilderImport
                {
                    public ApplicationBuilderImport(Func<string, TypeDefinition> findType, ModuleDefinition moduleDefinition)
                    {
                        Type = findType("AutoDI.ApplicationBuilder");

                        Ctor = moduleDefinition.ImportReference(Type.GetConstructors().Single(x => !x.HasParameters));
                    }

                    public TypeDefinition Type { get; }

                    public MethodReference Ctor { get; }
                }

                public class GlobalDIImport
                {
                    public GlobalDIImport(Func<string, TypeDefinition> findType, ModuleDefinition moduleDefinition)
                    {
                        TypeDefinition globalDiType = findType("AutoDI.GlobalDI");


                        Register = moduleDefinition.ImportReference(globalDiType.GetMethods()
                            .Single(m => m.Name == "Register"));
                        Unregister = moduleDefinition.ImportReference(globalDiType.GetMethods()
                            .Single(m => m.Name == "Unregister"));
                        GetService = moduleDefinition.ImportReference(globalDiType.GetMethods()
                            .Single(m =>
                                m.Name == "GetService" && m.HasGenericParameters && m.Parameters.Count == 1));
                    }

                    public MethodReference Register { get; }
                    public MethodReference Unregister { get; }
                    public MethodReference GetService { get; }
                }

                public class ServiceCollectionMixinsImport
                {
                    public MethodReference AddAutoDIService { get; }

                    public ServiceCollectionMixinsImport(Func<string, TypeDefinition> findType, ModuleDefinition moduleDefinition, Imports imports)
                    {
                        var type = findType("Microsoft.Extensions.DependencyInjection.AutoDIServiceCollectionMixins");

                        var addAutoDIService = type.GetMethods().Single(m => m.Name == "AddAutoDIService" && m.Parameters.Count == 5);
                        addAutoDIService.Parameters[0].ParameterType = imports.DependencyInjection.IServiceCollection;
                        addAutoDIService.ReturnType = imports.DependencyInjection.IServiceCollection;

                        AddAutoDIService = moduleDefinition.ImportReference(addAutoDIService);
                    }
                }
            }

            public class DependencyInjectionImport
            {
                public DependencyInjectionImport(Func<string, TypeDefinition> findType, ModuleDefinition moduleDefinition)
                {
                    IServiceCollection = moduleDefinition.ImportReference(findType("Microsoft.Extensions.DependencyInjection.IServiceCollection"));

                    var serviceProviderExtensions = findType("Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions");
                    ServiceProviderServiceExtensionsGetService = moduleDefinition.ImportReference(serviceProviderExtensions.Methods.Single(x => x.Name == "GetService"));
                }

                public TypeReference IServiceCollection { get; }

                public MethodReference ServiceProviderServiceExtensionsGetService { get; }

            }
        }
    }
}