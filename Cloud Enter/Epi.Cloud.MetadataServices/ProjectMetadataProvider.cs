﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Epi.Cloud.CacheServices;
using Epi.Cloud.Common;
using Epi.Cloud.Interfaces.MetadataInterfaces;
using Epi.Cloud.MetadataServices.ProxiesService;
using Epi.FormMetadata.DataStructures;
using Epi.FormMetadata.Extensions;

namespace Epi.Cloud.MetadataServices
{
    public class ProjectMetadataProvider : IProjectMetadataProvider
    {
        private static Guid _projectId;
        private readonly IEpiCloudCache _epiCloudCache;
        public ProjectMetadataProvider(IEpiCloudCache epiCloudCache)
        {
            _epiCloudCache = epiCloudCache;
        }

        public IEpiCloudCache Cache { get { return _epiCloudCache; } }

        public string ProjectId { get { return _projectId.ToString("N"); } }

        //Pass the project id and call the DBAccess API and get the project metadata.

        public async Task<Template> GetProjectMetadataAsync(ProjectScope scope)
        {
            Template metadata = null;
            if (scope == ProjectScope.TemplateWithAllPages)
            {
                metadata = _projectId != null ? _epiCloudCache.GetFullProjectTemplateMetadata(_projectId) : null;
                if (metadata == null)
                {
                    metadata = await RefreshCache(_projectId);
                }
            }
            else if (scope == ProjectScope.TemplateWithNoPages)
            {
                metadata = _projectId != null ? _epiCloudCache.GetProjectTemplateMetadata(_projectId, Guid.Empty, null) : null;
                if (metadata == null)
                {
                    Template fullMetadata = _projectId != null ? _epiCloudCache.GetFullProjectTemplateMetadata(_projectId) : null;
                    if (fullMetadata == null)
                    {
                        fullMetadata = await RefreshCache(_projectId);
                        _projectId = new Guid(fullMetadata.Project.Id);
                        metadata = _epiCloudCache.GetProjectTemplateMetadata(_projectId, Guid.Empty, null);
                        if (metadata == null)
                        {
                            metadata = fullMetadata;
                        }
                    }
                }
            }
            return metadata;
        }

        public async Task<Template> GetProjectMetadataWithPageByPageIdAsync(string formId, int pageId)
        {
            var metadata = _projectId != null ? _epiCloudCache.GetProjectTemplateMetadata(_projectId, new Guid(formId), pageId) : null;
            if (metadata == null)
            {
                var fullMetadata = await RefreshCache(_projectId);
                metadata = _epiCloudCache.GetProjectTemplateMetadata(_projectId, new Guid(formId), pageId);
            }
            return metadata;
        }

        public Task<Template> GetProjectMetadataAsync(string formId, ProjectScope scope)
        {
            if (scope == ProjectScope.TemplateWithAllPages)
            {
                return GetProjectMetadataAsync(scope);
            }
            return GetProjectMetadataWithPageByPageNumberAsync(formId, null);
        }

        public async Task<Template> GetProjectMetadataWithPageByPageNumberAsync(string formId, int? pageNumber)
        {
            var metadata = _projectId != null ? _epiCloudCache.GetProjectTemplateMetadataByPageNumber(_projectId, new Guid(formId), pageNumber) : null;
            if (metadata == null)
            {
                var fullMetadata = await RefreshCache(_projectId);
                metadata = _epiCloudCache.GetProjectTemplateMetadata(_projectId, new Guid(formId), pageNumber);
            }
            return metadata;
        }

        public async Task<Page> GetPageMetadataAsync(string formId, int pageId)
        {
            var metadata = _epiCloudCache.GetPageMetadata(_projectId, new Guid(formId), pageId);
            if (metadata == null)
            {
                var fullMetadata = await RefreshCache(_projectId);
                metadata = _epiCloudCache.GetPageMetadata(_projectId, new Guid(formId), pageId);
            }
            return metadata;
        }

        public async Task<FormDigest[]> GetFormDigestsAsync()
        {
            var formDigests = _projectId != null ? _epiCloudCache.GetFormDigests(_projectId) : null;
            if (formDigests == null)
            {
                var fullMetadata = await RefreshCache(_projectId);

                formDigests = _epiCloudCache.GetFormDigests(_projectId);
            }
            return formDigests;
        }

        public async Task<FormDigest> GetFormDigestAsync(string formId)
        {
            var formDigests = _projectId != null ? _epiCloudCache.GetFormDigests(_projectId) : null;
            if (formDigests == null)
            {
                var fullMetadata = await RefreshCache(_projectId);

                formDigests = _epiCloudCache.GetFormDigests(_projectId);
            }
            var formDigest = formDigests != null ? formDigests.Where(d => CaseInsensitiveEqualityComparer.Instance.Equals(d.FormId, formId)).SingleOrDefault() : null;
            return formDigest;
        }

        public async Task<PageDigest[][]> GetProjectPageDigestsAsync()
        {
            var projectPageDigests = _projectId != null ? _epiCloudCache.GetProjectPageDigests(_projectId) : null;
            if (projectPageDigests == null)
            {
                var fullMetadata = await RefreshCache(_projectId);
                projectPageDigests = fullMetadata.Project.FormPageDigests;
            }
            return projectPageDigests;
        }

        public async Task<PageDigest[]> GetPageDigestsAsync(string formId)
        {
            var pageDigests = _epiCloudCache.GetPageDigests(_projectId, new Guid(formId));
            if (pageDigests == null)
            {
                var projectPageDigests = GetProjectPageDigestsAsync().Result;
                foreach (var projectPageDigest in projectPageDigests)
                {
                    if (projectPageDigest[0].FormId == formId)
                    {
                        pageDigests = projectPageDigest;
                    }
                }
            }
            return await Task.FromResult(pageDigests);
        }

        public async Task<FieldDigest> GetFieldDigestAsync(string formId, string fieldName)
        {
            fieldName = fieldName.ToLower();
            var pageDigests = await GetPageDigestsAsync(formId);

            foreach (var pageDigest in pageDigests)
            {
                var field = pageDigest.Fields.Where(f => f.FieldName.ToLower() == fieldName).SingleOrDefault();
                if (field != null)
                {
                    return new FieldDigest(field, pageDigest);
                }
            }
            return null;
        }

        public async Task<FieldDigest[]> GetFieldDigestsAsync(string formId)
        {
            formId = formId.ToLower();
            List<FieldDigest> fieldDigests = new List<FieldDigest>();
            var pageDigests = await GetPageDigestsAsync(formId);
            foreach (var pageDigest in pageDigests)
            {
                fieldDigests.AddRange(pageDigest.Fields.Select(field => new FieldDigest(field, pageDigest)));
            }
            return fieldDigests.ToArray();
        }

        public async Task<FieldDigest[]> GetFieldDigestsAsync(string formId, IEnumerable<string> fieldNames)
        {
            formId = formId.ToLower();
            List<string> fieldNameList = fieldNames.Select(n => n.ToLower()).ToList();
            List<string> remainingFieldNamesList = fieldNames.Select(n => n.ToLower()).ToList();
            List<FieldDigest> fieldDigests = new List<FieldDigest>();
            int fieldNamesCount = fieldNames.Count();
            var pageDigests = await GetPageDigestsAsync(formId);
            foreach (var pageDigest in pageDigests)
            {
                fieldNameList = remainingFieldNamesList.ToList();
                foreach (string fieldName in fieldNameList)
                {
                    AbridgedFieldInfo field = pageDigest.Fields.Where(f => f.FieldName.ToLower() == fieldName).SingleOrDefault();
                    if (field != null)
                    {
                        fieldDigests.Add(new FieldDigest(field, pageDigest));
                        remainingFieldNamesList.Remove(fieldName);
                    }
                }
                if (remainingFieldNamesList.Count == 0) break;
            }
            return fieldDigests.ToArray();
        }

        private async Task<Template> RefreshCache(Guid projectId)
        {
            Template metadata = await RetrieveProjectMetadata(projectId);
#if CaptureMetadataJson
            var metadataFromService = Newtonsoft.Json.JsonConvert.SerializeObject(metadata);
            if (!System.IO.Directory.Exists(@"C:\Junk")) System.IO.Directory.CreateDirectory(@"C:\Junk");
            System.IO.File.WriteAllText(@"C:\Junk\ZikaMetadataFromService.json", metadataFromService);

            var json = System.IO.File.ReadAllText(@"C:\Junk\ZikaMetadataFromService.json");
            Template metadataObject = Newtonsoft.Json.JsonConvert.DeserializeObject<Template>(json);
#endif
            PopulateRequiredPageLevelSourceTables(metadata);
            GenerateDigests(metadata);

#if CaptureMetadataJson
            var metadataWithDigests = Newtonsoft.Json.JsonConvert.SerializeObject(metadata);
            if (!System.IO.Directory.Exists(@"C:\Junk")) System.IO.Directory.CreateDirectory(@"C:\Junk");
            System.IO.File.WriteAllText(@"C:\Junk\ZikaMetadataWithDigests.json", metadataWithDigests);
#endif
            _epiCloudCache.SetProjectTemplateMetadata(metadata);
            return metadata;
        }

        private static async Task<Template> RetrieveProjectMetadata(Guid projectId)
        {
            ProjectMetadataServiceProxy serviceProxy = new ProjectMetadataServiceProxy();
            var templateMetadata = await serviceProxy.GetProjectMetadataAsync(projectId.ToString("N"));
            _projectId = templateMetadata != null ? new Guid(templateMetadata.Project.Id) : Guid.Empty;
            return templateMetadata;
        }

        private void GenerateDigests(Template projectTemplateMetadata)
        {
            projectTemplateMetadata.Project.FormDigests = projectTemplateMetadata.ToFormDigests();
            projectTemplateMetadata.Project.FormPageDigests = projectTemplateMetadata.ToPageDigests();
        }


        private void PopulateRequiredPageLevelSourceTables(Template metadata)
        {
            foreach (var view in metadata.Project.Views)
            {
                var numberOfPages = view.Pages.Length;
                for (int i = 0; i < numberOfPages; ++i)
                {
                    var pageMetadata = view.Pages[i];
                    var pageId = pageMetadata.PageId.Value;
                    var fieldsRequiringSourceTable = pageMetadata.Fields.Where(f => !string.IsNullOrEmpty(f.SourceTableName));
                    foreach (var field in fieldsRequiringSourceTable)
                    {
                        field.SourceTableValues = metadata.SourceTables.Where(st => st.TableName == field.SourceTableName).First().Values;
                    }
                }
            }
        }
    }
 }
