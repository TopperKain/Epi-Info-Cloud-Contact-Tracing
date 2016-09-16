﻿using System;
using System.Collections.Generic;
using System.Linq;
using Epi.Cloud.Common.Configuration;
using Epi.Cloud.Interfaces.MetadataInterfaces;

namespace Epi.Cloud.Common.Metadata
{
    public class MetadataAccessor
    {
        public static class StaticCache
        {
            // Form Digests [FormId]
            public static FormDigest[] _formDigests = null;
            
            // PageDigests[FormId][PageId]
            public static PageDigest[][] _pageDigests = null;

            // FieldDigests[FormId][PageId]
            public static Dictionary<string, FieldDigest[]> _fieldDigests = new Dictionary<string, FieldDigest[]>();

            // Page[FormId][PageId]
            public static Dictionary<string, Dictionary<int, Page>> _pageMetadata = new Dictionary<string, Dictionary<int, Page>>();

            // FieldAttributes[FormId][PageId]
            public static Dictionary<string, Dictionary<int, IEnumerable<FieldAttributes>>> _pageFieldAttributes = new Dictionary<string, Dictionary<int, IEnumerable<FieldAttributes>>>();

            public static IProjectMetadataProvider _projectMetadataProvider = null;
        }

        protected string _formId;


        public MetadataAccessor()
        {
        }

        public MetadataAccessor(string surveyId)
        {
            _formId = surveyId;
        }

        public string CurrentFormId { get { return _formId; } set { _formId = value; } }

        public static IProjectMetadataProvider ProjectMetadataProvider
        {
            get
            {
                if (StaticCache._projectMetadataProvider == null)
                {
                    StaticCache._projectMetadataProvider = DependencyHelper.GetService<IProjectMetadataProvider>();
                }
                return StaticCache._projectMetadataProvider;
            }

            protected set { StaticCache._projectMetadataProvider = value; }
        }

        public FormDigest[] FormDigests
        {
            get
            {
                if (StaticCache._formDigests == null) StaticCache._formDigests = ProjectMetadataProvider.GetFormDigestsAsync().Result;
                return StaticCache._formDigests;
            }
        }

        public PageDigest[][] PageDigests
        {
            get
            {
                if (StaticCache._pageDigests == null) StaticCache._pageDigests = ProjectMetadataProvider.GetProjectPageDigestsAsync().Result;
                return StaticCache._pageDigests;
            }
        }

        public FieldDigest[] CurrentFormFieldDigests
        {
            get
            {
                return GetFieldDigests(_formId);
            }
        }

        public int PageIdFromPageNumber(string formId, int pageNumber)
        {
            PageDigest pageDigest = GetPageDigestByPageNumber(formId, pageNumber);
            var pageId = pageDigest.PageId;
            return pageId;
        }

        public FieldDigest[] GetFieldDigests(string formId)
        {
            FieldDigest[] fieldDigests = null;
            if (!StaticCache._fieldDigests.TryGetValue(formId, out fieldDigests))
                StaticCache._fieldDigests[formId] = fieldDigests = ProjectMetadataProvider.GetFieldDigestsAsync(formId).Result;
            return fieldDigests;
        }

        public Page GetCurrentFormPageMetadataByPageId(string pageId)
        {
            return GetCurrentFormPageMetadataByPageId(Convert.ToInt32(pageId));
        }

        public Page GetCurrentFormPageMetadataByPageId(int pageId)
        {
            return GetPageMetadataByPageId(_formId, pageId);
        }

        public Page GetPageMetadataByPageId(string formId, int pageId)
        {
            Dictionary<int, Page> pageMetadatas = null;
            if (!StaticCache._pageMetadata.TryGetValue(formId, out pageMetadatas))
            {
                StaticCache._pageMetadata[formId] = pageMetadatas = new Dictionary<int, Page>();
            }

            Page pageMetadata = null;
            if (!pageMetadatas.TryGetValue(pageId, out pageMetadata))
            {
                pageMetadatas[pageId] = pageMetadata = ProjectMetadataProvider.GetPageMetadataAsync(formId, pageId).Result;
            }

            return pageMetadata;
        }

        public IEnumerable<FieldAttributes> GetCurrentFormPageFieldAttributesByPageId(string pageId)
        {
            return GetCurrentFormPageFieldAttributesByPageId(Convert.ToInt32(pageId));
        }

        public IEnumerable<FieldAttributes> GetCurrentFormPageFieldAttributesByPageId(int pageId)
        {
            return GetPageFieldAttributesByPageId(_formId, pageId);
        }

        public IEnumerable<FieldAttributes> GetPageFieldAttributesByPageId(string formId, int pageId)
        {
            Dictionary<int, IEnumerable<FieldAttributes>> pageFieldAttributesByPageId = null;
            if (!StaticCache._pageFieldAttributes.TryGetValue(formId, out pageFieldAttributesByPageId))
            {
                StaticCache._pageFieldAttributes[formId] = pageFieldAttributesByPageId = new Dictionary<int, IEnumerable<FieldAttributes>>();
            }

            IEnumerable<FieldAttributes> pageFieldAttributes;
            if (!pageFieldAttributesByPageId.TryGetValue(pageId, out pageFieldAttributes))
            {
                var formDigest = FormDigests.Single(d => d.FormId == formId);
                var pageMetadata = GetCurrentFormPageMetadataByPageId(pageId);
                pageFieldAttributesByPageId[pageId] = pageFieldAttributes = FieldAttributes.MapFieldMetadataToFieldAttributes(pageMetadata, formDigest.CheckCode);
            }
            return pageFieldAttributes;
        }

        public FormDigest GetCurrentFormDigest()
        {
            return FormDigests.SingleOrDefault(f => f.FormId == _formId);
        }

        public FormDigest GetFormDigest(string formId)
        {
            return FormDigests.SingleOrDefault(f => f.FormId == formId);
        }

        public FormDigest GetFormDigestByFormName(string formName)
        {
            formName = formName.ToLower();
            return FormDigests.SingleOrDefault(f => f.FormName.ToLower() == formName);
        }

        public PageDigest[] GetCurrentFormPageDigests()
        {
            return GetPageDigests(_formId);
        }

        public PageDigest[] GetPageDigests(string formId)
        {
            var pageDigests = PageDigests.SingleOrDefault(d => d[0].FormId == formId);
            return pageDigests;
        }

        public PageDigest GetCurrentFormPageDigestByPageNumber(int pageNumber)
        {
            return GetPageDigestByPageNumber(_formId, pageNumber);
        }

        public PageDigest GetPageDigestByPageNumber(string formId, int pageNumber)
        {
            var pageDigests = PageDigests.Single(d => d[0].FormId == formId);
            var pageDigest = pageDigests.Single(d => d.PageNumber == pageNumber);
            return pageDigest;
        }

        public PageDigest GetCurrentFormPageDigestByPageId(string pageId)
        {
            return GetCurrentFormPageDigestByPageId(Convert.ToInt32(pageId));
        }

        public PageDigest GetCurrentFormPageDigestByPageId(int pageId)
        {
            return GetPageDigestByPageId(_formId, pageId);
        }

        public PageDigest GetPageDigestByPageId(string formId, int pageId)
        {
            var pageDigests = PageDigests.Single(d => d[0].FormId == formId);
            var pageDigest = pageDigests.Single(d => d.PageId == pageId);
            return pageDigest;
        }

        public FieldDigest[] GetCurrentFormFieldDigests(IEnumerable<string> fieldNames)
        {
            fieldNames = fieldNames.Select(n => n.ToLower());
            return CurrentFormFieldDigests.Where(d => fieldNames.Contains(d.FieldName.ToLower())).ToArray();
        }

        public FieldDigest[] GetCurrentFormFieldDigestsWithPageNumber(int pageNumber)
        {
            var pagePosition = pageNumber - 1;
            return CurrentFormFieldDigests.Where(d => d.Position == pagePosition).ToArray();
        }

        public FieldDigest[] GetCurrentFormFieldDigestsNotWithPageNumber(int pageNumber)
        {
            var pagePosition = pageNumber - 1;
            return CurrentFormFieldDigests.Where(d => d.Position != pagePosition).ToArray();
        }

        public FieldAttributes GetFieldAttributes(FieldDigest fieldDigest)
        {
            var formId = fieldDigest.FormId;
            var pageId = fieldDigest.PageId;
            var fieldName = fieldDigest.Field.FieldName;
            var fieldAttributes = GetFieldAttributesByPageId(formId, pageId, fieldName);
            return fieldAttributes;
        }

        public FieldAttributes GetFieldAttributesByPageId(string formId, string pageId, string fieldName)
        {
            return GetFieldAttributesByPageId(formId, Convert.ToInt32(pageId), fieldName);
        }

        public FieldAttributes GetFieldAttributesByPageId(string formId, int pageId, string fieldName)
        {
            fieldName = fieldName.ToLower();
            var pageFieldAttributes = GetPageFieldAttributesByPageId(formId, pageId);
            var fieldAttributes = pageFieldAttributes.SingleOrDefault(a => a.FieldName.ToLower() == fieldName);
            return fieldAttributes;
        }
    }
}
