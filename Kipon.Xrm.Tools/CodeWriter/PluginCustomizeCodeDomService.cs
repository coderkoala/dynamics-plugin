﻿using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Crm.Services.Utility;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Extensions;
using Microsoft.Xrm.Sdk.Metadata;


namespace Kipon.Xrm.Tools.CodeWriter
{
    public class PluginCustomizeCodeDomService : ICustomizeCodeDomService
    {
        #region static code to be generated
        private const string CRM_UNIT_OF_WORK_GENERIC = @"        public void Dispose()
        {
            context.Dispose();
        }

        public R ExecuteRequest<R>(OrganizationRequest request) where R : OrganizationResponse
        {
            return (R)this.context.Execute(request);
        }

        public OrganizationResponse Execute(OrganizationRequest request)
        {
            return this.context.Execute(request);
        }


        public Guid Create(Entity entity)
        {
            return this._service.Create(entity);
        }

        public void Update(Entity entity)
        {
            this._service.Update(entity);
        }

        public void Delete(Entity entity)
        {
            this._service.Delete(entity.LogicalName, entity.Id);
        }

        public void ClearContext()
        {
            var candidates = this.context.GetAttachedEntities().ToArray();
            foreach (var can in candidates) 
            {
                context.Detach(can);
            }
        }

        public void Detach(string logicalName, params Guid[] ids)
        {
            if (this.context != null)
            {
                var candidates = (from c in this.context.GetAttachedEntities() where c.LogicalName == logicalName select c);
                if (ids != null && ids.Length > 0)
                {
                    candidates = (from c in candidates where ids.Contains(c.Id) select c);
                }
                foreach (var r in candidates.ToArray())
                {
                    context.Detach(r);
                }
            }
        }

        public void Detach(Microsoft.Xrm.Sdk.EntityReference eref)
        {
            this.Detach(eref.LogicalName, eref.Id);
        }

        public void Detach(Microsoft.Xrm.Sdk.Entity ent)
        {
            this.Detach(ent.LogicalName, ent.Id);
        }";

        // 0 = namespace, 1 = ctxName
        private const string CRM_REPOSITORY_IMPL = @"   public class CrmRepository<T> : Kipon.Xrm.IRepository<T> where T: Microsoft.Xrm.Sdk.Entity, new() 
    {{
        private {1} context;
        private Microsoft.Xrm.Sdk.IOrganizationService _service;

        public CrmRepository({1} context, Microsoft.Xrm.Sdk.IOrganizationService service)
        {{
            this.context = context;
            this._service = service;
        }}

        public IQueryable<T> GetQuery()
        {{
            return context.CreateQuery<T>();
        }}

        public void Delete(T entity)
        {{
            this._service.Delete(entity.LogicalName, entity.Id);
            this.context.Detach(entity);
        }}

        public void Add(T entity)
        {{
            this._service.Create(entity);
            this.context.Attach(entity);
        }}

        public void Attach(T entity)
        {{
            this.context.Attach(entity);
        }}

        public void Detach(T entity)
        {{
            this.context.Detach(entity);
        }}

        public void Update(T entity)
        {{
            this._service.Update(entity);
            if (!this.context.IsAttached(entity))
            {{
                this.context.Attach(entity);
            }} else 
            {{
                var ch = (from c in this.context.GetAttachedEntities() 
                          where c.LogicalName == entity.LogicalName && 
                                c.Id == entity.Id 
                          select c).Single();

                foreach (var key in entity.Attributes.Keys)
                {{
                    // update the cache silent
                    ch.Attributes.Remove(key);
                    ch.Attributes.Add(key, entity[key]);
                }}
            }}
        }}

        public T GetById(Guid id)
        {{
            return (from q in this.GetQuery()
                    where q.Id == id
                    select q).Single();
        }}
    }}";

        #endregion

        void ICustomizeCodeDomService.CustomizeCodeDom(CodeCompileUnit codeUnit, IServiceProvider services)
        {
            Console.WriteLine("c 2020 Kipon ApS, " + this.GetType().FullName + ", Version: " + Kipon.Xrm.Tools.Version.No + ". All rights reserved");
            var ns = (from c in Environment.GetCommandLineArgs() where c.StartsWith("/namespace:") select c.Split(':')[1]).Single();
            var xrmNS = "Kipon.Xrm";

            var nsa = ns.Replace("Entities", "Actions");
            {
                var ap = (from c in Environment.GetCommandLineArgs() where c.StartsWith("/action-namespace:") select c).FirstOrDefault();
                if (!string.IsNullOrEmpty(ap))
                {
                    nsa = ap.Split(':')[1];
                }
            }

            var ctxName = (from c in Environment.GetCommandLineArgs() where c.StartsWith("/ServiceContextName") select c.Split(':')[1]).Single();

            var entities = CodeWriterFilter.ENTITIES;
            var actions = CodeWriterFilter.ACTIONS;

            using (var writer = new System.IO.StreamWriter("CrmUnitOfWork.Design.cs", false))
            {
                var sharedService = new SharedCustomizeCodeDomService(writer);

                writer.WriteLine($"// Plugin Version: {Kipon.Xrm.Tools.Version.No}, Dynamics 365 svcutil solid extension tool by Kipon ApS (c) 2019, Kjeld Poulsen");
                writer.WriteLine("// This file is autogenerated. Do not touch the code manually.");
                writer.WriteLine("");
                writer.WriteLine("using System;");
                writer.WriteLine("using System.Linq;");
                writer.WriteLine("using Microsoft.Xrm.Sdk;");

                #region version
                writer.WriteLine($"namespace {xrmNS}");
                writer.WriteLine("{");
                writer.WriteLine("\tpublic sealed class Version");
                writer.WriteLine("\t{");
                writer.WriteLine($"\t\tpublic const string No = \"{ Kipon.Xrm.Tools.Version.No }\";");
                writer.WriteLine("\t}");
                writer.WriteLine("}");
                #endregion


                writer.WriteLine("namespace " + ns);
                /* NS */ writer.WriteLine("{");

                #region unitOfWork work print methods
                void printConstructor(string name)
                {
                    writer.WriteLine($"\t\tprivate {ctxName} context;");
                    writer.WriteLine("\t\tprivate IOrganizationService _service;");

                    /*    */ writer.WriteLine($"\t\tpublic {name}(IOrganizationService orgService)");
                    /* CO */ writer.WriteLine("\t\t{");
                    /*    */ writer.WriteLine($"\t\t\tthis._service = orgService;");
                    /*    */ writer.WriteLine($"\t\t\tthis.context = new {ctxName}(_service);");
                    /* CO */ writer.WriteLine("\t\t}");
                }

                void printGenericAndRepositories()
                {
                    writer.WriteLine("");
                    writer.WriteLine(CRM_UNIT_OF_WORK_GENERIC);
                    writer.WriteLine("");

                    writer.WriteLine("\t\tvoid Kipon.Xrm.IService.OnStepFinalized()");
                    writer.WriteLine("\t\t{");
                    writer.WriteLine("\t\t\tforeach (var e in this.context.GetAttachedEntities().ToArray()) this.context.Detach(e);");
                    writer.WriteLine("\t\t}");
                    writer.WriteLine("");

                    foreach (var logicalname in entities.Keys)
                    {
                        var uowname = entities[logicalname].ServiceName;
                        writer.WriteLine($"\t\tprivate {xrmNS}.IRepository<" + logicalname + "> _" + uowname.ToLower() + "; ");
                        writer.WriteLine($"\t\tpublic {xrmNS}.IRepository<" + logicalname + "> " + uowname);
                        /* R1 */ writer.WriteLine("\t\t{");
                        /*    */ writer.WriteLine("\t\t\tget");
                        /* R2 */ writer.WriteLine("\t\t\t{");
                        /*    */ writer.WriteLine("\t\t\t\tif (_" + uowname.ToLower() + " == null)");
                        /* R3 */ writer.WriteLine("\t\t\t\t\t{");
                        /*    */ writer.WriteLine("\t\t\t\t\t\t_" + uowname.ToLower() + " = new CrmRepository<" + logicalname + ">(this.context, this._service);");
                        /* R3 */ writer.WriteLine("\t\t\t\t\t}");
                        /*    */ writer.WriteLine("\t\t\t\treturn _" + uowname.ToLower() + ";");
                        /* R2 */ writer.WriteLine("\t\t\t}");
                        /* R1 */ writer.WriteLine("\t\t}");
                    }
                }
                #endregion

                #region generate crmunitofwork
                writer.WriteLine("\t[Kipon.Xrm.Attributes.Export(typeof(IUnitOfWork))]");
                writer.WriteLine("\t[Kipon.Xrm.Attributes.Export(typeof(Kipon.Xrm.IUnitOfWork))]");
                writer.WriteLine("\tpublic sealed partial class CrmUnitOfWork: IUnitOfWork, IDisposable, Kipon.Xrm.IService");
                /* UOW */ writer.WriteLine("\t{");

                printConstructor("CrmUnitOfWork");
                printGenericAndRepositories();
                /* UOW */ writer.WriteLine("\t}");
                #endregion

                #region generate admin unit of work
                writer.WriteLine("\t[Kipon.Xrm.Attributes.Export(typeof(IAdminUnitOfWork))]");
                writer.WriteLine("\t[Kipon.Xrm.Attributes.Export(typeof(Kipon.Xrm.IAdminUnitOfWork))]");
                writer.WriteLine("\tpublic sealed partial class AdminCrmUnitOfWork : IAdminUnitOfWork, IDisposable, Kipon.Xrm.IService");
                /* UOW */ writer.WriteLine("\t{");
                printConstructor("AdminCrmUnitOfWork");
                printGenericAndRepositories();
                /* UOW */
                writer.WriteLine("\t}");

                #endregion

                #region genrate based entity interfaces for target, preimage, postimage and mergedimage
                foreach (var logicalname in entities.Keys)
                {
                    writer.WriteLine($"\tpublic partial interface I{logicalname}Target : {xrmNS}.Target<{logicalname}>"+"{ }");
                    writer.WriteLine($"\tpublic partial interface I{logicalname}Preimage : {xrmNS}.Preimage<{logicalname}>" + "{ }");
                    writer.WriteLine($"\tpublic partial interface I{logicalname}Postimage : {xrmNS}.Postimage<{logicalname}>" + "{ }");
                    writer.WriteLine($"\tpublic partial interface I{logicalname}Mergedimage : {xrmNS}.Mergedimage<{logicalname}>" + "{ }");

                    writer.WriteLine($"\tpublic sealed partial class {logicalname} :");
                    writer.WriteLine($"\t\tI{logicalname}Target,");
                    writer.WriteLine($"\t\tI{logicalname}Preimage,");
                    writer.WriteLine($"\t\tI{logicalname}Postimage,");
                    writer.WriteLine($"\t\tI{logicalname}Mergedimage");
                    /* C */ writer.WriteLine("\t{");
                    /* C */ writer.WriteLine("\t}");
                }
                #endregion

                #region generate target references for steps that takes a target, ex. Delete, Set status etc.
                foreach (var logicalname in entities.Keys)
                {
                    /*   */ writer.WriteLine($"\tpublic sealed class {logicalname}Reference : {xrmNS}.TargetReference<{logicalname}>");
                    /* C */ writer.WriteLine("\t{");
                    /*   */ writer.WriteLine($"\t\tpublic {logicalname}Reference(EntityReference target): base(target)" + "{ }");
                    /*   */ writer.WriteLine($"\t\tprotected sealed override string _logicalName => {logicalname}.EntityLogicalName;");
                    /* C */ writer.WriteLine("\t}");
                }
                #endregion

                #region generate IUnitOfWork interface
                writer.WriteLine("\tpublic partial interface IUnitOfWork : Kipon.Xrm.IUnitOfWork");
                writer.WriteLine("\t{");
                writer.WriteLine("\t\t#region entity repositories");
                foreach (var logicalname in entities.Keys)
                {
                    var uowname = entities[logicalname];
                    writer.WriteLine("\t\tKipon.Xrm.IRepository<" + logicalname + "> " + uowname.ServiceName + " { get; }");
                }
                writer.WriteLine("\t\t#endregion");
                writer.WriteLine("\t}");
                #endregion

                #region admin unitof work
                writer.WriteLine("\tpublic partial interface IAdminUnitOfWork : Kipon.Xrm.IAdminUnitOfWork, IUnitOfWork { }");
                #endregion

                #region CRMRepository Impl
                writer.WriteLine(string.Format(CRM_REPOSITORY_IMPL, ns, ctxName));
                #endregion


                sharedService.GlobalOptionSets(CodeWriterFilter.GLOBAL_OPTIONSET_INDEX.Values);
                sharedService.EntityOptionsetProperties(
                    CodeWriterFilter.ENTITIES, 
                    CodeWriterFilter.GLOBAL_OPTIONSET_INDEX, 
                    CodeWriterFilter.ATTRIBUTE_SCHEMANAME_MAP,
                    CodeWriterFilter.SUPRESSMAPPEDSTANDARDOPTIONSETPROPERTIES);
                /* NS */
                writer.WriteLine("}");

                #region xrm extension methods
                /*    */ writer.WriteLine("namespace Kipon.Xrm.Extensions.Sdk");
                /* NS */ writer.WriteLine("{");
                /*    */ writer.WriteLine("\tpublic static partial class KiponSdkGeneratedExtensionMethods");
                /* CL */ writer.WriteLine("\t{");
                /*    */ writer.WriteLine("\t\tstatic KiponSdkGeneratedExtensionMethods()");
                /* SC */ writer.WriteLine("\t\t{");
                foreach (var logicalname in entities.Keys)
                {
                    writer.WriteLine($"\t\t\tentittypes[{ns}.{logicalname}.EntityLogicalName] = typeof({ns}.{logicalname});");
                }
                /* SC */ writer.WriteLine("\t\t}");
                /* CL */ writer.WriteLine("\t}");
                /* NS */ writer.WriteLine("}");
                #endregion

                #region write actions request interface/response class
                if (actions != null && actions.Count > 0)
                {
                    #region generate action request interface and action response implementations
                    writer.WriteLine("namespace " + nsa);
                    writer.WriteLine("{");
                    foreach (var action in actions)
                    {
                        var activity = CodeWriterFilter.ACTIVITIES[action.LogicalName];
                        #region write request interface
                        if (activity.InputMembers != null && activity.InputMembers.Length > 0)
                        {
                            var targetInterface = string.Empty;

                            if (!string.IsNullOrEmpty(activity.LogicalName))
                            {
                                targetInterface = $": Kipon.Xrm.ActionTarget<{ns}.{CodeWriterFilter.LOGICALNAME2SCHEMANAME[activity.LogicalName]}>";
                            }

                            writer.WriteLine($"\tpublic partial interface I{action.Name}Request{targetInterface}");
                            writer.WriteLine("\t{");

                            foreach (var inp in activity.InputMembers)
                            {
                                writer.WriteLine($"\t\t{inp.Typename} {inp.Name} " + "{ get; }");
                            }
                            writer.WriteLine("\t}");
                        }
                        #endregion

                        #region write response interface
                        if (activity.OutputMembers != null && activity.OutputMembers.Length > 0)
                        {
                            writer.WriteLine($"\tpublic partial class {action.Name}Response");
                            writer.WriteLine("\t{");
                            foreach (var inp in activity.OutputMembers)
                            {
                                writer.WriteLine($"\t\t[Kipon.Xrm.Attributes.Output(\"{inp.Name}\", {(inp.Required? "true":"false")})]");
                                writer.WriteLine($"\t\t public {inp.Typename} {inp.Name} " + "{ get; set; }");
                            }
                            writer.WriteLine("\t}");
                        }
                        #endregion
                    }
                    writer.WriteLine("}");
                    #endregion

                    #region generate action request implementations
                    var hasInput = CodeWriterFilter.ACTIVITIES.Values.Where(r => r.InputMembers != null && r.InputMembers.Length > 0).Any();
                    if (hasInput)
                    {
                        writer.WriteLine($"namespace {nsa}.Implement");
                        writer.WriteLine("{");
                        writer.WriteLine($"\tusing {nsa};");
                        writer.WriteLine($"\tusing Kipon.Xrm.Actions;");

                        foreach (var action in actions)
                        {
                            var activity = CodeWriterFilter.ACTIVITIES[action.LogicalName];
                            if (activity.InputMembers != null && activity.InputMembers.Length > 0)
                            {
                                writer.WriteLine($"\tpublic partial class {action.Name}Request : AbstractActionRequest, I{action.Name}Request");
                                writer.WriteLine("\t{");
                                writer.WriteLine($"\t\tpublic {action.Name}Request(Microsoft.Xrm.Sdk.IPluginExecutionContext ctx): base(ctx)" + "{ }");

                                if (activity.LogicalName != null)
                                {
                                    writer.WriteLine($"\t\tpublic Microsoft.Xrm.Sdk.EntityReference Target " + "{ get => this.ValueOf<Microsoft.Xrm.Sdk.EntityReference>(\"Target\"); }");
                                }

                                foreach (var inp in activity.InputMembers)
                                {
                                    writer.WriteLine($"\t\tpublic {inp.Typename} {inp.Name} " + "{" + $"get => this.ValueOf<{inp.Typename}>(\"{inp.Name}\")"  +  ";}");
                                }

                                writer.WriteLine("\t}");
                            }
                        }

                        writer.WriteLine("}");
                    }
                    #endregion


                }
                #endregion
            }
        }
    }

    public static class LocalExtensions
    {
        public static string ReplaceLastElement(this string value, char delimiter, string replaceWith)
        {
            var values = value.Split(delimiter).ToList();
            values = values.Take(values.Count - 1).ToList();
            values.Add(replaceWith);
            return string.Join(delimiter.ToString(), values);
        }
    }
}
